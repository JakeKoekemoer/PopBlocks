using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Story;

/// <summary>One line of the loading card: where it comes from and what it says.</summary>
public sealed record RecapItem(string Source, string Text);

/// <summary>
/// When the loading card appears and how long it stays. Pure functions of elapsed time, so the thresholds are tested
/// without a screen.
/// </summary>
public static class LoadingPolicy
{
    /// <summary>The card is shown if the level is still not ready this long after it was asked for.</summary>
    public static bool ShouldShowCard(TimeSpan sinceRequested, bool levelReady) =>
        !levelReady && sinceRequested >= SeedingConfig.LoadingCardDelay;

    /// <summary>How much longer a card that has been up for <paramref name="shownFor"/> must stay, so it never flashes.</summary>
    public static TimeSpan HoldRemaining(TimeSpan shownFor) =>
        shownFor >= SeedingConfig.LoadingCardMinimum ? TimeSpan.Zero : SeedingConfig.LoadingCardMinimum - shownFor;
}

/// <summary>
/// The short recap shown on the loading card: a few lines from the story the player has already seen, most recent first.
/// It is built only from scenes and lore the unlock store says were seen or found, so it can never spoil anything.
/// </summary>
public static class LoadingRecap
{
    public const int MaxItems = 3;
    private const int MaxChars = 150;

    public static IReadOnlyList<RecapItem> Build(UnlockStore unlocks, int max = MaxItems)
    {
        var candidates = new List<(int Order, RecapItem Item)>();

        // Scenes the player has seen (skipping counts as seeing). One-line reaction banners are not worth recapping.
        for (int i = 0; i < StoryContent.Scenes.Count; i++)
        {
            var scene = StoryContent.Scenes[i];
            if (!scene.CanReplay || !unlocks.Has(CampaignService.SeenId(scene.Id))) continue;

            var line = scene.Lines.FirstOrDefault(l => l.Text.Length > 0);
            if (line is null) continue;
            candidates.Add((OrderOf(scene.UnlockLevel, i), new RecapItem(scene.Title, Trim(line.Text))));
        }

        // Journal entries and memories the player holds.
        for (int i = 0; i < StoryContent.Lore.Count; i++)
        {
            var lore = StoryContent.Lore[i];
            if (lore.Kind is not (LoreKind.Journal or LoreKind.Memory) || !unlocks.Has(CampaignService.LoreId(lore.Id))) continue;
            int level = lore.UnlockLevel > 0 ? lore.UnlockLevel : MemoryLevel(lore.Region);
            candidates.Add((OrderOf(level, 10_000 + i), new RecapItem(lore.Title, Trim(lore.Text))));
        }

        return candidates.OrderByDescending(c => c.Order).Take(max).Select(c => c.Item).ToList();
    }

    /// <summary>Later levels first; within a level, later content first.</summary>
    private static int OrderOf(int level, int index) => level * 100_000 + index;

    /// <summary>A memory has no clear level; place it at its region's second memory slot.</summary>
    private static int MemoryLevel(int region) =>
        RegionCatalog.Get(Math.Clamp(region, 1, RegionCatalog.All.Count)).FirstLevel + Levels.BonusPlanner.Offsets[^1];

    private static string Trim(string text)
    {
        text = text.Replace('\n', ' ').Trim();
        if (text.Length <= MaxChars) return text;
        int cut = text.LastIndexOf(' ', MaxChars);
        return text[..(cut > 40 ? cut : MaxChars)].TrimEnd('.', ',', ' ') + "…";
    }
}
