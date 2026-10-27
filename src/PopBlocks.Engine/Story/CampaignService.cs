using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Story;

/// <summary>
/// Connects level progress to the story: which scene to show, which rewards are due. All state is a set of ids in
/// <see cref="UnlockStore"/>; region state is derived from <see cref="PlayerProgress.HighestCleared"/>.
/// Rewards are granted through the store, so each can be paid only once however often a level is replayed or a
/// scene is skipped.
/// </summary>
public sealed class CampaignService
{
    public const string CosmeticGarden = "cosmetic_garden";

    private readonly UnlockStore _unlocks;
    private readonly PlayerProgress _progress;

    public CampaignService(UnlockStore unlocks, PlayerProgress progress)
    {
        _unlocks = unlocks;
        _progress = progress;
    }

    public static string SeenId(string sceneId) => "seen_" + sceneId;
    public static string LoreId(string loreId) => "lore_" + loreId;
    public static string FragmentUnlockId(Region region) => "frag_" + region.Id;
    public static string RegionGoldId(Region region) => "gold_" + region.Id;

    public bool HasSeen(string sceneId) => _unlocks.Has(SeenId(sceneId));

    /// <summary>Records that a scene was finished or skipped. Either counts: it will not be pushed on the player again.</summary>
    public void MarkSeen(string sceneId) => _unlocks.Unlock(SeenId(sceneId));

    public bool HasFragment(Region region) => _unlocks.Has(FragmentUnlockId(region));

    public int FragmentCount => RegionCatalog.All.Count(HasFragment);

    public bool HasLore(LoreEntry entry) =>
        entry.UnlockLevel == 0 || _unlocks.Has(LoreId(entry.Id));

    /// <summary>The furthest level cleared (a convenience for services built on the campaign).</summary>
    public int HighestCleared => _progress.HighestCleared;

    public RegionProgress ProgressOf(Region region) => RegionCatalog.Progress(region, _progress.HighestCleared);

    public int RestoredRegionCount => RegionCatalog.All.Count(r => ProgressOf(r).IsRestored);

    /// <summary>
    /// The scene to show when the player starts a level: a region's opening on its first level, or a level intro
    /// (such as the final puzzle's) on its level. Only the first time; replaying never returns a scene.
    /// </summary>
    public StoryScene? OnLevelEntered(int level)
    {
        var region = RegionCatalog.For(level);
        if (region == null) return null;

        if (level == region.FirstLevel)
        {
            var opening = StoryContent.FindScene(region.OpeningSceneId);
            if (opening != null && !HasSeen(opening.Id)) return opening;
        }

        return StoryContent.Scenes.FirstOrDefault(s =>
            s.Trigger == SceneTrigger.LevelIntro && s.Region == region.Number && s.UnlockLevel == level && !HasSeen(s.Id));
    }

    /// <summary>
    /// A one-line Pip remark for returning to a region's first level once its opening has been seen. It is a banner
    /// line, repeatable, and never marked seen: replays get "a brief replay line or no dialogue", not the scene again.
    /// </summary>
    public StoryScene? ReplayRemark(int level)
    {
        var region = RegionCatalog.For(level);
        if (region == null || level != region.FirstLevel || level > _progress.HighestCleared) return null;
        if (!HasSeen(region.OpeningSceneId)) return null;
        return StoryContent.FindScene(region.ReplaySceneId);
    }

    /// <summary>
    /// Call once per cleared level, after <see cref="PlayerProgress.Complete"/>. Does nothing for a replay.
    /// Rewards are granted here, before any scene is shown, so skipping a scene cannot lose them.
    /// </summary>
    public CampaignOutcome OnLevelCompleted(int level, CompletionResult result)
    {
        if (!result.FirstClear) return CampaignOutcome.None;

        var region = RegionCatalog.For(level);
        if (region == null) return CampaignOutcome.None;

        var scenes = StoryContent.Scenes
            .Where(s => s.Region == region.Number && s.UnlockLevel == level && FiresOnClear(s.Trigger) && !HasSeen(s.Id))
            .ToList();

        var lore = GrantLore(l => l.UnlockLevel == level);

        bool fragment = false;
        string? cosmetic = null;
        int regionGold = 0;
        IReadOnlyList<Cosmetic>? found = null;
        if (level == region.LastLevel)
        {
            (fragment, cosmetic) = GrantRegionCompletion(region);
            found = CosmeticCatalog.FoundByRestoring(region.Number).ToList();

            // A one-off reward for restoring a region in play. Players who are caught up quietly (Reconcile) are not
            // paid retroactively, so a save from before this existed keeps exactly the gold it had.
            if (_unlocks.Unlock(RegionGoldId(region)))
            {
                regionGold = ShopCatalog.RegionClearGold(region.Number);
                _progress.AwardGold(regionGold);
            }
        }

        RegionState? reached = level == region.LastLevel ? RegionState.Restored
            : level == region.MidLevel ? RegionState.Recovering
            : null;

        return new CampaignOutcome(region, reached, scenes, fragment, lore, cosmetic, regionGold, found);
    }

    private static bool FiresOnClear(SceneTrigger t) =>
        t is SceneTrigger.Milestone or SceneTrigger.MidDiscovery or SceneTrigger.RegionCompletion or SceneTrigger.Ending;

