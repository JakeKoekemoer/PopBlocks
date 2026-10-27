namespace PopBlocks.Engine.Logic;

/// <summary>
/// All scoring and difficulty rules in one place. Integer arithmetic only.
/// </summary>
public static class ScoringSystem
{
    /// <summary>Chain multiplier: x1 for the first wave, rising by one per wave, capped.</summary>
    public static int Multiplier(int chainIndex) =>
        Math.Clamp(chainIndex, 1, GameConfig.MaxChainMultiplier);

    /// <summary>Points for one group before the chain multiplier: base points plus a bonus for each cell beyond the minimum.</summary>
    public static int GroupPoints(int groupSize)
    {
        if (groupSize <= 0) return 0;
        int extra = Math.Max(0, groupSize - GameConfig.MinGroupSize);
        return checked(groupSize * GameConfig.PointsPerCell + extra * GameConfig.GroupBonusPerExtraCell);
    }

    /// <summary>Points for a whole wave (all groups cleared together) at the given chain position.</summary>
    public static int WavePoints(IEnumerable<int> groupSizes, int chainIndex)
    {
        int total = 0;
        foreach (int size in groupSizes)
            total = checked(total + GroupPoints(size));
        return checked(total * Multiplier(chainIndex));
    }

    public static int LevelForScore(int score) => 1 + Math.Max(0, score) / GameConfig.LevelScoreStep;

    /// <summary>Milliseconds per row: starts forgiving, speeds up linearly, never faster than the cap.</summary>
    public static int FallIntervalMs(int level)
    {
        int steps = Math.Max(0, level - 1);
        int interval = GameConfig.InitialFallIntervalMs - steps * GameConfig.FallIntervalStepMs;
        return Math.Max(GameConfig.MinFallIntervalMs, interval);
    }

    /// <summary>How many of the five colours can appear at this level.</summary>
    public static int ColourCountForLevel(int level)
    {
        int extra = Math.Max(0, level - 1) / GameConfig.LevelsPerExtraColour;
        return Math.Min(GameConfig.ColourFamilies, GameConfig.StartingColours + extra);
    }
}
