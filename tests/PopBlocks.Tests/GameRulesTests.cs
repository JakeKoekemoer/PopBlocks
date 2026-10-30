using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 custom rules: the model, how it is saved, and how the engine plays by it.</summary>
public class GameRulesTests
{
    // ------------------------------------------------------------------ the model

    [Fact]
    public void The_default_is_the_game_as_it_has_always_been()
    {
        var d = GameRules.Default;
        Assert.True(d.IsDefault);
        Assert.False(d.IsEasierThanDefault);
        Assert.Equal(1.0, d.GoldFactor);
        Assert.Equal(1.0, d.FallSpeedMultiplier);
        Assert.Equal(5, d.ColourCount);
        Assert.Equal(GameConfig.MinGroupSize, d.MinGroupSize);
        Assert.Equal(GameConfig.SolidPieceChance, d.SolidPieceChance);
        Assert.False(d.RemapsColours);
        Assert.False(d.ChangesPieces);
        Assert.Equal(800, d.FallIntervalMs(800));
        Assert.Equal(d, d.Normalized());
        Assert.True(d.SameRulesAs(d.Normalized()));
    }

    [Theory]
    [InlineData(0.75, true)]
    [InlineData(1.25, false)]
    public void Slower_falling_is_easier_and_faster_is_not(double speed, bool easier) =>
        Assert.Equal(easier, (GameRules.Default with { FallSpeedMultiplier = speed }).IsEasierThanDefault);

    [Fact]
    public void Fewer_colours_fewer_shapes_smaller_bursts_and_no_cracks_are_easier()
    {
        Assert.True((GameRules.Default with { EnabledColours = new[] { CellColour.Cyan, CellColour.Coral, CellColour.Lime } }).IsEasierThanDefault);
        Assert.True((GameRules.Default with { EnabledShapes = new[] { "Pair", "Bar", "Block" } }).IsEasierThanDefault);
        Assert.True((GameRules.Default with { MinGroupSize = 3 }).IsEasierThanDefault);
        Assert.True((GameRules.Default with { Fracture = false }).IsEasierThanDefault);
        Assert.True((GameRules.Default with { Orbs = OrbMode.Never }).IsEasierThanDefault);
        Assert.False((GameRules.Default with { MinGroupSize = 5 }).IsEasierThanDefault);
        Assert.False((GameRules.Default with { Orbs = OrbMode.Always }).IsEasierThanDefault);
        Assert.False((GameRules.Default with { SolidPieceChance = 0.4 }).IsEasierThanDefault);
    }

    [Fact]
    public void Easier_rules_pay_half_even_with_a_harder_rule_beside_them()
    {
        var mixed = GameRules.Default with { FallSpeedMultiplier = 0.75, MinGroupSize = 5 };
        Assert.True(mixed.IsEasierThanDefault);
        Assert.Equal(0.5, mixed.GoldFactor);
        Assert.Equal(7, mixed.ScaleGold(15));      // rounded down
        Assert.Equal(3, mixed.ScaleTenths(5));     // 2.5 tenths rounds up to the nearest tenth
        Assert.Equal(15, GameRules.Default.ScaleGold(15));
    }

    [Fact]
    public void Fall_speed_changes_the_interval_inside_its_limits()
    {
        var slow = GameRules.Default with { FallSpeedMultiplier = 0.5 };
        var fast = GameRules.Default with { FallSpeedMultiplier = 2.0 };
        Assert.Equal(1600, slow.FallIntervalMs(800));
        Assert.Equal(400, fast.FallIntervalMs(800));
        Assert.Equal(GameConfig.MinFallIntervalMs / 2, fast.FallIntervalMs(GameConfig.MinFallIntervalMs));      // not faster than half the fastest
        Assert.Equal(GameConfig.InitialFallIntervalMs * 2, slow.FallIntervalMs(GameConfig.InitialFallIntervalMs * 2));   // not slower than twice the slowest
    }

