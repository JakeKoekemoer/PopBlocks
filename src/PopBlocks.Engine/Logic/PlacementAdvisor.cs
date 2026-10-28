using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>Everything the advisor needs, copied out of a game so it can be worked on away from the UI thread.</summary>
public sealed record AdviceRequest(Board Board, Piece Piece, GoalTracker? Goals, int Score, int MinGroupSize);

/// <summary>
/// Where Pip's Insight says the piece in hand should go.
/// <paramref name="Rotations"/> is how many quarter turns to give it, <paramref name="LeftColumn"/> the column its leftmost
/// block ends in, and <paramref name="Landed"/> the blocks as they would rest (absolute board coordinates).
/// </summary>
public sealed record PlacementSuggestion(int Rotations, int LeftColumn, IReadOnlyList<BlockCell> Landed, int Popped, int Waves, double GoalProgress)
{
    /// <summary>The shape and column of the landed blocks, without how far down they are: equal to <see cref="Describe(Piece)"/> when the piece in hand is already in position.</summary>
    public string Signature => Describe(Landed);

    public static string Describe(Piece piece) => Describe(piece.BoardCells());

    private static string Describe(IEnumerable<BlockCell> cells)
    {
        var list = cells.ToList();
        int minX = list.Min(c => c.X), minY = list.Min(c => c.Y);
        return minX + ":" + string.Join(";", list.Select(c => $"{c.X - minX},{c.Y - minY},{(int)c.Colour}").OrderBy(s => s, StringComparer.Ordinal));
    }
}

/// <summary>
/// Pip's Insight: tries every way the piece in hand can be turned and moved (only ways the real rules allow), plays each one
/// out on a copy of the board with the same rules as the game (all groups of a wave pop together, gravity, repeat) and picks
/// the best by what it does for the level: first how much of the goals it completes, then how many blocks it pops and how long
/// a chain it sets off, and for a tie the lower stack and the fewer holes. It looks one piece ahead, never more, it never
/// plays the move, and it is the same answer every time for the same board.
/// </summary>
public static class PlacementAdvisor
{
    public static PlacementSuggestion? Best(AdviceRequest request)
    {
        var board = request.Board;
        PlacementSuggestion? best = null;
        (double Progress, int Popped, int Waves, int Height, int Holes) bestKey = default;
        var tried = new HashSet<string>(StringComparer.Ordinal);

        var rotated = request.Piece;
        for (int turns = 0; turns < 4; turns++)
        {
            if (turns > 0)
            {
                if (!CollisionDetector.TryRotate(board, rotated, clockwise: true, out var next)) break;   // blocked here: no further turns
                rotated = next;
            }

            var p = rotated;
            while (CollisionDetector.CanMove(board, p, -1, 0)) p = p.Moved(-1, 0);
            while (true)
            {
                var landed = p.Moved(0, CollisionDetector.HardDropDistance(board, p));
                var cells = landed.BoardCells().ToList();
                string signature = PlacementSuggestion.Describe(landed) + "@" + cells.Min(c => c.Y);
                if (tried.Add(signature))   // two turns that look the same count once, the fewer turns first
                {
                    var (key, suggestion) = Evaluate(request, cells, turns);
                    if (best is null || Better(key, bestKey))
                    {
                        best = suggestion;
                        bestKey = key;
                    }
                }
                if (!CollisionDetector.CanMove(board, p, 1, 0)) break;
                p = p.Moved(1, 0);
            }
        }
        return best;
    }

    private static bool Better((double Progress, int Popped, int Waves, int Height, int Holes) a, (double Progress, int Popped, int Waves, int Height, int Holes) b)
    {
        if (Math.Abs(a.Progress - b.Progress) > 1e-9) return a.Progress > b.Progress;
        if (a.Popped != b.Popped) return a.Popped > b.Popped;
        if (a.Waves != b.Waves) return a.Waves > b.Waves;
        if (a.Height != b.Height) return a.Height < b.Height;
        return a.Holes < b.Holes;
    }

    private static ((double Progress, int Popped, int Waves, int Height, int Holes) Key, PlacementSuggestion Suggestion) Evaluate(
        AdviceRequest request, List<BlockCell> cells, int turns)
    {
        var board = request.Board.Clone();
        foreach (var c in cells) board[c.X, c.Y] = c.Colour;
        ColourMatcher.MarkDormant(board, cells.Select(c => (c.X, c.Y)), request.MinGroupSize);

        var tracker = request.Goals?.Clone();
        int score = request.Score, popped = 0, waves = 0;
        while (true)
        {
            var groups = ColourMatcher.FindGroups(board, request.MinGroupSize);
            if (groups.Count == 0) break;
            waves++;
            var cleared = groups.SelectMany(g => g.Cells).ToList();
            int multiplier = ScoringSystem.Multiplier(waves);
            int points = ScoringSystem.WavePoints(groups.Select(g => g.Size), waves);
            GravityResolver.Clear(board, cleared);
            var moves = GravityResolver.Apply(board);
            score += points;
            popped += cleared.Count;
            tracker?.OnWave(new BurstWave(waves, multiplier, points, groups, cleared, moves));
        }

        double progress = 0;
        if (tracker is not null)
        {
            var states = tracker.Evaluate(score, board.CountOccupied());
            if (states.Count > 0) progress = states.Average(s => s.Fraction);
        }

        int height = StackHeight(board), holes = Holes(board);
        int left = cells.Min(c => c.X);
        return ((progress, popped, waves, height, holes), new PlacementSuggestion(turns, left, cells, popped, waves, progress));
    }

    private static int StackHeight(Board board)
    {
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board[x, y] != CellColour.Empty) return board.Height - y;
        return 0;
    }

    /// <summary>Empty cells with a block somewhere above them in the same column.</summary>
    private static int Holes(Board board)
    {
        int holes = 0;
        for (int x = 0; x < board.Width; x++)
        {
            bool covered = false;
            for (int y = 0; y < board.Height; y++)
            {
                if (board[x, y] != CellColour.Empty) covered = true;
                else if (covered) holes++;
            }
        }
        return holes;
    }
}
