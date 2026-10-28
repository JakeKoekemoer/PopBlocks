namespace PopBlocks.Engine.Models;

/// <summary>A connected same-colour group. Cell coordinates are absolute board coordinates.</summary>
public sealed record ColourGroup(CellColour Colour, IReadOnlyList<BlockCell> Cells)
{
    public int Size => Cells.Count;
}

/// <summary>A cell that fell from <see cref="FromY"/> to <see cref="ToY"/> in column <see cref="X"/>.</summary>
public readonly record struct CellMove(int X, int FromY, int ToY, CellColour Colour);

/// <summary>
/// A stack of blocks hanging over empty cells. (X, Y) is the lowest block of the stack, <see cref="Gap"/> is how many empty
/// cells sit directly under it (it would fall that far), and <see cref="Colour"/> is the lowest block's colour.
/// </summary>
public readonly record struct HangingRun(int X, int Y, int Gap, CellColour Colour);

/// <summary>Everything that happened in one burst-and-gravity wave (used for scoring display and effects).</summary>
public sealed record BurstWave(
    int ChainIndex,
    int Multiplier,
    int Points,
    IReadOnlyList<ColourGroup> Groups,
    IReadOnlyList<BlockCell> ClearedCells,
    IReadOnlyList<CellMove> Moves);
