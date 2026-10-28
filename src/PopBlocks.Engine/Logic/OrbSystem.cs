using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>
/// The Orb's board rules: where one can appear, how blocks are pushed aside for it, what it clears when it breaks and what
/// it does when its timer runs out. The Orb is a 2x2 unit of <see cref="CellColour.Orb"/> cells; it is not a colour, never
/// joins a group, and moves as one rigid piece (see <see cref="GravityResolver.Apply"/>). All of this is deterministic: the
/// only chance in it is the engine's own seeded roll.
/// </summary>
public static class OrbSystem
{
    public const int WindowSize = 4;
    public const int MinBlocksInWindow = 10;
    public const int MinColoursInWindow = 4;
    public const int MinLonelyInWindow = 6;

    /// <summary>Pieces that must be locked after an Orb leaves (or a level starts) before another can appear.</summary>
    public const int PiecesBetween = 4;

    /// <summary>Chance of an Orb appearing at a settle that meets the conditions (1.0 when the rules say Always).</summary>
    public const double SpawnChance = 0.35;

    /// <summary>If no Orb has appeared by this many pieces, the next settle spawns one anyway.</summary>
    public const int GuaranteeByPiece = 8;

    /// <summary>Gold (not tenths) for breaking an Orb.</summary>
    public const int BreakBonusGold = 25;

