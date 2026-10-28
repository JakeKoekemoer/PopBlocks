using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 continues: five more moves for gold after running out, three per attempt.</summary>
public class ContinueTests
{
    private static readonly string[] Dots = Enumerable.Range(0, 60)
        .Select(i => "Dot:" + "RYLP"[i % 4]).ToArray();

    private static LevelDefinition Unwinnable(int moves, int number = 900) =>
        Story.Level(new[] { "........" }, Dots, new[] { new Goal(GoalKind.PopBlocks, 9999) }, moves: moves, number: number);

    /// <summary>Drops the pieces in a spread of columns, so nothing piles up to the top, until the game ends or <paramref name="max"/> are placed.</summary>
    private static void PlaceUntilOver(GameEngine e, int max = 40)
    {
        for (int i = 0; i < max && e.State == GameState.Playing; i++)
        {
            Story.MoveTo(e, i % 8);
            e.HardDrop();
            Story.Settle(e);
        }
    }

    // ------------------------------------------------------------------ pricing

    [Fact]
    public void The_ladder_is_30_42_55_and_stops_at_three()
    {
        Assert.Equal(30, ContinuePricing.Price(1));
        Assert.Equal(42, ContinuePricing.Price(2));
        Assert.Equal(55, ContinuePricing.Price(3));
        Assert.Equal(0, ContinuePricing.Price(0));
        Assert.Equal(0, ContinuePricing.Price(4));
        Assert.Equal(3, ContinuePricing.MaxPerAttempt);
        Assert.Equal(5, ContinuePricing.ExtraMoves);
    }

    // ------------------------------------------------------------------ the engine

    [Fact]
    public void Running_out_of_moves_offers_a_continue_that_adds_five_moves()
    {
        var e = Story.Start(Unwinnable(moves: 3));
        PlaceUntilOver(e);

        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.OutOfMoves, e.FailReason);
        Assert.True(e.CanContinue);
        int placed = e.PiecesPlaced;

        Assert.True(e.Continue());

