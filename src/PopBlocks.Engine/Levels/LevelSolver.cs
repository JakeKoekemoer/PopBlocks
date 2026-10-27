using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

/// <summary>One placement chosen by the bot: rotate the piece this many times, then put its left edge in this column.</summary>
public readonly record struct SolvedMove(int Rotations, int LeftColumn);

public sealed record SolveResult(bool Won, int Moves, IReadOnlyList<SolvedMove> Plan);

/// <summary>
/// A small look-ahead bot. It plays a level with a lightweight copy of the rules and reports whether
/// (and in how many pieces) the goals can be met. The level catalogue uses it to set move limits that
/// are always achievable, and the tests replay its plans through the real engine to prove the copy is faithful.
///
/// It is deliberately conservative: it never counts drop-bonus points, and it only hard-drops.
/// </summary>
public static class LevelSolver
{
    private const int W = GameConfig.Columns;
    private const int H = GameConfig.TotalRows;
    private const int Hidden = GameConfig.HiddenRows;
    private const int TopCandidates = 6;

    private sealed class Scratch
    {
        public readonly int[] Stack = new int[W * H];
        public readonly int[] Members = new int[W * H];
        public readonly byte[] Seen = new byte[W * H];
        public readonly byte[] Mark = new byte[W * H];
    }

    private sealed class State
    {
        public byte[] Cells = new byte[W * H];
        public int Score, Popped, BestChain, BestGroup;
        public int[] ByColour = new int[6];

        public State Clone()
        {
            var c = new State { Score = Score, Popped = Popped, BestChain = BestChain, BestGroup = BestGroup };
            Array.Copy(Cells, c.Cells, Cells.Length);
            Array.Copy(ByColour, c.ByColour, ByColour.Length);
            return c;
        }
    }

    private readonly record struct Placement(int Rotations, int Left, int[] CellIndexes, byte[] Colours);

    public static SolveResult Solve(LevelDefinition level, int maxMoves)
    {
        var scratch = new Scratch();
        var generator = level.CreateGenerator();
        var sequence = new List<Piece>();
        Piece PieceAt(int i)
        {
            while (sequence.Count <= i) sequence.Add(generator.Next(level.Colours));
            return sequence[i];
        }

        var state = new State();
        var start = level.CreateBoard();
        int startBlocks = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                state.Cells[y * W + x] = (byte)start[x, y];
                if (start[x, y] != CellColour.Empty) startBlocks++;
            }

        var plan = new List<SolvedMove>();
        for (int move = 0; move < maxMoves; move++)
        {
            var current = PieceAt(move);
            var next = PieceAt(move + 1);

            Placement? best = null;
            State? bestState = null;
            double bestValue = double.NegativeInfinity;

            var firsts = new List<(double Value, Placement P, State S)>();
            foreach (var p in Placements(state, current))
            {
                var after = Apply(state, p, level, scratch);
                if (Dead(after)) continue;
                double v = Value(after, level, startBlocks, scratch, out bool done);
                if (done)
                {
                    plan.Add(new SolvedMove(p.Rotations, p.Left));
                    return new SolveResult(true, move + 1, plan);
                }
                firsts.Add((v, p, after));
            }

            if (firsts.Count == 0) return new SolveResult(false, move, plan);
            firsts.Sort((a, b) => b.Value.CompareTo(a.Value));

            int take = Math.Min(TopCandidates, firsts.Count);
            for (int i = 0; i < take; i++)
            {
                var (v1, p1, s1) = firsts[i];
                double v2 = double.NegativeInfinity;
                foreach (var p2 in Placements(s1, next))
                {
                    var after2 = Apply(s1, p2, level, scratch);
                    if (Dead(after2)) continue;
                    double v = Value(after2, level, startBlocks, scratch, out bool done2);
                    if (done2) v += 50000;
                    if (v > v2) v2 = v;
                }
                double combined = v2 == double.NegativeInfinity ? v1 - 5000 : 0.3 * v1 + 0.7 * v2;
                if (combined > bestValue)
                {
                    bestValue = combined;
                    best = p1;
                    bestState = s1;
                }
            }

            plan.Add(new SolvedMove(best!.Value.Rotations, best.Value.Left));
            state = bestState!;
        }

