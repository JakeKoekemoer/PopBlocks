using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>
/// Decides which seed each Story level plays with, counts failures, and runs rerolls. It owns the rules of the spec's
/// "Which seed a level plays" table and keeps the player's current-level state saved as it changes:
///
/// <list type="bullet">
/// <item>Current level (HighestCleared + 1): its stored seed, or a fresh roll on first entry (stored).</item>
/// <item>A cleared level: the seed it was first cleared with; a level cleared before seeds existed replays the original.</item>
/// <item>A reroll on a cleared level: a fresh roll for this session only. Free. Never stored.</item>
/// </list>
///
/// Rolls take a moment, so they run on a worker thread. A roll for a level is shared by everyone who asks for it (single
/// flight), and a roll started early (a prefetch) is simply picked up later.
/// </summary>
public sealed class StoryLevels
{
    /// <summary>Rerolls on a cleared level count from here, so they never repeat a seed the player may already have cleared with.</summary>
    private const int SessionRerollBase = 1000;

    private sealed class RollEntry
    {
        public required Task<RolledLevel> Task { get; init; }
        public required CancellationTokenSource Cancel { get; init; }
        public bool Needed { get; set; }
    }

    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;
    private readonly PlayerIdentity _identity;
    private readonly Func<int, int, int, CancellationToken, RolledLevel> _roll;

    private readonly object _gate = new();
    private readonly Dictionary<(int Level, int Reroll), RollEntry> _rolls = new();
    private readonly Dictionary<int, int> _sessionSeeds = new();     // levels replayed with a rerolled seed this session
    private readonly Dictionary<int, int> _sessionRerolls = new();   // rerolls used on cleared levels this session

    public StoryLevels(ILocalSettings settings, PlayerProgress progress, PlayerIdentity identity,
        Func<int, int, int, CancellationToken, RolledLevel>? roll = null)
    {
        _settings = settings;
        _progress = progress;
        _identity = identity;
        _roll = roll ?? ((level, salt, reroll, ct) => LevelRoller.Roll(level, salt, reroll, ct));
    }

    /// <summary>Raised when failures, rerolls or a seed change.</summary>
    public event Action? Changed;

    // ------------------------------------------------------------------ the current level

    public int CurrentLevel => _progress.NextLevel;
    public bool IsCurrent(int level) => level == _progress.NextLevel;
    public bool IsCleared(int level) => _progress.IsCleared(level);

    private CurrentLevelState Current => SeedStore.ReadCurrent(_settings, _progress.NextLevel);

    /// <summary>Failures on this level. Only the player's current level counts; any other level reports 0.</summary>
    public int Failures(int level) => IsCurrent(level) ? Current.Failures : 0;

    /// <summary>Rerolls used on the current level.</summary>
    public int Rerolls(int level) => IsCurrent(level) ? Current.Rerolls : 0;

    // ------------------------------------------------------------------ losses and easing (v1.1)

    /// <summary>How many times this level has been lost, ever. Unlike <see cref="Failures"/> it never resets.</summary>
    public int Losses(int level) => SeedStore.ReadLosses(_settings, level);

    /// <summary>"There is always a way through" is shown after this many losses on a level (the caller also checks the rules in force).</summary>
    public bool ShowsSolvableNote(int level) => Losses(level) >= SeedingConfig.LossesForSolvableNote;

    /// <summary>The player chose to ease this level.</summary>
    public bool IsEased(int level) => _progress.IsEased(level);

    /// <summary>The level on the board is the eased form: it is the player's current level and they eased it.</summary>
    public bool IsEasedPlay(int level) => IsCurrent(level) && IsEased(level);

    /// <summary>
    /// Easing is offered on the player's current level after <see cref="SeedingConfig.LossesForEase"/> losses, never on a
    /// region's finale and not on a level that is already eased.
    /// </summary>
    public bool CanEase(int level) =>
        IsCurrent(level) && !IsEased(level) && Losses(level) >= SeedingConfig.LossesForEase && !IsFinale(level);

