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

/// <summary>
/// What one popped group paid: where it was (the centre of its cells, in board cells, so the app can float a number there),
/// how many cells, the chain multiplier it popped at, and the pop gold in tenths of a gold (0 when the level pays none).
/// </summary>
public readonly record struct GroupGold(float CentreX, float CentreY, int Cells, int Multiplier, int GoldTenths, CellColour Colour);

/// <summary>Everything that happened in one burst-and-gravity wave (used for scoring display and effects).</summary>
public sealed record BurstWave(
    int ChainIndex,
    int Multiplier,
    int Points,
    IReadOnlyList<ColourGroup> Groups,
    IReadOnlyList<BlockCell> ClearedCells,
    IReadOnlyList<CellMove> Moves)
{
    /// <summary>One entry per popped group, in the same order as <see cref="Groups"/>.</summary>
    public IReadOnlyList<GroupGold> Gold { get; init; } = Array.Empty<GroupGold>();

    /// <summary>Blocks that broke in this wave because a third crack reached them (they are also in <see cref="ClearedCells"/>, not in <see cref="Groups"/>).</summary>
    public IReadOnlyList<BlockCell> Broken { get; init; } = Array.Empty<BlockCell>();

    /// <summary>Blocks that survived this wave with a new crack (positions as they were when the wave popped).</summary>
    public IReadOnlyList<BlockCell> Cracked { get; init; } = Array.Empty<BlockCell>();

    /// <summary>Tenths of a gold this wave paid, over all its groups.</summary>
    public int GoldTenths
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Gold.Count; i++) total += Gold[i].GoldTenths;
            return total;
        }
    }
}
