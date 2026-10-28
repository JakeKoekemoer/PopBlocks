using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>
/// Awards achievements. Each is stored once as <c>ach_&lt;id&gt;</c> in <see cref="UnlockStore"/>, and its gold is paid
/// at that moment only, so replaying or reloading can never pay it twice. Nothing here leaves the device.
/// </summary>
public sealed class AchievementService
{
    private readonly UnlockStore _unlocks;
    private readonly PlayerProgress _progress;
    private readonly CampaignService _campaign;
    private readonly LifetimeStats? _lifetime;
    private readonly CosmeticLoadout? _loadout;
    private readonly StoryLevels? _levels;

    public AchievementService(UnlockStore unlocks, PlayerProgress progress, CampaignService campaign,
        LifetimeStats? lifetime = null, CosmeticLoadout? loadout = null, StoryLevels? levels = null)
    {
        _unlocks = unlocks;
        _progress = progress;
        _campaign = campaign;
        _lifetime = lifetime;
        _loadout = loadout;
        _levels = levels;
    }

    /// <summary>Raised for each achievement as it is earned (for a toast).</summary>
    public event Action<Achievement>? Earned;

    public bool IsEarned(string id) => _unlocks.Has(ShopCatalog.AchievementUnlockId(id));

    public int EarnedCount => AchievementCatalog.All.Count(a => IsEarned(a.Id));

    public int TotalCount => AchievementCatalog.All.Count;

    /// <summary>The facts as they stand now, plus whatever has just happened.</summary>
    public AchievementFacts Facts(RunStats? run = null, LevelDefinition? level = null, int classicScore = 0) => new()
    {
        Run = run,
        Level = level,
        ClassicScore = classicScore,
        HighestCleared = _progress.HighestCleared,
        RegionsRestored = _campaign.RestoredRegionCount,
        ArchiveEntries = _campaign.ArchiveEntryCount,
        RegionsWithMemory = _campaign.RegionsWithMemory,
        AnyRegionFullyRemembered = _campaign.AnyRegionFullyRemembered,
        CampaignComplete = _progress.HighestCleared >= RegionCatalog.LastCampaignLevel,
        Rerolls = _lifetime?.Rerolls ?? 0,
        OrbsBrokenTotal = _lifetime?.OrbsBroken ?? 0,
        FractureBreaksTotal = _lifetime?.FractureBreaks ?? 0,
        Shares = _lifetime?.Shares ?? 0,
        CleanWins = _lifetime?.CleanWins ?? 0,
        LevelLosses = level is { IsTutorial: false } && _levels is not null ? _levels.Losses(level.Number) : 0,
        DressedUp = IsDressedUp(),
    };

    /// <summary>A block set, a board, a frame and a Pip look that were all bought (not given) are equipped together.</summary>
    private bool IsDressedUp()
    {
        if (_loadout is null) return false;
        foreach (var slot in new[] { CosmeticSlot.Skin, CosmeticSlot.Board, CosmeticSlot.Frame, CosmeticSlot.Accessory })
            if (CosmeticCatalog.Find(_loadout.Selected(slot)) is not { Price: > 0 }) return false;
        return true;
    }

    /// <summary>Checks now, without a run (something happened outside play: the share sheet was opened, a look was equipped).</summary>
    public IReadOnlyList<Achievement> CheckNow() => Check(Facts());

    /// <summary>Checks every achievement not yet earned against these facts. Returns the ones earned just now.</summary>
    public IReadOnlyList<Achievement> Check(AchievementFacts facts)
    {
        var earned = new List<Achievement>();
        foreach (var a in AchievementCatalog.All)
        {
            if (IsEarned(a.Id) || !a.IsMet(facts)) continue;
            if (!_unlocks.Unlock(ShopCatalog.AchievementUnlockId(a.Id))) continue;

            _progress.AwardGold(a.Gold);
            earned.Add(a);
            Earned?.Invoke(a);
        }
        return earned;
    }

    /// <summary>Convenience for the app: check after a run (Story or Classic) has ended.</summary>
    public IReadOnlyList<Achievement> OnRunEnded(RunStats run, LevelDefinition? level, int classicScore = 0) =>
        Check(Facts(run, level, classicScore));
}
