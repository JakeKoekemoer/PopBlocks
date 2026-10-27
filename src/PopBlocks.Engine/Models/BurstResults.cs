namespace PopBlocks.Engine.Models;

/// <summary>A connected same-colour group. Cell coordinates are absolute board coordinates.</summary>
public sealed record ColourGroup(CellColour Colour, IReadOnlyList<BlockCell> Cells)
{
    public int Size => Cells.Count;
}

/// <summary>A cell that fell from <see cref="FromY"/> to <see cref="ToY"/> in column <see cref="X"/>.</summary>
public readonly record struct CellMove(int X, int FromY, int ToY, CellColour Colour);

/// <summary>Everything that happened in one burst-and-gravity wave (used for scoring display and effects).</summary>
public sealed record BurstWave(
    int ChainIndex,
    int Multiplier,
    int Points,
    IReadOnlyList<ColourGroup> Groups,
    IReadOnlyList<BlockCell> ClearedCells,
    IReadOnlyList<CellMove> Moves);
