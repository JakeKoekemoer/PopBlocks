using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Levels;

/// <summary>One placement chosen by the bot: rotate the piece this many times, then put its left edge in this column.</summary>
public readonly record struct SolvedMove(int Rotations, int LeftColumn);

public sealed record SolveResult(bool Won, int Moves, IReadOnlyList<SolvedMove> Plan);

/// <summary>
/// How hard the bot thinks. <see cref="Full"/> is the bot that sets move limits. <see cref="Weak"/> takes the single best
/// placement with no look-ahead; how many more pieces it needs than the full bot is a measure of how much planning a
/// board demands (see <c>LevelRoller</c>).
/// </summary>
public readonly record struct SolveOptions(int Candidates, bool LookAhead)
{
    public static SolveOptions Full => new(6, true);
    public static SolveOptions Weak => new(1, false);
}

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

    // The board the Story is played on (the default height), so the Orb's windows and the camera that picks them are the same
    // rows the engine sees. The pile of a level is put on the floor of it, like the engine does.
    private const int H = GameConfig.DefaultBoardRows + GameConfig.HiddenRows;
    private const int Hidden = GameConfig.HiddenRows;
    private const int ChainSlots = 9;
    private const byte OrbCell = (byte)CellColour.Orb;

    private static int ChainsOfAtLeast(State s, int waves)
    {
        int count = 0;
        for (int i = Math.Max(1, waves); i < ChainSlots; i++) count += s.ChainsByLength[i];
        return count;
    }

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

        /// <summary>1 where the block sleeps (a solid piece nothing has joined yet). Mirrors <see cref="Board.IsDormant"/>.</summary>
        public byte[] Dormant = new byte[W * H];

        /// <summary>Cracks on each block (0 to 2, 3 = waiting to break). Mirrors <see cref="Board.FractureAt"/>.</summary>
        public byte[] Fracture = new byte[W * H];
        public int Score, Popped, BestChain, BestGroup;
        public int[] ByColour = new int[6];

        /// <summary>How many finished chains had 1, 2, ... waves (the last slot holds longer ones too).</summary>
        public int[] ChainsByLength = new int[ChainSlots];

        // The Orb (see OrbSystem and GameEngine.MaybeSpawnOrb). OrbPos is the index of its top-left cell, or -1.
        public int Placed, OrbPos = -1, OrbTimer, PiecesSinceOrb, OrbRolls;
        public bool OrbEver, OrbBrokeThisResolution;

        public State Clone()
        {
            var c = new State
            {
                Score = Score, Popped = Popped, BestChain = BestChain, BestGroup = BestGroup,
                Placed = Placed, OrbPos = OrbPos, OrbTimer = OrbTimer, PiecesSinceOrb = PiecesSinceOrb, OrbRolls = OrbRolls,
                OrbEver = OrbEver, OrbBrokeThisResolution = OrbBrokeThisResolution,
            };
            Array.Copy(Cells, c.Cells, Cells.Length);
            Array.Copy(Dormant, c.Dormant, Dormant.Length);
            Array.Copy(Fracture, c.Fracture, Fracture.Length);
            Array.Copy(ByColour, c.ByColour, ByColour.Length);
            Array.Copy(ChainsByLength, c.ChainsByLength, ChainsByLength.Length);
            return c;
        }
    }

    private readonly record struct Placement(int Rotations, int Left, int[] CellIndexes, byte[] Colours, int Drop);

    /// <summary>
    /// Plays the level with the bot. With a <paramref name="bonus"/> the bot must also meet that optional objective
    /// (used to check that a memory objective is possible before a level offers it).
    /// </summary>
    public static SolveResult Solve(LevelDefinition level, int maxMoves, BonusObjective? bonus = null, SolveOptions? options = null)
    {
        var opts = options ?? SolveOptions.Full;
        var scratch = new Scratch();

        // The bot normally ignores drop-bonus points (conservative). When checking an objective it must know exactly
        // when the real game would end the level, so it counts them like the engine does.
        bool exactScore = bonus is not null;
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
        int lift = H - start.Height;   // the pile sits on the floor of the (taller) board
        for (int y = 0; y < start.Height; y++)
            for (int x = 0; x < W; x++)
            {
                state.Cells[(y + lift) * W + x] = (byte)start[x, y];
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
                var after = Apply(state, p, level, scratch, exactScore);
                if (Dead(after)) continue;
                double v = Value(after, level, startBlocks, scratch, bonus, out bool done, out bool dead);
                if (dead) continue;   // the level would end here with the objective missed
                if (done)
                {
                    plan.Add(new SolvedMove(p.Rotations, p.Left));
                    return new SolveResult(true, move + 1, plan);
                }
                firsts.Add((v, p, after));
            }

            if (firsts.Count == 0) return new SolveResult(false, move, plan);
            firsts.Sort((a, b) => b.Value.CompareTo(a.Value));

            int take = Math.Min(opts.Candidates, firsts.Count);
            if (!opts.LookAhead)
            {
                // The weak bot: the best-looking placement now, without asking what the next piece would do with it.
                best = firsts[0].P;
                bestState = firsts[0].S;
                take = 0;
            }
            for (int i = 0; i < take; i++)
            {
                var (v1, p1, s1) = firsts[i];
                double v2 = double.NegativeInfinity;
                foreach (var p2 in Placements(s1, next))
                {
                    var after2 = Apply(s1, p2, level, scratch, exactScore);
                    if (Dead(after2)) continue;
                    double v = Value(after2, level, startBlocks, scratch, bonus, out bool done2, out bool dead2);
                    if (dead2) continue;
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

    /// <summary>
    /// For the tests: the bot's own copy of the board after each move of a plan, as <c>H</c> rows of cells (colour numbers, the
    /// Orb as 6) and their cracks, so it can be compared with the real engine move by move. Stops early if a move is not found.
    /// </summary>
    public static List<(int[] Cells, int[] Cracks, int Timer, int Score)> Trace(LevelDefinition level, IReadOnlyList<SolvedMove> plan)
    {
        var result = new List<(int[], int[], int, int)>();
        var generator = level.CreateGenerator();
        var scratch = new Scratch();
        var state = new State();
        var start = level.CreateBoard();
        int lift = H - start.Height;
        for (int y = 0; y < start.Height; y++)
            for (int x = 0; x < W; x++)
                state.Cells[(y + lift) * W + x] = (byte)start[x, y];
        foreach (var move in plan)
        {
            var piece = generator.Next(level.Colours);
            Placement? chosen = null;
            foreach (var p in Placements(state, piece))
                if (p.Rotations == move.Rotations && p.Left == move.LeftColumn) { chosen = p; break; }
            if (chosen is null) break;
            state = Apply(state, chosen.Value, level, scratch, false);
            result.Add((state.Cells.Select(c => (int)c).ToArray(), state.Fracture.Select(c => (int)c).ToArray(), state.OrbTimer, state.Score));
        }
        return result;
    }

    /// <summary>
    /// Plays a bot plan through the real <see cref="GameEngine"/> (no timing) and returns the finished engine and what
    /// happened. The engine, not the bot's lightweight copy of the rules, is the final word on whether a plan works.
    /// </summary>
    public static (GameEngine Engine, RunStats Run) ReplayInEngine(LevelDefinition level, SolveResult plan)
    {
        var engine = new GameEngine(new PieceGenerator(new Random(1)));
        engine.StartLevel(level);
        var run = new RunStats();
        run.Attach(engine);

        foreach (var move in plan.Plan)
        {
            if (engine.State != GameState.Playing || engine.CurrentPiece is null) break;

            for (int r = 0; r < move.Rotations; r++)
            {
                int guard = 0;
                while (!engine.Rotate())
                {
                    engine.Tick();
                    if (++guard > 4 || engine.State != GameState.Playing) return (engine, run);
                }
            }

            for (int guard = 0; guard < 12; guard++)
            {
                int left = engine.CurrentPiece!.BoardCells().Min(c => c.X);
                if (left == move.LeftColumn) break;
                if (left > move.LeftColumn) engine.MoveLeft(); else engine.MoveRight();
            }

            engine.HardDrop();
            for (int i = 0; i < 50 && engine.State == GameState.ResolvingBursts; i++) engine.Tick();
        }
        return (engine, run);
    }

    /// <summary>
    /// True if a person could play this plan: every piece can be rotated and slid to the column the bot chose in the real
    /// engine (a tall pile can block a slide the bot's copy of the rules does not notice), and the plan then wins the level
    /// within its move limit. A level whose plan fails this is not truly "bot-verified", so a roll does not accept it.
    /// </summary>
    public static bool PlanIsPlayable(LevelDefinition level, IReadOnlyList<SolvedMove> plan)
    {
        var engine = new GameEngine(new PieceGenerator(new Random(1)));
        engine.StartLevel(level);

        foreach (var move in plan)
        {
            if (engine.State != GameState.Playing || engine.CurrentPiece is null) break;

            for (int r = 0; r < move.Rotations; r++)
            {
                int guard = 0;
                while (!engine.Rotate())
                {
                    engine.Tick();
                    if (++guard > 4 || engine.State != GameState.Playing) return false;
                }
            }

            bool reached = false;
            for (int guard = 0; guard < 12; guard++)
            {
                int left = engine.CurrentPiece!.BoardCells().Min(c => c.X);
                if (left == move.LeftColumn) { reached = true; break; }
                if (left > move.LeftColumn) engine.MoveLeft(); else engine.MoveRight();
            }
            if (!reached) return false;

            engine.HardDrop();
            for (int i = 0; i < 50 && engine.State == GameState.ResolvingBursts; i++) engine.Tick();
        }

        return engine.State == GameState.LevelComplete && (!engine.HasMoveLimit || engine.PiecesPlaced <= engine.MoveLimit);
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
        return new Placement(rotations, left, idx, col, drop);
    }

    // ------------------------------------------------------------------ rules (mirrors GameEngine)

    private static State Apply(State before, Placement p, LevelDefinition level, Scratch sc, bool exactScore)
    {
        var s = before.Clone();
        if (exactScore) s.Score += p.Drop * GameConfig.HardDropPointsPerRow;
        for (int i = 0; i < p.CellIndexes.Length; i++)
        {
            s.Cells[p.CellIndexes[i]] = p.Colours[i];
            s.Dormant[p.CellIndexes[i]] = 0;
            s.Fracture[p.CellIndexes[i]] = 0;
        }

        // Like the engine: a piece that is a burst-sized group all by itself does not pop by landing; it sleeps.
        MarkDormant(s, p, sc);

        bool fracture = FractureOn(level);
        bool orbs = OrbsOn(level);

        // An Orb counts pieces down; at zero it cracks the blocks round it (GameEngine.LockPiece).
        s.Placed++;
        s.OrbBrokeThisResolution = false;
        if (orbs)
        {
            s.PiecesSinceOrb++;
            if (s.OrbPos >= 0 && --s.OrbTimer <= 0) CorruptOrb(s, fracture);
        }

        int chain = Settle(s, sc, fracture, orbs);
        if (chain > 0) s.ChainsByLength[Math.Min(chain, ChainSlots - 1)]++;
        if (chain == 0 && level.NoPopPenalty > 0 && s.Score > 0)
            s.Score -= Math.Min(s.Score, level.NoPopPenalty);

        if (orbs) MaybeSpawnOrb(s, level);
        return s;
    }

    // ------------------------------------------------------------------ the Orb (mirrors GameEngine and OrbSystem)

    /// <summary>Mirrors <c>GameEngine.OrbsActive</c> under the normal rules (the solver only ever plays those).</summary>
    private static bool OrbsOn(LevelDefinition level) =>
        RuleFeatures.Orbs && RuleFeatures.OrbsInStory && GameRules.Default.Orbs != OrbMode.Never
        && !level.IsTutorial && level.Number >= RegionRules.OrbIntro;

    private static Board ToBoard(State s)
    {
        var board = new Board(W, H);
        for (int i = 0; i < W * H; i++)
        {
            if (s.Cells[i] == 0) continue;
            board[i % W, i / W] = (CellColour)s.Cells[i];
            if (s.Dormant[i] != 0) board.SetDormant(i % W, i / W);
            if (s.Fracture[i] != 0) board.SetFracture(i % W, i / W, s.Fracture[i]);
        }
        return board;
    }

    private static void FromBoard(State s, Board board)
    {
        s.OrbPos = -1;
        for (int i = 0; i < W * H; i++)
        {
            int x = i % W, y = i / W;
            s.Cells[i] = (byte)board[x, y];
            s.Dormant[i] = (byte)(board.IsDormant(x, y) ? 1 : 0);
            s.Fracture[i] = (byte)board.FractureAt(x, y);
            if (s.OrbPos < 0 && s.Cells[i] == OrbCell) s.OrbPos = i;
        }
    }

    /// <summary>Mirrors <c>GameEngine.CorruptOrb</c>: the twelve cells round the Orb take a crack and the Orb goes.</summary>
    private static void CorruptOrb(State s, bool fracture)
    {
        int ox = s.OrbPos % W, oy = s.OrbPos / W;
        RemoveOrb(s);
        if (fracture)
            foreach (var (x, y) in OrbSystem.Ring(ox, oy))
            {
                if ((uint)x >= W || (uint)y >= H) continue;
                int i = y * W + x;
                if (s.Cells[i] != 0 && s.Cells[i] != OrbCell) s.Fracture[i] = (byte)Math.Min(Board.Broken, s.Fracture[i] + 1);
            }
        Gravity(s);
        s.PiecesSinceOrb = 0;
        s.OrbBrokeThisResolution = true;
    }

    /// <summary>
    /// Is there a 4x4 window on the screen with enough blocks, colours and lonely blocks for an Orb (the same test as the first
    /// half of <see cref="OrbSystem.Spots"/>, but only asking whether one exists)?
    /// </summary>
    private static bool AnyOrbWindow(State s, int viewTop)
    {
        int last = Math.Min(H - 1, viewTop + GameConfig.ViewportRows - 1);
        int rows = last - viewTop + 1;
        if (rows < OrbSystem.WindowSize) return false;

        // One pass over the screen: for each cell, whether it is a block, whether it is lonely, and its colour bit.
        Span<byte> blockAt = stackalloc byte[GameConfig.ViewportRows * W];
        Span<byte> lonelyAt = stackalloc byte[GameConfig.ViewportRows * W];
        Span<int> colourAt = stackalloc int[GameConfig.ViewportRows * W];
        int total = 0;
        for (int r = 0; r < rows; r++)
        {
            int y = viewTop + r;
            for (int x = 0; x < W; x++)
            {
                byte c = s.Cells[y * W + x];
                if (c == 0 || c == OrbCell) continue;
                total++;
                int k = r * W + x;
                blockAt[k] = 1;
                colourAt[k] = 1 << c;
                bool same = (x > 0 && s.Cells[y * W + x - 1] == c) || (x < W - 1 && s.Cells[y * W + x + 1] == c)
                    || (y > 0 && s.Cells[(y - 1) * W + x] == c) || (y < H - 1 && s.Cells[(y + 1) * W + x] == c);
                if (!same) lonelyAt[k] = 1;
            }
        }
        if (total < OrbSystem.MinBlocksInWindow) return false;

        Span<int> colBlocks = stackalloc int[W], colLonely = stackalloc int[W], colColours = stackalloc int[W];
        for (int r0 = 0; r0 + OrbSystem.WindowSize <= rows; r0++)
        {
            for (int x = 0; x < W; x++)
            {
                int b = 0, l = 0, m = 0;
                for (int r = r0; r < r0 + OrbSystem.WindowSize; r++)
                {
                    int k = r * W + x;
                    b += blockAt[k]; l += lonelyAt[k]; m |= colourAt[k];
                }
                colBlocks[x] = b; colLonely[x] = l; colColours[x] = m;
            }
            for (int x0 = 0; x0 + OrbSystem.WindowSize <= W; x0++)
            {
                int blocks = 0, lonely = 0, colours = 0;
                for (int x = x0; x < x0 + OrbSystem.WindowSize; x++) { blocks += colBlocks[x]; lonely += colLonely[x]; colours |= colColours[x]; }
                if (blocks >= OrbSystem.MinBlocksInWindow && System.Numerics.BitOperations.PopCount((uint)colours) >= OrbSystem.MinColoursInWindow
                    && lonely >= OrbSystem.MinLonelyInWindow) return true;
            }
        }
        return false;
    }

    private static void RemoveOrb(State s)
    {
        int ox = s.OrbPos % W, oy = s.OrbPos / W;
        for (int cy = oy; cy <= oy + 1; cy++)
            for (int cx = ox; cx <= ox + 1; cx++)
                if (s.Cells[cy * W + cx] == OrbCell) s.Cells[cy * W + cx] = 0;
        s.OrbPos = -1;
    }

    /// <summary>Mirrors <c>GameEngine.MaybeSpawnOrb</c>. The places and the push-aside are <see cref="OrbSystem"/> itself.</summary>
    private static void MaybeSpawnOrb(State s, LevelDefinition level)
    {
        if (s.OrbBrokeThisResolution || s.OrbPos >= 0) return;
        bool guarantee = !s.OrbEver && s.Placed >= OrbSystem.GuaranteeByPiece;
        if (!guarantee && s.PiecesSinceOrb < OrbSystem.PiecesBetween) return;

        int stackTop = 0;
        for (int i = 0; i < W * H; i++)
            if (s.Cells[i] != 0) { stackTop = H - i / W; break; }
        int viewTop = H - GameEngine.CameraFor(stackTop, H - Hidden) - GameConfig.ViewportRows;

        // The engine asks "is there a window?" and only then rolls; both are answered from the arrays here, so no Board is built
        // unless an Orb is really going down (the search asks millions of times).
        if (!guarantee)
        {
            if (!AnyOrbWindow(s, viewTop)) return;
            double u = (SeedMix.Mix32(SeedMix.Mix32(level.PieceSeed, 0x0B0B), ++s.OrbRolls) & 0xFFFFFF) / 16777216.0;
            if (!(u < OrbSystem.SpawnChance)) return;
        }

        var board = ToBoard(s);
        var spots = OrbSystem.Spots(board, viewTop, GameConfig.ViewportRows, relaxed: false);

        bool placed = false;
        foreach (var (x, y) in spots)
            if (placed = OrbSystem.TryPlace(board, x, y)) break;
        if (!placed && guarantee)
            foreach (var (x, y) in OrbSystem.Spots(board, viewTop, GameConfig.ViewportRows, relaxed: true))
                if (placed = OrbSystem.TryPlace(board, x, y)) break;
        if (!placed) return;

        GravityResolver.Apply(board);   // what was pushed up settles on the Orb
        FromBoard(s, board);
        s.OrbTimer = GameRules.Default.OrbTimer;
        s.OrbEver = true;
        s.PiecesSinceOrb = 0;
    }

    /// <summary>
    /// Mirrors <c>GravityResolver.Apply</c>: blocks fall; an Orb is a floor for what is above it and falls as one 2x2 unit
    /// when both cells under it are empty, taking what rests on it along. Repeats until nothing moves.
    /// </summary>
    private static void Gravity(State s)
    {
        do Compact(s);
        while (DropOrb(s));
    }

    private static void Compact(State s)
    {
        for (int x = 0; x < W; x++)
        {
            int write = H - 1;
            for (int y = H - 1; y >= 0; y--)
            {
                byte c = s.Cells[y * W + x];
                if (c == 0) continue;
                if (c == OrbCell)
                {
                    write = y - 1;   // blocks above rest on the Orb; the Orb itself does not move here
                    continue;
                }
                if (y != write)
                {
                    s.Cells[write * W + x] = c;
                    s.Cells[y * W + x] = 0;
                    s.Dormant[write * W + x] = s.Dormant[y * W + x];   // a sleeping block keeps sleeping while it falls
                    s.Dormant[y * W + x] = 0;
                    s.Fracture[write * W + x] = s.Fracture[y * W + x];   // and a cracked block keeps its cracks
                    s.Fracture[y * W + x] = 0;
                }
                write--;
            }
        }
    }

    private static bool DropOrb(State s)
    {
        if (s.OrbPos < 0) return false;
        int ox = s.OrbPos % W, oy = s.OrbPos / W, below = oy + 2;
        if (below >= H || s.Cells[below * W + ox] != 0 || s.Cells[below * W + ox + 1] != 0) return false;
        RemoveOrb(s);
        s.OrbPos = (oy + 1) * W + ox;
        for (int cy = oy + 1; cy <= oy + 2; cy++)
            for (int cx = ox; cx <= ox + 1; cx++)
                s.Cells[cy * W + cx] = OrbCell;
        return true;
    }

    /// <summary>
    /// Mirrors <c>ColourMatcher.MarkDormant</c>: right after a piece lands, any burst-sized group made only of that piece's
    /// own cells (a solid piece that touches nothing of its colour) is put to sleep. A group that also holds older blocks
    /// is left awake, so it pops as usual.
    /// </summary>
    private static void MarkDormant(State s, Placement p, Scratch sc)
    {
        Array.Clear(sc.Seen);
        foreach (int start in p.CellIndexes)
        {
            if (sc.Seen[start] != 0) continue;
            byte colour = s.Cells[start];

            int n = 0, sp = 0;
            bool onlyOwn = true;
            sc.Stack[sp++] = start;
            sc.Seen[start] = 1;
            while (sp > 0)
            {
                int c = sc.Stack[--sp];
                sc.Members[n++] = c;
                if (Array.IndexOf(p.CellIndexes, c) < 0) onlyOwn = false;
                int x = c % W, y = c / W;
                if (x > 0) Push(s, sc, c - 1, colour, ref sp);
                if (x < W - 1) Push(s, sc, c + 1, colour, ref sp);
                if (y > 0) Push(s, sc, c - W, colour, ref sp);
                if (y < H - 1) Push(s, sc, c + W, colour, ref sp);
            }

            if (n >= GameConfig.MinGroupSize && onlyOwn)
                for (int j = 0; j < n; j++) s.Dormant[sc.Members[j]] = 1;
        }
    }

    /// <summary>Mirrors <c>GameEngine.FractureActive</c> under the normal rules (the solver only ever plays those).</summary>
    private static bool FractureOn(LevelDefinition level) =>
        RuleFeatures.Fracture && !level.IsTutorial && level.Number >= RegionRules.FractureIntro;

    /// <summary>Pops groups, applies gravity and repeats until stable. Returns the chain length.</summary>
    private static int Settle(State s, Scratch sc, bool fracture, bool orbs)
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
                if (colour == 0 || colour == OrbCell || sc.Seen[i] != 0) continue;

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

                // A group pops only if at least one of its blocks is awake (a sleeping solid piece waits for company).
                bool live = false;
                for (int j = 0; j < n && !live; j++) live = s.Dormant[sc.Members[j]] == 0;

                if (n >= GameConfig.MinGroupSize && live)
                {
                    for (int j = 0; j < n; j++) sc.Mark[sc.Members[j]] = 1;
                    marked += n;
                    wavePoints += ScoringSystem.GroupPoints(n);
                    s.ByColour[colour] += n;
                    if (n > s.BestGroup) s.BestGroup = n;
                }
            }

            // Blocks that cracked a third time in the wave before break now (unless a burst takes them anyway).
            int brokenNow = 0;
            if (fracture)
                for (int i = 0; i < W * H; i++)
                    if (s.Fracture[i] == Board.Broken && s.Cells[i] != 0 && sc.Mark[i] == 0)
                    {
                        sc.Mark[i] = 2;
                        s.ByColour[s.Cells[i]]++;   // counts for its colour's goals, not for the burst-group goals
                        brokenNow++;
                    }

            if (marked == 0 && brokenNow == 0) break;

            // An Orb touched by a pop (a group, or a block that broke) breaks: it takes every block in its two rows and two
            // columns (GameEngine.ApplyWave). Those count as popped and for their colours, but earn no points.
            int orbPopped = 0;
            bool orbGone = false;
            if (orbs && s.OrbPos >= 0)
            {
                int ox = s.OrbPos % W, oy = s.OrbPos / W;
                bool touched = false;
                for (int i = 0; i < W * H && !touched; i++)
                {
                    if (sc.Mark[i] == 0) continue;
                    int x = i % W, y = i / W;
                    touched = (x >= ox - 1 && x <= ox + 2 && y >= oy && y <= oy + 1) || (y >= oy - 1 && y <= oy + 2 && x >= ox && x <= ox + 1);
                }
                if (touched)
                {
                    orbGone = true;
                    for (int i = 0; i < W * H; i++)
                    {
                        int x = i % W, y = i / W;
                        bool inCross = y == oy || y == oy + 1 || x == ox || x == ox + 1;
                        if (!inCross || s.Cells[i] == 0 || s.Cells[i] == OrbCell || sc.Mark[i] != 0) continue;
                        sc.Mark[i] = 3;
                        s.ByColour[s.Cells[i]]++;
                        orbPopped++;
                    }
                }
            }

            chain++;
            if (chain > s.BestChain) s.BestChain = chain;
            s.Score += wavePoints * ScoringSystem.Multiplier(chain);
            s.Popped += marked + brokenNow + orbPopped;

            for (int i = 0; i < W * H; i++)
                if (sc.Mark[i] != 0) { s.Cells[i] = 0; s.Dormant[i] = 0; s.Fracture[i] = 0; }
            if (orbGone)
            {
                RemoveOrb(s);
                s.PiecesSinceOrb = 0;
                s.OrbBrokeThisResolution = true;   // no new Orb in the resolution that broke one
            }

            // Survivors with a cleared neighbour on two or more different sides take one more crack (once per wave).
            if (fracture)
            {
                int hitCount = 0;
                for (int i = 0; i < W * H; i++)
                {
                    if (s.Cells[i] == 0 || s.Cells[i] == OrbCell) continue;
                    int x = i % W, y = i / W, sides = 0;
                    if (x > 0 && sc.Mark[i - 1] != 0) sides++;
                    if (x < W - 1 && sc.Mark[i + 1] != 0) sides++;
                    if (y > 0 && sc.Mark[i - W] != 0) sides++;
                    if (y < H - 1 && sc.Mark[i + W] != 0) sides++;
                    if (sides >= 2) sc.Stack[hitCount++] = i;   // (Stack is free here: the flood fill is done)
                }
                for (int k = 0; k < hitCount; k++)
                {
                    int i = sc.Stack[k];
                    if (s.Fracture[i] < Board.Broken) s.Fracture[i]++;
                }
            }

            Gravity(s);
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

    /// <summary>
    /// Scores a position. <paramref name="done"/> means the level is won (and the objective, if any, is met).
    /// <paramref name="dead"/> means the main goals are met but the objective is not: the real game ends the level the
    /// moment the main goals are met, so such a position can never lead to the objective.
    /// </summary>
    private static double Value(State s, LevelDefinition level, int startBlocks, Scratch sc, BonusObjective? bonus,
        out bool done, out bool dead)
    {
        int cells = 0, holes = 0, maxHeight = 0, bonusColour = 0;
        for (int x = 0; x < W; x++)
        {
            int top = -1;
            for (int y = 0; y < H; y++)
            {
                byte here = s.Cells[y * W + x];
                if (here == 0) continue;
                if (top < 0) top = y;
                if (here == OrbCell) continue;   // an Orb is not one of the level's blocks (Board.CountOccupied)
                cells++;
                if (bonus is not null && here == (byte)bonus.Colour) bonusColour++;
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
        dead = false;
        foreach (var g in level.Goals)
        {
            double p = g.Kind switch
            {
                GoalKind.Score => s.Score / (double)Math.Max(1, g.Target),
                GoalKind.PopBlocks => s.Popped / (double)Math.Max(1, g.Target),
                GoalKind.PopColour => s.ByColour[(int)g.Colour] / (double)Math.Max(1, g.Target),
                GoalKind.Chain => s.BestChain / (double)Math.Max(1, g.Target),
                GoalKind.BigGroup => s.BestGroup / (double)Math.Max(1, g.Target),
                GoalKind.ChainSet => ChainsOfAtLeast(s, Math.Max(2, g.MinWaves)) / (double)Math.Max(1, g.Target),
                _ => cells <= g.Target ? 1.0 : 1.0 - (cells - g.Target) / (double)Math.Max(1, startBlocks - g.Target),
            };
            if (p < 1.0) done = false;
            progress += Math.Clamp(p, 0.0, 1.0);
        }

        if (bonus is not null)
        {
            double p = bonus.ProgressFraction(s.BestChain, s.BestGroup, bonusColour);
            if (p < 1.0)
            {
                if (done) dead = true;
                done = false;
            }
            progress += p;
        }

        // Pairs and triples of one colour are one block away from popping: worth building.
        double potential = 0;
        Array.Clear(sc.Seen);
        for (int i = 0; i < W * H; i++)
        {
            byte colour = s.Cells[i];
            if (colour == 0 || colour == OrbCell || sc.Seen[i] != 0) continue;
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
