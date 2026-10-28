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

    /// <summary>
    /// A "solid" piece is one that is already a burst-sized group of a single colour all by itself (say a one-colour
    /// square). Landing never pops one: its cells go dormant and wait until another block of that colour joins them.
    /// This is the chance that the generator lets a would-be solid piece stay solid; every other would-be solid piece
    /// is quietly recoloured so it is not. Roughly 1 piece in 60 comes out solid at 0.15.
    /// </summary>
    public const double SolidPieceChance = 0.15;

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
