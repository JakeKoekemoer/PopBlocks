using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Audio;

/// <summary>
/// One song the player can hear and pick: the Spectrum theme, or a region's piece in one of its three states.
/// <see cref="Number"/> is 1 to <see cref="Soundtrack.TrackCount"/> and is what is saved (0 means "automatic").
/// </summary>
public sealed record Track(int Number, string Title, string Place, RegionId Region, RegionState State)
{
    /// <summary>"Forgotten Garden · Faded", or "Main theme" for the Spectrum.</summary>
    public string Detail => Region == RegionId.Menu ? Place : $"{Place} · {StateName}";

    public string StateName => State switch
    {
        RegionState.Restored => "Restored",
        RegionState.Recovering => "Recovering",
        _ => "Faded",
    };
}

/// <summary>
/// The songs, and which of them the player has earned. A region's three songs are the same piece in its three states:
/// the faded song when the region opens, the recovering song at the mid-region discovery, the restored song when the
/// region is restored. The Spectrum theme (the menu music) is there from the start.
/// </summary>
public static class Soundtrack
{
    public const int TrackCount = 25;

    // The faded and restored titles belong to the region's music profile; the recovering ones are named here.
    private static readonly string[] RecoveringTitles =
    {
        "The First Bloom Returns",       // Forgotten Garden
        "A Voice in the Stone",          // Crystal Caves
        "Roots Begin to Listen",         // Verdant Wilds
        "Light Finds the Water",         // Sunken City
        "Gears Remember Their Turn",     // Clockwork Wastes
        "The First Thaw",                // Frozen Expanse
        "A Door Left Ajar",              // Shattered Citadel
        "Colours Reaching Home",         // Heart of the Spectrum
    };

    public static IReadOnlyList<Track> All { get; } = Build();

    private static Track[] Build()
    {
        var list = new List<Track>(TrackCount) { new(1, "Spectrum", "Main theme", RegionId.Menu, RegionState.Faded) };
        for (int n = 1; n <= 8; n++)
        {
            var profile = RegionProfiles.Get((RegionId)n);
            string place = RegionCatalog.Get(n).Title;
            int first = 2 + (n - 1) * 3;
            list.Add(new Track(first, profile.FadedThemeId, place, (RegionId)n, RegionState.Faded));
            list.Add(new Track(first + 1, RecoveringTitles[n - 1], place, (RegionId)n, RegionState.Recovering));
            list.Add(new Track(first + 2, profile.RestoredThemeId, place, (RegionId)n, RegionState.Restored));
        }
        return list.ToArray();
    }

    /// <summary>The song with this number, or null for 0 (automatic) or anything out of range.</summary>
    public static Track? Get(int number) => number >= 1 && number <= All.Count ? All[number - 1] : null;

    /// <summary>The song a region plays on its own in this state (what "automatic" means).</summary>
    public static Track For(int regionNumber, RegionState state)
    {
        if (regionNumber is < 1 or > 8) return All[0];
        return All[1 + (regionNumber - 1) * 3 + (int)state];
    }

    /// <summary>Whether a song has been earned, given the highest level the player has cleared.</summary>
    public static bool IsUnlocked(Track track, int highestCleared)
    {
        if (track.Region == RegionId.Menu) return true;
        var progress = RegionCatalog.Progress(RegionCatalog.Get((int)track.Region), highestCleared);
        return track.State switch
        {
            RegionState.Faded => progress.IsUnlocked,
            RegionState.Recovering => progress.IsUnlocked && progress.State >= RegionState.Recovering,
            _ => progress.IsUnlocked && progress.State == RegionState.Restored,
        };
    }

    public static IReadOnlyList<Track> Unlocked(int highestCleared) =>
        All.Where(t => IsUnlocked(t, highestCleared)).ToList();
}
