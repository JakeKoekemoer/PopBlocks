using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class RewindableGeneratorTests
{
    private sealed class Counting : IPieceGenerator
    {
        public int Calls;
        public Piece Next(int colourCount) => Pieces.Pair(CellColour.Cyan, (CellColour)(2 + Calls++ % 4));
    }

    [Fact]
    public void Rewinding_hands_out_the_same_pieces_again_and_never_asks_the_inner_generator_twice()
    {
        var inner = new Counting();
        var g = new RewindableGenerator(inner);
        var first = new[] { g.Next(4), g.Next(4), g.Next(4) };

        g.Rewind(1);
        Assert.Same(first[1], g.Next(4));
        Assert.Same(first[2], g.Next(4));
        Assert.Equal(3, inner.Calls);

        var fresh = g.Next(4);   // beyond what was remembered: the inner generator carries on
        Assert.Equal(4, inner.Calls);
        Assert.Equal(4, g.Position);
        Assert.False(ReferenceEquals(first[2], fresh));
    }

    [Fact]
    public void Trimmed_pieces_cannot_be_rewound_to_but_later_ones_still_can()
    {
        var g = new RewindableGenerator(new Counting());
        for (int i = 0; i < 6; i++) g.Next(4);
        g.Trim(4);
        g.Rewind(4);
        g.Next(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.Rewind(2));
    }
}

public class PieceColourOrderTests
{
    private static string Colours(Piece p) => string.Concat(p.Cells.Select(c => Board.ToChar(c.Colour)));

    [Fact]
    public void A_single_coloured_piece_has_one_colouring_and_cannot_be_shifted()
    {
        var piece = Pieces.Bar(CellColour.Cyan);
        Assert.Equal(1, piece.DistinctColourOrders);
        Assert.Null(piece.WithNextColourOrder());
    }

    [Fact]
    public void Colourings_are_counted_without_repeats()
    {
        Assert.Equal(2, Pieces.Pair(CellColour.Cyan, CellColour.Coral).DistinctColourOrders);
        var three = Piece.AtSpawn(new[]
        {
            new BlockCell(-1, 0, CellColour.Cyan), new BlockCell(0, 0, CellColour.Cyan), new BlockCell(1, 0, CellColour.Coral),
        });
        Assert.Equal(3, three.DistinctColourOrders);   // CCR, CRC, RCC
        var four = Piece.AtSpawn(new[]
        {
            new BlockCell(0, 0, CellColour.Cyan), new BlockCell(1, 0, CellColour.Coral),
            new BlockCell(0, 1, CellColour.Yellow), new BlockCell(1, 1, CellColour.Yellow),
        });
        Assert.Equal(12, four.DistinctColourOrders);   // 4! / 2!
    }

    [Fact]
    public void Shifting_cycles_through_every_colouring_once_and_comes_back_without_changing_the_shape()
    {
        var start = Piece.AtSpawn(new[]
        {
            new BlockCell(0, 0, CellColour.Cyan), new BlockCell(1, 0, CellColour.Coral),
            new BlockCell(0, 1, CellColour.Yellow), new BlockCell(1, 1, CellColour.Yellow),
        }, pivot2X: 1, pivot2Y: 1);

        var seen = new HashSet<string> { Colours(start) };
        var p = start;
        for (int i = 1; i < start.DistinctColourOrders; i++)
        {
            p = p.WithNextColourOrder()!;
            Assert.True(seen.Add(Colours(p)), "a colouring repeated before the cycle was complete");
            Assert.Equal(start.X, p.X);
            Assert.Equal(start.Y, p.Y);
            Assert.Equal(start.Pivot2X, p.Pivot2X);
            Assert.Equal(start.Pivot2Y, p.Pivot2Y);
            Assert.Equal(start.Cells.Select(c => (c.X, c.Y)), p.Cells.Select(c => (c.X, c.Y)));
            Assert.Equal(start.Cells.Select(c => c.Colour).OrderBy(c => c), p.Cells.Select(c => c.Colour).OrderBy(c => c));
        }
        Assert.Equal(start.DistinctColourOrders, seen.Count);
        Assert.Equal(Colours(start), Colours(p.WithNextColourOrder()!));
    }

    [Fact]
    public void A_pair_just_swaps()
    {
        var p = Pieces.Pair(CellColour.Cyan, CellColour.Coral);
        var q = p.WithNextColourOrder()!;
        Assert.NotEqual(Colours(p), Colours(q));
        Assert.Equal(Colours(p), Colours(q.WithNextColourOrder()!));
    }
}

