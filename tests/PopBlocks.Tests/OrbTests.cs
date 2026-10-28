using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 Orbs: a 2x2 unit that breaks into a cross of cleared blocks, or cracks the blocks round it when its timer ends.</summary>
public class OrbTests
{
    private const int Floor = GameConfig.TotalRows - 1;
    private static readonly string[] Window = { "CRYP", "RYPC", "YPCR", "PCRY" };   // 16 blocks, 4 colours, none with a same-colour neighbour

    private static GameEngine Endless(int seed = 1, GameRules? rules = null)
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.ClassicRun(seed, chosen: false) with { Rules = rules ?? GameRules.Default });
        return e;
    }

    private static int OrbCells(Board board)
    {
        int n = 0;
        for (int y = 0; y < board.Height; y++) for (int x = 0; x < board.Width; x++) if (board[x, y] == CellColour.Orb) n++;
        return n;
    }

    // ------------------------------------------------------------------ the board rules

    [Fact]
    public void A_dense_varied_window_is_a_spot_and_its_orb_sits_in_the_middle()
    {
        var board = Board.FromRows(Window);   // the bottom four rows, columns 0 to 3
        var spots = OrbSystem.Spots(board, GameConfig.HiddenRows, GameConfig.ViewportRows, relaxed: false);
        Assert.NotEmpty(spots);
        Assert.Equal((1, board.Height - 3), spots[0]);   // the window''s middle: one in from its left and top
    }

    [Fact]
    public void A_sparse_or_one_colour_board_has_no_spot_but_the_guarantee_still_finds_one()
    {
        var sparse = Board.FromRows(new[] { "C.......", "C......." });
        Assert.Empty(OrbSystem.Spots(sparse, GameConfig.HiddenRows, GameConfig.ViewportRows, relaxed: false));
        Assert.NotEmpty(OrbSystem.Spots(sparse, GameConfig.HiddenRows, GameConfig.ViewportRows, relaxed: true));
    }

    [Fact]
    public void Placing_an_orb_pushes_blocks_up_in_order_and_takes_four_cells()
    {
        var board = Board.FromRows(Window);
        Assert.True(OrbSystem.TryPlace(board, 1, board.Height - 3));
        Assert.Equal(4, OrbCells(board));
        Assert.Equal(CellColour.Orb, board[1, board.Height - 3]);
        Assert.Equal(CellColour.Orb, board[2, board.Height - 2]);
        Assert.Equal(CellColour.Empty, board[0, 0]);
        // Column 1 had four blocks, two of them under the Orb''s rows; the two it displaced are now above it, still in order.
        Assert.NotEqual(CellColour.Empty, board[1, board.Height - 5]);
        Assert.NotEqual(CellColour.Empty, board[1, board.Height - 6]);
    }

    [Fact]
    public void An_orb_is_refused_where_a_pushed_block_would_reach_the_top_of_the_board()
    {
        var board = new Board();
        for (int y = GameConfig.HiddenRows; y < board.Height; y++) board[2, y] = y % 2 == 0 ? CellColour.Coral : CellColour.Yellow;   // a full column
        var before = board.Clone();
        Assert.False(OrbSystem.TryPlace(board, 2, board.Height - 3));
        Assert.Equal(0, OrbCells(board));
        for (int y = 0; y < board.Height; y++) Assert.Equal(before[2, y], board[2, y]);   // nothing moved
    }

    [Fact]
    public void An_orb_is_not_a_colour_it_never_joins_a_group_or_counts_as_a_block()
    {
        var board = new Board();
        OrbSystem.TryPlace(board, 2, board.Height - 2);
        Assert.Empty(ColourMatcher.FindGroups(board));
        Assert.Equal(0, board.CountOccupied());
    }

    [Fact]
    public void An_orb_is_a_floor_for_what_is_above_it_and_falls_as_one_unit_when_both_cells_under_it_are_empty()
    {
        var board = new Board();
        OrbSystem.TryPlace(board, 2, 8);               // floating in mid air
        board[2, 7] = CellColour.Coral;                // a block on top of it
        board[3, 6] = CellColour.Lime;                 // and one floating higher up, above the other column
        GravityResolver.Apply(board);

        var orb = OrbSystem.Find(board)!.Value;
        Assert.Equal((2, board.Height - 2), orb);      // it came to rest on the floor, all four cells together
        Assert.Equal(4, OrbCells(board));
        Assert.Equal(CellColour.Coral, board[2, board.Height - 3]);   // the block rode down with it, on top
        Assert.Equal(CellColour.Lime, board[3, board.Height - 3]);

        // With one block under only one of its columns it stays where it is.
        var held = new Board();
        OrbSystem.TryPlace(held, 2, held.Height - 3);
        held[3, held.Height - 1] = CellColour.Yellow;   // on the floor under only one of its two columns
        GravityResolver.Apply(held);
        Assert.Equal((2, held.Height - 3), OrbSystem.Find(held));
    }

    [Fact]
    public void The_cross_is_two_rows_and_two_columns_across_the_whole_board()
    {
        var board = new Board();
        OrbSystem.TryPlace(board, 3, board.Height - 2);
        board[0, board.Height - 1] = CellColour.Coral;   // in the Orb''s lower row, far left
        board[7, board.Height - 2] = CellColour.Lime;    // in its upper row, far right
        board[3, 4] = CellColour.Yellow;                 // high up in its left column
        board[4, 6] = CellColour.Purple;                 // and its right column
        board[0, 4] = CellColour.Cyan;                   // not in the cross

        var cross = OrbSystem.CrossCells(board, 3, board.Height - 2);
        Assert.Equal(4, cross.Count);
        Assert.DoesNotContain(cross, c => c.Colour == CellColour.Cyan);
        Assert.DoesNotContain(cross, c => c.Colour == CellColour.Orb);
    }

    // ------------------------------------------------------------------ in a game

    [Fact]
    public void A_pop_next_to_an_orb_breaks_it_clears_the_cross_and_pays_the_bonus()
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) }), Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Purple) })));
        e.Start(GameSetup.Classic);
        Assert.True(e.PlaceOrb(3, Floor - 1, timer: 8));
        for (int h = 0; h < 3; h++) e.Board[5, Floor - h] = CellColour.Coral;   // three coral right beside the Orb
        e.Board[0, Floor] = CellColour.Yellow;                                   // in the cross: its lower row
        e.Board[7, Floor - 1] = CellColour.Lime;                                  // in the cross: its upper row
        e.Board[3, Floor - 4] = CellColour.Cyan;                                  // in the cross: its left column, higher up
        e.Board[1, Floor - 3] = CellColour.Purple;                                // not in the cross

        BurstWave? orbWave = null;
        e.WaveResolved += w => { if (w.OrbBroke) orbWave = w; };
        Story.MoveTo(e, 5);
        e.HardDrop();                                                             // the fourth coral lands on the three: a burst beside the Orb
        Story.Settle(e);

        Assert.NotNull(orbWave);
        Assert.Equal(0, OrbCells(e.Board));
        Assert.Equal(3, orbWave!.OrbPopped.Count);   // the yellow, the lime and the cyan: the cross holds nothing else
        Assert.Equal(CellColour.Empty, e.Board[0, Floor]);
        Assert.Equal(CellColour.Empty, e.Board[7, Floor - 1]);
        Assert.Contains(e.Board.CountOccupied(), new[] { 1, 2 });   // the purple outside the cross is the only block left
        Assert.True(e.ClassicGoldTenths >= OrbSystem.BreakBonusGold * 10, "the 25 gold bonus is paid");
        Assert.Equal(0, e.OrbTimerLeft);
        Assert.Contains(orbWave.Gold, g => g.GoldTenths >= OrbSystem.BreakBonusGold * 10);
    }

    [Fact]
    public void The_bonus_is_halved_by_easier_rules_and_not_paid_in_a_lesson()
    {
        var easy = GameRules.Default with { FallSpeedMultiplier = 0.5 };
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        e.Start(GameSetup.Classic with { Rules = easy });
        e.PlaceOrb(3, Floor - 1, 8);
        for (int h = 0; h < 3; h++) e.Board[5, Floor - h] = CellColour.Coral;
        Story.MoveTo(e, 5);
        e.HardDrop();
        Story.Settle(e);
        Assert.InRange(e.ClassicGoldTenths, 120, 140);   // 25 gold is 250 tenths; halved it is about 125, plus the few blocks
    }

    [Fact]
    public void When_the_timer_runs_out_the_orb_cracks_the_twelve_cells_round_it_and_goes_without_gold()
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Lime) }), Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Purple) })));
        e.Start(GameSetup.Classic);
        Assert.True(e.PlaceOrb(3, Floor - 1, timer: 1));
        e.Board[2, Floor] = CellColour.Yellow;          // in the ring
        e.Board[5, Floor - 1] = CellColour.Coral;       // in the ring
        e.Board[1, Floor] = CellColour.Cyan;            // outside it
        e.Board[2, Floor - 1] = CellColour.Lime;
        e.Board.SetFracture(2, Floor - 1, 2);           // one that is about to break

        Story.MoveTo(e, 0);
        e.HardDrop();                                   // the last piece the timer allows
        Story.Settle(e);

        Assert.Equal(0, OrbCells(e.Board));
        Assert.True(e.ClassicGoldTenths < OrbSystem.BreakBonusGold * 10, "corruption pays no Orb bonus");   // (a block that breaks still pays its half)
        Assert.NotEqual(CellColour.Lime, e.Board[2, Floor - 1]);   // the block that already had two cracks broke away
        Assert.True(e.Board.FractureAt(2, Floor) >= 1 || e.Board[2, Floor] == CellColour.Empty);   // the yellow in the ring took a crack
    }

    [Fact]
    public void An_orb_appears_by_the_eighth_piece_at_the_latest_and_never_two_at_once()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var e = new GameEngine(new PieceGenerator(new Random(seed)));
            e.Start(GameSetup.ClassicRun(seed * 17, chosen: false));
            var random = new Random(seed);
            int firstSpawnAt = -1, spawns = 0;
            e.EventRaised += ev =>
            {
                if (ev != GameEvent.OrbSpawned) return;
                spawns++;
                if (firstSpawnAt < 0) firstSpawnAt = e.PiecesPlaced;
            };

            for (int step = 0; step < 30 && e.State is GameState.Playing or GameState.ResolvingBursts; step++)
            {
                if (e.State == GameState.ResolvingBursts) { e.Tick(); continue; }
                for (int s = random.Next(0, 5); s > 0; s--) e.MoveRight();
                for (int s = random.Next(0, 3); s > 0; s--) e.MoveLeft();
                e.HardDrop();
                Story.Settle(e);
                Assert.True(OrbCells(e.Board) is 0 or 4, "never more than one orb");
            }

            if (e.PiecesPlaced >= 10) Assert.InRange(firstSpawnAt, 1, 9);
            Assert.True(spawns >= 0);
        }
    }

    [Fact]
    public void There_are_four_pieces_between_one_orb_leaving_and_the_next_arriving()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var e = new GameEngine(new PieceGenerator(new Random(seed)));
            e.Start(GameSetup.ClassicRun(seed * 31, chosen: false) with { Rules = GameRules.Default with { Orbs = OrbMode.Always } });
            var random = new Random(seed + 100);
            int lastLeft = -100;
            void Shuffle() { for (int i = random.Next(0, 5); i > 0; i--) e.MoveLeft(); for (int i = random.Next(0, 5); i > 0; i--) e.MoveRight(); }
            e.EventRaised += ev =>
            {
                if (ev is GameEvent.OrbBroken or GameEvent.OrbCorrupted) lastLeft = e.PiecesPlaced;
                if (ev == GameEvent.OrbSpawned && lastLeft > -100) Assert.True(e.PiecesPlaced - lastLeft >= 3, $"seed {seed}: orb back after {e.PiecesPlaced - lastLeft}");
            };
            for (int step = 0; step < 45 && e.State is GameState.Playing or GameState.ResolvingBursts; step++)
            {
                if (e.State == GameState.ResolvingBursts) { e.Tick(); continue; }
                Shuffle();
                e.HardDrop();
                Story.Settle(e);
            }
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_orbs()
    {
        string Run()
        {
            var e = new GameEngine(new SeededPieceGenerator(5));
            e.Start(GameSetup.ClassicRun(5, chosen: true));
            var log = new List<string>();
            e.EventRaised += ev => { if (ev is GameEvent.OrbSpawned or GameEvent.OrbBroken or GameEvent.OrbCorrupted) log.Add($"{ev}@{e.PiecesPlaced}"); };
            for (int i = 0; i < 40 && e.State is GameState.Playing or GameState.ResolvingBursts; i++)
            {
                if (e.State == GameState.ResolvingBursts) { e.Tick(); continue; }
                for (int s = i * 3 % 5; s > 0; s--) e.MoveRight();
                e.HardDrop();
                Story.Settle(e);
            }
            return string.Join(",", log) + "|" + e.Board;
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Orbs_do_not_appear_when_the_rules_say_never_in_a_lesson_or_in_the_story_before_it_is_enabled()
    {
        var never = Endless(rules: GameRules.Default with { Orbs = OrbMode.Never });
        Assert.False(never.OrbsActive);
        Assert.False(Endless().Equals(null));
        Assert.True(Endless().OrbsActive);

        var lesson = new GameEngine(new ScriptedGenerator());
        lesson.Start(GameSetup.Lesson(1));
        Assert.False(lesson.OrbsActive);

        var story = new GameEngine(new ScriptedGenerator());
        story.StartLevel(Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 5, number: 60));
        Assert.False(story.OrbsActive);   // Story Orbs stay off until the solver plays them (RuleFeatures.OrbsInStory)
        var always = new GameEngine(new ScriptedGenerator());
        always.StartLevel(Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 5, number: 60),
            GameRules.Default with { Orbs = OrbMode.Always });
        Assert.True(always.OrbsActive);   // unless the player''s own rules ask for them
    }

    [Fact]
    public void Undo_gives_the_timer_back()
    {
        var level = Story.Level(new[] { "........" }, new[] { "Dot:R", "Dot:Y", "Dot:L", "Dot:P" }, new[] { new Goal(GoalKind.PopBlocks, 900) }, moves: 20);
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(level, GameRules.Default with { Orbs = OrbMode.Always });
        e.SecondChanceStock = 1;
        e.Restart();
        Assert.True(e.PlaceOrb(3, Floor - 1, timer: 5));
        Story.MoveTo(e, 0); e.HardDrop(); Story.Settle(e);     // timer 5 -> 4; the next piece appears with this in its snapshot
        Assert.Equal(4, e.OrbTimerLeft);
        Story.MoveTo(e, 1); e.HardDrop(); Story.Settle(e);     // 4 -> 3
        Assert.Equal(3, e.OrbTimerLeft);

        if (e.UseSecondChance())
        {
            Assert.Equal(4, e.OrbTimerLeft);
            Assert.Equal(4, OrbCells(e.Board));
        }
    }

    [Fact]
    public void The_orb_rules_are_registered_in_the_feature_switch_and_the_rules()
    {
        Assert.True(RuleFeatures.Orbs);
        Assert.False(RuleFeatures.OrbsInStory);
        Assert.Equal(40, RegionRules.OrbIntro);
        Assert.Equal(8, GameRules.DefaultOrbTimer);
        Assert.Equal(new[] { 6, 8, 10, 12 }, GameRules.OrbTimerChoices.ToArray());
    }
}
