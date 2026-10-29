using PopBlocks.Engine;

namespace PopBlocks.Graphics;

/// <summary>
/// Converts board cells to screen positions. When <c>flip</c> is on (Extreme levels) the picture is turned
/// upside down: board row 17 (the floor) is at the top of the screen, and column 0 is on the right.
/// Only the drawing is turned; the engine is unchanged.
/// </summary>
public static class BoardSpace
{
    /// <summary>
    /// The board row (y) at the top of the screen. On the standard board this is the first row below the hidden ones, always.
    /// On a tall board it follows the camera (the drawing sets it once per frame, easing between camera positions), so every
    /// effect that maps a board row to the screen moves with the board. The UI draws on one thread, so a plain field is enough.
    /// </summary>
    public static float ViewTop = GameConfig.HiddenRows;

    /// <summary>Is this board row above the screen (not drawn)? With the standard board that is exactly the hidden rows.</summary>
    public static bool AboveView(int y) => y < MathF.Floor(ViewTop);

    /// <summary>Is this board row well below the screen (not drawn)? One row of margin is kept for the camera's ease.</summary>
    public static bool BelowView(int y) => y > ViewTop + GameConfig.ViewportRows + 1f;

    /// <summary>Left edge of board column <paramref name="x"/> on screen.</summary>
    public static float ScreenX(int x, bool flip, float originX, float cell) =>
        originX + (flip ? GameConfig.Columns - 1 - x : x) * cell;

    /// <summary>
    /// Top edge of board row <paramref name="y"/> (hidden rows included) on screen. <paramref name="fallOffset"/> is the
    /// animation offset in cells from <see cref="BlastEffects.FallOffsetCells"/> (zero or negative: "further up the board").
    /// </summary>
    public static float ScreenY(int y, bool flip, float originY, float cell, float fallOffset = 0f)
    {
        float row = y - ViewTop;
        float screenRow = flip ? (GameConfig.VisibleRows - 1 - row) - fallOffset : row + fallOffset;
        return originY + screenRow * cell;
    }

    /// <summary>Screen column of a board column (0 = leftmost on screen).</summary>
    public static int ScreenColumn(int x, bool flip) => flip ? GameConfig.Columns - 1 - x : x;

    /// <summary>Cheap repeatable "random" number in 0..1 from two integers (for particles; no allocation, no state).</summary>
    public static float Noise(int a, int b)
    {
        double v = Math.Sin(a * 12.9898 + b * 78.233) * 43758.5453;
        return (float)(v - Math.Floor(v));
    }
}