public class UndoTests
{
    private static readonly Goal[] Never = { new(GoalKind.Score, 999_999) };

    private static GameEngine Game(LevelDefinition level, int secondChance = 1, int prism = 0, int free = 0)
    {
        var e = new GameEngine(new ScriptedGenerator()) { SecondChanceStock = secondChance, PrismShiftStock = prism, PrismFreeCharges = free };
        e.StartLevel(level);
        return e;
    }

    private static LevelDefinition Simple(string[]? script = null, int moves = 6, Goal[]? goals = null, int number = 900, bool tutorial = false) =>
        Story.Level(new[] { "RRR.CCC." }, script ?? new[] { "Dot:R", "Dot:C", "Dot:Y", "Dot:P" }, goals ?? Never, moves, number: number, tutorial: tutorial);

    private static void Drop(GameEngine e, int column)
    {
        Story.MoveTo(e, column);
        e.HardDrop();
        Story.Settle(e);
    }

    private static string Look(GameEngine e) =>
        string.Join("|", e.Board.ToRows()) + $"#{e.Score}/{e.PiecesPlaced}/{e.TotalPopped}/{e.State}/{e.LastChain}/{e.MovesLeft}" +
        $"#{string.Join(",", e.CurrentPiece?.BoardCells().Select(c => $"{c.X}.{c.Y}{c.Colour}") ?? Array.Empty<string>())}" +
        $"#{string.Join(";", e.Upcoming.Select(p => string.Concat(p.Cells.Select(c => $"{c.X}.{c.Y}{c.Colour}"))))}" +
        $"#{string.Join(",", e.Goals.Select(g => $"{g.Current}:{g.IsComplete}"))}";

    [Fact]
    public void Second_Chance_puts_back_the_board_score_moves_goals_and_pieces()
    {
        var level = Simple(goals: new[] { new Goal(GoalKind.PopBlocks, 100), new Goal(GoalKind.BigGroup, 4) });
        var e = Game(level);
        e.PreviewCount = 1;
        string before = Look(e);

        Drop(e, 3);   // Dot:R joins RRR: four pop
        Assert.True(e.Score > 0);
        Assert.Equal(1, e.PiecesPlaced);
        Assert.NotEqual(before, Look(e));

        Assert.True(e.UseSecondChance());

        Assert.Equal(before, Look(e));
        Assert.Equal(GameState.Playing, e.State);
        Assert.Equal(0, e.Chain);
    }

    [Fact]
    public void Playing_the_same_moves_after_an_undo_gives_exactly_what_no_undo_gives()
    {
        var level = Simple(moves: 8);
        var plain = Game(level, secondChance: 0);
        Drop(plain, 3);
        Drop(plain, 3);
        Drop(plain, 0);

        var withUndo = Game(level);
        Drop(withUndo, 3);
        Drop(withUndo, 6);        // a placement the player regrets
        withUndo.UseSecondChance();
        Drop(withUndo, 3);        // the same piece again, this time where the plain game put it
        Drop(withUndo, 0);

        Assert.Equal(Look(plain), Look(withUndo));
    }

    [Fact]
    public void It_goes_back_one_step_only_and_only_once_per_level()
    {
        var e = Game(Simple(), secondChance: 3);   // plenty in stock, still one per level
        Assert.Equal(AbilityStatus.NothingToUndo, e.SecondChanceStatus);
        Assert.False(e.UseSecondChance());

        Drop(e, 3);
        Drop(e, 0);
        Assert.Equal(AbilityStatus.Ready, e.SecondChanceStatus);
        Assert.True(e.UseSecondChance());
        Assert.Equal(1, e.PiecesPlaced);              // two placed, one taken back

        Drop(e, 0);
        Assert.Equal(AbilityStatus.NoCharges, e.SecondChanceStatus);
        Assert.False(e.UseSecondChance());
        Assert.Equal(2, e.PiecesPlaced);
    }

    [Fact]
    public void Right_after_an_undo_there_is_nothing_further_to_take_back()
    {
        var e = Game(Simple());
        Drop(e, 3);
        Drop(e, 0);
        Assert.True(e.Rewind());
        Assert.False(e.CanRewind);
        Assert.False(e.Rewind());
    }

    [Fact]
    public void Without_stock_it_cannot_be_used_even_when_there_is_something_to_undo()
    {
        var e = Game(Simple(), secondChance: 0);
        Drop(e, 3);
        Assert.Equal(AbilityStatus.NoCharges, e.SecondChanceStatus);
        Assert.False(e.UseSecondChance());
        Assert.Equal(1, e.PiecesPlaced);
    }

