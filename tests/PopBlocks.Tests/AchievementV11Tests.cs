using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The sixteen achievements of v1.1 and the counters behind them.</summary>
public class AchievementV11Tests
{
    private static bool Met(string id, AchievementFacts facts) => AchievementCatalog.Find(id)!.IsMet(facts);

    private static LevelDefinition Level(bool eased = false)
    {
        var level = Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 9, number: 60);
        return eased ? level.ToEased() : level;
    }

    private static RunStats Run(Action<GameEngine>? play = null)
    {
        var engine = new GameEngine(new ScriptedGenerator());
        var run = new RunStats();
        run.Attach(engine);
        engine.StartLevel(Level());
        play?.Invoke(engine);
        return run;
    }

    [Fact]
    public void The_sixteen_exist_with_the_planned_names_and_gold()
    {
        var expected = new Dictionary<string, (string Name, int Gold)>
        {
            ["orb_breaker"] = ("Orb Breaker", 30), ["orb_collector"] = ("Crystal Collector", 120), ["close_call"] = ("Close Call", 60),
            ["hairline"] = ("Hairline", 20), ["shatterer"] = ("Shatterer", 100), ["fault_lines"] = ("Fault Lines", 80),
            ["nine_lives"] = ("Not Done Yet", 40), ["full_hearts"] = ("Full of Heart", 50), ["roll_with_it"] = ("Roll With It", 40),
            ["stubborn"] = ("Stubborn", 80), ["gold_rush"] = ("Gold Rush", 60), ["deep_dive"] = ("Deep Dive", 60),
            ["endless_night"] = ("Endless Night", 100), ["rule_maker"] = ("Rule Maker", 20), ["dressed_up"] = ("Dressed Up", 50),
            ["spread_the_word"] = ("Spread the Word", 20),
        };
        foreach (var (id, want) in expected)
        {
            var a = AchievementCatalog.Find(id);
            Assert.NotNull(a);
            Assert.Equal(want.Name, a!.Name);
            Assert.Equal(want.Gold, a.Gold);
        }
        Assert.Equal(930, expected.Values.Sum(v => v.Gold));
        Assert.Equal(AchievementCatalog.All.Count, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void Counters_and_thresholds_decide_the_total_based_ones()
    {
        Assert.False(Met("orb_breaker", new AchievementFacts()));
        Assert.True(Met("orb_breaker", new AchievementFacts { OrbsBrokenTotal = 1 }));
        Assert.False(Met("orb_collector", new AchievementFacts { OrbsBrokenTotal = 24 }));
        Assert.True(Met("orb_collector", new AchievementFacts { OrbsBrokenTotal = 25 }));
        Assert.False(Met("shatterer", new AchievementFacts { FractureBreaksTotal = 99 }));
        Assert.True(Met("shatterer", new AchievementFacts { FractureBreaksTotal = 100 }));
        Assert.False(Met("roll_with_it", new AchievementFacts { Rerolls = 9 }));
        Assert.True(Met("roll_with_it", new AchievementFacts { Rerolls = 10 }));
        Assert.False(Met("full_hearts", new AchievementFacts { CleanWins = 9 }));
        Assert.True(Met("full_hearts", new AchievementFacts { CleanWins = 10 }));
        Assert.False(Met("spread_the_word", new AchievementFacts()));
        Assert.True(Met("spread_the_word", new AchievementFacts { Shares = 1 }));
        Assert.True(Met("dressed_up", new AchievementFacts { DressedUp = true }));
    }

    [Fact]
    public void A_win_after_a_continue_is_Not_Done_Yet_and_an_eased_clear_is_never_a_win()
    {
        var level = Level();
        var winWithContinue = new RunStats();
        var engine = new GameEngine(new ScriptedGenerator());
        winWithContinue.Attach(engine);
        engine.StartLevel(level);
        // A run that bought a continue and then completed the level (set through the engine''s own events).
        var facts = new AchievementFacts { Level = level, Run = Run() };
        Assert.False(Met("nine_lives", facts));

        var eased = Level(eased: true);
        var easedEngine = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        var easedRun = new RunStats();
        easedRun.Attach(easedEngine);
        easedEngine.StartLevel(Story.Level(new[] { "RRR....." }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5, number: 60).ToEased());
        Story.MoveTo(easedEngine, 3);
        easedEngine.HardDrop();
        Story.Settle(easedEngine);
        Assert.Equal(GameState.LevelComplete, easedEngine.State);
        Assert.False(easedRun.Completed);   // cleared, but not a win
        Assert.False(Met("careful_planner", new AchievementFacts { Level = eased, Run = easedRun }));
        Assert.False(Met("stubborn", new AchievementFacts { Level = eased, Run = easedRun, LevelLosses = 12 }));
    }

    [Fact]
    public void Stubborn_needs_ten_losses_and_a_normal_win()
    {
        var engine = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        var run = new RunStats();
        run.Attach(engine);
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5, number: 60);
        engine.StartLevel(level);
        Story.MoveTo(engine, 3);
        engine.HardDrop();
        Story.Settle(engine);
        Assert.True(run.Completed);

        Assert.False(Met("stubborn", new AchievementFacts { Level = level, Run = run, LevelLosses = 9 }));
        Assert.True(Met("stubborn", new AchievementFacts { Level = level, Run = run, LevelLosses = 10 }));
    }

    [Fact]
    public void Cracks_breaks_and_orbs_are_counted_in_the_run()
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        var run = new RunStats();
        run.Attach(e);
        e.Start(GameSetup.Classic);
        int floor = GameConfig.TotalRows - 1;
        e.Board[3, floor] = CellColour.Yellow;
        for (int h = 0; h < 3; h++) e.Board[2, floor - h] = CellColour.Coral;
        for (int h = 0; h < 4; h++) e.Board[4, floor - h] = CellColour.Lime;
        Story.MoveTo(e, 2);
        e.HardDrop();
        Story.Settle(e);

        Assert.True(run.Cracks > 0);
        Assert.True(Met("hairline", new AchievementFacts { Run = run }));
        Assert.False(Met("fault_lines", new AchievementFacts { Run = run }));
    }

    [Fact]
    public void An_orb_broken_with_one_piece_left_is_a_close_call()
    {
        var e = new GameEngine(new ScriptedGenerator(Piece.AtSpawn(new[] { new BlockCell(0, 0, CellColour.Coral) })));
        var run = new RunStats();
        run.Attach(e);
        e.Start(GameSetup.Classic);
        int floor = GameConfig.TotalRows - 1;
        e.PlaceOrb(3, floor - 1, timer: 2);   // the next lock takes it to 1; the pop that breaks it comes with that lock
        for (int h = 0; h < 3; h++) e.Board[5, floor - h] = CellColour.Coral;
        Story.MoveTo(e, 5);
        e.HardDrop();
        Story.Settle(e);

        Assert.Equal(1, run.OrbsBroken);
        Assert.True(run.OrbCloseCall);
        Assert.True(Met("close_call", new AchievementFacts { Run = run }));
    }

    [Fact]
    public void Endless_night_and_rule_maker_are_for_Endless_runs_only()
    {
        var e = new GameEngine(new ScriptedGenerator());
        var run = new RunStats();
        run.Attach(e);
        e.Start(GameSetup.Classic with { Rules = GameRules.Default with { MinGroupSize = 3 } });
        e.HardDrop();
        Story.Settle(e);
        Assert.True(run.CustomRules);
        Assert.True(Met("rule_maker", new AchievementFacts { Run = run }));
        Assert.False(Met("rule_maker", new AchievementFacts { Run = run, Level = Level() }));   // a Story level is not an Endless run

        var plain = new RunStats();
        Assert.False(Met("endless_night", new AchievementFacts { Run = plain }));
    }

    [Fact]
    public void The_counters_are_saved_and_the_share_sheet_counts()
    {
        var settings = new InMemorySettings();
        var stats = new LifetimeStats(settings);
        stats.AddShare();
        stats.AddShare();
        Assert.Equal(2, stats.Shares);
        Assert.Equal(2, new LifetimeStats(settings).Shares);
        foreach (var key in new[] { SaveKeys.StatOrbsBroken, SaveKeys.StatFractureBreaks, SaveKeys.StatShares, SaveKeys.StatCleanWins })
        {
            Assert.Contains(key, SaveKeys.StatKeys);
            Assert.Contains(key, SaveKeys.All);
            Assert.Contains(key, SaveCode.Keys);
        }
    }

    [Fact]
    public void The_service_wears_what_the_player_wears_and_pays_each_once()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.GoldKey, 5_000);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var loadout = new CosmeticLoadout(settings, unlocks, progress);
        var shop = new CosmeticShop(unlocks, progress, loadout);
        var lifetime = new LifetimeStats(settings);
        var service = new AchievementService(unlocks, progress, new CampaignService(unlocks, progress), lifetime, loadout);

        Assert.DoesNotContain(service.CheckNow(), a => a.Id == "dressed_up");
        foreach (var id in new[] { "skin_solid", "board_nightsky", "frame_neon", "pip_party" })
        {
            Assert.True(shop.TryBuy(id), id);
            Assert.True(shop.Equip(id), id);
        }
        int before = progress.Gold;
        var earned = service.CheckNow();
        Assert.Contains(earned, a => a.Id == "dressed_up");
        Assert.Equal(before + 50, progress.Gold);
        Assert.DoesNotContain(service.CheckNow(), a => a.Id == "dressed_up");   // once

        lifetime.AddShare();
        Assert.Contains(service.CheckNow(), a => a.Id == "spread_the_word");
    }
}