    [Fact]
    public void Normalizing_forces_every_rule_into_its_legal_range()
    {
        var wild = new GameRules
        {
            FallSpeedMultiplier = 9.0,
            EnabledColours = new[] { CellColour.Lime },
            EnabledShapes = new[] { "Pair", "Pair", "Nope" },
            MinGroupSize = 99,
            SolidPieceChance = 0.31,
            BoardRows = 21,
            OrbTimer = 11,
            Orbs = (OrbMode)7,
        }.Normalized();

        Assert.Equal(2.0, wild.FallSpeedMultiplier);
        Assert.Equal(5, wild.ColourCount);                 // fewer than three colours falls back to all five
        Assert.True(wild.EnabledShapes.Count >= 3);        // fewer than three shapes falls back to all of them
        Assert.Equal(6, wild.MinGroupSize);
        Assert.Contains(wild.SolidPieceChance, GameRules.SolidChoices);
        Assert.Contains(wild.BoardRows, GameRules.BoardRowChoices);
        Assert.Contains(wild.OrbTimer, GameRules.OrbTimerChoices);
        Assert.Equal(OrbMode.Auto, wild.Orbs);
    }

    [Fact]
    public void The_shapes_always_include_one_with_four_or_more_cells()
    {
        var small = (GameRules.Default with { EnabledShapes = new[] { "Pair", "Bar", "Elbow" } }).Normalized();
        Assert.Contains(small.EnabledShapes, n => ShapeCatalog.Find(n).CellCount >= 4);
        var ok = (GameRules.Default with { EnabledShapes = new[] { "Pair", "Bar", "Block" } }).Normalized();
        Assert.Equal(3, ok.EnabledShapes.Count);
    }

    // ------------------------------------------------------------------ saving

    [Fact]
    public void A_save_with_no_rules_reads_as_the_default_and_writes_nothing_until_asked()
    {
        var settings = new InMemorySettings();
        var store = new GameRulesStore(settings);
        Assert.True(store.Rules.IsDefault);
        Assert.False(store.Rules.UseInStory);
        Assert.Equal(string.Empty, settings.Dump());
    }

