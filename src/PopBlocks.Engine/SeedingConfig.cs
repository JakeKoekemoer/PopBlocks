namespace PopBlocks.Engine;

/// <summary>
/// Every tunable number behind seeded levels, rerolls and Classic gold, in one place. They are starting values: retune
/// them from the sweep (docs/story/seed-sweep.csv) and from playtests, not by hunting through the code.
/// </summary>
public static class SeedingConfig
{
    // ---- Seeds ----
    /// <summary>A level seed is at most 30 bits: positive, and at most ten digits when shown.</summary>
    public const int SeedMask = 0x3FFFFFFF;

    // ---- Search (LevelRoller) ----
    /// <summary>How many candidate seeds one roll tries before settling for the closest verified one.</summary>
    public const int RollAttempts = 24;

    /// <summary>A roll stops looking after this long and uses the closest verified candidate it has.</summary>
    public static readonly TimeSpan RollBudget = TimeSpan.FromSeconds(2);

    /// <summary>Hardness is capped here: <c>min(HardnessCap, weakNeed / fullNeed)</c>.</summary>
    public const double HardnessCap = 2.0;

    // ---- Rerolls ----
    /// <summary>Failures after which a reroll is free. The price falls by a tenth of the first-clear gold per failure.</summary>
    public const int FailuresForFreeReroll = 10;

    // ---- Help after losing (v1.1) ----
    /// <summary>Total losses on a level after which "there is always a way through" is shown on the failed card.</summary>
    public const int LossesForSolvableNote = 5;

    /// <summary>Total losses on a level after which the player may ease it.</summary>
    public const int LossesForEase = 15;

    /// <summary>An eased level gets this many times the moves (rounded up) and is rolled one difficulty band lower.</summary>
    public const double EasedMoveFactor = 1.5;

    // ---- Player ----
    public const int NameMinLength = 1;
    public const int NameMaxLength = 20;
    public const string DefaultName = "Traveller";

    // ---- Loading card ----
    /// <summary>The recap card appears if a level is not ready this long after it was requested.</summary>
    public static readonly TimeSpan LoadingCardDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Once shown, the card stays at least this long, so it never flashes.</summary>
    public static readonly TimeSpan LoadingCardMinimum = TimeSpan.FromMilliseconds(600);

    // ---- Cache ----
    /// <summary>Generated (non-legacy) levels kept: the current level, the next level, one prefetched reroll, and slack.</summary>
    public const int CacheSize = 8;

    // ---- Classic gold ----
    /// <summary>Tenths of a gold per block popped at a Classic level: <c>min(ClassicMaxTenths, 1 + (L - 1) / ClassicLevelsPerStep)</c>.</summary>
    public const int ClassicMaxTenths = 5;
    public const int ClassicLevelsPerStep = 4;

    public static int ClassicTenthsPerBlock(int classicLevel) =>
        Math.Min(ClassicMaxTenths, 1 + (Math.Max(1, classicLevel) - 1) / ClassicLevelsPerStep);
}
