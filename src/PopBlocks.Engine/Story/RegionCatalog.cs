namespace PopBlocks.Engine.Story;

/// <summary>
/// The eight regions of the main campaign (levels 1-200, 25 each). Levels above the last region are the endless
/// "Beyond the Spectrum" band and belong to no region. Region state is a pure function of the highest cleared
/// level, so there is no second definition of campaign progress to keep in step with <c>PlayerProgress</c>.
/// </summary>
public static class RegionCatalog
{
    public const int LevelsPerRegion = 25;
    public const string BeyondTitle = "Beyond the Spectrum";

    public static IReadOnlyList<Region> All { get; } = new[]
    {
        Make(1, "r1", "The Forgotten Garden", "nature",
            "Faded leaves, closed flowers, dim lights.",
            "Green growth, open flowers, warm lights."),
        Make(2, "r2", "The Crystal Caves", "crystal",
            "Dark crystals, blocked paths.",
            "Glowing crystals, readable inscriptions."),
        Make(3, "r3", "The Verdant Wilds", "leaf",
            "Still vines, silent machines.",
            "Moving water, growing plants, workshop lamps."),
        Make(4, "r4", "The Sunken City", "water",
            "Dark underwater streets.",
            "Lit beacons, glowing water channels."),
        Make(5, "r5", "The Clockwork Wastes", "clockwork",
            "Stopped gears, broken bridges.",
            "Moving mechanisms, connected local power."),
        Make(6, "r6", "The Frozen Expanse", "ice",
            "Opaque ice, hidden paths.",
            "Clear ice, visible lights, open routes."),
        Make(7, "r7", "The Shattered Citadel", "citadel",
            "Broken bridges, sealed hall.",
            "Reconnected paths, lit archive."),
        Make(8, "r8", "The Heart of Spectrum", "spectrum",
            "Fractured central chamber.",
            "Reunited Heart with light flowing outward."),
    };

    public static int LastCampaignLevel => All[^1].LastLevel;

    private static Region Make(int number, string id, string title, string theme, string faded, string restored) =>
        new(number, id, title, (number - 1) * LevelsPerRegion + 1, number * LevelsPerRegion, theme, faded, restored);

    /// <summary>The region a level belongs to, or null above the campaign ("Beyond the Spectrum") and below level 1.</summary>
    public static Region? For(int level) => All.FirstOrDefault(r => r.Contains(level));

    public static Region Get(int number) => All[number - 1];

    /// <summary>The player's progress through a region, given the highest level they have cleared.</summary>
    public static RegionProgress Progress(Region region, int highestCleared)
    {
        int cleared = Math.Clamp(highestCleared - (region.FirstLevel - 1), 0, region.LevelCount);

        var state = highestCleared >= region.LastLevel ? RegionState.Restored
            : highestCleared >= region.MidLevel ? RegionState.Recovering
            : RegionState.Faded;

        // A region opens when the player reaches its first level, i.e. cleared the one before it.
        bool unlocked = highestCleared + 1 >= region.FirstLevel;
        return new RegionProgress(region, cleared, state, unlocked);
    }
}
