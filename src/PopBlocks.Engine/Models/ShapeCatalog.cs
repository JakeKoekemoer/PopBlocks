namespace PopBlocks.Engine.Models;

/// <summary>
/// One piece shape: cell offsets plus the rotation centre. Pivots are doubled (see <see cref="Piece"/>),
/// so a centre on a cell is an even number and a centre between four cells is odd.
/// </summary>
public sealed record ShapeDef(string Name, int Weight, (int Dx, int Dy)[] Offsets, int Pivot2X = 0, int Pivot2Y = 0)
{
    public int CellCount => Offsets.Length;

    /// <summary>
    /// True when every cell touches another cell of the shape sideways or up/down, so the shape is one solid
    /// piece. A line with a missing middle, or two cells that only meet at a corner, is not connected and is
    /// hard to read and place. Every shape in <see cref="ShapeCatalog"/> must satisfy this (a test enforces it).
    /// </summary>
    public bool IsConnected => IsEdgeConnected(Offsets);

    public static bool IsEdgeConnected(IReadOnlyList<(int Dx, int Dy)> cells)
    {
        if (cells.Count == 0) return false;
        var remaining = new HashSet<(int, int)>(cells.Select(c => (c.Dx, c.Dy)));
        if (remaining.Count != cells.Count) return false;   // two cells on the same spot

        var queue = new Queue<(int X, int Y)>();
        var first = cells[0];
        queue.Enqueue((first.Dx, first.Dy));
        remaining.Remove((first.Dx, first.Dy));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (remaining.Remove(next)) queue.Enqueue(next);
        }
        return remaining.Count == 0;
    }
}

/// <summary>
/// Every shape in the game. The Classic set is the original tetromino-like family; the Exotic set
/// (plus, long, zig-zag, stairs...) appears in Story mode from level 11 on.
///
/// Every shape must be one solid, edge-connected piece: no gaps in a line and no cells that touch only at
/// a corner. (An earlier alpha had a "Gap" and a "Diagonal" piece; playtesters found them unreadable and
/// hard to place, so they were removed.)
/// </summary>
public static class ShapeCatalog
{
    public static readonly ShapeDef[] Classic =
    {
        new("Pair",   3, new[] { (0, 0), (1, 0) }),
        new("Bar",    2, new[] { (-1, 0), (0, 0), (1, 0) }),
        new("Elbow",  3, new[] { (0, 0), (1, 0), (0, 1) }),
        new("Block",  1, new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, Pivot2X: 1, Pivot2Y: 1),
        new("Hook",   1, new[] { (-1, 0), (0, 0), (1, 0), (1, 1) }),
        new("Tee",    1, new[] { (-1, 0), (0, 0), (1, 0), (0, 1) }),
    };

    public static readonly ShapeDef[] Exotic =
    {
        new("Plus",     2, new[] { (0, -1), (-1, 0), (0, 0), (1, 0), (0, 1) }),
        new("Zig",      2, new[] { (0, 0), (1, 0), (-1, 1), (0, 1) }),
        new("Zag",      2, new[] { (-1, 0), (0, 0), (0, 1), (1, 1) }),
        new("Corner",   1, new[] { (-1, 0), (0, 0), (1, 0), (1, 1), (1, 2) }, Pivot2X: 2, Pivot2Y: 0),
        new("Cup",      1, new[] { (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) }, Pivot2X: 0, Pivot2Y: 2),
        new("Long",     1, new[] { (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0) }),
        new("Stair",    1, new[] { (-1, 0), (-1, 1), (0, 1), (0, 2), (1, 2) }, Pivot2X: 0, Pivot2Y: 2),
    };

    /// <summary>Single block. Never drawn at random; used by hand-made tutorial levels.</summary>
    public static readonly ShapeDef Dot = new("Dot", 0, new[] { (0, 0) });

    public static IEnumerable<ShapeDef> All => Classic.Concat(Exotic).Append(Dot);

    public static ShapeDef Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown shape '{name}'.", nameof(name));

    /// <summary>Builds a piece at the spawn position, one colour per cell in offset order.</summary>
    public static Piece Build(ShapeDef shape, IReadOnlyList<CellColour> colours)
    {
        if (colours.Count != shape.CellCount)
            throw new ArgumentException($"{shape.Name} has {shape.CellCount} cells but {colours.Count} colours were given.");
        var cells = new BlockCell[shape.CellCount];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new BlockCell(shape.Offsets[i].Dx, shape.Offsets[i].Dy, colours[i]);
        return Piece.AtSpawn(cells, shape.Pivot2X, shape.Pivot2Y);
    }

    /// <summary>Parses "Pair:RC" (shape name, then one colour letter per cell) into a spawn-positioned piece.</summary>
    public static Piece Parse(string spec)
    {
        int colon = spec.IndexOf(':');
        if (colon <= 0) throw new ArgumentException($"Piece spec '{spec}' must look like 'Pair:RC'.", nameof(spec));
        var shape = Find(spec[..colon]);
        var letters = spec[(colon + 1)..].Trim();
        var colours = letters.Select(Board.FromChar).ToList();
        return Build(shape, colours);
    }
}
