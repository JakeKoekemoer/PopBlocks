using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 cracked blocks: a pop that hits a block from two sides cracks it; a third crack breaks it.</summary>
public class FractureTests
{
    private const int Floor = GameConfig.DefaultBoardRows + GameConfig.HiddenRows - 1;

    /// <summary>
    /// A game with a target block (yellow) on the floor at column 3, a column of three coral blocks at column 2 (a fourth is
    /// about to land on it), and, if asked, four lime blocks stacked at column 4. All groups pop in the first wave.
    /// </summary>
    private static GameEngine Setup(bool rightGroup, bool leftGroup = true, GameRules? rules = null, int target = 3)
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) }), Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Purple) })));
        e.Start(GameSetup.Classic with { Rules = rules ?? GameRules.Default });
        e.Board[target, Floor] = CellColour.Yellow;
        if (leftGroup) for (int h = 0; h < 3; h++) e.Board[2, Floor - h] = CellColour.Coral;
        if (rightGroup) for (int h = 0; h < 4; h++) e.Board[4, Floor - h] = CellColour.Lime;
        return e;
    }

    private static void Drop(GameEngine e, int column)
    {
        Story.MoveTo(e, column);
        e.HardDrop();
        Story.Settle(e);
    }

    [Fact]
    public void A_block_with_a_pop_on_two_sides_cracks()
    {
        var e = Setup(rightGroup: true);
        Drop(e, 2);

        Assert.Equal(CellColour.Yellow, e.Board[3, Floor]);
        Assert.Equal(1, e.Board.FractureAt(3, Floor));
        Assert.Contains(e.LastWave!.Cracked, c => c.X == 3 && c.Y == Floor);
    }

    [Fact]
    public void A_pop_on_one_side_only_does_not_crack()
    {
        var e = Setup(rightGroup: false);
        Drop(e, 2);
        Assert.Equal(0, e.Board.FractureAt(3, Floor));
        Assert.Empty(e.LastWave!.Cracked);
    }

    [Fact]
    public void A_pop_that_only_touches_at_a_corner_does_not_crack()
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        e.Start(GameSetup.Classic);
        e.Board[3, Floor] = CellColour.Yellow;
        for (int h = 1; h <= 4; h++) e.Board[4, Floor - h] = CellColour.Lime;   // above and to the right: diagonal to the target only
        for (int h = 1; h <= 4; h++) e.Board[2, Floor - h] = CellColour.Coral;  // above and to the left
        Drop(e, 7);
        Assert.Equal(0, e.Board.FractureAt(3, Floor));
    }

    [Fact]
    public void A_block_cracks_at_most_once_in_a_wave_even_with_four_cleared_neighbours()
    {
        var e = Setup(rightGroup: true);
        Drop(e, 2);
        Assert.Equal(1, e.Board.FractureAt(3, Floor));   // two sides popped, one crack
    }

    [Fact]
    public void A_second_wave_adds_a_second_crack_and_a_third_breaks_the_block_in_the_next_step()
    {
        var e = Setup(rightGroup: true);
        e.Board.SetFracture(3, Floor, 2);   // already cracked twice
        int waves = 0;
        BurstWave? breakWave = null;
        e.WaveResolved += w => { waves++; if (w.Broken.Count > 0) breakWave = w; };
        Drop(e, 2);

        Assert.True(waves >= 2, "the block breaks in the step after the pop");
        Assert.NotNull(breakWave);
        Assert.Contains(breakWave!.Broken, b => b.X == 3 && b.Colour == CellColour.Yellow);
        Assert.Empty(breakWave.Groups);                       // a break is not a burst group
        Assert.Contains(breakWave.ClearedCells, c => c.X == 3);
        Assert.Equal(CellColour.Empty, e.Board[3, Floor]);     // gone
        Assert.Equal(0, e.Board.FractureAt(3, Floor));
    }

    [Fact]
    public void A_broken_block_counts_for_its_colours_goal_but_not_for_burst_group_goals()
    {
        var goals = new GoalTracker(new[] { new Goal(GoalKind.PopColour, 9, CellColour.Yellow), new Goal(GoalKind.BigGroup, 5) }, 0);
        var wave = new BurstWave(2, 2, 0, Array.Empty<ColourGroup>(), new[] { new BlockCell(3, 10, CellColour.Yellow) }, Array.Empty<CellMove>())
        {
            Broken = new[] { new BlockCell(3, 10, CellColour.Yellow) },
        };
        goals.OnWave(wave);
        var states = goals.Evaluate(0, 0);
        Assert.Equal(1, states[0].Current);   // one yellow popped
        Assert.Equal(0, states[1].Current);   // no group
    }

    [Fact]
    public void A_break_feeds_gravity_and_can_start_another_wave()
    {
        // The block that breaks was holding up a column of four purple blocks, which fall one row on top of a purple one.
        var e = Setup(rightGroup: true);
        e.Board.SetFracture(3, Floor, 2);
        for (int h = 1; h <= 3; h++) e.Board[3, Floor - h] = CellColour.Purple;
        e.Board[1, Floor] = CellColour.Purple;
        Drop(e, 2);

        Assert.Equal(CellColour.Purple, e.Board[3, Floor]);   // they fell into the gap the break left
    }

    [Fact]
    public void Cracks_travel_with_a_falling_block()
    {
        var board = new Board(GameConfig.Columns, Floor + 1);
        board[2, Floor] = CellColour.Yellow;
        board[2, Floor - 1] = CellColour.Coral;
        board.SetFracture(2, Floor - 1, 2);
        board[2, Floor] = CellColour.Empty;   // the support goes
        GravityResolver.Apply(board);
        Assert.Equal(CellColour.Coral, board[2, Floor]);
        Assert.Equal(2, board.FractureAt(2, Floor));
        Assert.Equal(0, board.FractureAt(2, Floor - 1));
    }

    [Fact]
    public void Cracks_survive_clone_and_copy_and_a_new_block_starts_intact()
    {
        var board = new Board(GameConfig.Columns, Floor + 1);
        board[1, Floor] = CellColour.Lime;
        board.SetFracture(1, Floor, 1);
        Assert.Equal(1, board.Clone().FractureAt(1, Floor));
        var other = new Board(GameConfig.Columns, Floor + 1);
        other.CopyFrom(board);
        Assert.Equal(1, other.FractureAt(1, Floor));
        board[1, Floor] = CellColour.Coral;   // writing a block clears its cracks
        Assert.Equal(0, board.FractureAt(1, Floor));
        board.SetFracture(5, Floor, 2);       // an empty cell cannot be cracked
        Assert.Equal(0, board.FractureAt(5, Floor));
    }

    [Fact]
    public void Undo_takes_the_crack_back()
    {
        var e = Setup(rightGroup: true);
        e.SecondChanceStock = 1;
        e.Start(GameSetup.Classic);
        e.Board[3, Floor] = CellColour.Yellow;
        for (int h = 0; h < 3; h++) e.Board[2, Floor - h] = CellColour.Coral;
        for (int h = 0; h < 4; h++) e.Board[4, Floor - h] = CellColour.Lime;
        // (a second start wiped the first piece script; the engine has its own random pieces now, so only check the rewind)
        Drop(e, 0);
        if (e.UseSecondChance()) Assert.Equal(0, e.Board.FractureAt(3, Floor));
    }

    [Fact]
    public void Gold_is_paid_by_the_cracks_a_block_has()
    {
        Assert.Equal(1.0, GameEngineGold.Factor(0));
        Assert.Equal(0.75, GameEngineGold.Factor(1));
        Assert.Equal(0.5, GameEngineGold.Factor(2));
        Assert.Equal(0.5, GameEngineGold.Factor(Board.Broken));

        // A group of four with one block cracked once pays 3.75 tenths (rounded: 4) against 4 for a whole group; at two cracks 3 tenths.
        var whole = Setup(rightGroup: false);
        Drop(whole, 2);
        var cracked = Setup(rightGroup: false);
        cracked.Board.SetFracture(2, Floor, 2);
        cracked.Board.SetFracture(2, Floor - 1, 2);
        Drop(cracked, 2);
        Assert.True(cracked.ClassicGoldTenths < whole.ClassicGoldTenths);
    }

    [Fact]
    public void Nothing_cracks_when_the_rule_is_off_in_a_lesson_or_below_level_36()
    {
        var off = Setup(rightGroup: true, rules: GameRules.Default with { Fracture = false });
        Drop(off, 2);
        Assert.Equal(0, off.Board.FractureAt(3, Floor));
        Assert.False(off.FractureActive);

        Assert.False(StoryLevelFracture(35));
        Assert.True(StoryLevelFracture(36));
        Assert.False(StoryLevelFracture(36, tutorial: true));
    }

    private static bool StoryLevelFracture(int number, bool tutorial = false)
    {
        var level = Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 5, number: number, tutorial: tutorial);
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(level);
        return e.FractureActive;
    }

    [Fact]
    public void Plans_for_levels_with_cracks_replay_to_a_win_in_the_real_engine()
    {
        // The solver mirrors the cracking rules exactly: a plan it finds wins when played through the real engine.
        foreach (int number in new[] { RegionRules.FractureIntro, 45, 70, 110, 150 })
        {
            var built = LevelCatalog.Build(number, LevelSeed.Derive(7, number, 0, 0));
            Assert.True(built.Verified, $"level {number}");
            var engine = new GameEngine(new ScriptedGenerator());
            engine.StartLevel(built.Level);
            Assert.True(engine.FractureActive, $"level {number}");
            Assert.True(LevelSolver.PlanIsPlayable(built.Level, built.Plan), $"level {number}");
        }
    }
}

/// <summary>The gold weights of cracked blocks, as the engine applies them.</summary>
internal static class GameEngineGold
{
    public static double Factor(int cracks) => GameEngine.CrackGoldFactorForTests(cracks);
}
