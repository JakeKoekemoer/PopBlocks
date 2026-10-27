using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// Alpha 0.0.1 playtest feedback: some pieces were not solid (a three-long line with the middle missing,
/// two blocks touching only at a corner). Every piece the game can hand out must be one connected shape,
/// in every orientation it can be turned to.
/// </summary>
public class ShapeCatalogTests
{
    public static IEnumerable<object[]> AllShapes() => ShapeCatalog.All.Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void Every_shape_is_one_connected_piece(string name)
    {
        var shape = ShapeCatalog.Find(name);
        Assert.True(shape.IsConnected, $"{shape.Name} has a gap or touches only at a corner.");
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void Every_shape_stays_connected_in_all_four_rotations(string name)
    {
        var shape = ShapeCatalog.Find(name);
        var piece = ShapeCatalog.Build(shape, Enumerable.Repeat(CellColour.Cyan, shape.CellCount).ToList());

        for (int turn = 0; turn < 4; turn++)
        {
            var cells = piece.Cells.Select(c => (c.X, c.Y)).ToList();
            Assert.Equal(shape.CellCount, cells.Distinct().Count());
            Assert.True(ShapeDef.IsEdgeConnected(cells), $"{shape.Name} falls apart after {turn} turn(s).");
            piece = piece.Rotated();
        }
    }

    [Fact]
    public void Gap_and_Diagonal_pieces_are_gone()
    {
        var names = ShapeCatalog.All.Select(s => s.Name).ToList();
        Assert.DoesNotContain("Gap", names);
        Assert.DoesNotContain("Diagonal", names);
    }

    [Fact]
    public void Connectivity_check_rejects_the_shapes_players_reported()
    {
        // Three in a row with the middle missing.
        Assert.False(ShapeDef.IsEdgeConnected(new[] { (-1, 0), (1, 0) }));
        // Two blocks that only touch at a corner.
        Assert.False(ShapeDef.IsEdgeConnected(new[] { (0, 0), (1, 1) }));
        // A corner made of three cells is fine.
        Assert.True(ShapeDef.IsEdgeConnected(new[] { (0, 0), (1, 0), (1, 1) }));
    }

    [Fact]
    public void Random_pieces_are_always_connected_and_fit_on_the_board()
    {
        var gen = new PieceGenerator(new Random(2024), exoticShare: 0.6);
        for (int i = 0; i < 2000; i++)
        {
            var piece = gen.Next(5);
            var cells = piece.Cells.Select(c => (c.X, c.Y)).ToList();
            Assert.True(ShapeDef.IsEdgeConnected(cells));
            foreach (var c in piece.BoardCells())
                Assert.InRange(c.X, 0, GameConfig.Columns - 1);
        }
    }
}