        Assert.Equal(GameState.Playing, e.State);
        Assert.Equal(FailReason.None, e.FailReason);
        Assert.Equal(placed, e.PiecesPlaced);
        Assert.Equal(8, e.MoveLimit);
        Assert.Equal(5, e.MovesLeft);
        Assert.Equal(1, e.ContinuesUsed);
        Assert.NotNull(e.CurrentPiece);
    }

    [Fact]
    public void A_continue_does_nothing_before_the_game_is_lost()
    {
        var e = Story.Start(Unwinnable(moves: 3));
        Assert.False(e.CanContinue);
        Assert.False(e.Continue());
        Assert.Equal(3, e.MoveLimit);
        Assert.Equal(0, e.ContinuesUsed);
    }

    [Fact]
    public void The_next_piece_comes_from_the_same_stream_as_if_the_level_had_more_moves()
    {
        var continued = Story.Start(Unwinnable(moves: 3));
        PlaceUntilOver(continued);
        continued.Continue();

        var control = Story.Start(Unwinnable(moves: 8));
        for (int i = 0; i < 3; i++)
        {
            Story.MoveTo(control, i % 8);
            control.HardDrop();
            Story.Settle(control);
        }

        Assert.Equal(control.CurrentPiece!.BoardCells().Select(c => c.Colour), continued.CurrentPiece!.BoardCells().Select(c => c.Colour));
        Assert.Equal(control.NextPiece!.BoardCells().Select(c => c.Colour), continued.NextPiece!.BoardCells().Select(c => c.Colour));
    }

    [Fact]
    public void A_continue_takes_an_undo_snapshot_like_any_new_piece()
    {
        var e = Story.Start(Unwinnable(moves: 2));
        e.SecondChanceStock = 1;
        e.Restart();
        PlaceUntilOver(e);
        Assert.True(e.Continue());

        Story.MoveTo(e, 0);
        e.HardDrop();
        Story.Settle(e);

        Assert.True(e.UseSecondChance());   // takes back the piece placed after the continue, not the lost one
        Assert.Equal(GameState.Playing, e.State);
    }

    [Fact]
    public void Three_continues_per_attempt_and_no_fourth()
    {
        var e = Story.Start(Unwinnable(moves: 2));
        for (int n = 1; n <= 3; n++)
        {
            PlaceUntilOver(e);
            Assert.Equal(GameState.GameOver, e.State);
            Assert.True(e.CanContinue, $"continue {n}");
            Assert.True(e.Continue());
            Assert.Equal(n, e.ContinuesUsed);
        }

        PlaceUntilOver(e);
        Assert.Equal(GameState.GameOver, e.State);
        Assert.False(e.CanContinue);
        Assert.False(e.Continue());
        Assert.Equal(3, e.ContinuesUsed);
        Assert.Equal(2 + 15, e.MoveLimit);
    }

    [Fact]
    public void A_retry_starts_a_fresh_attempt_with_all_three_continues()
    {
        var e = Story.Start(Unwinnable(moves: 2));
        PlaceUntilOver(e);
        e.Continue();
        Assert.Equal(1, e.ContinuesUsed);

        e.Restart();

        Assert.Equal(0, e.ContinuesUsed);
        Assert.Equal(2, e.MoveLimit);
    }

    [Fact]
    public void A_stack_overflow_is_never_offered_a_continue()
    {
        var e = Story.Start(Unwinnable(moves: 30));
        // A board packed to the spawn rows with colours that never join: the next piece cannot be placed.
        for (int y = GameConfig.HiddenRows + 1; y < e.Board.Height; y++)
            for (int x = 0; x < GameConfig.Columns; x++)
                e.Board[x, y] = (CellColour)((x + y * 2) % 3);
        e.HardDrop();
        Story.Settle(e);
        for (int i = 0; i < 6 && e.State == GameState.Playing; i++) { e.HardDrop(); Story.Settle(e); }

        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.StackOverflow, e.FailReason);
        Assert.False(e.CanContinue);
        Assert.False(e.Continue());
    }

    [Fact]
    public void A_finale_is_never_offered_a_continue()
    {
        foreach (var region in RegionCatalog.All)
        {
            var e = Story.Start(Unwinnable(moves: 2, number: region.LastLevel));
            PlaceUntilOver(e);
            Assert.Equal(FailReason.OutOfMoves, e.FailReason);
            Assert.False(e.CanContinue, $"finale {region.LastLevel}");
        }
    }

    [Fact]
    public void A_level_the_player_eased_and_a_lesson_are_never_offered_a_continue()
    {
        var eased = Story.Start(Unwinnable(moves: 2).ToEased());
        PlaceUntilOver(eased);
        Assert.Equal(FailReason.OutOfMoves, eased.FailReason);
        Assert.False(eased.CanContinue);

        var lesson = Story.Start(Story.Level(new[] { "........" }, Dots, new[] { new Goal(GoalKind.PopBlocks, 9999) }, moves: 2, tutorial: true));
        PlaceUntilOver(lesson);
        Assert.Equal(FailReason.OutOfMoves, lesson.FailReason);
        Assert.False(lesson.CanContinue);
    }

    [Fact]
    public void Run_stats_count_continues_and_forget_the_failure()
    {
        var e = Story.Start(Unwinnable(moves: 2));
        var run = new RunStats();
        run.Attach(e);
        e.Restart();
        PlaceUntilOver(e);
        Assert.True(run.Failed);

        e.Continue();

        Assert.False(run.Failed);
        Assert.Equal(1, run.Continues);

        var stats = new LifetimeStats(new InMemorySettings());
        stats.Add(run);
        Assert.Equal(1, stats.Continues);
        Assert.Contains(SaveKeys.StatContinues, SaveKeys.StatKeys);
    }
}
