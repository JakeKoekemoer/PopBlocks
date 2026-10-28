namespace PopBlocks.Engine.Audio;

/// <summary>
/// Composes each region's score the first time it is needed and keeps it. Composing is deterministic, so the same notes come
/// out every run, and it takes a few milliseconds, so it is done off the UI thread by <see cref="GameAudio"/>.
/// </summary>
public static class ScoreLibrary
{
    private static readonly Dictionary<RegionId, MusicScore> Scores = new();

    public static MusicScore Get(RegionId region)
    {
        lock (Scores)
        {
            if (!Scores.TryGetValue(region, out var score))
                Scores[region] = score = MusicComposer.Compose(RegionProfiles.Get(region));
            return score;
        }
    }

    /// <summary>The 1–8 number a region has in the story to its audio id (0 or anything else is the menu theme).</summary>
    public static RegionId FromNumber(int regionNumber) =>
        regionNumber is >= 1 and <= 8 ? (RegionId)regionNumber : RegionId.Menu;
}
