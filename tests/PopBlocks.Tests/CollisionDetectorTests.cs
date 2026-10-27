using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class CollisionDetectorTests
{
    private static BlockCell C(int x, int y, CellColour colour = CellColour.Cyan) => new(x, y, colour);

    [Fact]
    public void Piece_in_open_space_is_valid()
    {
        var board = new Board();
        var piece = Pieces.At(3, 5, C(0, 0), C(1, 0));
        Assert.True(CollisionDetector.IsValid(board, piece));
    }

    [Fact]
    public void Movement_within_boundaries_is_allowed()
    {
        var board = new Board();
        var piece = Pieces.At(3, 5, C(0, 0), C(1, 0));
        Assert.True(CollisionDetector.CanMove(board, piece, -1, 0));
        Assert.True(CollisionDetector.CanMove(board, piece, 1, 0));
        Assert.True(CollisionDetector.CanMove(board, piece, 0, 1));
    }

    [Fact]
    public void Movement_beyond_left_and_right_walls_is_blocked()
    {
        var board = new Board();
        var atLeft = Pieces.At(0, 5, C(0, 0), C(1, 0));
        var atRight = Pieces.At(GameConfigColumnsMinusOne(), 5, C(-1, 0), C(0, 0));
        Assert.False(CollisionDetector.CanMove(board, atLeft, -1, 0));
        Assert.False(CollisionDetector.CanMove(board, atRight, 1, 0));
    }

    [Fact]
    public void Movement_below_the_floor_is_blocked()
    {
        var board = new Board();
        int bottom = board.Height - 1;
        var piece = Pieces.At(3, bottom, C(0, 0), C(1, 0));
        Assert.False(CollisionDetector.CanMove(board, piece, 0, 1));
    }

    [Fact]
    public void Movement_into_an_occupied_cell_is_blocked()
    {
        var board = Board.FromRows(new[] { "...L...." });   // bottom row, x = 3
        int bottom = board.Height - 1;
        var piece = Pieces.At(2, bottom - 1, C(0, 0), C(1, 0));   // sits just above the block
        Assert.False(CollisionDetector.CanMove(board, piece, 0, 1));
        Assert.True(CollisionDetector.CanMove(board, piece, -2, 0));
    }

    [Fact]
    public void Hard_drop_distance_stops_on_the_stack()
    {
        var board = Board.FromRows(new[] { "...L....", "...L...." });   // two high at x = 3
        var piece = Pieces.At(3, 1, C(0, 0));
        // Falls from row 1 until it rests on row (Height - 3).
        Assert.Equal(board.Height - 3 - 1, CollisionDetector.HardDropDistance(board, piece));
    }

    [Fact]
    public void Rotation_in_open_space_succeeds()
    {
        var board = new Board();
        var bar = Pieces.At(4, 8, C(-1, 0), C(0, 0), C(1, 0));
        Assert.True(CollisionDetector.TryRotate(board, bar, true, out var rotated));
        Assert.True(CollisionDetector.IsValid(board, rotated));
    }

    [Fact]
    public void Rotation_against_the_left_wall_kicks_the_piece_inward()
    {
        var board = new Board();
        // A vertical bar hugging the left wall; rotating it flat would poke out at x = -1 without a kick.
        var vertical = Pieces.At(0, 8, C(0, -1), C(0, 0), C(0, 1));
        Assert.True(CollisionDetector.TryRotate(board, vertical, true, out var rotated));
        Assert.True(CollisionDetector.IsValid(board, rotated));
        Assert.Equal(1, rotated.X);
    }

    [Fact]
    public void Rotation_that_cannot_fit_anywhere_is_refused()
    {
        // Fully walled-in pivot: every neighbour occupied, so no rotation or kick can fit.
        var board = Board.FromRows(new[]
        {
            "LLLLLLLL",
            "LL....LL",
            "LLLLLLLL",
        });
        int row = board.Height - 2;
        var bar = Pieces.At(3, row, C(-1, 0), C(0, 0), C(1, 0));
        Assert.True(CollisionDetector.IsValid(board, bar));
        Assert.False(CollisionDetector.TryRotate(board, bar, true, out var result));
        Assert.Equal(bar, result);
    }

    [Fact]
    public void Spawn_is_blocked_when_the_spawn_area_is_occupied()
    {
        var board = new Board();
        var spawn = Pieces.Pair(CellColour.Cyan, CellColour.Coral);
        Assert.True(CollisionDetector.IsValid(board, spawn));

        board[3, 1] = CellColour.Lime;
        Assert.False(CollisionDetector.IsValid(board, spawn));
    }

    private static int GameConfigColumnsMinusOne() => PopBlocks.Engine.GameConfig.Columns - 1;
}
