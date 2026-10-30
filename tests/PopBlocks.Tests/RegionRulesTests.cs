using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Each region plays like its theme, finales are tougher and clean, and the chain-set goal counts what it says.</summary>
public class RegionRulesTests
{
    private static IEnumerable<LevelDefinition> LevelsOf(Region r) =>
        Enumerable.Range(r.FirstLevel, r.LevelCount).Where(n => n > StoryConfig.TutorialLastLevel + 1).Select(LevelCatalog.Get);

    private static (int Of, int Total) Count(IEnumerable<LevelDefinition> levels, GoalKind kind)
    {
        int of = 0, total = 0;
        foreach (var level in levels)
            foreach (var g in level.Goals) { total++; if (g.Kind == kind) of++; }
        return (of, total);
    }

    // ---------------------------------------------------------------- the table

    [Fact]
    public void Every_region_has_a_rule_and_a_theme()
    {
        Assert.Equal(RegionCatalog.All.Count, RegionRules.All.Count);
        for (int i = 0; i < RegionRules.All.Count; i++)
        {
            Assert.Equal(i + 1, RegionRules.All[i].Region);
            Assert.True(RegionRules.All[i].Theme.Length > 5);
            if (i > 0) Assert.True(RegionRules.All[i].IntroText.Length > 20);
        }
    }

    [Fact]
    public void Levels_above_the_campaign_play_like_the_last_region()
    {
        Assert.Equal(8, RegionRules.For(201).Region);
        Assert.Equal(8, RegionRules.For(999).Region);
        Assert.Equal(1, RegionRules.For(1).Region);
    }

    [Fact]
    public void A_favoured_goal_kind_is_clearly_more_common_in_its_own_region()
    {
        foreach (var rule in RegionRules.All.Where(r => r.Favoured is not null))
        {
            var kind = rule.Favoured!.Value;
            var own = Count(LevelsOf(RegionCatalog.Get(rule.Region)), kind);

            // Everywhere else it can appear (from its introduction onwards).
            var others = RegionCatalog.All.Where(r => r.Number != rule.Region)
                .SelectMany(r => LevelsOf(r)).Where(l => l.Number >= 26);
            var rest = Count(others, kind);

            double ownShare = own.Of / (double)own.Total;
            double restShare = rest.Of / (double)rest.Total;
            Assert.True(ownShare >= restShare * 1.5,
                $"region {rule.Region} ({kind}): {ownShare:P0} here against {restShare:P0} elsewhere");
        }
    }

    [Fact]
    public void Goal_kinds_appear_no_earlier_than_their_introduction()
    {
        for (int n = 12; n <= 220; n++)
            foreach (var g in LevelCatalog.Get(n).Goals)
            {
                if (g.Kind == GoalKind.PopColour) Assert.True(n >= RegionRules.PopColourIntro);
                if (g.Kind == GoalKind.ClearDown) Assert.True(n >= RegionRules.ClearDownIntro);
                if (g.Kind == GoalKind.Chain) Assert.True(n >= RegionRules.ChainIntro);
                if (g.Kind == GoalKind.BigGroup) Assert.True(n >= RegionRules.BigGroupIntro);
                if (g.Kind == GoalKind.ChainSet) Assert.Equal(RegionRules.FinalLevel, n);
            }
        Assert.Equal(GoalKind.PopColour, LevelCatalog.Get(RegionRules.PopColourIntro).Goals[0].Kind);
        Assert.Equal(GoalKind.ClearDown, LevelCatalog.Get(RegionRules.ClearDownIntro).Goals[0].Kind);
        Assert.Equal(GoalKind.Chain, LevelCatalog.Get(RegionRules.ChainIntro).Goals[0].Kind);
        Assert.Equal(GoalKind.BigGroup, LevelCatalog.Get(RegionRules.BigGroupIntro).Goals[0].Kind);
    }

    [Fact]
    public void The_citadel_asks_for_several_goals_at_once()
    {
        for (int n = 151; n <= 174; n++)
            Assert.True(LevelCatalog.Get(n).Goals.Count >= 2, $"level {n}");
        Assert.Contains(Enumerable.Range(161, 14), n => LevelCatalog.Get(n).Goals.Count == 3);
        Assert.Equal(3, LevelCatalog.Get(RegionRules.TripleGoalIntro).Goals.Count);
        Assert.Equal(2, LevelCatalog.Get(RegionRules.DualGoalIntro).Goals.Count);
    }