    /// <summary>A region's last level (the finale, played without helpers). Never eased.</summary>
    public static bool IsFinale(int level) => RegionCatalog.For(level) is { } region && region.LastLevel == level;

    /// <summary>
    /// Swaps the level for an easier one: the same as a free reroll, except the new level is found one difficulty band lower
    /// and played with more moves, and it will pay no gold. Moves the story on when cleared. Does nothing if easing is not offered.
    /// </summary>
    public bool TryEase(int level)
    {
        if (!CanEase(level)) return false;
        SeedStore.SetEased(_settings, level, true);
        var state = Current;
        SeedStore.WriteCurrent(_settings, state with { Seed = 0, Rerolls = state.Rerolls + 1, Failures = 0 });
        Changed?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------ failures

    /// <summary>
    /// Counts a failure: a loss, or a restart after a piece was placed. Only the current level counts, and replays never
    /// do. Saved at once. Returns the new count, or 0 if this level does not count.
    /// </summary>
    public int RecordFailure(int level)
    {
        if (!IsCurrent(level)) return 0;
        var state = Current;
        state = state with { Failures = state.Failures + 1 };
        SeedStore.WriteCurrent(_settings, state);
        SeedStore.AddLoss(_settings, level);
        Changed?.Invoke();
        return state.Failures;
    }

    // ------------------------------------------------------------------ reroll

    /// <summary>A reroll is offered on the current level after its first failure, and always on a cleared level.</summary>
    public bool CanOfferReroll(int level) => IsCleared(level) || (IsCurrent(level) && Failures(level) >= 1);

    /// <summary>What a reroll costs now. Always free on a cleared level.</summary>
    public int RerollPrice(int level) => IsCleared(level) ? 0 : RerollPricing.Price(level, Failures(level));

    public bool CanAffordReroll(int level) => CanOfferReroll(level) && RerollPrice(level) <= _progress.Gold;

    /// <summary>How many more failures until the current level's reroll is free.</summary>
    public int FailuresUntilFree(int level) => IsCleared(level) ? 0 : RerollPricing.FailuresUntilFree(Failures(level));

    /// <summary>
    /// Swaps the level for a new one. On the current level this costs <see cref="RerollPrice"/>; on a cleared level it is
    /// free. Afterwards the level has no seed yet, so the next <see cref="ResolveAsync"/> rolls a new one. Does nothing
    /// (and costs nothing) if the reroll is not offered or the player cannot pay.
    /// </summary>
    public bool TryReroll(int level)
    {
        if (!CanOfferReroll(level)) return false;

        if (IsCleared(level))
        {
            lock (_gate)
            {
                _sessionRerolls[level] = (_sessionRerolls.TryGetValue(level, out int k) ? k : 0) + 1;
                _sessionSeeds.Remove(level);
            }
            Changed?.Invoke();
            return true;
        }

        int price = RerollPrice(level);
        if (price > _progress.Gold) return false;

        // The new reroll count and the cleared seed are saved first, then the gold is taken. If the app dies in between the
        // player gets the reroll for free; the other order could take gold and give nothing. Roll is deterministic for a
        // given reroll count, so after a restart the player gets the same new level.
        var state = Current;
        // The failure count starts again with the new level (the lifetime count of losses lives in its own key).
        SeedStore.WriteCurrent(_settings, state with { Seed = 0, Rerolls = state.Rerolls + 1, Failures = 0 });
        SeedStore.SetEased(_settings, level, false);   // a reroll is a normal level again
        if (price > 0) _progress.TrySpend(price);
        Changed?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------ which seed a level plays

    /// <summary>
    /// The seed this level plays with, if it is already known: the stored seed of the current level, the recorded seed
    /// of a cleared level (or the original level, if none was recorded), or a seed rolled earlier this session. Null means
    /// a roll is needed first.
    /// </summary>
    public LevelSeed? KnownSeed(int level)
    {
        if (level < 1) return LevelSeed.Legacy;

        if (IsCleared(level))
        {
            lock (_gate)
            {
                if (_sessionSeeds.TryGetValue(level, out int s)) return new LevelSeed(s);
                if (_sessionRerolls.ContainsKey(level)) return null;   // rerolled, not rolled yet
            }
            if (IsEased(level)) return null;   // cleared in its eased form: a replay plays a normal level, so it is rolled
            return SeedStore.ReadFirstClear(_settings).TryGetValue(level, out int recorded) ? new LevelSeed(recorded) : LevelSeed.Legacy;
        }

        if (IsCurrent(level))
        {
            int stored = Current.Seed;
            return stored != 0 ? new LevelSeed(stored) : null;
        }

        lock (_gate) return _sessionSeeds.TryGetValue(level, out int s) ? new LevelSeed(s) : null;   // a level not unlocked yet (dev tools)
    }

    /// <summary>
    /// The seed to show for a level on the levels page: a cleared level's first-clear seed, or the current level's seed once
    /// it has one. Null for the original (legacy) levels, locked levels and a current level that has no seed yet.
    /// </summary>
    public int? SeedNumber(int level)
    {
        if (IsCleared(level))
            return SeedStore.ReadFirstClear(_settings).TryGetValue(level, out int recorded) ? recorded : null;
        if (IsCurrent(level))
            return Current.Seed is var s and not 0 ? s : null;
        return null;
    }

    /// <summary>
    /// The small text under a level on the levels page: "Seed 483920117", "Original" for a level cleared before seeds
    /// existed, or nothing for a level that is still locked. Information for bug reports only; it promises nothing about
    /// reproducing the level after an update.
    /// </summary>
    public string SeedText(int level)
    {
        if (!IsCleared(level) && !IsCurrent(level)) return string.Empty;
        return SeedNumber(level) is { } seed
            ? "Seed " + seed.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : IsCleared(level) ? "Original" : "Seed given when you start";
    }

    /// <summary>The roll a level needs when it has no known seed: which reroll number to use.</summary>
    private int RerollFor(int level)
    {
        if (IsCurrent(level)) return Current.Rerolls + (IsEased(level) ? LevelRoller.EasedRerollOffset : 0);
        lock (_gate) return _sessionRerolls.TryGetValue(level, out int k) ? SessionRerollBase + k : 0;
    }

    /// <summary>
    /// The seed for a level if it can be had without waiting: already known, or rolled in the background and finished.
    /// The seed is stored where the rules say it should be.
    /// </summary>
    public bool TryGetReady(int level, out LevelSeed seed)
    {
        if (KnownSeed(level) is { } known) { seed = known; return true; }

        RollEntry? entry;
        lock (_gate) _rolls.TryGetValue((level, RerollFor(level)), out entry);
        if (entry is { Task.IsCompletedSuccessfully: true })
        {
            seed = Commit(level, entry.Task.Result);
            return true;
        }

        seed = LevelSeed.Legacy;
        return false;
    }

    /// <summary>The seed for a level, rolling it if needed. Safe to await from the UI thread; the search runs on a worker.</summary>
    public async Task<LevelSeed> ResolveAsync(int level, CancellationToken cancel = default)
    {
        while (true)
        {
            if (KnownSeed(level) is { } known) return known;

            int reroll = RerollFor(level);
            var entry = StartRoll(level, reroll, needed: true);
            var rolled = await entry.Task.WaitAsync(cancel).ConfigureAwait(true);

            // The player may have rerolled again while this one was being found: only keep a roll that is still the one wanted.
            if (RerollFor(level) != reroll) continue;
            if (KnownSeed(level) is { } already) return already;
            return Commit(level, rolled);
        }
    }

    private LevelSeed Commit(int level, RolledLevel rolled)
    {
        LevelCatalog.Prime(level, rolled.Seed, rolled.Level);

        if (IsCurrent(level))
        {
            var state = Current;
            SeedStore.WriteCurrent(_settings, state with { Seed = rolled.Seed.Value });
        }
        else
        {
            lock (_gate) _sessionSeeds[level] = rolled.Seed.Value;
        }
        Changed?.Invoke();
        return rolled.Seed;
    }

    // ------------------------------------------------------------------ development tools only

    /// <summary>Sets the failure count of the current level directly. For testing.</summary>
    public void DevSetFailures(int failures)
    {
        var state = Current;
        SeedStore.WriteCurrent(_settings, state with { Failures = Math.Max(0, failures) });
        Changed?.Invoke();
    }

    /// <summary>Forgets every recorded seed, the current level's seed, rerolls and failures. For testing; the name is kept.</summary>
    public void DevReset()
    {
        SeedStore.ResetCurrent(_settings);
        _settings.SetString(SaveKeys.FirstClearSeeds, "");
        _settings.SetString(SaveKeys.LevelLosses, "");
        _settings.SetString(SaveKeys.EasedLevels, "");
        lock (_gate)
        {
            _sessionSeeds.Clear();
            _sessionRerolls.Clear();
            foreach (var entry in _rolls.Values) entry.Cancel.Cancel();
            _rolls.Clear();
        }
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ prefetch

    /// <summary>Rolls ahead for the level after <paramref name="level"/>, in the background. Nothing happens if its seed is already known.</summary>
    public void PrefetchAfter(int level)
    {
        int next = level + 1;
        if (KnownSeed(next) is not null) return;
        StartRoll(next, RerollFor(next), needed: false);
    }

    /// <summary>
    /// After a failure on the current level, rolls its next reroll in the background, so choosing REROLL is instant. Does
    /// nothing before the first failure, to save battery.
    /// </summary>
    public void PrefetchReroll(int level)
    {
        if (!IsCurrent(level) || Failures(level) < 1) return;
        StartRoll(level, Current.Rerolls + 1, needed: false);
    }

    /// <summary>Stops background rolls nobody is waiting for (the player left the level).</summary>
    public void CancelPrefetches()
    {
        lock (_gate)
        {
            foreach (var (key, entry) in _rolls.ToList())
            {
                if (entry.Needed) continue;
                entry.Cancel.Cancel();
                _rolls.Remove(key);
            }
        }
    }

    /// <summary>True while a background roll for this level and reroll is running or has finished and not been used.</summary>
    public bool HasRoll(int level, int reroll)
    {
        lock (_gate) return _rolls.ContainsKey((level, reroll));
    }

    private RollEntry StartRoll(int level, int reroll, bool needed)
    {
        lock (_gate)
        {
            if (_rolls.TryGetValue((level, reroll), out var existing))
            {
                if (needed) existing.Needed = true;
                return existing;
            }

            // Keep only a few rolls around: the current level, the next one and one reroll.
            while (_rolls.Count >= 4)
            {
                var droppable = _rolls.Where(kv => !kv.Value.Needed || kv.Value.Task.IsCompleted).ToList();
                if (droppable.Count == 0) break;   // everything is being waited for: let them all finish
                droppable[0].Value.Cancel.Cancel();
                _rolls.Remove(droppable[0].Key);
            }

            var cts = new CancellationTokenSource();
            int salt = _identity.Salt;
            var entry = new RollEntry
            {
                Cancel = cts,
                Needed = needed,
                Task = Task.Run(() => _roll(level, salt, reroll, cts.Token), cts.Token),
            };
            _rolls[(level, reroll)] = entry;

            // A roll that failed or was cancelled is forgotten, so asking again tries again.
            _ = entry.Task.ContinueWith(t =>
            {
                lock (_gate)
                {
                    if (_rolls.TryGetValue((level, reroll), out var current) && ReferenceEquals(current, entry)) _rolls.Remove((level, reroll));
                }
                _ = t.Exception;   // observed
            }, TaskContinuationOptions.NotOnRanToCompletion);

            return entry;
        }
    }
}
