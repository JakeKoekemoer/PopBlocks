using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// A "solid" piece is a burst-sized group of one colour all by itself (a one-colour square, say). It used to pop the moment
/// it landed, even touching nothing. Now the generator makes them rare and the board puts them to sleep: they only pop once
/// another block of their colour joins them.
/// </summary>
public class SolidPieceTests
{
    private const CellColour Coral = CellColour.Coral;

    private static GameEngine NewEngine(params Piece[] script) => new(new ScriptedGenerator(script));

    private static void Drop(GameEngine engine)
    {
        engine.HardDrop();
        Story.Settle(engine);
    }

    // ---------------------------------------------------------------- the engine

    [Fact]
    public void A_solid_piece_does_not_pop_when_it_lands()
    {
        var engine = NewEngine(Pieces.Square(Coral));
        engine.Start();
        Drop(engine);

        Assert.Equal(0, engine.TotalPopped);
        Assert.Equal(4, engine.Board.CountOccupied());
        Assert.Equal(GameState.Playing, engine.State);

        int bottom = engine.Board.Height - 1;
        foreach (var (x, y) in new[] { (3, bottom), (4, bottom), (3, bottom - 1), (4, bottom - 1) })
        {
            Assert.Equal(Coral, engine.Board[x, y]);
            Assert.True(engine.Board.IsDormant(x, y));
        }
    }

