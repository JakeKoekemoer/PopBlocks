using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Optional memory objectives: where they appear, that they are always possible, and what finding one does.</summary>
public class BonusObjectiveTests
{
    private static IEnumerable<int> MemoryLevels() =>
        RegionCatalog.All.SelectMany(r => BonusPlanner.Offsets.Select(o => r.FirstLevel + o));

    // ---------------------------------------------------------------- where they are

    [Fact]
    public void Each_region_has_two_memory_levels_and_both_offer_an_objective()
    {
        foreach (var region in RegionCatalog.All)
        {
            var levels = BonusPlanner.Offsets.Select(o => LevelCatalog.Get(region.FirstLevel + o)).ToList();
            Assert.Equal(2, levels.Count);
            foreach (var level in levels)
            {
                Assert.NotNull(level.Bonus);
                Assert.False(level.IsTutorial);
                Assert.False(level.IsFinale);
                Assert.True(level.Bonus!.Describe().Length > 5);
            }
        }
    }

    [Fact]
    public void Only_memory_levels_carry_an_objective_and_its_memory_exists()
    {
        var memoryLevels = MemoryLevels().ToHashSet();
        for (int n = 1; n <= 260; n++)
        {
            var level = LevelCatalog.Get(n);
            if (!memoryLevels.Contains(n))
            {
                Assert.Null(level.Bonus);
                continue;
            }
            var memory = StoryContent.FindLore(level.Bonus!.MemoryId);
            Assert.NotNull(memory);
            Assert.Equal(LoreKind.Memory, memory!.Kind);
            Assert.Equal(RegionCatalog.For(n)!.Number, memory.Region);
        }
    }

    [Fact]
    public void Every_memory_is_reached_by_exactly_one_level()
    {
        var ids = MemoryLevels().Select(n => LevelCatalog.Get(n).Bonus!.MemoryId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var all = RegionCatalog.All.SelectMany(r => StoryContent.MemoriesOf(r.Number)).Select(m => m.Id).ToHashSet();
        Assert.True(all.SetEquals(ids));
    }

    [Fact]
    public void Memory_levels_are_never_a_goal_introduction_or_a_finale()
    {
        int[] intros = { 13, 16, 20, 26, 32, 128, 134, 161, 200 };
        foreach (int n in MemoryLevels())
        {
            Assert.DoesNotContain(n, intros);
            Assert.False(RegionRules.IsFinale(n));
        }
    }

    // ---------------------------------------------------------------- never impossible

    [Fact]
    public void Every_objective_is_met_by_the_bot_inside_the_move_limit_when_played_through_the_real_engine()
    {
        foreach (int n in MemoryLevels())
        {
            var level = LevelCatalog.Get(n);
            var bonus = level.Bonus!;
            string label = $"level {n} ({bonus.Describe()})";

            int cap = bonus.Kind == BonusKind.MovesLeft ? level.MoveLimit - bonus.Target : level.MoveLimit;
            var plan = bonus.Kind == BonusKind.MovesLeft ? LevelSolver.Solve(level, cap) : LevelSolver.Solve(level, cap, bonus);
            Assert.True(plan.Won, label + " cannot be met by the bot");

            var engine = new GameEngine(new ScriptedGenerator());
            engine.StartLevel(level);
            var run = new RunStats();
            run.Attach(engine);
            Assert.Equal(GameState.LevelComplete, Story.Replay(engine, plan));
            Assert.True(engine.PiecesPlaced <= level.MoveLimit, label);
            Assert.True(bonus.IsEarned(run), label + " was not earned in the real engine");
        }
    }

    [Fact]
    public void The_main_goals_are_still_winnable_without_the_objective()
    {
        foreach (int n in MemoryLevels())
        {
            var level = LevelCatalog.Get(n);
            Assert.True(LevelSolver.Solve(level, level.MoveLimit).Won, $"level {n}");
        }
    }

    [Fact]
    public void An_objective_is_asked_for_beyond_what_the_main_goals_already_need()
    {
        foreach (int n in MemoryLevels())
        {
            var level = LevelCatalog.Get(n);
            var bonus = level.Bonus!;
            if (bonus.Kind == BonusKind.Chain)
                Assert.True(bonus.Target > level.Goals.Where(g => g.Kind == GoalKind.Chain).Select(g => g.Target).DefaultIfEmpty(0).Max());
            if (bonus.Kind == BonusKind.BigGroup)
                Assert.True(bonus.Target > level.Goals.Where(g => g.Kind == GoalKind.BigGroup).Select(g => g.Target).DefaultIfEmpty(0).Max());
            if (bonus.Kind == BonusKind.MovesLeft)
                Assert.True(level.MoveLimit - bonus.Target >= 1);
        }
    }

    // ---------------------------------------------------------------- the objectives themselves

    private static (GameEngine Engine, RunStats Run) Play(LevelDefinition level, params int[] columns)
    {
        var e = Story.Start(level);
        var run = new RunStats();
        run.Attach(e);
        foreach (int c in columns)
        {
            Story.MoveTo(e, c);
            e.HardDrop();
            Story.Settle(e);
        }
        return (e, run);
    }

    [Fact]
    public void A_chain_objective_needs_the_chain_and_a_win()
    {
        var bonus = new BonusObjective(BonusKind.Chain, 2, CellColour.Empty, "mem_r1_1");
        var level = Story.Level(new[] { "...C....", ".RRRCCC." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopBlocks, 8) }, moves: 3) with { Bonus = bonus };
        var (_, run) = Play(level, 0);
        Assert.True(bonus.IsEarned(run));

        var tooBig = bonus with { Target = 3 };
        Assert.False(tooBig.IsEarned(run));
    }