        return new SolveResult(false, maxMoves, plan);
    }

    // ------------------------------------------------------------------ placements

    private static IEnumerable<Placement> Placements(State state, Piece piece)
    {
        var seen = new HashSet<string>();
        var rotated = piece;
        for (int r = 0; r < 4; r++, rotated = rotated.Rotated())
        {
            // Skip rotations that give the same shape as an earlier one.
            var key = string.Join(";", rotated.Cells.Select(c => $"{c.X},{c.Y},{(int)c.Colour}").OrderBy(s => s, StringComparer.Ordinal));
            if (!seen.Add(key)) continue;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue;
            foreach (var c in rotated.Cells)
            {
                minX = Math.Min(minX, c.X);
                maxX = Math.Max(maxX, c.X);
                minY = Math.Min(minY, c.Y);
            }

            // Sit the piece with its top on the last hidden row, then slide it across and drop it.
            int topY = (Hidden - 1) - minY;
            for (int px = -minX; px + maxX < W; px++)
            {
                var placement = Drop(state, rotated, px, topY, r, px + minX);
                if (placement is not null) yield return placement.Value;
            }
        }
    }

    private static Placement? Drop(State state, Piece rotated, int px, int py, int rotations, int left)
    {
        int n = rotated.Cells.Count;
        var xs = new int[n];
        var ys = new int[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = px + rotated.Cells[i].X;
            ys[i] = py + rotated.Cells[i].Y;
            if (xs[i] < 0 || xs[i] >= W || ys[i] < 0 || ys[i] >= H) return null;
            if (state.Cells[ys[i] * W + xs[i]] != 0) return null;
        }

        int drop = 0;
        while (true)
        {
            bool free = true;
            for (int i = 0; i < n && free; i++)
            {
                int ny = ys[i] + drop + 1;
                if (ny >= H || state.Cells[ny * W + xs[i]] != 0) free = false;
            }
            if (!free) break;
            drop++;
        }

        var idx = new int[n];
        var col = new byte[n];
        for (int i = 0; i < n; i++)
        {
            idx[i] = (ys[i] + drop) * W + xs[i];
            col[i] = (byte)rotated.Cells[i].Colour;
        }
        return new Placement(rotations, left, idx, col);
    }

    // ------------------------------------------------------------------ rules (mirrors GameEngine)

    private static State Apply(State before, Placement p, LevelDefinition level, Scratch sc)
    {
        var s = before.Clone();
        for (int i = 0; i < p.CellIndexes.Length; i++) s.Cells[p.CellIndexes[i]] = p.Colours[i];

        int chain = Settle(s, sc);
        if (chain == 0 && level.NoPopPenalty > 0 && s.Score > 0)
            s.Score -= Math.Min(s.Score, level.NoPopPenalty);
        return s;
    }

    /// <summary>Pops groups, applies gravity and repeats until stable. Returns the chain length.</summary>
    private static int Settle(State s, Scratch sc)
    {
        int chain = 0;
        while (true)
        {
            Array.Clear(sc.Seen);
            Array.Clear(sc.Mark);
            int marked = 0, wavePoints = 0;

            for (int i = 0; i < W * H; i++)
            {
                byte colour = s.Cells[i];
                if (colour == 0 || sc.Seen[i] != 0) continue;

                int n = 0, sp = 0;
                sc.Stack[sp++] = i;
                sc.Seen[i] = 1;
                while (sp > 0)
                {
                    int c = sc.Stack[--sp];
                    sc.Members[n++] = c;
                    int x = c % W, y = c / W;
                    if (x > 0) Push(s, sc, c - 1, colour, ref sp);
                    if (x < W - 1) Push(s, sc, c + 1, colour, ref sp);
                    if (y > 0) Push(s, sc, c - W, colour, ref sp);
                    if (y < H - 1) Push(s, sc, c + W, colour, ref sp);
                }

                if (n >= GameConfig.MinGroupSize)
                {
                    for (int j = 0; j < n; j++) sc.Mark[sc.Members[j]] = 1;
                    marked += n;
                    wavePoints += ScoringSystem.GroupPoints(n);
                    s.ByColour[colour] += n;
                    if (n > s.BestGroup) s.BestGroup = n;
                }
            }

            if (marked == 0) break;

            chain++;
            if (chain > s.BestChain) s.BestChain = chain;
            s.Score += wavePoints * ScoringSystem.Multiplier(chain);
            s.Popped += marked;

            for (int i = 0; i < W * H; i++)
                if (sc.Mark[i] != 0) s.Cells[i] = 0;

            for (int x = 0; x < W; x++)
            {
                int write = H - 1;
                for (int y = H - 1; y >= 0; y--)
                {
                    byte c = s.Cells[y * W + x];
                    if (c == 0) continue;
                    if (y != write)
                    {
                        s.Cells[write * W + x] = c;
                        s.Cells[y * W + x] = 0;
                    }
                    write--;
                }
            }
        }
        return chain;
    }

    private static void Push(State s, Scratch sc, int index, byte colour, ref int sp)
    {
        if (s.Cells[index] != colour || sc.Seen[index] != 0) return;
        sc.Seen[index] = 1;
        sc.Stack[sp++] = index;
    }

    private static bool Dead(State s)
    {
        for (int i = 0; i < Hidden * W; i++)
            if (s.Cells[i] != 0) return true;
        return false;
    }

    // ------------------------------------------------------------------ evaluation

    private static double Value(State s, LevelDefinition level, int startBlocks, Scratch sc, out bool done)
    {
        int cells = 0, holes = 0, maxHeight = 0;
        for (int x = 0; x < W; x++)
        {
            int top = -1;
            for (int y = 0; y < H; y++)
            {
                if (s.Cells[y * W + x] == 0) continue;
                if (top < 0) top = y;
                cells++;
            }
            if (top >= 0)
            {
                maxHeight = Math.Max(maxHeight, H - top);
                for (int y = top; y < H; y++)
                    if (s.Cells[y * W + x] == 0) holes++;
            }
        }

        double progress = 0;
        done = level.Goals.Count > 0;
        foreach (var g in level.Goals)
        {
            double p = g.Kind switch
            {
                GoalKind.Score => s.Score / (double)Math.Max(1, g.Target),
                GoalKind.PopBlocks => s.Popped / (double)Math.Max(1, g.Target),
                GoalKind.PopColour => s.ByColour[(int)g.Colour] / (double)Math.Max(1, g.Target),
                GoalKind.Chain => s.BestChain / (double)Math.Max(1, g.Target),
                GoalKind.BigGroup => s.BestGroup / (double)Math.Max(1, g.Target),
                _ => cells <= g.Target ? 1.0 : 1.0 - (cells - g.Target) / (double)Math.Max(1, startBlocks - g.Target),
            };
            if (p < 1.0) done = false;
            progress += Math.Clamp(p, 0.0, 1.0);
        }

        // Pairs and triples of one colour are one block away from popping: worth building.
        double potential = 0;
        Array.Clear(sc.Seen);
        for (int i = 0; i < W * H; i++)
        {
            byte colour = s.Cells[i];
            if (colour == 0 || sc.Seen[i] != 0) continue;
            int n = 0, sp = 0;
            sc.Stack[sp++] = i;
            sc.Seen[i] = 1;
            while (sp > 0)
            {
                int c = sc.Stack[--sp];
                n++;
                int x = c % W, y = c / W;
                if (x > 0) Push(s, sc, c - 1, colour, ref sp);
                if (x < W - 1) Push(s, sc, c + 1, colour, ref sp);
                if (y > 0) Push(s, sc, c - W, colour, ref sp);
                if (y < H - 1) Push(s, sc, c + W, colour, ref sp);
            }
            if (n >= 2) potential += n * n;
        }

        double value = 1000.0 * progress + 3.0 * potential - 4.0 * cells - 5.0 * holes;
        if (maxHeight > 9) value -= 60.0 * (maxHeight - 9);
        if (done) value += 100000;
        return value;
    }
}
