using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class ClassicGoldRateTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(4, 1)]
    [InlineData(5, 2)] [InlineData(8, 2)]
    [InlineData(9, 3)] [InlineData(12, 3)]
    [InlineData(13, 4)] [InlineData(16, 4)]
    [InlineData(17, 5)] [InlineData(40, 5)] [InlineData(5000, 5)]
    [InlineData(0, 1)] [InlineData(-3, 1)]
    public void The_rate_follows_the_table(int classicLevel, int tenths) =>
        Assert.Equal(tenths, SeedingConfig.ClassicTenthsPerBlock(classicLevel));
}

public class ClassicGoldEngineTests
{
    private static GameEngine ClassicGame(params Piece[] pieces)
    {
        var engine = new GameEngine(new ScriptedGenerator(pieces));
        engine.Start(GameSetup.Classic);
        return engine;
    }

    [Fact]
    public void Blocks_popped_pay_one_tenth_each_at_the_first_Classic_level()
    {
        // A lone solid square would now sleep where it lands, so the pop comes from a bar joining a block already there.
        var engine = ClassicGame(Pieces.Bar(CellColour.Coral), Pieces.Square(CellColour.Lime));
        engine.Board[5, engine.Board.Height - 1] = CellColour.Coral;   // the bar lands beside it: 3 + 1 = 4

        engine.HardDrop();
        Story.Settle(engine);

        Assert.Equal(4, engine.TotalPopped);
        Assert.Equal(4, engine.ClassicGoldTenths);   // 4 blocks x 1 tenth
    }

    [Fact]
    public void A_Story_level_never_earns_Classic_gold()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5);
        var engine = Story.Start(level);

        engine.MoveLeft(); engine.MoveLeft(); engine.MoveLeft(); engine.MoveLeft();
        engine.HardDrop();
        Story.Settle(engine);

        Assert.True(engine.TotalPopped > 0);
        Assert.Equal(0, engine.ClassicGoldTenths);
    }

    [Fact]
    public void Drop_points_do_not_count_and_the_count_starts_again_with_each_game()
    {
        var engine = ClassicGame(Pieces.Bar(CellColour.Coral), Pieces.Square(CellColour.Coral));
        engine.Board[5, engine.Board.Height - 1] = CellColour.Coral;   // the bar lands beside it: 3 + 1 = 4
        engine.HardDrop();
        Story.Settle(engine);
        Assert.True(engine.Score > engine.TotalPopped * 10, "the score includes drop points that gold ignores");
        Assert.Equal(engine.TotalPopped, engine.ClassicGoldTenths);

        engine.Start(GameSetup.Classic);
        Assert.Equal(0, engine.ClassicGoldTenths);
    }

    [Fact]
    public void A_whole_run_pays_by_the_Classic_level_at_each_pop_with_no_chain_multiplier()
    {
        var engine = new GameEngine(new PieceGenerator(new Random(7)));
        engine.Start(GameSetup.Classic);
        var random = new Random(3);

        int guard = 0;
        while (engine.State is GameState.Playing or GameState.ResolvingBursts && guard++ < 4000)
        {
            if (engine.State == GameState.ResolvingBursts) { engine.Tick(); continue; }
            for (int i = random.Next(0, 7); i > 0; i--) engine.MoveRight();
            for (int i = random.Next(0, 7); i > 0; i--) engine.MoveLeft();
            engine.HardDrop();
        }

        Assert.True(engine.TotalPopped > 0);
        Assert.InRange(engine.ClassicGoldTenths, engine.TotalPopped, engine.TotalPopped * SeedingConfig.ClassicMaxTenths);
        if (engine.Level == 1) Assert.Equal(engine.TotalPopped, engine.ClassicGoldTenths);
    }

    [Fact]
    public void A_typed_seed_cannot_set_a_personal_best_but_a_random_one_can()
    {
        Assert.False(GameSetup.ClassicRun(42, chosen: true).CountsForBest);
        Assert.True(GameSetup.ClassicRun(42, chosen: false).CountsForBest);
        Assert.True(GameSetup.Classic.CountsForBest);
    }
}

public class ClassicGoldBankTests
{
    private static (ClassicGold Gold, PlayerProgress Progress, InMemorySettings Settings) Make(InMemorySettings? s = null)
    {
        s ??= new InMemorySettings();
        var progress = new PlayerProgress(s);
        return (new ClassicGold(s, progress), progress, s);
    }