    /// <summary>The top-left cell of the Orb on the board, or null.</summary>
    public static (int X, int Y)? Find(Board board)
    {
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board[x, y] == CellColour.Orb) return (x, y);
        return null;
    }

    /// <summary>Is the cell part of the Orb or touching it on a side?</summary>
    public static bool Touches(Board board, int x, int y)
    {
        return IsOrb(board, x - 1, y) || IsOrb(board, x + 1, y) || IsOrb(board, x, y - 1) || IsOrb(board, x, y + 1);
    }

    private static bool IsOrb(Board board, int x, int y) => board.IsInside(x, y) && board[x, y] == CellColour.Orb;

    // ------------------------------------------------------------------ where it appears

    /// <summary>
    /// The places an Orb could go, best first. <paramref name="relaxed"/> is the guarantee: with no thresholds, the 2x2 with the
    /// most different colours touching it. Otherwise 4x4 windows on the screen holding at least ten blocks, four colours and
    /// six "lonely" blocks (no neighbour of their own colour); the one with the most lonely blocks wins, then the lower one,
    /// then the left one. The Orb sits in the middle of its window.
    /// </summary>
    public static List<(int X, int Y)> Spots(Board board, int viewTopRow, int viewRows, bool relaxed)
    {
        var scored = new List<(int X, int Y, int Score)>();
        int lastTop = Math.Min(board.Height - 2, viewTopRow + viewRows - 2);

        if (!relaxed)
        {
            for (int y0 = viewTopRow; y0 + WindowSize - 1 <= Math.Min(board.Height - 1, viewTopRow + viewRows - 1); y0++)
                for (int x0 = 0; x0 + WindowSize <= board.Width; x0++)
                {
                    int blocks = 0, lonely = 0, colours = 0;
                    for (int y = y0; y < y0 + WindowSize; y++)
                        for (int x = x0; x < x0 + WindowSize; x++)
                        {
                            var c = board[x, y];
                            if (c == CellColour.Empty || c == CellColour.Orb) continue;
                            blocks++;
                            colours |= 1 << (int)c;
                            if (!HasSameNeighbour(board, x, y, c)) lonely++;
                        }
                    if (blocks >= MinBlocksInWindow && System.Numerics.BitOperations.PopCount((uint)colours) >= MinColoursInWindow && lonely >= MinLonelyInWindow)
                        scored.Add((x0 + 1, y0 + 1, lonely));
                }
        }
        else
        {
            for (int y0 = Math.Max(0, viewTopRow); y0 <= lastTop; y0++)
                for (int x0 = 0; x0 + 2 <= board.Width; x0++)
                {
                    int colours = 0;
                    for (int y = y0 - 1; y <= y0 + 2; y++)
                        for (int x = x0 - 1; x <= x0 + 2; x++)
                            if (board.IsInside(x, y) && board[x, y] is not (CellColour.Empty or CellColour.Orb)) colours |= 1 << (int)board[x, y];
                    scored.Add((x0, y0, System.Numerics.BitOperations.PopCount((uint)colours)));
                }
        }

        // Most first; ties: lower (a bigger y), then to the left.
        scored.Sort((a, b) =>
        {
            int c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;
            c = b.Y.CompareTo(a.Y);
            return c != 0 ? c : a.X.CompareTo(b.X);
        });
        return scored.Select(s => (s.X, s.Y)).ToList();
    }

    private static bool HasSameNeighbour(Board board, int x, int y, CellColour c) =>
        (x > 0 && board[x - 1, y] == c) || (x < board.Width - 1 && board[x + 1, y] == c)
        || (y > 0 && board[x, y - 1] == c) || (y < board.Height - 1 && board[x, y + 1] == c);

    // ------------------------------------------------------------------ putting it there

    /// <summary>
    /// Puts the Orb with its top-left cell at (x, y). Blocks in its two columns at or above its rows are pushed up, in order,
    /// to make room. Refuses (and changes nothing) if any pushed block would reach the top of the board.
    /// </summary>
    public static bool TryPlace(Board board, int x, int y)
    {
        if (x < 0 || y < 0 || x + 1 >= board.Width || y + 1 >= board.Height) return false;
        var work = board.Clone();
        for (int cx = x; cx <= x + 1; cx++)
        {
            int inside = 0;
            for (int r = y; r <= y + 1; r++) if (work[cx, r] != CellColour.Empty) inside++;
            if (inside == 0) continue;

            // Everything from the top of the column down to the Orb's lower row moves up by the number of blocks it displaces.
            for (int r = 0; r <= y + 1; r++)
            {
                if (work[cx, r] == CellColour.Empty) continue;
                int to = r - inside;
                if (to < GameConfig.HiddenRows) return false;   // would reach the top of the board
                Move(work, cx, r, to);
            }
        }
        for (int cy = y; cy <= y + 1; cy++)
            for (int cx = x; cx <= x + 1; cx++)
                work[cx, cy] = CellColour.Orb;
        board.CopyFrom(work);
        return true;
    }

    private static void Move(Board board, int x, int from, int to)
    {
        var colour = board[x, from];
        bool dormant = board.IsDormant(x, from);
        int cracks = board.FractureAt(x, from);
        board[x, from] = CellColour.Empty;
        board[x, to] = colour;
        if (dormant) board.SetDormant(x, to);
        if (cracks > 0) board.SetFracture(x, to, cracks);
    }

    // ------------------------------------------------------------------ breaking and corrupting

    /// <summary>
    /// Every block in the Orb's two rows and two columns across the whole board (a cross two cells thick). Not the Orb itself.
    /// </summary>
    public static List<BlockCell> CrossCells(Board board, int x, int y)
    {
        var cells = new List<BlockCell>();
        for (int cy = 0; cy < board.Height; cy++)
            for (int cx = 0; cx < board.Width; cx++)
            {
                bool inRows = cy == y || cy == y + 1, inCols = cx == x || cx == x + 1;
                if (!inRows && !inCols) continue;
                var c = board[cx, cy];
                if (c is CellColour.Empty or CellColour.Orb) continue;
                cells.Add(new BlockCell(cx, cy, c));
            }
        return cells;
    }

    /// <summary>The twelve cells ringing the 2x2 Orb.</summary>
    public static IEnumerable<(int X, int Y)> Ring(int x, int y)
    {
        for (int cy = y - 1; cy <= y + 2; cy++)
            for (int cx = x - 1; cx <= x + 2; cx++)
                if (cx < x || cx > x + 1 || cy < y || cy > y + 1) yield return (cx, cy);
    }

    /// <summary>Takes the Orb off the board (all four cells).</summary>
    public static void Remove(Board board, int x, int y)
    {
        for (int cy = y; cy <= y + 1; cy++)
            for (int cx = x; cx <= x + 1; cx++)
                if (board.IsInside(cx, cy) && board[cx, cy] == CellColour.Orb) board[cx, cy] = CellColour.Empty;
    }
}
