using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class GravityResolverTests
{
    [Fact]
    public void Cells_fall_into_empty_spaces()
    {
        var board = Board.FromRows(new[]
        {
            "C.......",
            "........",
            "........",
        });
        var moves = GravityResolver.Apply(board);

        Assert.Equal("C.......", board.BottomRows(1));
        Assert.Single(moves);
        Assert.Equal(board.Height - 3, moves[0].FromY);
        Assert.Equal(board.Height - 1, moves[0].ToY);
    }

    [Fact]
    public void Vertical_order_of_remaining_cells_is_preserved()
    {
        var board = Board.FromRows(new[]
        {
            "C.......",
            ".R......",
            "Y.......",
            "........",
            "P.......",
        });
        // Column 0 top-to-bottom: C, Y, P with gaps. After gravity it must stay C, Y, P.
        GravityResolver.Apply(board);
        // Column 1's lone coral cell also lands on the floor, beside the purple.
        Assert.Equal("C.......\nY.......\nPR......", board.BottomRows(3));
    }

    [Fact]
    public void A_settled_board_does_not_change()
    {
        var board = Board.FromRows(new[] { "C.......", "CR......" });
        string before = board.ToString();
        var moves = GravityResolver.Apply(board);
        Assert.Equal(before, board.ToString());
        Assert.Empty(moves);
    }

    [Fact]
    public void Clearing_cells_then_applying_gravity_compacts_each_column()
    {
        var board = Board.FromRows(new[]
        {
            "R.......",
            "CCCC....",
        });
        var groups = ColourMatcher.FindGroups(board);
        GravityResolver.Clear(board, groups.SelectMany(g => g.Cells));
        GravityResolver.Apply(board);
        Assert.Equal("R.......", board.BottomRows(1));
        Assert.Equal(1, board.CountOccupied());
    }

    [Fact]
    public void Cascading_matches_are_detected_after_compaction()
    {
        var board = Board.FromRows(new[]
        {
            "...R....",
            "RRR.....",
            "CCCC....",
        });
        // Wave 1: the cyan row bursts.
        var first = ColourMatcher.FindGroups(board);
        Assert.Single(first);
        Assert.Equal(CellColour.Cyan, first[0].Colour);

        GravityResolver.Clear(board, first.SelectMany(g => g.Cells));
        GravityResolver.Apply(board);

        // After the fall the four coral cells line up along the bottom.
        Assert.Equal("RRRR....", board.BottomRows(1));
        var second = ColourMatcher.FindGroups(board);
        Assert.Single(second);
        Assert.Equal(CellColour.Coral, second[0].Colour);
    }
}