    [Fact]
    public void Banking_pays_whole_gold_and_keeps_the_tenths()
    {
        var (gold, progress, settings) = Make();

        Assert.Equal(1, gold.Bank(14));

        Assert.Equal(1, progress.Gold);
        Assert.Equal(1, settings.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(4, gold.Carry);
        Assert.Equal(4, settings.GetInt(SaveKeys.ClassicGoldCarry, -1));
    }

    [Fact]
    public void Banking_the_same_run_twice_pays_once()
    {
        var (gold, progress, _) = Make();
        Assert.Equal(2, gold.Bank(25));
        Assert.Equal(0, gold.Bank(25));   // game over, then the player leaves
        Assert.Equal(0, gold.Bank(25));   // and the app goes to the background
        Assert.Equal(2, progress.Gold);
    }

    [Fact]
    public void Later_pops_in_the_same_run_pay_only_what_is_new()
    {
        var (gold, progress, _) = Make();
        Assert.Equal(1, gold.Bank(14));    // carry 4
        Assert.Equal(1, gold.Bank(27));    // 13 new + 4 carried = 17 -> 1 gold, carry 7
        Assert.Equal(2, progress.Gold);
        Assert.Equal(7, gold.Carry);
    }

    [Fact]
    public void The_fraction_carries_over_into_the_next_run_and_across_a_restart()
    {
        var (gold, progress, settings) = Make();
        gold.Bank(7);
        Assert.Equal(0, progress.Gold);
        Assert.Equal(7, gold.Carry);

        var (restarted, restartedProgress, _) = Make(settings);
        restarted.BeginRun();
        Assert.Equal(7, restarted.Carry);
        Assert.Equal(1, restarted.Bank(5));   // 7 + 5 = 12
        Assert.Equal(1, restartedProgress.Gold);
        Assert.Equal(2, restarted.Carry);
    }

    [Fact]
    public void A_new_run_starts_from_zero_banked()
    {
        var (gold, progress, _) = Make();
        gold.Bank(30);
        gold.BeginRun();
        Assert.Equal(3, gold.Bank(30));   // a different run that also earned 30
        Assert.Equal(6, progress.Gold);
    }

    [Fact]
    public void The_carry_stays_between_zero_and_nine_whatever_is_banked()
    {
        var (gold, _, settings) = Make();
        var random = new Random(1);
        int run = 0;
        for (int i = 0; i < 500; i++)
        {
            run += random.Next(0, 40);
            gold.Bank(run);
            Assert.InRange(settings.GetInt(SaveKeys.ClassicGoldCarry, 0), 0, 9);
            if (random.Next(10) == 0) { gold.BeginRun(); run = 0; }
        }
    }

    [Fact]
    public void Gold_plus_carry_always_equals_everything_earned()
    {
        var (gold, progress, _) = Make();
        var random = new Random(5);
        int earned = 0, run = 0;
        for (int i = 0; i < 300; i++)
        {
            int add = random.Next(0, 33);
            run += add;
            earned += add;
            gold.Bank(run);
            Assert.Equal(earned, progress.Gold * 10 + gold.Carry);
        }
    }

    [Fact]
    public void Previewing_pays_nothing()
    {
        var (gold, progress, settings) = Make();
        gold.Bank(6);
        string before = settings.Dump();
        Assert.Equal(1, gold.Preview(10));   // 6 carried + 4 new tenths = 10
        Assert.Equal(0, gold.Preview(6));
        Assert.Equal(before, settings.Dump());
        Assert.Equal(0, progress.Gold);
    }

    [Fact]
    public void Nothing_to_pay_changes_nothing()
    {
        var (gold, _, settings) = Make();
        string before = settings.Dump();
        Assert.Equal(0, gold.Bank(0));
        Assert.Equal(before, settings.Dump());
    }

    [Fact]
    public void Classic_gold_reads_and_writes_no_Story_state()
    {
        var spy = new SpySettings();
        var progress = new PlayerProgress(spy);
        var gold = new ClassicGold(spy, progress);
        var engine = new GameEngine(new SeededPieceGenerator(11));
        engine.Start(GameSetup.ClassicRun(11, chosen: false));

        gold.BeginRun();
        engine.HardDrop();
        gold.Bank(engine.ClassicGoldTenths + 25);

        string[] story =
        {
            SaveKeys.PlayerName, SaveKeys.PlayerSalt, SaveKeys.FirstClearSeeds, SaveKeys.CurrentLevelFor,
            SaveKeys.CurrentLevelSeed, SaveKeys.CurrentLevelRerolls, SaveKeys.CurrentLevelFailures,
        };
        Assert.Empty(spy.Touched.Intersect(story));
    }

    private sealed class SpySettings : ILocalSettings
    {
        private readonly InMemorySettings _inner = new();
        public HashSet<string> Touched { get; } = new();
        public int GetInt(string key, int d) { Touched.Add(key); return _inner.GetInt(key, d); }
        public void SetInt(string key, int v) { Touched.Add(key); _inner.SetInt(key, v); }
        public bool GetBool(string key, bool d) { Touched.Add(key); return _inner.GetBool(key, d); }
        public void SetBool(string key, bool v) { Touched.Add(key); _inner.SetBool(key, v); }
        public string GetString(string key, string d) { Touched.Add(key); return _inner.GetString(key, d); }
        public void SetString(string key, string v) { Touched.Add(key); _inner.SetString(key, v); }
    }
}

public class ClassicSeedTests
{
    private static string Describe(Piece p) => string.Join(";", p.Cells.OrderBy(c => c.X).ThenBy(c => c.Y).Select(c => $"{c.X},{c.Y},{(int)c.Colour}"));

