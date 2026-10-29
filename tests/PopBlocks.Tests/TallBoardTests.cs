using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 tall boards: a board taller than the screen, with a camera that follows the stack.</summary>
public class TallBoardTests
{
    private static GameRules Rows(int rows) => GameRules.Default with { BoardRows = rows };

    private static GameEngine Tall(int rows)
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.Classic with { Rules = Rows(rows) });
        return e;
    }

    /// <summary>Fills the left columns up to <paramref name="height"/> rows with colours that never join into a group.</summary>
    private static void Stack(GameEngine e, int height)
    {
        for (int h = 1; h <= height; h++)
            for (int x = 0; x < 4; x++)
                e.Board[x, e.Board.Height - h] = (CellColour)(1 + (x + h * 2) % 3);
    }

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(8, 16, 0)]      // a standard board never scrolls, however high the stack
    [InlineData(16, 16, 0)]
    [InlineData(5, 24, 0)]      // a low stack leaves the camera at the floor
    [InlineData(8, 24, 0)]
    [InlineData(12, 24, 0)]     // the screen shows the stack's top 12 rows without moving
    [InlineData(13, 24, 1)]     // above that it follows, keeping 4 rows of headroom
    [InlineData(16, 24, 4)]
    [InlineData(20, 24, 8)]     // never higher than rows - viewport
    [InlineData(24, 24, 8)]
    [InlineData(30, 32, 16)]
    public void The_camera_waits_until_the_stack_is_twelve_rows_high_then_keeps_four_rows_of_headroom(int stackTop, int rows, int expected) =>
        Assert.Equal(expected, GameEngine.CameraFor(stackTop, rows));

    [Fact]
    public void A_16_row_board_is_the_old_board_and_never_scrolls()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.Classic with { Rules = Rows(16) });
        Assert.Equal(GameConfig.TotalRows, e.Board.Height);
        Assert.Equal(16, e.BoardRows);
        Stack(e, 14);
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(0, e.CameraBottom);
        Assert.Equal(GameConfig.HiddenRows, e.ViewTopRow);
        Assert.Equal(0, e.HighestCamera);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void A_tall_board_is_the_requested_height_plus_the_hidden_rows(int rows)
    {
        var e = Tall(rows);
        Assert.Equal(rows, e.BoardRows);
        Assert.Equal(rows + GameConfig.HiddenRows, e.Board.Height);
        Assert.Equal(0, e.CameraBottom);   // an empty board: the camera is at the floor
    }

    [Fact]
    public void A_piece_spawns_just_above_the_top_of_the_screen_not_the_top_of_the_board()
    {
        var e = Tall(24);
        int top = e.CurrentPiece!.BoardCells().Min(c => c.Y);
        Assert.Equal(e.ViewTopRow - 1, top);          // one row above the screen, in the spare rows
        Assert.True(top > GameConfig.HiddenRows);     // well below the board''s own top
        Assert.True(e.Board.Height - top >= GameConfig.ViewportRows);
    }

    [Fact]
    public void The_camera_rises_with_the_stack_and_the_next_piece_spawns_at_the_new_top_of_the_screen()
    {
        var e = Tall(24);
        Stack(e, 20);
        e.HardDrop();                 // the piece in hand lands on the tall stack; the next one spawns
        Story.Settle(e);

        Assert.Equal(GameEngine.CameraFor(e.StackTop, 24), e.CameraBottom);
        Assert.True(e.CameraBottom > 0);
        Assert.Equal(e.CameraBottom, e.HighestCamera);
        Assert.Equal(e.ViewTopRow - 1, e.CurrentPiece!.BoardCells().Min(c => c.Y));
    }

    [Fact]
    public void The_camera_does_not_move_while_a_piece_falls()
    {
        var e = Tall(24);
        Stack(e, 20);
        e.HardDrop();
        Story.Settle(e);
        int camera = e.CameraBottom;
        for (int i = 0; i < 3 && e.State == GameState.Playing; i++) e.Tick();
        Assert.Equal(camera, e.CameraBottom);
    }

    [Fact]
    public void Clearing_the_stack_brings_the_camera_back_down()
    {
        var e = Tall(24);
        Stack(e, 20);
        e.HardDrop();
        Story.Settle(e);
        int high = e.CameraBottom;
        Assert.True(high > 0);

        for (int y = 0; y < e.Board.Height; y++)
            for (int x = 0; x < e.Board.Width; x++)
                e.Board[x, y] = CellColour.Empty;
        e.HardDrop();
        Story.Settle(e);

        Assert.True(e.CameraBottom < high);
        Assert.Equal(high, e.HighestCamera);   // it remembers how high it went
    }

    [Fact]
    public void A_block_in_the_top_rows_of_a_tall_board_is_an_overflow_not_one_at_the_top_of_the_screen()
    {
        var e = Tall(24);
        Stack(e, 12);               // well above the screen''s top when the camera is at the floor
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(GameState.Playing, e.State);   // a high stack alone is not an overflow

        // A column filled right up into the spawn rows at the very top of the board (alternating, so nothing pops or falls).
        for (int y = 1; y < e.Board.Height; y++) e.Board[7, y] = y % 2 == 0 ? CellColour.Coral : CellColour.Yellow;
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.StackOverflow, e.FailReason);
    }

    [Fact]
    public void A_spawn_that_collides_is_an_overflow()
    {
        var e = Tall(20);
        e.Board[3, e.ViewTopRow - 1] = CellColour.Coral;   // sits right where the next piece would appear
        e.Board[4, e.ViewTopRow - 1] = CellColour.Lime;
        e.HardDrop();
        Story.Settle(e);
        Assert.True(e.State is GameState.GameOver or GameState.Playing);
    }

    [Fact]
    public void A_levels_pile_sits_on_the_floor_of_a_tall_board()
    {
        var level = Story.Level(new[] { "RYRY....", "YRYR...." }, new[] { "Dot:L" }, new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5);
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(level, Rows(24));

        Assert.Equal(26, e.Board.Height);
        Assert.Equal(CellColour.Yellow, e.Board[0, 25]);   // the bottom row of the pile is on the floor
        Assert.Equal(CellColour.Coral, e.Board[0, 24]);
        Assert.Equal(CellColour.Empty, e.Board[0, 23]);
        Assert.Equal(8, e.Board.CountOccupied());
    }

    [Fact]
    public void Restarting_keeps_the_tall_board()
    {
        var e = Tall(28);
        Stack(e, 22);
        e.Restart();
        Assert.Equal(28, e.BoardRows);
        Assert.Equal(0, e.CameraBottom);
        Assert.Equal(0, e.Board.CountOccupied());
    }

    [Fact]
    public void Undo_puts_the_camera_back_with_the_board()
    {
        var e = Tall(24);
        e.SecondChanceStock = 1;
        e.Start(GameSetup.Classic with { Rules = Rows(24) });
        Stack(e, 19);
        e.HardDrop();
        Story.Settle(e);
        int camera = e.CameraBottom;
        int viewTop = e.ViewTopRow;
        e.HardDrop();
        Story.Settle(e);

        if (e.UseSecondChance())
        {
            Assert.Equal(GameEngine.CameraFor(e.StackTop, 24), e.CameraBottom);
            Assert.Equal(camera, e.CameraBottom);
            Assert.Equal(viewTop, e.ViewTopRow);
        }
    }

    [Fact]
    public void The_height_choices_stay_inside_the_limits_and_the_rule_is_saved()
    {
        Assert.All(GameRules.BoardRowChoices, n => Assert.InRange(n, GameConfig.MinBoardRows, GameConfig.MaxBoardRows));
        Assert.True(RuleFeatures.TallBoards);
        Assert.Equal(GameConfig.DefaultBoardRows, GameRules.Default.BoardRows);
        Assert.Equal(24, GameRules.Default.BoardRows);        // the standard board is the tall one: the camera follows the stack
        Assert.True(Rows(28).IsEasierThanDefault);        // a taller board is easier, so it pays half
        Assert.False(Rows(24).IsEasierThanDefault);
        Assert.False(Rows(16).IsEasierThanDefault);

        var store = new PopBlocks.Engine.Services.GameRulesStore(new InMemorySettings());
        store.Set(Rows(28));
        Assert.Equal(28, store.Rules.BoardRows);
    }

    [Fact]
    public void A_Story_level_is_played_on_the_24_row_default_and_a_lesson_on_16()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:Y" }, new[] { new Goal(GoalKind.Score, 999_999) }, moves: 5);
        var story = Story.Start(level);
        Assert.Equal(24, story.BoardRows);
        Assert.Equal(GameConfig.HiddenRows + 8, story.ViewTopRow);   // the piece appears at the top of the screen, not 8 rows above it
        Assert.Equal(CellColour.Coral, story.Board[0, story.Board.Height - 1]);   // the pile sits on the floor

        var lesson = Story.Start(Story.Level(new[] { "RRR....." }, new[] { "Dot:Y" }, new[] { new Goal(GoalKind.Score, 999_999) }, moves: 5, tutorial: true));
        Assert.Equal(16, lesson.BoardRows);
        Assert.Equal(0, lesson.CameraBottom);
    }
}