    [Fact]
    public void It_is_refused_while_blocks_are_popping_while_paused_and_after_the_level_ends()
    {
        var e = Game(Simple());
        e.Timing = new PopTiming(100, 100);
        Drop(e, 3);              // lands and starts popping; Settle plays the pops through

        Assert.Equal(GameState.Playing, e.State);
        Story.MoveTo(e, 3);
        e.HardDrop();            // another lock; now mid-pop or just done
        if (e.State == GameState.ResolvingBursts)
        {
            Assert.Equal(AbilityStatus.Busy, e.SecondChanceStatus);
            Assert.False(e.UseSecondChance());
            Story.Settle(e);
        }

        Assert.Equal(GameState.Playing, e.State);
        e.Pause();
        Assert.Equal(AbilityStatus.Busy, e.SecondChanceStatus);
        Assert.False(e.UseSecondChance());
        Assert.False(e.Rewind());
        e.Resume();
        Assert.True(e.UseSecondChance());

        var over = Game(Simple(goals: new[] { new Goal(GoalKind.PopBlocks, 4) }));
        Drop(over, 3);
        Assert.Equal(GameState.LevelComplete, over.State);
        Assert.False(over.UseSecondChance());
        Assert.False(over.Rewind());
    }

    [Fact]
    public void It_does_not_work_in_a_tutorial_on_a_finale_or_in_Classic()
    {
        var tutorial = Game(Simple(tutorial: true));
        Drop(tutorial, 3);
        Assert.Equal(AbilityStatus.NotAllowed, tutorial.SecondChanceStatus);
        Assert.False(tutorial.UseSecondChance());

        var finale = Game(Simple() with { AllowAbilities = false, IsFinale = true });
        Drop(finale, 3);
        Assert.Equal(AbilityStatus.NotAllowed, finale.SecondChanceStatus);
        Assert.False(finale.UseSecondChance());

        var classic = new GameEngine(new PieceGenerator(new Random(3))) { SecondChanceStock = 3 };
        classic.Start();
        classic.HardDrop();
        Story.Settle(classic);
        Assert.Equal(AbilityStatus.NotAllowed, classic.SecondChanceStatus);
        Assert.False(classic.UseSecondChance());
        Assert.True(classic.Rewind());   // the mechanism itself still works; the rule is what forbids it
    }

    [Fact]
    public void Undo_raises_only_its_own_event_so_no_sound_or_tutorial_step_fires_again()
    {
        var e = Game(Simple());
        Drop(e, 3);
        var seen = new List<GameEvent>();
        e.EventRaised += seen.Add;
        e.UseSecondChance();
        Assert.Equal(new[] { GameEvent.SecondChanceUsed }, seen);
    }

    [Fact]
    public void Taking_back_a_move_does_not_refund_it_twice()
    {
        var e = Game(Simple(moves: 5));
        Assert.Equal(5, e.MovesLeft);
        Drop(e, 3);
        Drop(e, 0);
        Assert.Equal(3, e.MovesLeft);
        e.UseSecondChance();
        Assert.Equal(4, e.MovesLeft);
        Drop(e, 0);
        Assert.Equal(3, e.MovesLeft);
    }

    [Fact]
    public void A_restart_gives_the_level_its_charges_back()
    {
        var e = Game(Simple());
        Drop(e, 3);
        e.UseSecondChance();
        Assert.Equal(1, e.SecondChanceUsed);
        e.Restart();
        Assert.Equal(0, e.SecondChanceUsed);
        Assert.Equal(1, e.SecondChancesLeft);
    }

    [Fact]
    public void Run_stats_forget_what_the_undone_placement_did_but_remember_the_helper_was_used()
    {
        var e = Game(Simple());
        var run = new RunStats();
        run.Attach(e);

        Drop(e, 3);                       // pops a group of 4
        Assert.Equal(4, run.LargestGroup);
        Assert.Equal(1, run.PiecesPlaced);

        e.UseSecondChance();

        Assert.Equal(0, run.LargestGroup);
        Assert.Equal(0, run.CellsPopped);
        Assert.Equal(0, run.Waves);
        Assert.Equal(0, run.PiecesPlaced);
        Assert.Equal(1, run.AbilitiesUsed);
    }