    [Fact]
    public void A_sleeping_solid_piece_pops_with_everything_that_joins_it()
    {
        var engine = NewEngine(Pieces.Square(Coral), Pieces.Pair(Coral, Coral));
        engine.Start();
        Drop(engine);          // the square sleeps on the floor
        Drop(engine);          // the pair lands on top of it: 2 + 4 are one group

        Assert.Equal(6, engine.TotalPopped);
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    [Fact]
    public void A_sleeping_solid_piece_stays_put_while_other_colours_land_on_it()
    {
        var engine = NewEngine(Pieces.Square(Coral), Pieces.Pair(CellColour.Cyan, CellColour.Purple));
        engine.Start();
        Drop(engine);
        Drop(engine);

        int bottom = engine.Board.Height - 1;
        Assert.Equal(0, engine.TotalPopped);
        Assert.Equal(6, engine.Board.CountOccupied());
        Assert.True(engine.Board.IsDormant(3, bottom - 1));    // still asleep
        Assert.False(engine.Board.IsDormant(3, bottom - 2));   // the pair on top is an ordinary block
    }

    [Fact]
    public void Two_solid_pieces_that_touch_pop_together()
    {
        var engine = NewEngine(Pieces.Square(Coral), Pieces.Square(Coral));
        engine.Start();
        Drop(engine);
        engine.MoveRight();
        engine.MoveRight();    // the second square lands right beside the first
        Drop(engine);

        Assert.Equal(8, engine.TotalPopped);
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    [Fact]
    public void A_piece_that_connects_to_older_blocks_pops_as_usual()
    {
        var engine = NewEngine(Pieces.Bar(Coral));
        engine.Start();
        engine.Board[5, engine.Board.Height - 1] = Coral;   // the bar (3) lands beside it
        Drop(engine);

        Assert.Equal(4, engine.TotalPopped);
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    private static int SleepingBlocks(Board board)
    {
        int n = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board.IsDormant(x, y)) n++;
        return n;
    }

    [Fact]
    public void Sleeping_blocks_are_still_asleep_after_a_Second_Chance()
    {
        var level = Story.Level(new[] { "CCC.CCC." }, new[] { "Block:RRRR", "Dot:Y", "Dot:Y", "Dot:Y" },
            new[] { new Goal(GoalKind.Score, 999_999) }, moves: 8) with { AllowAbilities = true };
        var engine = new GameEngine(new ScriptedGenerator()) { SecondChanceStock = 1 };
        engine.StartLevel(level);

        Drop(engine);                          // the Coral block lands and sleeps
        Story.MoveTo(engine, 0);
        Drop(engine);                          // a Yellow dot lands somewhere else
        Assert.Equal(4, SleepingBlocks(engine.Board));

        Assert.True(engine.UseSecondChance()); // takes the dot back
        Assert.Equal(4, SleepingBlocks(engine.Board));
        Assert.Equal(0, engine.TotalPopped);
    }

    // ---------------------------------------------------------------- the board

    [Fact]
    public void Dormant_blocks_fall_with_gravity_and_stay_dormant()
    {
        var board = new Board();
        board[2, 10] = Coral;
        board.SetDormant(2, 10);

        GravityResolver.Apply(board);

        Assert.Equal(Coral, board[2, board.Height - 1]);
        Assert.True(board.IsDormant(2, board.Height - 1));
        Assert.False(board.IsDormant(2, 10));
    }

    [Fact]
    public void Dormancy_is_copied_with_the_board_and_cleared_by_writing_the_cell()
    {
        var board = new Board();
        board[1, 5] = Coral;
        board.SetDormant(1, 5);

        Assert.True(board.Clone().IsDormant(1, 5));
        var other = new Board();
        other.CopyFrom(board);
        Assert.True(other.IsDormant(1, 5));

        board.SetDormant(2, 5);                    // an empty cell cannot be asleep
        Assert.False(board.IsDormant(2, 5));

        board[1, 5] = Coral;                       // writing a cell makes it an ordinary block again
        Assert.False(board.IsDormant(1, 5));

        other.Clear();
        Assert.False(other.IsDormant(1, 5));
    }

    // ---------------------------------------------------------------- the matcher

    [Fact]
    public void A_group_of_only_dormant_blocks_is_ignored_until_a_live_block_joins_it()
    {
        var board = Board.FromRows(new[] { "CCCC...." });
        int y = board.Height - 1;
        for (int x = 0; x < 4; x++) board.SetDormant(x, y);
        Assert.Empty(ColourMatcher.FindGroups(board));

        board[4, y] = CellColour.Cyan;
        var group = Assert.Single(ColourMatcher.FindGroups(board));
        Assert.Equal(5, group.Size);
    }

    [Fact]
    public void Landing_beside_a_block_of_the_same_colour_is_a_connection_not_a_sleeper()
    {
        var board = Board.FromRows(new[] { "C......." });
        int y = board.Height - 1;
        for (int x = 1; x <= 3; x++) board[x, y] = CellColour.Cyan;

        ColourMatcher.MarkDormant(board, new[] { (1, y), (2, y), (3, y) });

        Assert.False(board.IsDormant(1, y));
        Assert.Single(ColourMatcher.FindGroups(board));
    }

    [Fact]
    public void The_placement_preview_agrees_with_the_engine()
    {
        var board = new Board();
        var square = Pieces.Square(Coral);
        Assert.Equal(0, PlacementPreview.CountWaves(board, square));   // sleeps, so no burst
        Assert.False(PlacementPreview.StartsChain(board, square));

        board[5, board.Height - 1] = Coral;                            // now it would touch a Coral block
        Assert.Equal(1, PlacementPreview.CountWaves(board, square));
    }

    // ---------------------------------------------------------------- the pieces

    [Fact]
    public void Solid_pieces_are_spotted_by_connection_not_by_count()
    {
        Assert.True(Pieces.Square(Coral).IsSolid);
        Assert.Equal(Coral, Pieces.Square(Coral).SolidColour);
        Assert.False(Pieces.Bar(Coral).IsSolid);                       // three is not enough
        Assert.False(ShapeCatalog.Parse("Tee:CCCR").IsSolid);
        Assert.True(ShapeCatalog.Parse("Plus:CCCCR").IsSolid);         // four arms-and-middle that touch
        Assert.False(ShapeCatalog.Parse("Plus:CCRCC").IsSolid);        // the middle is the odd one out: four arms that only touch it
        Assert.True(ShapeCatalog.Parse("Long:CCCCR").IsSolid);
        Assert.False(ShapeCatalog.Parse("Long:CCRCC").IsSolid);        // split into two pairs
    }

    // ---------------------------------------------------------------- the generator

    private static int CountSolid(PieceGenerator generator, int colours, int pieces)
    {
        int solid = 0;
        for (int i = 0; i < pieces; i++)
            if (generator.Next(colours).IsSolid) solid++;
        return solid;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void With_no_chance_of_solid_pieces_none_are_made(int colours)
    {
        var generator = new PieceGenerator(new Random(100 + colours), exoticShare: 0.5, solidChance: 0.0);
        Assert.Equal(0, CountSolid(generator, colours, 20000));
    }

    [Fact]
    public void By_default_solid_pieces_are_rare_but_they_do_turn_up()
    {
        // Before the fix about 11 pieces in 100 were solid; now about 1.6 in 100.
        int solid = CountSolid(new PieceGenerator(new Random(7)), colours: 4, pieces: 20000);
        Assert.InRange(solid, 100, 700);
    }

    [Fact]
    public void With_every_chance_kept_solid_pieces_come_at_the_old_rate()
    {
        int solid = CountSolid(new PieceGenerator(new Random(7), solidChance: 1.0), colours: 4, pieces: 20000);
        Assert.InRange(solid, 1500, 3000);
    }

    [Fact]
    public void Seeded_Classic_pieces_follow_the_same_rule()
    {
        int solid = 0;
        var generator = new SeededPieceGenerator(1234);
        for (int i = 0; i < 20000; i++)
            if (generator.Next(4).IsSolid) solid++;
        Assert.InRange(solid, 100, 700);
    }

    // ---------------------------------------------------------------- Prism Shift

    [Fact]
    public void Prism_Shift_will_not_turn_an_ordinary_piece_into_a_solid_one()
    {
        // "Plus:CCRCC" has the odd colour in the middle. Every other way to deal those colours out is a solid piece.
        var level = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:Y", "Plus:CCRCC" }, new[] { new Goal(GoalKind.Score, 999_999) }, moves: 8)
            with { AllowAbilities = true };
        var engine = new GameEngine(new ScriptedGenerator()) { PrismShiftStock = 1, PreviewCount = 2 };
        engine.StartLevel(level);

        Assert.False(engine.NextPiece!.IsSolid);
        Assert.False(engine.ShiftNextPiece());
        Assert.Equal(1, engine.PrismShiftsLeft);   // nothing was spent
    }
}