    [Fact]
    public void Rules_survive_a_restart_and_a_save_code()
    {
        var settings = new InMemorySettings();
        var rules = GameRules.Default with
        {
            FallSpeedMultiplier = 0.75,
            EnabledColours = new[] { CellColour.Cyan, CellColour.Yellow, CellColour.Lime },
            EnabledShapes = new[] { "Pair", "Bar", "Block", "Plus" },
            MinGroupSize = 5,
            SolidPieceChance = 0.0,
            UseInStory = true,
        };
        new GameRulesStore(settings).Set(rules);

        var again = new GameRulesStore(settings).Rules;
        Assert.True(again.SameRulesAs(rules.Normalized()));
        Assert.True(again.UseInStory);

        var other = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(settings), other).Ok);
        Assert.True(new GameRulesStore(other).Rules.SameRulesAs(rules.Normalized()));

        foreach (var key in new[] { SaveKeys.RuleFallSpeed, SaveKeys.RuleColours, SaveKeys.RuleShapes, SaveKeys.RuleMinGroup, SaveKeys.RuleUseInStory, SaveKeys.SettingsTab })
        {
            Assert.Contains(key, SaveKeys.All);
            Assert.Contains(key, SaveCode.Keys);
        }
    }

    [Fact]
    public void Broken_saved_values_fall_back_to_the_normal_ones()
    {
        var settings = new InMemorySettings();
        settings.SetInt(SaveKeys.RuleFallSpeed, 5000);
        settings.SetString(SaveKeys.RuleColours, "zz");
        settings.SetString(SaveKeys.RuleShapes, "z9");
        settings.SetInt(SaveKeys.RuleMinGroup, -3);
        var rules = new GameRulesStore(settings).Rules;
        Assert.Equal(2.0, rules.FallSpeedMultiplier);
        Assert.Equal(5, rules.ColourCount);
        Assert.Equal(GameRules.Default.EnabledShapes.Count, rules.EnabledShapes.Count);
        Assert.Equal(3, rules.MinGroupSize);
    }

    [Fact]
    public void Rules_apply_to_Endless_always_to_the_Story_only_when_asked_and_never_to_a_lesson_or_finale()
    {
        var settings = new InMemorySettings();
        var store = new GameRulesStore(settings);
        var mine = GameRules.Default with { MinGroupSize = 3 };
        store.Set(mine);

        Assert.True(store.EffectiveFor(GameSetup.Classic).SameRulesAs(mine));
        Assert.True(store.EffectiveFor(GameSetup.Story(40)).IsDefault);
        Assert.False(store.CustomRulesInStory);

        store.Set(mine with { UseInStory = true });
        Assert.True(store.EffectiveFor(GameSetup.Story(40)).SameRulesAs(mine));
        Assert.True(store.CustomRulesInStory);
        Assert.True(store.EffectiveFor(GameSetup.Story(RegionCatalog.All[0].LastLevel)).IsDefault);   // a finale
        Assert.True(store.EffectiveFor(GameSetup.Lesson(2)).IsDefault);

        store.Reset();
        Assert.True(store.Rules.IsDefault);
        Assert.False(store.Rules.UseInStory);
    }

    // ------------------------------------------------------------------ the engine

    [Fact]
    public void A_smaller_burst_pops_what_the_normal_rules_leave_alone()
    {
        var level = Story.Level(new[] { "RR......" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 3) }, moves: 5);

        var normal = new GameEngine(new ScriptedGenerator());
        normal.StartLevel(level);
        Story.MoveTo(normal, 2);
        normal.HardDrop();
        Story.Settle(normal);
        Assert.Equal(0, normal.TotalPopped);

        var custom = new GameEngine(new ScriptedGenerator());
        custom.StartLevel(level, GameRules.Default with { MinGroupSize = 3 });
        Story.MoveTo(custom, 2);
        custom.HardDrop();
        Story.Settle(custom);
        Assert.Equal(3, custom.TotalPopped);
        Assert.Equal(3, custom.Rules.MinGroupSize);
    }

    [Fact]
    public void A_bigger_burst_needs_more_blocks_before_anything_pops()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5);

        var normal = new GameEngine(new ScriptedGenerator());
        normal.StartLevel(level);
        Story.MoveTo(normal, 3);
        normal.HardDrop();
        Story.Settle(normal);
        Assert.Equal(4, normal.TotalPopped);

        var custom = new GameEngine(new ScriptedGenerator());
        custom.StartLevel(level, GameRules.Default with { MinGroupSize = 5 });
        Story.MoveTo(custom, 3);
        custom.HardDrop();
        Story.Settle(custom);
        Assert.Equal(0, custom.TotalPopped);

        custom.Restart();
        Assert.Equal(5, custom.Rules.MinGroupSize);   // a restart keeps the rules
    }

    [Fact]
    public void A_goal_that_names_a_disabled_colour_becomes_any_colour()
    {
        var level = Story.Level(new[] { "........" }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopColour, 8, CellColour.Cyan), new Goal(GoalKind.PopColour, 8, CellColour.Lime) }, moves: 5);
        var rules = GameRules.Default with { EnabledColours = new[] { CellColour.Coral, CellColour.Yellow, CellColour.Lime } };

        var normal = new GameEngine(new ScriptedGenerator());
        normal.StartLevel(level);
        Assert.Equal(CellColour.Cyan, normal.Story!.Goals[0].Colour);

        var restricted = new GameEngine(new ScriptedGenerator());
        restricted.StartLevel(level, rules);
        Assert.Equal(CellColour.Empty, restricted.Story!.Goals[0].Colour);   // cyan is switched off: any colour will do
        Assert.Equal(CellColour.Lime, restricted.Story!.Goals[1].Colour);    // lime is on: unchanged
        restricted.Restart();
        Assert.Equal(CellColour.Empty, restricted.Story!.Goals[0].Colour);
    }

    [Fact]
    public void Only_the_enabled_colours_are_dealt_out_in_Endless()
    {
        var rules = GameRules.Default with { EnabledColours = new[] { CellColour.Coral, CellColour.Purple, CellColour.Lime } };
        var engine = new GameEngine(new PieceGenerator(new Random(7)));
        engine.Start(GameSetup.Classic with { Rules = rules });

        var seen = new HashSet<CellColour>();
        for (int i = 0; i < 40 && engine.State == GameState.Playing; i++)
        {
            foreach (var c in engine.CurrentPiece!.Cells) seen.Add(c.Colour);
            foreach (var c in engine.NextPiece!.Cells) seen.Add(c.Colour);
            Story.MoveTo(engine, i % 6);
            engine.HardDrop();
            Story.Settle(engine);
        }

        Assert.NotEmpty(seen);
        Assert.All(seen, c => Assert.Contains(c, rules.EnabledColours));
        Assert.True(seen.Count >= 2);
    }

    [Fact]
    public void Only_the_enabled_shapes_are_dealt_out()
    {
        var rules = (GameRules.Default with { EnabledShapes = new[] { "Pair", "Bar", "Block" } }).Normalized();
        var generator = new PieceGenerator(new Random(11), rules: rules);
        var sizes = new HashSet<int>();
        for (int i = 0; i < 300; i++) sizes.Add(generator.Next(4).Cells.Count);
        Assert.Subset(new HashSet<int> { 2, 3, 4 }, sizes);   // Pair, Bar and Block only: never an elbow, a tee or a plus
    }

    [Fact]
    public void The_default_rules_leave_the_piece_stream_exactly_as_it_was()
    {
        var plain = new PieceGenerator(new Random(5));
        var withDefault = new PieceGenerator(new Random(5), rules: GameRules.Default);
        for (int i = 0; i < 200; i++)
            Assert.Equal(plain.Next(4).Cells.Select(c => (c.X, c.Y, c.Colour)), withDefault.Next(4).Cells.Select(c => (c.X, c.Y, c.Colour)));

        var a = new GameEngine(new SeededPieceGenerator(3));
        var b = new GameEngine(new SeededPieceGenerator(3));
        a.Start(GameSetup.ClassicRun(42, chosen: false));
        b.Start(GameSetup.ClassicRun(42, chosen: false) with { Rules = GameRules.Default });
        Assert.Equal(a.CurrentPiece!.Cells.Select(c => c.Colour), b.CurrentPiece!.Cells.Select(c => c.Colour));
    }

    [Fact]
    public void Slower_falling_changes_the_engines_interval_but_not_the_pop_timing()
    {
        var engine = new GameEngine(new ScriptedGenerator());
        engine.Start(GameSetup.Classic);
        int normal = engine.FallIntervalMs;

        engine.Start(GameSetup.Classic with { Rules = GameRules.Default with { FallSpeedMultiplier = 0.5 } });
        Assert.Equal(normal * 2, engine.FallIntervalMs);
        Assert.Equal(engine.FallIntervalMs, engine.TimerIntervalMs);
    }


    [Fact]
    public void Pop_gold_is_halved_by_easier_rules()
    {
        var normal = new GameEngine(new ScriptedGenerator(Pieces.Bar(CellColour.Coral), Pieces.Square(CellColour.Lime)));
        normal.Start(GameSetup.Classic);
        normal.Board[5, normal.Board.Height - 1] = CellColour.Coral;
        normal.HardDrop();
        Story.Settle(normal);
        Assert.Equal(4, normal.ClassicGoldTenths);

        var easy = new GameEngine(new ScriptedGenerator(Pieces.Bar(CellColour.Coral), Pieces.Square(CellColour.Lime)));
        easy.Start(GameSetup.Classic with { Rules = GameRules.Default with { FallSpeedMultiplier = 0.5 } });
        easy.Board[5, easy.Board.Height - 1] = CellColour.Coral;
        easy.HardDrop();
        Story.Settle(easy);
        Assert.Equal(2, easy.ClassicGoldTenths);   // 4 blocks x 1 tenth x 0.5
    }

    [Fact]
    public void The_first_clear_and_the_consolation_are_halved_too()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        progress.DevSetHighestCleared(59);
        int full = GoldRewards.For(60, false);
        var level = LevelCatalog.Get(60, LevelSeed.Derive(1234, 60, 0, 0));
        var done = progress.Complete(level, goldFactor: 0.5);
        Assert.Equal(full / 2, done.GoldAwarded);

        var settings2 = new InMemorySettings();
        var progress2 = new PlayerProgress(settings2);
        progress2.DevSetHighestCleared(59);
        var half = new Consolation(settings2, progress2).Preview(60, 0, 1.0, false, false, goldFactor: 0.5);
        Assert.Equal((full / 2) / 2, half.ProgressPart);
    }

    [Fact]
    public void A_run_on_custom_rules_never_sets_a_best_score()
    {
        Assert.True(GameSetup.Classic.CountsForBest);
        Assert.False((GameSetup.Classic with { Rules = GameRules.Default with { MinGroupSize = 3 } }).CountsForBest);
    }
}