    [Fact]
    public void A_memory_objective_cannot_be_met_by_popping_and_then_taking_it_back()
    {
        var level = Simple(goals: new[] { new Goal(GoalKind.PopBlocks, 8) });
        var bonus = new BonusObjective(BonusKind.BigGroup, 4, CellColour.Empty, "mem_x");
        var e = Game(level);
        var run = new RunStats();
        run.Attach(e);

        Drop(e, 3);                       // a group of 4
        Assert.True(bonus.Evaluate(e, run).Met);
        e.UseSecondChance();
        Drop(e, 7);                       // the same piece, somewhere that pops nothing
        Drop(e, 7);
        Assert.False(bonus.Evaluate(e, run).Met);
    }

    [Fact]
    public void Undo_works_the_same_for_a_generated_Story_level_with_a_random_piece_stream()
    {
        var level = LevelCatalog.Get(34);
        var a = new GameEngine(new ScriptedGenerator()) { SecondChanceStock = 1, PreviewCount = 3 };
        a.StartLevel(level);
        var b = new GameEngine(new ScriptedGenerator()) { PreviewCount = 3 };
        b.StartLevel(level);

        string start = Look(a);
        PlayMove(a, 1, 2);
        PlayMove(a, 0, 5);
        Assert.True(a.UseSecondChance());
        PlayMove(a, 2, 1);

        PlayMove(b, 1, 2);
        PlayMove(b, 2, 1);
        Assert.Equal(Look(b), Look(a));
        Assert.NotEqual(start, Look(a));
    }

    // ---- The property: any pattern of random play with random undos equals replaying just the moves that were kept ----

    private static void PlayMove(GameEngine e, int rotations, int column)
    {
        if (e.State != GameState.Playing) return;
        for (int i = 0; i < rotations; i++) e.Rotate();
        for (int guard = 0; guard < 12 && e.CurrentPiece is not null; guard++)
        {
            int left = e.CurrentPiece.BoardCells().Min(c => c.X);
            if (left == column) break;
            if (!(left > column ? e.MoveLeft() : e.MoveRight())) break;
        }
        if (e.State == GameState.Playing) e.HardDrop();
        Story.Settle(e);
    }

    [Theory]
    [InlineData(12, 1)]
    [InlineData(34, 2)]
    [InlineData(61, 3)]
    [InlineData(95, 4)]
    [InlineData(130, 5)]
    [InlineData(170, 6)]
    [InlineData(34, 7)]
    [InlineData(61, 8)]
    public void Random_play_with_random_undos_never_diverges_from_replaying_the_kept_moves(int levelNumber, int seed)
    {
        var level = LevelCatalog.Get(levelNumber);
        var rng = new Random(seed);

        var played = new GameEngine(new ScriptedGenerator()) { PreviewCount = 1 + seed % 3 };
        played.StartLevel(level);

        var kept = new List<(int Rot, int Col)>();
        int undos = 0;
        for (int step = 0; step < 40 && played.State == GameState.Playing; step++)
        {
            var move = (rng.Next(4), rng.Next(GameConfig.Columns));
            PlayMove(played, move.Item1, move.Item2);
            kept.Add(move);

            if (played.State == GameState.Playing && rng.NextDouble() < 0.35 && played.Rewind())
            {
                kept.RemoveAt(kept.Count - 1);
                undos++;
            }
        }

        var replay = new GameEngine(new ScriptedGenerator()) { PreviewCount = played.PreviewCount };
        replay.StartLevel(level);
        foreach (var (rot, col) in kept) PlayMove(replay, rot, col);

        Assert.Equal(Look(replay), Look(played));
        Assert.True(undos > 0 || played.State != GameState.Playing || kept.Count < 3, "the run should exercise undo");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Classic_rewinds_too_while_the_difficulty_level_has_not_changed(int seed)
    {
        // Classic raises its colour count with the score; a rewind replays pieces drawn at the higher count. That is only
        // visible after a level-up, so this stays in the first level. (Classic offers no Second Chance in the app.)
        PieceGenerator Fresh() => new(new Random(seed));
        var played = new GameEngine(Fresh());
        played.Start();
        var rng = new Random(seed * 17);
        var kept = new List<(int, int)>();

        for (int step = 0; step < 14 && played.State == GameState.Playing && played.Level == 1; step++)
        {
            var move = (rng.Next(4), rng.Next(GameConfig.Columns));
            PlayMove(played, move.Item1, move.Item2);
            if (played.Level != 1) return;      // out of the range this property covers
            kept.Add(move);
            if (played.State == GameState.Playing && rng.NextDouble() < 0.4 && played.Rewind())
                kept.RemoveAt(kept.Count - 1);
        }

        var replay = new GameEngine(Fresh());
        replay.Start();
        foreach (var (rot, col) in kept) PlayMove(replay, rot, col);

        Assert.Equal(Look(replay), Look(played));
    }
}
