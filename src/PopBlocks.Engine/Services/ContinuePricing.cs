namespace PopBlocks.Engine.Services;

/// <summary>
/// What it costs to keep playing a Story level that ran out of moves: five more moves for gold, up to three times per
/// attempt (a retry, a reroll or a new level starts a fresh attempt with all three again). Each one costs more than the last.
/// </summary>
public static class ContinuePricing
{
    /// <summary>How many moves one continue adds.</summary>
    public const int ExtraMoves = 5;

    /// <summary>How many continues one attempt may buy.</summary>
    public const int MaxPerAttempt = 3;

    private static readonly int[] Prices = { 30, 42, 55 };

    /// <summary>The price of the <paramref name="ordinal"/>-th continue of an attempt: 1 is 30 gold, 2 is 42, 3 is 55. 0 past the cap.</summary>
    public static int Price(int ordinal) =>
        ordinal >= 1 && ordinal <= MaxPerAttempt ? Prices[ordinal - 1] : 0;
}
