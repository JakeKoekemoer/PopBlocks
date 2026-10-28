using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// Pieces lock where they land and gravity runs only after a pop, so blocks can hang over a gap for a while. The app shows
/// those gaps; <see cref="GravityResolver.FindHanging"/> decides which ones.
/// </summary>
public class HangingBlocksTests
{
    private static List<HangingRun> Hanging(Board board)
    {
        var list = new List<HangingRun>();
        GravityResolver.FindHanging(board, list);
        return list;
    }

    [Fact]
    public void A_board_resting_on_the_floor_hangs_nowhere()
    {
        var board = Board.FromRows(new[] { "CRC.....", "RCR.CC.." });
        Assert.Empty(Hanging(board));
    }

    [Fact]
    public void A_block_over_empty_floor_hangs_the_whole_way_down()
    {
        var board = new Board();
        board[2, 10] = CellColour.Cyan;

        var run = Assert.Single(Hanging(board));
        Assert.Equal(new HangingRun(2, 10, board.Height - 1 - 10, CellColour.Cyan), run);
    }

    [Fact]
    public void A_stack_is_reported_once_by_its_lowest_block_and_the_gap_under_it()
    {
        var board = new Board();
        int floor = board.Height - 1;
        board[3, floor] = CellColour.Coral;
        board[3, floor - 1] = CellColour.Coral;     // supported
        board[3, floor - 4] = CellColour.Cyan;      // two empty cells, then a stack of two
        board[3, floor - 5] = CellColour.Purple;

        var run = Assert.Single(Hanging(board));
        Assert.Equal(new HangingRun(3, floor - 4, 2, CellColour.Cyan), run);
    }

    [Fact]
    public void Two_stacks_in_one_column_are_both_found()
    {
        var board = new Board();
        int floor = board.Height - 1;
        board[0, floor - 1] = CellColour.Lime;     // one empty cell under it
        board[0, floor - 4] = CellColour.Yellow;   // two empty cells under it

        var runs = Hanging(board);
        Assert.Equal(2, runs.Count);
        Assert.Contains(new HangingRun(0, floor - 1, 1, CellColour.Lime), runs);
        Assert.Contains(new HangingRun(0, floor - 4, 2, CellColour.Yellow), runs);
    }

    [Fact]
    public void The_list_is_cleared_each_time_and_gravity_leaves_nothing_hanging()
    {
        var board = new Board();
        board[1, 5] = CellColour.Cyan;
        board[4, 9] = CellColour.Purple;

        var list = new List<HangingRun>();
        GravityResolver.FindHanging(board, list);
        Assert.Equal(2, list.Count);

        GravityResolver.Apply(board);
        GravityResolver.FindHanging(board, list);   // reused list, so the old entries must go
        Assert.Empty(list);
    }

    [Fact]
    public void A_piece_that_locks_half_over_a_gap_leaves_that_half_hanging()
    {
        // The pair lands across a gap: one half on the pile, the other half over air.
        var engine = new GameEngine(new ScriptedGenerator(Pieces.Pair(CellColour.Cyan, CellColour.Purple)));
        engine.Start();
        engine.Board[3, engine.Board.Height - 1] = CellColour.Coral;   // pile under the pair's left half only
        engine.HardDrop();

        // Left half rests on the pile; the right half locks in the air beside the empty column 4.
        var runs = Hanging(engine.Board);
        Assert.Single(runs);
        Assert.Equal(4, runs[0].X);
        Assert.Equal(CellColour.Purple, runs[0].Colour);
        Assert.Equal(1, runs[0].Gap);
    }
}
