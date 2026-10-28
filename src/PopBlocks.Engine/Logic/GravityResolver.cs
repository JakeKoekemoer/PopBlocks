using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

public static class GravityResolver
{
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
        for (int x = 0; x < board.Width; x++)
        {
            int writeY = board.Height - 1;
            for (int y = board.Height - 1; y >= 0; y--)
            {
                var colour = board[x, y];
                if (colour == CellColour.Empty) continue;

                if (y != writeY)
                {
                    bool dormant = board.IsDormant(x, y);   // a sleeping block keeps sleeping while it falls
                    board[x, writeY] = colour;
                    board[x, y] = CellColour.Empty;
                    if (dormant) board.SetDormant(x, writeY);
                    moves.Add(new CellMove(x, y, writeY, colour));
                }
                writeY--;
            }
        }
        return moves;
    }
}
