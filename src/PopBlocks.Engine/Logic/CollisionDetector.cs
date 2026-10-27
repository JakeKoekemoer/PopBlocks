using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>Boundary and occupancy checks. All gameplay collision lives here, never in drawing code.</summary>
public static class CollisionDetector
{
    // Horizontal offsets tried, in order, when a rotation does not fit in place.
    private static readonly int[] RotationKicks = { 0, -1, 1, -2, 2 };

    /// <summary>True if every cell of the piece is inside the board and on an empty cell.</summary>
    public static bool IsValid(Board board, Piece piece)
    {
        foreach (var c in piece.Cells)
        {
            int x = piece.X + c.X;
            int y = piece.Y + c.Y;
            if (!board.IsInside(x, y) || board[x, y] != CellColour.Empty)
                return false;
        }
        return true;
    }

    public static bool CanMove(Board board, Piece piece, int dx, int dy) =>
        IsValid(board, piece.Moved(dx, dy));

    /// <summary>Rotates, nudging sideways (a small "wall kick") if the plain rotation collides.</summary>
    public static bool TryRotate(Board board, Piece piece, bool clockwise, out Piece result)
    {
        var rotated = piece.Rotated(clockwise);
        foreach (int kick in RotationKicks)
        {
            var candidate = rotated.Moved(kick, 0);
            if (IsValid(board, candidate))
            {
                result = candidate;
                return true;
            }
        }
        result = piece;
        return false;
    }

    /// <summary>How many rows the piece can fall before it lands.</summary>
    public static int HardDropDistance(Board board, Piece piece)
    {
        int distance = 0;
        while (CanMove(board, piece, 0, distance + 1))
            distance++;
        return distance;
    }
}
