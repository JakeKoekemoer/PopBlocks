namespace PopBlocks.Engine.Models;

/// <summary>
/// A falling cluster of 2-4 coloured cells. Immutable: moving or rotating returns a new piece.
/// Cell X/Y values are offsets from the pivot; (X, Y) is the pivot's board position.
/// </summary>
public sealed class Piece
{
    public IReadOnlyList<BlockCell> Cells { get; }

    /// <summary>Board position of the pivot.</summary>
    public int X { get; }
    public int Y { get; }

    /// <summary>
    /// Rotation centre in doubled offset units, so a centre between cells is representable
    /// (e.g. a 2x2 square rotates about (1,1)/2 = (0.5,0.5)). (0,0) means "rotate about the pivot cell".
    /// </summary>
    public int Pivot2X { get; }
    public int Pivot2Y { get; }

    public int Count => Cells.Count;

    public Piece(IReadOnlyList<BlockCell> cells, int x, int y, int pivot2X = 0, int pivot2Y = 0)
    {
        if (cells.Count < 1) throw new ArgumentException("A piece needs at least one cell.", nameof(cells));
        Cells = cells;
        X = x;
        Y = y;
        Pivot2X = pivot2X;
        Pivot2Y = pivot2Y;
    }

    /// <summary>
    /// Places a piece in the hidden spawn area: horizontally centred, with its top row on the
    /// last hidden row so it enters the visible board after a single step.
    /// </summary>
    public static Piece AtSpawn(IReadOnlyList<BlockCell> cells, int pivot2X = 0, int pivot2Y = 0)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue;
        foreach (var c in cells)
        {
            if (c.X < minX) minX = c.X;
            if (c.X > maxX) maxX = c.X;
            if (c.Y < minY) minY = c.Y;
        }
        int width = maxX - minX + 1;
        int x = (GameConfig.Columns - width) / 2 - minX;
        int y = (GameConfig.HiddenRows - 1) - minY;
        return new Piece(cells, x, y, pivot2X, pivot2Y);
    }

    public Piece Moved(int dx, int dy) => new(Cells, X + dx, Y + dy, Pivot2X, Pivot2Y);

    public Piece MovedTo(int x, int y) => new(Cells, x, y, Pivot2X, Pivot2Y);

    /// <summary>Rotates 90 degrees about the pivot. Colours travel with their cells.</summary>
    public Piece Rotated(bool clockwise = true)
    {
        var rotated = new BlockCell[Cells.Count];
        for (int i = 0; i < Cells.Count; i++)
        {
            var c = Cells[i];
            int rx = 2 * c.X - Pivot2X;   // offset from the rotation centre, doubled
            int ry = 2 * c.Y - Pivot2Y;
            // Screen coordinates (Y down): clockwise maps right -> down.
            int nx2 = clockwise ? -ry : ry;
            int ny2 = clockwise ? rx : -rx;
            rotated[i] = new BlockCell((nx2 + Pivot2X) / 2, (ny2 + Pivot2Y) / 2, c.Colour);
        }
        return new Piece(rotated, X, Y, Pivot2X, Pivot2Y);
    }

    /// <summary>Absolute board coordinates of every cell (allocates; for logic and tests, not per-frame drawing).</summary>
    public IEnumerable<BlockCell> BoardCells()
    {
        foreach (var c in Cells)
            yield return new BlockCell(X + c.X, Y + c.Y, c.Colour);
    }
}
