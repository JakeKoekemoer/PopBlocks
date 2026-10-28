namespace PopBlocks.Engine.Services;

/// <summary>
/// What a reroll costs. A stuck player can swap the level they are on for a new one; the price is the level's first-clear
/// gold, less a tenth of it for every failure so far, and nothing at all from the tenth failure on. So gold can never
/// keep anyone from the story: after enough tries the way forward is free.
/// </summary>
public static class RerollPricing
{
    /// <summary>
    /// <c>(reward × (N − min(failures, N)) + N − 1) / N</c> with N = <see cref="SeedingConfig.FailuresForFreeReroll"/>, rounded up,
    /// in integers. Levels that pay no gold (the opening levels) are always free.
    /// </summary>
    public static int Price(int level, int failures)
    {
        int n = SeedingConfig.FailuresForFreeReroll;
        int reward = GoldRewards.For(level, isTutorial: false);
        int f = Math.Clamp(failures, 0, n);
        return (int)(((long)reward * (n - f) + (n - 1)) / n);
    }

    /// <summary>How many more failures until the reroll is free. 0 once it is.</summary>
    public static int FailuresUntilFree(int failures) => Math.Max(0, SeedingConfig.FailuresForFreeReroll - Math.Max(0, failures));
}
