using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Services;

/// <summary>What finishing a level paid out.</summary>
public readonly record struct CompletionResult(bool FirstClear, int GoldAwarded);

/// <summary>Gold paid the first time a level is cleared. It grows with the difficulty tier.</summary>
public static class GoldRewards
{
    public static int For(int level, bool isTutorial)
    {
        if (isTutorial || level <= StoryConfig.TutorialLastLevel) return 0;

        var tier = StoryConfig.TierFor(level);
        int into = level - StoryConfig.FirstLevelOf(tier);
        return tier switch
        {
            Tier.Easy => 10 + into / 4,
            Tier.Medium => 30 + into * 2 / 5,
            Tier.Hard => 60 + into * 3 / 5,
            _ => 100 + Math.Min(200, into / 2),
        };
    }
}

/// <summary>
/// How far the player has got in Story mode, and their gold. Saved as it changes.
/// Levels unlock in order; gold is paid once per level, the first time it is cleared.
/// </summary>
public sealed class PlayerProgress
{
    public const string HighestClearedKey = "progress_highest";
    public const string GoldKey = "gold";

    private readonly ILocalSettings _settings;

    public PlayerProgress(ILocalSettings settings)
    {
        _settings = settings;
        HighestCleared = Math.Max(0, settings.GetInt(HighestClearedKey, 0));
        Gold = Math.Max(0, settings.GetInt(GoldKey, 0));
    }

    /// <summary>Raised when gold or progress changes.</summary>
    public event Action? Changed;

    public int HighestCleared { get; private set; }
    public int Gold { get; private set; }

    /// <summary>The level to continue with.</summary>
    public int NextLevel => HighestCleared + 1;

    public bool IsCleared(int level) => level >= 1 && level <= HighestCleared;

    /// <summary>The player eased this level (shown as a feather on the Map once it is cleared).</summary>
    public bool IsEased(int level) => SeedStore.IsEased(_settings, level);
    public bool IsUnlocked(int level) => level >= 1 && level <= HighestCleared + 1;

    /// <summary>
    /// Records a cleared level. Only the first clear of a level moves progress on and pays gold;
    /// replaying a cleared level (or a tutorial) pays nothing.
    /// </summary>
    public CompletionResult Complete(LevelDefinition level, double goldFactor = 1.0)
    {
        if (level.Number != HighestCleared + 1)
        {
            // A level that was cleared in its eased form still owes its first-clear gold: a normal win pays it, once, and
            // takes the feather off the Map.
            if (!level.Eased && !level.IsTutorial && level.Number >= 1 && level.Number <= HighestCleared && IsEased(level.Number))
            {
                int owed = (int)Math.Floor(GoldRewards.For(level.Number, level.IsTutorial) * goldFactor + 1e-9);
                SeedStore.SetEased(_settings, level.Number, false);
                if (owed > 0)
                {
                    Gold += owed;
                    _settings.SetInt(GoldKey, Gold);
                }
                Changed?.Invoke();
                return new CompletionResult(false, owed);
            }
            return new CompletionResult(false, 0);
        }

        HighestCleared = level.Number;
        // An eased clear moves the story on and pays nothing. Rules that make the game easier pay half (goldFactor 0.5).
        int gold = level.Eased ? 0 : (int)Math.Floor(GoldRewards.For(level.Number, level.IsTutorial) * goldFactor + 1e-9);
        Gold += gold;

        _settings.SetInt(HighestClearedKey, HighestCleared);
        _settings.SetInt(GoldKey, Gold);

        // The seed of the first clear is remembered (once, never overwritten) so the level can be replayed as it was beaten;
        // every other seed is thrown away. Then the next level starts with a clean slate: no seed, rerolls or failures.
        SeedStore.RecordFirstClear(_settings, level.Number, level.Seed);
        SeedStore.ResetCurrent(_settings);
        if (level.Eased) SeedStore.SetEased(_settings, level.Number, true);

        Changed?.Invoke();
        return new CompletionResult(true, gold);
    }

    /// <summary>Adds gold earned outside a level clear (a region reward, an achievement). Never negative.</summary>
    public void AwardGold(int amount)
    {
        if (amount <= 0) return;
        Gold = (int)Math.Min(int.MaxValue, (long)Gold + amount);
        _settings.SetInt(GoldKey, Gold);
        Changed?.Invoke();
    }

    public bool TrySpend(int cost)
    {
        if (cost < 0 || cost > Gold) return false;
        Gold -= cost;
        _settings.SetInt(GoldKey, Gold);
        Changed?.Invoke();
        return true;
    }

    // ---- Development tools only (see DevTools). Normal play never calls these. ----

    /// <summary>Sets cleared-level progress directly, without paying gold. For testing.</summary>
    public void DevSetHighestCleared(int level)
    {
        HighestCleared = Math.Max(0, level);
        _settings.SetInt(HighestClearedKey, HighestCleared);
        Changed?.Invoke();
    }

    /// <summary>Sets gold directly. For testing.</summary>
    public void DevSetGold(int gold)
    {
        Gold = Math.Max(0, gold);
        _settings.SetInt(GoldKey, Gold);
        Changed?.Invoke();
    }
}
