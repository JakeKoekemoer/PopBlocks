using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

public static class GravityResolver
{
    /// <summary>
    /// Finds the stacks that hang over empty cells. A piece locks exactly where it lands and gravity runs only after a pop,
    /// so blocks can hang for several moves until something pops and everything falls. Read-only: the app uses this to show
    /// the player what will drop and how far. Hidden spawn rows are ignored. <paramref name="result"/> is cleared first,
    /// so a caller can reuse one list every frame.
    /// </summary>
    public static void FindHanging(Board board, List<HangingRun> result)
    {
        result.Clear();
        for (int x = 0; x < board.Width; x++)
        {
            int gap = 0;   // empty cells seen so far, going up, since the last block
            for (int y = board.Height - 1; y >= GameConfig.HiddenRows; y--)
            {
                var colour = board[x, y];
                if (colour == CellColour.Empty) { gap++; continue; }
                if (gap > 0) result.Add(new HangingRun(x, y, gap, colour));   // lowest block of a stack with air under it
                gap = 0;
            }
        }
    }

    /// <summary>Empties the given cells. Call only after every group has been identified.</summary>
    public static void Clear(Board board, IEnumerable<BlockCell> cells)
    {
        foreach (var c in cells)
            board[c.X, c.Y] = CellColour.Empty;
    }

    /// <summary>
    /// Compacts every column downward, preserving the vertical order of the remaining cells.
    /// Returns the cells that moved (used to animate the fall).
    /// </summary>
    public static IReadOnlyList<CellMove> Apply(Board board)
    {
        var moves = new List<CellMove>();
        // Blocks fall; an Orb is a floor for what is above it, and falls (as one 2x2 unit) when both cells under it are
        // empty; then what was resting on it falls with it. Repeat until nothing moves.
        do Compact(board, moves);
        while (DropOrb(board));
        return moves;
    }

    /// <summary>The Orb, if both cells under it are empty, drops one row. True if it did.</summary>
    private static bool DropOrb(Board board)
    {
        var orb = OrbSystem.Find(board);
        if (orb is not { } o) return false;
        int below = o.Y + 2;
        if (below >= board.Height || board[o.X, below] != CellColour.Empty || board[o.X + 1, below] != CellColour.Empty) return false;
        OrbSystem.Remove(board, o.X, o.Y);
        for (int cy = o.Y + 1; cy <= o.Y + 2; cy++)
            for (int cx = o.X; cx <= o.X + 1; cx++)
                board[cx, cy] = CellColour.Orb;
        return true;
    }

    private static void Compact(Board board, List<CellMove> moves)
    {
        for (int x = 0; x < board.Width; x++)
        {
            int writeY = board.Height - 1;
            for (int y = board.Height - 1; y >= 0; y--)
            {
                var colour = board[x, y];
                if (colour == CellColour.Empty) continue;
                if (colour == CellColour.Orb)
                {
                    writeY = y - 1;   // blocks above rest on the Orb; the Orb itself does not move here
                    continue;
                }

                if (y != writeY)
                {
                    bool dormant = board.IsDormant(x, y);   // a sleeping block keeps sleeping while it falls
                    int cracks = board.FractureAt(x, y);    // and a cracked block keeps its cracks
                    board[x, writeY] = colour;
                    board[x, y] = CellColour.Empty;
                    if (dormant) board.SetDormant(x, writeY);
                    if (cracks > 0) board.SetFracture(x, writeY, cracks);
                    moves.Add(new CellMove(x, y, writeY, colour));
                }
                writeY--;
            }
        }
    }
}