    [Fact]
    public void An_objective_met_in_a_lost_level_does_not_count()
    {
        var bonus = new BonusObjective(BonusKind.BigGroup, 4, CellColour.Empty, "mem_r1_1");
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R", "Dot:C" },
            new[] { new Goal(GoalKind.Score, 999_999) }, moves: 1) with { Bonus = bonus };
        var (e, run) = Play(level, 3);
        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(4, run.LargestGroup);
        Assert.False(bonus.IsEarned(run));
    }

    [Fact]
    public void A_clear_the_colour_objective_looks_at_the_finish()
    {
        var level = Story.Level(new[] { "RRR.C..." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 3);

        var (_, withCyan) = Play(level with { Bonus = new BonusObjective(BonusKind.ClearColour, 0, CellColour.Cyan, "m") }, 3);
        Assert.True(withCyan.Completed);
        Assert.False(new BonusObjective(BonusKind.ClearColour, 0, CellColour.Cyan, "m").IsEarned(withCyan));
        Assert.True(new BonusObjective(BonusKind.ClearColour, 0, CellColour.Coral, "m").IsEarned(withCyan));
    }

    [Fact]
    public void A_spare_moves_objective_counts_against_the_levels_own_limit()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5);
        var e = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 6 };
        e.StartLevel(level);
        var run = new RunStats();
        run.Attach(e);
        Story.MoveTo(e, 3); e.HardDrop(); Story.Settle(e);

        Assert.True(new BonusObjective(BonusKind.MovesLeft, 4, CellColour.Empty, "m").IsEarned(run));
        Assert.False(new BonusObjective(BonusKind.MovesLeft, 5, CellColour.Empty, "m").IsEarned(run));
    }

    [Fact]
    public void The_hud_state_follows_the_run()
    {
        var bonus = new BonusObjective(BonusKind.Chain, 3, CellColour.Empty, "m");
        var level = Story.Level(new[] { "...C....", ".RRRCCC." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.Score, 999_999) }, moves: 3) with { Bonus = bonus };
        var (e, run) = Play(level);
        Assert.False(bonus.Evaluate(e, run).Met);
        Assert.Equal("x0 / x3", bonus.Evaluate(e, run).Progress);

        Story.MoveTo(e, 0); e.HardDrop(); Story.Settle(e);
        var state = bonus.Evaluate(e, run);
        Assert.False(state.Met);
        Assert.Equal("x2 / x3", state.Progress);
    }

    [Fact]
    public void Every_kind_describes_itself()
    {
        Assert.Equal("Make a x3 chain", new BonusObjective(BonusKind.Chain, 3, CellColour.Empty, "m").Describe());
        Assert.Equal("Pop a group of 8", new BonusObjective(BonusKind.BigGroup, 8, CellColour.Empty, "m").Describe());
        Assert.Equal("Finish with no red blocks left", new BonusObjective(BonusKind.ClearColour, 0, CellColour.Coral, "m").Describe());
        Assert.Equal("Finish with 3 moves to spare", new BonusObjective(BonusKind.MovesLeft, 3, CellColour.Empty, "m").Describe());
        Assert.Equal("Finish with a move to spare", new BonusObjective(BonusKind.MovesLeft, 1, CellColour.Empty, "m").Describe());
    }

    // ---------------------------------------------------------------- finding a memory

    private static (CampaignService Campaign, UnlockStore Unlocks, PlayerProgress Progress) Campaign()
    {
        var settings = new InMemorySettings();
        var unlocks = new UnlockStore(settings);
        var progress = new PlayerProgress(settings);
        return (new CampaignService(unlocks, progress), unlocks, progress);
    }

    private static RunStats WonRun(LevelDefinition level)
    {
        var bonus = level.Bonus!;
        int cap = bonus.Kind == BonusKind.MovesLeft ? level.MoveLimit - bonus.Target : level.MoveLimit;
        var plan = bonus.Kind == BonusKind.MovesLeft ? LevelSolver.Solve(level, cap) : LevelSolver.Solve(level, cap, bonus);
        var engine = new GameEngine(new ScriptedGenerator());
        engine.StartLevel(level);
        var run = new RunStats();
        run.Attach(engine);
        Story.Replay(engine, plan);
        return run;
    }

    [Fact]
    public void Earning_the_objective_adds_the_memory_to_the_archive_once()
    {
        var (campaign, _, _) = Campaign();
        var level = LevelCatalog.Get(RegionCatalog.Get(2).FirstLevel + BonusPlanner.Offsets[0]);
        var run = WonRun(level);

        Assert.DoesNotContain(campaign.UnlockedLore(), l => l.Id == level.Bonus!.MemoryId);

        var found = campaign.OnBonusEarned(level, run);
        Assert.Equal(level.Bonus!.MemoryId, Assert.Single(found).Id);
        Assert.Contains(campaign.UnlockedLore(), l => l.Id == level.Bonus.MemoryId);
        Assert.Equal(1, campaign.MemoriesFound(RegionCatalog.Get(2)));

        Assert.Empty(campaign.OnBonusEarned(level, run));   // a replay finds nothing new
        Assert.Equal(1, campaign.MemoriesFound(RegionCatalog.Get(2)));
    }

    [Fact]
    public void A_missed_objective_or_a_level_without_one_adds_nothing()
    {
        var (campaign, _, _) = Campaign();
        var level = LevelCatalog.Get(RegionCatalog.Get(3).FirstLevel + BonusPlanner.Offsets[1]);
        Assert.Empty(campaign.OnBonusEarned(level, new RunStats()));
        Assert.Empty(campaign.OnBonusEarned(LevelCatalog.Get(14), WonRun(LevelCatalog.Get(12))));
        Assert.Equal(0, campaign.TotalMemoriesFound);
    }

    [Fact]
    public void A_memory_pays_no_gold_and_does_not_move_progress()
    {
        var (campaign, _, progress) = Campaign();
        var level = LevelCatalog.Get(12);
        campaign.OnBonusEarned(level, WonRun(level));
        Assert.Equal(0, progress.Gold);
        Assert.Equal(0, progress.HighestCleared);
    }

    [Fact]
    public void A_memory_in_every_region_opens_the_epilogue()
    {
        var (campaign, _, _) = Campaign();
        var epilogue = StoryContent.FindScene(StoryContent.EpilogueSceneId)!;

        foreach (var region in RegionCatalog.All)
        {
            Assert.False(campaign.EpilogueUnlocked);
            Assert.False(campaign.IsSceneAvailable(epilogue));
            var level = LevelCatalog.Get(region.FirstLevel + BonusPlanner.Offsets[0]);
            var found = campaign.OnBonusEarned(level, WonRun(level));

            if (region.Number < 8) Assert.Single(found);
            else
            {
                Assert.Equal(2, found.Count);
                Assert.Contains(found, l => l.Id == StoryContent.EpilogueJournalId);
            }
        }

        Assert.True(campaign.EpilogueUnlocked);
        Assert.True(campaign.IsSceneAvailable(epilogue));
        Assert.Contains(campaign.UnlockedLore(), l => l.Id == StoryContent.EpilogueJournalId);
    }

    [Fact]
    public void The_epilogue_stays_locked_while_a_region_has_no_memory()
    {
        var (campaign, _, _) = Campaign();
        foreach (var region in RegionCatalog.All.Where(r => r.Number != 5))
        {
            var level = LevelCatalog.Get(region.FirstLevel + BonusPlanner.Offsets[1]);
            campaign.OnBonusEarned(level, WonRun(level));
        }
        Assert.False(campaign.EpilogueUnlocked);
        Assert.DoesNotContain(campaign.UnlockedLore(), l => l.Id == StoryContent.EpilogueJournalId);
        Assert.Equal(7, campaign.TotalMemoriesFound);
        Assert.Equal(16, campaign.TotalMemories);
    }

    [Fact]
    public void Finding_memories_does_not_change_whether_the_ending_can_be_reached()
    {
        // The campaign path needs only main goals: every level 1-200 is bot-winnable with no objective at all.
        for (int n = 11; n <= 200; n += 7)
            Assert.True(LevelSolver.Solve(LevelCatalog.Get(n), LevelCatalog.Get(n).MoveLimit).Won, $"level {n}");
    }
}
