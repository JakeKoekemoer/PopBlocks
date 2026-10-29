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

    // ---- Tall boards (v1.1): the board can be taller than the screen; the camera follows the stack ----
    /// <summary>Rows the screen shows at once.</summary>
    public const int ViewportRows = VisibleRows;

    /// <summary>
    /// Empty rows the camera keeps above the top of the stack. The screen scrolls only once the stack is taller than
    /// <c>ViewportRows - CameraHeadroomRows</c> (12 rows), and then keeps the top of the stack that far below the top of the screen,
    /// so the player always sees the stack's top three quarters and still has room above it to steer a piece. (Half the screen,
    /// 8 rows, scrolled too early and hid too much of the stack.)
    /// </summary>
    public const int CameraHeadroomRows = 4;

    /// <summary>The standard board height and the shortest and tallest a player can ask for (rows, not counting the hidden spawn rows).</summary>
    public const int MinBoardRows = 16;
    public const int MaxBoardRows = 32;

    /// <summary>
    /// Board height when the player has not chosen one: 24 rows, of which the screen shows 16 and follows the stack. A solved Story
    /// level is still winnable here (the solver plays on 16 rows, and extra room never takes a move away). Lessons stay at 16.
    /// </summary>
    public const int DefaultBoardRows = 24;

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
