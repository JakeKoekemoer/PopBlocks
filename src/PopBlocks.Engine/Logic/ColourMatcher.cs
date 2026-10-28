using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>
/// Finds connected same-colour groups using orthogonal adjacency only.
/// Read-only: it never mutates the board, so all qualifying groups come from one consistent snapshot.
/// A group made only of dormant blocks (a solid piece nothing has joined yet) is not reported: it waits.
/// </summary>
public static class ColourMatcher
{
    public static IReadOnlyList<ColourGroup> FindGroups(Board board, int minSize = GameConfig.MinGroupSize)
    {
        int width = board.Width;
        var groups = new List<ColourGroup>();
        var visited = new bool[width * board.Height];
        var stack = new Stack<int>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int start = y * width + x;
                var colour = board[x, y];
                if (colour == CellColour.Empty || visited[start]) continue;

                var cells = new List<BlockCell>();
                bool live = false;   // true once the group has a block that is not dormant
                visited[start] = true;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    int cx = index % width;
                    int cy = index / width;
                    cells.Add(new BlockCell(cx, cy, colour));
                    if (!board.IsDormant(cx, cy)) live = true;

                    TryVisit(cx - 1, cy);
                    TryVisit(cx + 1, cy);
                    TryVisit(cx, cy - 1);
                    TryVisit(cx, cy + 1);
                }

                if (cells.Count >= minSize && live)
                    groups.Add(new ColourGroup(colour, cells));

                void TryVisit(int nx, int ny)
                {
                    if (!board.IsInside(nx, ny)) return;
                    int ni = ny * width + nx;
                    if (visited[ni] || board[nx, ny] != colour) return;
                    visited[ni] = true;
                    stack.Push(ni);
                }
            }
        }

        return groups;
    }

    /// <summary>
    /// Call right after a piece's cells are written to the board. Any burst-sized group made only of those cells (a solid
    /// piece that touches nothing of its colour) is put to sleep, so landing does not pop it. A group that also holds
    /// older blocks is left awake: the piece connected to something, so it pops as usual, sleeping blocks included.
    /// </summary>
    public static void MarkDormant(Board board, IEnumerable<(int X, int Y)> placed)
    {
        var own = new HashSet<(int, int)>(placed.Select(p => (p.X, p.Y)));
        foreach (var group in FindGroups(board))
        {
            if (group.Cells.All(c => own.Contains((c.X, c.Y))))
                foreach (var c in group.Cells)
                    board.SetDormant(c.X, c.Y);
        }
    }

    /// <summary>
    /// Groups that are too small to pop yet: from <paramref name="minSize"/> up to <paramref name="maxSize"/> cells.
    /// The Colour Lens uses this to outline groups that are a block or two short. Read-only, like
    /// <see cref="FindGroups"/>.
    /// </summary>
    public static IReadOnlyList<ColourGroup> FindNearGroups(Board board, int minSize, int maxSize)
    {
        if (maxSize >= GameConfig.MinGroupSize) maxSize = GameConfig.MinGroupSize - 1;
        if (minSize < 2) minSize = 2;
        return FindGroups(board, minSize).Where(g => g.Size <= maxSize).ToList();
    }
}
