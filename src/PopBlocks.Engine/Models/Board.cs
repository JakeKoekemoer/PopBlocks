using System.Text;

namespace PopBlocks.Engine.Models;

/// <summary>
/// Grid of locked cells. Origin is top-left, X grows right, Y grows down.
/// Rows 0..HiddenRows-1 are the hidden spawn area.
/// </summary>
public sealed class Board
{
    private readonly CellColour[] _cells;

    // Dormant cells belong to a solid piece (see GameConfig.SolidPieceChance). They never start a burst on their own: a
    // group pops only if at least one of its cells is not dormant. Writing a cell clears its flag, so a block that is
    // placed is always live unless the engine puts it to sleep afterwards. Clone, CopyFrom and gravity carry the flags.
    private readonly bool[] _dormant;

    // Cracks (0 intact, 1, 2; 3 = broken by a third crack and waiting to be removed in the next step of the same resolution).
    // Written with the cell: setting a block clears its cracks. Clone, CopyFrom and gravity carry them.
    private readonly byte[] _fracture;

    /// <summary>The block would break if it cracked once more.</summary>
    public const byte Broken = 3;

    public int Width { get; }
    public int Height { get; }

    public Board(int width = GameConfig.Columns, int height = GameConfig.TotalRows)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _cells = new CellColour[width * height];
        _dormant = new bool[width * height];
        _fracture = new byte[width * height];
    }

    public CellColour this[int x, int y]
    {
        get
        {
            if (!IsInside(x, y)) throw new ArgumentOutOfRangeException($"({x},{y}) is outside the board.");
            return _cells[y * Width + x];
        }
        set
        {
            if (!IsInside(x, y)) throw new ArgumentOutOfRangeException($"({x},{y}) is outside the board.");
            _cells[y * Width + x] = value;
            _dormant[y * Width + x] = false;
            _fracture[y * Width + x] = 0;
        }
    }

    public bool IsInside(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public bool IsEmpty(int x, int y) => this[x, y] == CellColour.Empty;

    /// <summary>True if the cell holds a dormant block (outside the board or empty cells are never dormant).</summary>
    public bool IsDormant(int x, int y) => IsInside(x, y) && _dormant[y * Width + x];

    /// <summary>Puts the block at (x, y) to sleep, or wakes it. An empty cell cannot be dormant.</summary>
    public void SetDormant(int x, int y, bool dormant = true)
    {
        if (this[x, y] == CellColour.Empty) dormant = false;
        _dormant[y * Width + x] = dormant;
    }

    public void Clear()
    {
        Array.Clear(_cells);
        Array.Clear(_dormant);
        Array.Clear(_fracture);
    }

    /// <summary>Cracks on the block at (x, y): 0 to 2, or <see cref="Broken"/>. 0 for an empty cell or outside the board.</summary>
    public int FractureAt(int x, int y) => IsInside(x, y) ? _fracture[y * Width + x] : 0;

    /// <summary>Sets the cracks on a block (0 to <see cref="Broken"/>). An empty cell cannot be cracked.</summary>
    public void SetFracture(int x, int y, int level)
    {
        if (this[x, y] == CellColour.Empty) level = 0;
        _fracture[y * Width + x] = (byte)Math.Clamp(level, 0, Broken);
    }

    /// <summary>Is any block waiting to break?</summary>
    public bool AnyBroken()
    {
        for (int i = 0; i < _fracture.Length; i++)
            if (_fracture[i] == Broken && _cells[i] != CellColour.Empty) return true;
        return false;
    }

    /// <summary>The blocks waiting to break, top to bottom.</summary>
    public List<BlockCell> TakeBrokenList()
    {
        var list = new List<BlockCell>();
        for (int i = 0; i < _fracture.Length; i++)
            if (_fracture[i] == Broken && _cells[i] != CellColour.Empty)
                list.Add(new BlockCell(i % Width, i / Width, _cells[i]));
        return list;
    }

    /// <summary>
    /// Fills this (taller or equal) board from another one, bottom to bottom: the other board's floor is this board's floor and
    /// the extra rows above stay empty. This is how a level's starting pile is put on a taller board.
    /// </summary>
    public void CopyBottomFrom(Board other)
    {
        if (other.Width != Width) throw new ArgumentException("Boards must be the same width.", nameof(other));
        Clear();
        int rows = Math.Min(other.Height, Height);
        for (int r = 0; r < rows; r++)
        {
            int from = (other.Height - 1 - r) * Width, to = (Height - 1 - r) * Width;
            Array.Copy(other._cells, from, _cells, to, Width);
            Array.Copy(other._dormant, from, _dormant, to, Width);
            Array.Copy(other._fracture, from, _fracture, to, Width);
        }
    }

    /// <summary>Overwrites this board's cells with another board's. Both must be the same size.</summary>
    public void CopyFrom(Board other)
    {
        if (other.Width != Width || other.Height != Height)
            throw new ArgumentException("Boards must be the same size.", nameof(other));
        Array.Copy(other._cells, _cells, _cells.Length);
        Array.Copy(other._dormant, _dormant, _dormant.Length);
        Array.Copy(other._fracture, _fracture, _fracture.Length);
    }

    public Board Clone()
    {
        var copy = new Board(Width, Height);
        Array.Copy(_cells, copy._cells, _cells.Length);
        Array.Copy(_dormant, copy._dormant, _dormant.Length);
        Array.Copy(_fracture, copy._fracture, _fracture.Length);
        return copy;
    }

    /// <summary>True if any cell in rows [startRow, endRow) is occupied.</summary>
    public bool AnyInRows(int startRow, int endRow)
    {
        for (int y = Math.Max(0, startRow); y < Math.Min(Height, endRow); y++)
            for (int x = 0; x < Width; x++)
                if (_cells[y * Width + x] != CellColour.Empty)
                    return true;
        return false;
    }

    public int CountOccupied()
    {
        int n = 0;
        foreach (var c in _cells) if (c != CellColour.Empty) n++;
        return n;
    }

    // ---- Text helpers (used heavily by tests) ----
    // '.' empty, C cyan, R coral, Y yellow, P purple, L lime.

    /// <summary>
    /// Builds a board from text rows. Rows are bottom-aligned (the last row is the bottom of the board),
    /// and short rows are padded with empty cells on the right.
    /// </summary>
    public static Board FromRows(IReadOnlyList<string> rows, int width = GameConfig.Columns, int height = GameConfig.TotalRows)
    {
        if (rows.Count > height) throw new ArgumentException("More rows than the board is tall.", nameof(rows));
        var board = new Board(width, height);
        int top = height - rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            string row = rows[i];
            if (row.Length > width) throw new ArgumentException($"Row {i} is wider than the board.", nameof(rows));
            for (int x = 0; x < row.Length; x++)
                board[x, top + i] = FromChar(row[x]);
        }
        return board;
    }

    public string[] ToRows()
    {
        var rows = new string[Height];
        var sb = new StringBuilder(Width);
        for (int y = 0; y < Height; y++)
        {
            sb.Clear();
            for (int x = 0; x < Width; x++) sb.Append(ToChar(_cells[y * Width + x]));
            rows[y] = sb.ToString();
        }
        return rows;
    }

    /// <summary>The bottom <paramref name="count"/> rows joined with newlines.</summary>
    public string BottomRows(int count)
    {
        var rows = ToRows();
        return string.Join("\n", rows.Skip(Math.Max(0, rows.Length - count)));
    }

    public override string ToString() => string.Join("\n", ToRows());

    public static char ToChar(CellColour c) => c switch
    {
        CellColour.Empty => '.',
        CellColour.Cyan => 'C',
        CellColour.Coral => 'R',
        CellColour.Yellow => 'Y',
        CellColour.Purple => 'P',
        CellColour.Lime => 'L',
        _ => '?',
    };

    public static CellColour FromChar(char ch) => char.ToUpperInvariant(ch) switch
    {
        '.' or ' ' => CellColour.Empty,
        'C' => CellColour.Cyan,
        'R' => CellColour.Coral,
        'Y' => CellColour.Yellow,
        'P' => CellColour.Purple,
        'L' => CellColour.Lime,
        _ => throw new ArgumentException($"Unknown board character '{ch}'."),
    };
}
