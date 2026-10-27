namespace PopBlocks.Engine.Story;

/// <summary>How far a region has been restored. Derived from the highest cleared level, never saved.</summary>
public enum RegionState
{
    /// <summary>Not substantially restored.</summary>
    Faded,

    /// <summary>Past the mid-region discovery: colour is coming back.</summary>
    Recovering,

    /// <summary>The region's last level has been cleared.</summary>
    Restored,
}

/// <summary>One of the eight regions of the campaign: a band of consecutive level numbers with its own story.</summary>
public sealed record Region(
    int Number,
    string Id,
    string Title,
    int FirstLevel,
    int LastLevel,
    string ThemeKey,
    string FadedDescription,
    string RestoredDescription)
{
    public int LevelCount => LastLevel - FirstLevel + 1;

    /// <summary>The level whose first clear starts the region's recovery (the 13th of 25).</summary>
    public int MidLevel => FirstLevel + LevelCount / 2;

    public string OpeningSceneId => $"{Id}_open";
    public string MidSceneId => $"{Id}_mid";
    public string CompletionSceneId => $"{Id}_complete";
    public string ReplaySceneId => $"{Id}_replay";
    public string FragmentId => $"fragment_{Id}";

    public bool Contains(int level) => level >= FirstLevel && level <= LastLevel;

    /// <summary>"The Crystal Caves" becomes "the Crystal Caves" for use in a sentence.</summary>
    public string TitleInSentence => Title.StartsWith("The ", StringComparison.Ordinal) ? "the " + Title[4..] : Title;
}

/// <summary>A region together with the player's progress through it.</summary>
public sealed record RegionProgress(Region Region, int LevelsCleared, RegionState State, bool IsUnlocked)
{
    public bool IsRestored => State == RegionState.Restored;

    /// <summary>0..1 for a progress bar.</summary>
    public double Fraction => Region.LevelCount == 0 ? 0 : (double)LevelsCleared / Region.LevelCount;

    /// <summary>The first level of the region the player has not cleared (the region's last level when finished).</summary>
    public int NextLevel => Math.Min(Region.LastLevel, Region.FirstLevel + LevelsCleared);
}
