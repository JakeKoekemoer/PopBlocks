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

    /// <summary>
    /// The same piece with its colours dealt out again in <paramref name="colours"/> order (one per cell, in cell order).
    /// Geometry, position and rotation centre never change, so the new piece fits wherever this one did.
    /// </summary>
    public Piece WithColourOrder(IReadOnlyList<CellColour> colours)
    {
        if (colours.Count != Cells.Count) throw new ArgumentException("One colour per cell is needed.", nameof(colours));
        var cells = new BlockCell[Cells.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = new BlockCell(Cells[i].X, Cells[i].Y, colours[i]);
        return new Piece(cells, X, Y, Pivot2X, Pivot2Y);
    }

    /// <summary>How many different colourings this piece's colours allow (1 for a single-coloured piece).</summary>
    public int DistinctColourOrders
    {
        get
        {
            long total = 1;
            for (int i = 2; i <= Cells.Count; i++) total *= i;
            foreach (var group in Cells.GroupBy(c => c.Colour))
                for (int i = 2; i <= group.Count(); i++) total /= i;
            return (int)total;
        }
    }

    /// <summary>
    /// The next different colouring of the same colours, in a fixed order that visits every colouring once and then
    /// starts over. Null when there is only one colouring (a single-coloured piece): nothing can be rearranged.
    /// </summary>
    public Piece? WithNextColourOrder()
    {
        if (DistinctColourOrders < 2) return null;

        var order = Cells.Select(c => (byte)c.Colour).ToArray();
        if (!NextPermutation(order)) Array.Sort(order);   // the last colouring wraps to the first
        return WithColourOrder(order.Select(b => (CellColour)b).ToArray());
    }

    /// <summary>Lexicographic next permutation in place; false (and unchanged) when already the last.</summary>
    private static bool NextPermutation(byte[] a)
    {
        int i = a.Length - 2;
        while (i >= 0 && a[i] >= a[i + 1]) i--;
        if (i < 0) return false;
        int j = a.Length - 1;
        while (a[j] <= a[i]) j--;
        (a[i], a[j]) = (a[j], a[i]);
        Array.Reverse(a, i + 1, a.Length - i - 1);
        return true;
    }

    /// <summary>
    /// The colour of a connected same-colour group of <see cref="GameConfig.MinGroupSize"/> or more cells inside this
    /// piece, or <see cref="CellColour.Empty"/> when there is none. A piece with one is "solid": it is a burst-sized
    /// group all by itself, so the board keeps it dormant instead of popping it the moment it lands.
    /// </summary>
    public CellColour SolidColour
    {
        get
        {
            var colourAt = new Dictionary<(int, int), CellColour>(Cells.Count);
            foreach (var c in Cells) colourAt[(c.X, c.Y)] = c.Colour;

            var seen = new HashSet<(int, int)>();
            foreach (var start in colourAt)
            {
                if (!seen.Add(start.Key)) continue;
                var stack = new Stack<(int X, int Y)>();
                stack.Push(start.Key);
                int size = 0;
                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();
                    size++;
                    foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                        if (colourAt.TryGetValue(next, out var colour) && colour == start.Value && seen.Add(next))
                            stack.Push(next);
                }
                if (size >= GameConfig.MinGroupSize) return start.Value;
            }
            return CellColour.Empty;
        }
    }

    /// <summary>True when the piece is a burst-sized group of one colour by itself (see <see cref="SolidColour"/>).</summary>
    public bool IsSolid => SolidColour != CellColour.Empty;

    /// <summary>Absolute board coordinates of every cell (allocates; for logic and tests, not per-frame drawing).</summary>
    public IEnumerable<BlockCell> BoardCells()
    {
        foreach (var c in Cells)
            yield return new BlockCell(X + c.X, Y + c.Y, c.Colour);
    }
}