    private static List<string> Stream(int seed, int count, int colours = 4)
    {
        var gen = new SeededPieceGenerator(seed);
        return Enumerable.Range(0, count).Select(_ => Describe(gen.Next(colours))).ToList();
    }

    [Fact]
    public void The_same_seed_gives_the_same_pieces_and_a_different_seed_does_not()
    {
        Assert.Equal(Stream(77, 60), Stream(77, 60));
        Assert.NotEqual(Stream(77, 60), Stream(78, 60));
    }

    [Fact]
    public void A_piece_does_not_depend_on_the_colours_used_before_it()
    {
        // Piece 10 with four colours is the same whether the earlier nine pieces had four colours or five.
        var a = new SeededPieceGenerator(5);
        var b = new SeededPieceGenerator(5);
        for (int i = 0; i < 10; i++) { a.Next(4); b.Next(i % 2 == 0 ? 5 : 4); }
        Assert.Equal(Describe(a.Next(4)), Describe(b.Next(4)));
    }

    [Fact]
    public void Two_seeded_games_see_the_same_pieces_whatever_the_player_does()
    {
        // Records each piece as it appears (before the player touches it), then plays it in a different way.
        List<string> Spawned(Action<GameEngine, int> play)
        {
            var engine = new GameEngine(new PieceGenerator(new Random(1)));
            engine.Start(GameSetup.ClassicRun(2024, chosen: true));
            var seen = new List<string>();
            for (int i = 0; i < 6 && engine.State == GameState.Playing && engine.CurrentPiece is not null; i++)
            {
                seen.Add(Describe(engine.CurrentPiece));
                play(engine, i);
                engine.HardDrop();
                Story.Settle(engine);
            }
            return seen;
        }

        var moved = Spawned((e, i) => { for (int k = 0; k <= i % 3; k++) { e.MoveLeft(); e.Rotate(); } });
        var plain = Spawned((_, _) => { });

        Assert.Equal(plain, moved);
        Assert.Equal(Stream(2024, plain.Count), plain);
    }

    [Fact]
    public void A_restart_with_the_same_seed_replays_the_same_opening_pieces()
    {
        var engine = new GameEngine(new PieceGenerator(new Random(9)));
        engine.Start(GameSetup.ClassicRun(31337, chosen: true));
        string first = Describe(engine.CurrentPiece!) + "/" + Describe(engine.NextPiece!);

        engine.Start(GameSetup.ClassicRun(31337, chosen: true));
        Assert.Equal(first, Describe(engine.CurrentPiece!) + "/" + Describe(engine.NextPiece!));

        engine.Start(GameSetup.ClassicRun(31338, chosen: true));
        Assert.NotEqual(first, Describe(engine.CurrentPiece!) + "/" + Describe(engine.NextPiece!));
    }

    [Fact]
    public void An_unseeded_Classic_game_still_uses_the_engines_own_generator()
    {
        var scripted = new GameEngine(new ScriptedGenerator(Pieces.Square(CellColour.Coral)));
        scripted.Start(GameSetup.Classic);
        Assert.Equal(4, scripted.CurrentPiece!.Cells.Count);
        Assert.All(scripted.CurrentPiece.Cells, c => Assert.Equal(CellColour.Coral, c.Colour));
    }

    [Fact]
    public void Seeded_streams_work_with_undo_and_rewind()
    {
        var rewindable = new RewindableGenerator(new SeededPieceGenerator(8));
        var first = Enumerable.Range(0, 5).Select(_ => Describe(rewindable.Next(4))).ToList();
        rewindable.Rewind(2);
        var again = Enumerable.Range(0, 3).Select(_ => Describe(rewindable.Next(4))).ToList();
        Assert.Equal(first.Skip(2).ToList(), again);
        Assert.Equal(Stream(8, 7).Skip(5).First(), Describe(rewindable.Next(4)));
    }
}
