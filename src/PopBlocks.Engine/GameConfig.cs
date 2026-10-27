namespace PopBlocks.Engine;

/// <summary>
/// Single home for board dimensions and gameplay tuning values.
/// Nothing else in the solution should hard-code these numbers.
/// </summary>
public static class GameConfig
{
    // ---- Board (spec section 3) ----
    public const int Columns = 8;
    public const int VisibleRows = 16;
    public const int HiddenRows = 2;
    public const int TotalRows = VisibleRows + HiddenRows;

    // ---- Matching ----
    public const int MinGroupSize = 4;
    public const int ColourFamilies = 5;

    // ---- Speed (spec section 5.3) ----
    public const int InitialFallIntervalMs = 800;
    public const int MinFallIntervalMs = 140;
    public const int FallIntervalStepMs = 40;
    public const int LevelScoreStep = 800;

    /// <summary>Pause between chained burst waves so the player can read the chain.</summary>
    public const int ResolveStepMs = 260;

    // ---- Colours in play (keeps the opening forgiving) ----
    public const int StartingColours = 4;
    public const int LevelsPerExtraColour = 2;

    // ---- Scoring (spec section 5.8) ----
    public const int PointsPerCell = 10;
    public const int GroupBonusPerExtraCell = 10;
    public const int MaxChainMultiplier = 5;
    public const int SoftDropPointsPerRow = 1;
    public const int HardDropPointsPerRow = 2;
}