    [Fact]
    public void The_city_has_tight_move_levels_and_the_wastes_have_scattered_piles()
    {
        Assert.True(Enumerable.Range(80, 20).Count(n => RegionRules.For(n).VariantAt(n) == 2) >= 8);
        Assert.True(Enumerable.Range(105, 20).Count(n => RegionRules.For(n).VariantAt(n) == 4) >= 6);
        Assert.Equal(-1, RegionRules.For(30).VariantAt(30));
        Assert.Equal(0, RegionRules.For(41).VariantAt(41));
    }

    // ---------------------------------------------------------------- flips and penalties

    [Fact]
    public void Flipped_boards_and_pop_penalties_begin_in_the_frozen_expanse()
    {
        for (int n = 1; n < 126; n++)
        {
            Assert.False(LevelCatalog.Get(n).FlipView, $"level {n}");
            Assert.Equal(0, LevelCatalog.Get(n).NoPopPenalty);
        }

        var expanse = Enumerable.Range(126, 25).Select(LevelCatalog.Get).ToList();
        Assert.Equal(128, expanse.First(l => l.FlipView).Number);
        Assert.Equal(134, expanse.First(l => l.NoPopPenalty > 0).Number);
        Assert.True(expanse.Count(l => l.FlipView) >= 2);
        Assert.True(expanse.Count(l => l.NoPopPenalty > 0) >= 2);

        // A penalty level always has a score goal, and the card for each new rule exists.
        foreach (var l in expanse.Where(l => l.NoPopPenalty > 0))
            Assert.Contains(l.Goals, g => g.Kind == GoalKind.Score);
        Assert.True(LevelCatalog.Get(128).Steps.Count > 0);
        Assert.True(LevelCatalog.Get(134).Steps.Count > 0);

        // Later regions keep the pattern; the endless band does too.
        Assert.True(LevelCatalog.Get(151).FlipView);
        Assert.Contains(Enumerable.Range(151, 60), n => LevelCatalog.Get(n).NoPopPenalty > 0);
        Assert.Contains(Enumerable.Range(151, 60), n => !LevelCatalog.Get(n).FlipView);
    }

    // ---------------------------------------------------------------- finales

    [Fact]
    public void The_last_level_of_every_region_is_a_finale_and_nothing_else_is()
    {
        for (int n = 1; n <= 260; n++)
        {
            bool expected = n <= 200 && n % 25 == 0;
            Assert.Equal(expected, RegionRules.IsFinale(n));
            if (n > StoryConfig.TutorialLastLevel)
            {
                Assert.Equal(expected, LevelCatalog.Get(n).IsFinale);
                Assert.Equal(!expected, LevelCatalog.Get(n).AllowAbilities);
            }
        }
    }

    [Fact]
    public void Finales_are_clean_tougher_tests_with_a_story_tip()
    {
        foreach (var region in RegionCatalog.All)
        {
            var level = LevelCatalog.Get(region.LastLevel);
            string label = $"finale {region.LastLevel}";
            Assert.False(level.FlipView, label);
            Assert.Equal(0, level.NoPopPenalty);
            Assert.False(string.IsNullOrWhiteSpace(level.Tip), label);
            if (region.LastLevel >= RegionRules.DualGoalIntro && region.LastLevel != RegionRules.FinalLevel)
                Assert.True(level.Goals.Count >= 2, label);
        }
    }

    [Fact]
    public void Level_200_is_the_final_puzzle_and_uses_the_chain_set_goal()
    {
        var level = LevelCatalog.Get(RegionRules.FinalLevel);
        Assert.True(level.IsFinale);
        var goal = Assert.Single(level.Goals);
        Assert.Equal(GoalKind.ChainSet, goal.Kind);
        Assert.True(goal.Target >= 2);
        Assert.True(goal.MinWaves >= 2);
        Assert.Equal("Do not force every colour into one perfect shape. Let each connection support the next.", level.Tip);
        Assert.True(level.Steps.Count > 0);
    }