    /// <summary>
    /// A banner reaction for the player's first burst, or first chain. Returned at most once ever per kind; it is
    /// marked seen immediately because a banner is not something the player can "skip". A first burst that is
    /// already a chain gives only the chain line.
    /// </summary>
    public StoryScene? OnBurst(int chainLength)
    {
        var burst = StoryContent.FindScene(StoryContent.ReactionFirstBurst);
        var chain = StoryContent.FindScene(StoryContent.ReactionFirstChain);

        if (chainLength >= 2 && chain != null && !HasSeen(chain.Id))
        {
            MarkSeen(chain.Id);
            if (burst != null) MarkSeen(burst.Id);
            return chain;
        }

        if (burst != null && !HasSeen(burst.Id))
        {
            MarkSeen(burst.Id);
            return burst;
        }

        return null;
    }

    /// <summary>
    /// Grants rewards for everything the player had already passed before the story existed (or after a jump). Quiet:
    /// returns nothing to show, and does not mark scenes seen (they stay readable in the Archive through progress).
    /// </summary>
    public void Reconcile()
    {
        int highest = _progress.HighestCleared;
        GrantLore(l => l.UnlockLevel > 0 && l.UnlockLevel <= highest);

        foreach (var region in RegionCatalog.All)
            if (ProgressOf(region).IsRestored) GrantRegionCompletion(region);

        GrantEpilogueJournalIfDue();
    }

    /// <summary>Is a scene available to reread in the Archive: seen, or the player has progressed past its trigger.</summary>
    public bool IsSceneAvailable(StoryScene scene) =>
        scene.CanReplay && (HasSeen(scene.Id)
                            || (scene.UnlockLevel > 0 && _progress.HighestCleared >= scene.UnlockLevel)
                            || (scene.Id == StoryContent.EpilogueSceneId && EpilogueUnlocked));

    /// <summary>Archive entries the player can read now. Entries with an unlock level need the matching reward.</summary>
    public IEnumerable<LoreEntry> UnlockedLore() => StoryContent.Lore.Where(HasLore);

    /// <summary>Grants an entry that only an explicit event unlocks (a memory). True the first time.</summary>
    public bool GrantLoreEntry(string loreId) => _unlocks.Unlock(LoreId(loreId));

    // ---- Memories (optional objectives) ----

    public int MemoriesFound(Region region) => StoryContent.MemoriesOf(region.Number).Count(m => _unlocks.Has(LoreId(m.Id)));

    public int TotalMemoriesFound => RegionCatalog.All.Sum(MemoriesFound);

    /// <summary>Archive entries the player can read now (story, characters, fragments, memories, scenes).</summary>
    public int ArchiveEntryCount => UnlockedLore().Count() + StoryContent.Scenes.Count(IsSceneAvailable);

    /// <summary>Regions in which at least one memory has been found.</summary>
    public int RegionsWithMemory => RegionCatalog.All.Count(r => MemoriesFound(r) > 0);

    /// <summary>Some region has every one of its memories found.</summary>
    public bool AnyRegionFullyRemembered =>
        RegionCatalog.All.Any(r => { int total = StoryContent.MemoriesOf(r.Number).Count(); return total > 0 && MemoriesFound(r) == total; });

    public int TotalMemories => RegionCatalog.All.Sum(r => StoryContent.MemoriesOf(r.Number).Count());

    /// <summary>At least one memory has been found in every region: the optional epilogue and its journal entry open up.</summary>
    public bool EpilogueUnlocked => RegionCatalog.All.All(r => MemoriesFound(r) > 0);

    /// <summary>
    /// Call when a level has been won, with that run's stats. If the level carries a memory objective and it was met,
    /// the memory is added to the Archive. Works on replays too (a memory is found, not bought, and pays no gold).
    /// Returns what was newly added: the memory, and the epilogue journal if this was the last region still missing one.
    /// </summary>
    public IReadOnlyList<LoreEntry> OnBonusEarned(LevelDefinition level, RunStats run)
    {
        if (level.Bonus is not { } bonus || !bonus.IsEarned(run)) return Array.Empty<LoreEntry>();

        var found = new List<LoreEntry>();
        var memory = StoryContent.FindLore(bonus.MemoryId);
        if (memory is null || memory.Kind != LoreKind.Memory) return found;

        if (GrantLoreEntry(memory.Id)) found.Add(memory);
        if (found.Count > 0) GrantEpilogueJournalIfDue(found);
        return found;
    }

    private void GrantEpilogueJournalIfDue(List<LoreEntry>? found = null)
    {
        if (!EpilogueUnlocked) return;
        if (GrantLoreEntry(StoryContent.EpilogueJournalId) && StoryContent.FindLore(StoryContent.EpilogueJournalId) is { } journal)
            found?.Add(journal);
    }

    private List<LoreEntry> GrantLore(Func<LoreEntry, bool> which)
    {
        var granted = new List<LoreEntry>();
        foreach (var entry in StoryContent.Lore)
            if (entry.UnlockLevel != 0 && which(entry) && _unlocks.Unlock(LoreId(entry.Id)) && entry.Kind != LoreKind.Fragment)
                granted.Add(entry);
        return granted;
    }

    private (bool Fragment, string? Cosmetic) GrantRegionCompletion(Region region)
    {
        bool fragment = _unlocks.Unlock(FragmentUnlockId(region));
        string? cosmetic = null;
        if (region.Number == 1 && _unlocks.Unlock(CosmeticGarden)) cosmetic = CosmeticGarden;
        return (fragment, cosmetic);
    }
}
