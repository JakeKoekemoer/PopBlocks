using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class ColourMatcherTests
{
    [Fact]
    public void Group_of_three_does_not_burst()
    {
        var board = Board.FromRows(new[] { "CCC....." });
        Assert.Empty(ColourMatcher.FindGroups(board));
    }

    [Fact]
    public void Group_of_four_bursts()
    {
        var board = Board.FromRows(new[] { "CCCC...." });
        var groups = ColourMatcher.FindGroups(board);
        Assert.Single(groups);
        Assert.Equal(4, groups[0].Size);
        Assert.Equal(CellColour.Cyan, groups[0].Colour);
    }

    [Fact]
    public void Horizontal_group_is_recognised()
    {
        var board = Board.FromRows(new[] { ".RRRR..." });
        Assert.Single(ColourMatcher.FindGroups(board));
    }

    [Fact]
    public void Vertical_group_is_recognised()
    {
        var board = Board.FromRows(new[]
        {
            "Y.......",
            "Y.......",
            "Y.......",
            "Y.......",
        });
        var groups = ColourMatcher.FindGroups(board);
        Assert.Single(groups);
        Assert.Equal(4, groups[0].Size);
    }

    [Fact]
    public void Diagonal_only_connections_do_not_burst()
    {
        var board = Board.FromRows(new[]
        {
            "P.......",
            ".P......",
            "..P.....",
            "...P....",
        });
        Assert.Empty(ColourMatcher.FindGroups(board));
    }

    [Fact]
    public void L_shaped_group_counts_as_connected()
    {
        var board = Board.FromRows(new[]
        {
            "L.......",
            "L.......",
            "LL......",
        });
        var groups = ColourMatcher.FindGroups(board);
        Assert.Single(groups);
        Assert.Equal(4, groups[0].Size);
    }

    [Fact]
    public void Different_colours_do_not_join()
    {
        var board = Board.FromRows(new[] { "CCRR...." });
        Assert.Empty(ColourMatcher.FindGroups(board));
    }

    [Fact]
    public void Separate_qualifying_groups_are_all_found_from_the_same_board()
    {
        var board = Board.FromRows(new[]
        {
            "CCCC....",
            "........",
            "....RRRR",
        });
        var groups = ColourMatcher.FindGroups(board);
        Assert.Equal(2, groups.Count);
        Assert.Equal(8, groups.Sum(g => g.Size));
    }

    [Fact]
    public void Larger_group_is_reported_as_one_group()
    {
        var board = Board.FromRows(new[]
        {
            "CCC.....",
            "CCC.....",
        });
        var groups = ColourMatcher.FindGroups(board);
        Assert.Single(groups);
        Assert.Equal(6, groups[0].Size);
    }

    [Fact]
    public void Finding_groups_does_not_change_the_board()
    {
        var board = Board.FromRows(new[] { "CCCC....", "RRRR...." });
        string before = board.ToString();
        ColourMatcher.FindGroups(board);
        Assert.Equal(before, board.ToString());
    }
}