    [Fact]
    public void Level_200_is_winnable_with_no_upgrades_and_the_engine_agrees_with_the_bot()
    {
        var level = LevelCatalog.Get(RegionRules.FinalLevel);
        var plan = LevelSolver.Solve(level, level.MoveLimit);
        Assert.True(plan.Won);

        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(level);
        Assert.Equal(GameState.LevelComplete, Story.Replay(e, plan));
        Assert.True(GoalTracker.AllComplete(e.Goals));
        Assert.Equal(level.Goals[0].Target, e.Goals[0].Current >= level.Goals[0].Target ? level.Goals[0].Target : -1);
    }

    [Fact]
    public void Cards_open_every_region_after_the_first()
    {
        foreach (var region in RegionCatalog.All.Skip(1))
        {
            var cards = LevelCatalog.Get(region.FirstLevel).Steps;
            Assert.True(cards.Count > 0, region.Title);
            Assert.Equal(region.Title, cards[0].Title);
        }
        foreach (int n in new[] { 13, 16, 20, 26, 32, 51, 101, 128, 134, 151, 161, 200 })
            Assert.True(LevelCatalog.Get(n).Steps.Count > 0, $"level {n}");
    }
}

/// <summary>The chain-set goal ("make N separate chains of at least M waves") and how it counts.</summary>
public class ChainSetGoalTests
{
    private static BurstWave Wave(int index) => new(index, index, 0,
        Array.Empty<ColourGroup>(), Array.Empty<BlockCell>(), Array.Empty<CellMove>());

    private static GoalTracker Tracker(params int[] chains)
    {
        var tracker = new GoalTracker(new[] { new Goal(GoalKind.ChainSet, 3, CellColour.Empty, 3) }, 10);
        foreach (int length in chains)
            for (int i = 1; i <= length; i++) tracker.OnWave(Wave(i));
        return tracker;
    }

    [Fact]
    public void A_new_first_wave_closes_the_chain_before_it()
    {
        var t = Tracker(3, 2, 3);
        Assert.Equal(2, t.ChainsOfAtLeast(3));
        Assert.Equal(3, t.ChainsOfAtLeast(2));
        Assert.Equal(3, t.ChainsOfAtLeast(1));
    }

    [Fact]
    public void The_chain_still_open_counts_once_it_is_long_enough()
    {
        Assert.Equal(1, Tracker(3).ChainsOfAtLeast(3));
        Assert.Equal(0, Tracker(2).ChainsOfAtLeast(3));
        Assert.Equal(0, Tracker().ChainsOfAtLeast(1));
    }

    [Fact]
    public void One_long_chain_is_not_several_chains()
    {
        var state = Tracker(7).Evaluate(0, 10)[0];
        Assert.Equal(1, state.Current);
        Assert.False(state.IsComplete);
    }

    [Fact]
    public void The_goal_completes_after_enough_separate_chains()
    {
        var t = Tracker(3, 4);
        Assert.False(t.Evaluate(0, 10)[0].IsComplete);
        t.OnWave(Wave(1)); t.OnWave(Wave(2)); t.OnWave(Wave(3));
        var state = t.Evaluate(0, 10)[0];
        Assert.True(state.IsComplete);
        Assert.Equal(3, state.Current);
    }

    [Fact]
    public void The_goal_reads_clearly()
    {
        var goal = new Goal(GoalKind.ChainSet, 3, CellColour.Empty, 3);
        Assert.Equal("Make 3 separate chains of x3 or more", goal.Describe());
        var state = Tracker(3, 3).Evaluate(0, 10)[0];
        Assert.Equal("2 / 3 chains", state.ProgressText);
        Assert.Equal(2 / 3.0, state.Fraction, 3);
    }

    [Fact]
    public void A_restart_forgets_earlier_chains()
    {
        var e = Story.Start(Story.Level(new[] { "...C....", ".RRRCCC." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.ChainSet, 1, CellColour.Empty, 2) }, moves: 3));
        Story.MoveTo(e, 0);
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(GameState.LevelComplete, e.State);

        e.Restart();
        Assert.Equal(0, e.Goals[0].Current);
    }
}
