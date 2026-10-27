using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class PieceTests
{
    [Fact]
    public void Four_clockwise_rotations_return_to_the_start()
    {
        var piece = Piece.AtSpawn(new[]
        {
            new BlockCell(-1, 0, CellColour.Cyan), new BlockCell(0, 0, CellColour.Coral),
            new BlockCell(1, 0, CellColour.Yellow), new BlockCell(1, 1, CellColour.Purple),
        });
        var turned = piece.Rotated().Rotated().Rotated().Rotated();
        Assert.Equal(piece.Cells.ToList(), turned.Cells.ToList());
    }

    [Fact]
    public void Clockwise_then_counter_clockwise_is_a_no_op()
    {
        var piece = Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Lime), new BlockCell(1, 0, CellColour.Cyan) });
        Assert.Equal(piece.Cells.ToList(), piece.Rotated(true).Rotated(false).Cells.ToList());
    }

    [Fact]
    public void Clockwise_rotation_turns_a_rightward_cell_downward()
    {
        var piece = Pieces.At(4, 5, new BlockCell(0, 0, CellColour.Cyan), new BlockCell(1, 0, CellColour.Coral));
        var rotated = piece.Rotated(true);
        Assert.Contains(new BlockCell(0, 1, CellColour.Coral), rotated.Cells.ToList());
    }

    [Fact]
    public void Square_rotates_in_place_but_shuffles_its_colours()
    {
        var square = Piece.AtSpawn(new[]
        {
            new BlockCell(0, 0, CellColour.Cyan), new BlockCell(1, 0, CellColour.Coral),
            new BlockCell(0, 1, CellColour.Yellow), new BlockCell(1, 1, CellColour.Purple),
        }, pivot2X: 1, pivot2Y: 1);

        var turned = square.Rotated();
        // Same footprint...
        var footprint = turned.Cells.Select(c => (c.X, c.Y)).OrderBy(p => p).ToList();
        Assert.Equal(new[] { (0, 0), (0, 1), (1, 0), (1, 1) }.ToList(), footprint);
        // ...but the top-left cell's colour has moved to the top-right.
        Assert.Contains(new BlockCell(1, 0, CellColour.Cyan), turned.Cells.ToList());
    }

    [Fact]
    public void Spawned_pieces_are_centred_and_sit_in_the_hidden_rows()
    {
        var gen = new PieceGenerator(new Random(7));
        for (int i = 0; i < 200; i++)
        {
            var piece = gen.Next(5);
            Assert.InRange(piece.Count, 2, 4);
            foreach (var c in piece.BoardCells())
            {
                Assert.InRange(c.X, 0, GameConfig.Columns - 1);
                Assert.InRange(c.Y, GameConfig.HiddenRows - 1, GameConfig.HiddenRows);
            }
        }
    }

    [Fact]
    public void Generator_respects_the_colour_count()
    {
        var gen = new PieceGenerator(new Random(11));
        for (int i = 0; i < 300; i++)
        {
            var piece = gen.Next(3);
            foreach (var c in piece.Cells)
                Assert.InRange((int)c.Colour, 1, 3);
        }
    }

    [Fact]
    public void Generator_produces_both_single_and_multi_colour_pieces()
    {
        var gen = new PieceGenerator(new Random(3));
        var distinctCounts = new HashSet<int>();
        for (int i = 0; i < 200; i++)
            distinctCounts.Add(gen.Next(5).Cells.Select(c => c.Colour).Distinct().Count());
        Assert.Contains(1, distinctCounts);
        Assert.Contains(2, distinctCounts);
    }
}
