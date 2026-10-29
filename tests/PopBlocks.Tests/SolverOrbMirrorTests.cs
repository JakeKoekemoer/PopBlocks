using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;
using Xunit.Abstractions;

namespace PopBlocks.Tests;

/// <summary>The solver's copy of the Orb, cracks and camera rules, checked against the real engine one move at a time.</summary>
public class SolverOrbMirrorTests
{
    private readonly ITestOutputHelper _out;
    public SolverOrbMirrorTests(ITestOutputHelper output) => _out = output;

    private static string Show(int[] cells, int[] cracks, int rows)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < GameConfig.Columns; x++) sb.Append(cells[y * GameConfig.Columns + x] == 0 ? '.' : (char)('0' + cells[y * GameConfig.Columns + x]));
            sb.Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>Plays the plan in the engine; returns the first move at which the engine's board differs from the solver's copy, or -1.</summary>
    private static (int Move, string Detail) FirstDifference(LevelDefinition level, SolveResult plan)
    {
        var trace = LevelSolver.Trace(level, plan.Plan);
        var engine = new GameEngine(new PieceGenerator(new Random(1)));
        engine.StartLevel(level);
        for (int m = 0; m < trace.Count; m++)
        {
            var move = plan.Plan[m];
            if (engine.State != GameState.Playing || engine.CurrentPiece is null) return (m, "engine not playing: " + engine.State);
            for (int r = 0; r < move.Rotations; r++) { int g = 0; while (!engine.Rotate()) { engine.Tick(); if (++g > 4) return (m, "rotate"); } }
            for (int g = 0; g < 12; g++)
            {
                int left = engine.CurrentPiece!.BoardCells().Min(c => c.X);
                if (left == move.LeftColumn) break;
                if (left > move.LeftColumn) engine.MoveLeft(); else engine.MoveRight();
            }
            engine.HardDrop();
            for (int i = 0; i < 50 && engine.State == GameState.ResolvingBursts; i++) engine.Tick();
            if (engine.State == GameState.LevelComplete) return (-1, "");

            var (cells, cracks, timer, _) = trace[m];
            int rows = engine.Board.Height;
            var mine = new int[GameConfig.Columns * rows];
            var theirs = new int[mine.Length];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < GameConfig.Columns; x++)
                    theirs[y * GameConfig.Columns + x] = (int)engine.Board[x, y];
            for (int i = 0; i < mine.Length; i++) mine[i] = cells[i];
            if (!mine.SequenceEqual(theirs) || timer != engine.OrbTimerLeft && engine.OrbAt is not null)
                return (m, $"solver {Show(mine, cracks, rows)}\n   engine {Show(theirs, cracks, rows)}\n   timer {timer} vs {engine.OrbTimerLeft}");
        }
        return (trace.Count > 0 && engine.State != GameState.LevelComplete ? trace.Count - 1 : -1, "the plan ended without winning: " + engine.State + " pieces " + engine.PiecesPlaced + "/" + engine.MoveLimit);
    }

    [Fact]
    public void The_solvers_copy_of_the_board_matches_the_engine_move_by_move_with_orbs_in_the_story()
    {
        int bad = 0;
        for (int n = 1; n <= 220; n++)
        {
            var level = LevelCatalog.Get(n);
            var plan = LevelSolver.Solve(level, level.MoveLimit > 0 ? level.MoveLimit : 12);
            var (move, detail) = FirstDifference(level, plan);
            if (move >= 0)
            {
                _out.WriteLine($"level {n}: differs at move {move}\n   {detail}");
                bad++;
            }
        }
        Assert.Equal(0, bad);
    }

    [Fact]
    public void The_same_holds_for_levels_built_from_player_seeds_and_orbs_really_do_appear()
    {
        int bad = 0, withOrb = 0;
        for (int n = 40; n <= 200; n += 8)
            for (int s = 1; s <= 3; s++)
            {
                var seed = LevelSeed.Derive(1234 + s, n, 0, 0);
                var built = LevelCatalog.Build(n, seed);
                if (!built.Verified) continue;
                var plan = new SolveResult(true, built.Plan.Count, built.Plan);
                var (move, detail) = FirstDifference(built.Level, plan);
                if (move >= 0) { _out.WriteLine($"level {n} seed {s}: differs at move {move}\n   {detail}"); bad++; }

                int orbs = 0;
                var engine = new GameEngine(new PieceGenerator(new Random(1)));
                engine.EventRaised += e => { if (e == GameEvent.OrbSpawned) orbs++; };
                engine.StartLevel(built.Level);
                foreach (var m in built.Plan)
                {
                    if (engine.State != GameState.Playing || engine.CurrentPiece is null) break;
                    for (int r = 0; r < m.Rotations; r++) engine.Rotate();
                    for (int g = 0; g < 12; g++) { int left = engine.CurrentPiece!.BoardCells().Min(c => c.X); if (left == m.LeftColumn) break; if (left > m.LeftColumn) engine.MoveLeft(); else engine.MoveRight(); }
                    engine.HardDrop();
                    for (int i = 0; i < 50 && engine.State == GameState.ResolvingBursts; i++) engine.Tick();
                }
                if (orbs > 0) withOrb++;
            }
        Assert.Equal(0, bad);
        Assert.True(withOrb >= 5, $"only {withOrb} of the played levels had an Orb");
    }

    [Fact]
    public void Levels_before_the_orbs_begin_never_get_one()
    {
        var level = LevelCatalog.Get(RegionRules.OrbIntro - 1);
        var plan = LevelSolver.Solve(level, level.MoveLimit);
        var (e, _) = LevelSolver.ReplayInEngine(level, plan);
        Assert.Null(e.OrbAt);
        Assert.False(e.OrbsActive);
    }
}
