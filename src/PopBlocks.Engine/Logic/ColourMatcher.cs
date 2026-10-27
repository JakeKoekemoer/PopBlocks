using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>
/// Finds connected same-colour groups using orthogonal adjacency only.
/// Read-only: it never mutates the board, so all qualifying groups come from one consistent snapshot.
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
                visited[start] = true;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    int cx = index % width;
                    int cy = index / width;
                    cells.Add(new BlockCell(cx, cy, colour));

                    TryVisit(cx - 1, cy);
                    TryVisit(cx + 1, cy);
                    TryVisit(cx, cy - 1);
                    TryVisit(cx, cy + 1);
                }

                if (cells.Count >= minSize)
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
}
