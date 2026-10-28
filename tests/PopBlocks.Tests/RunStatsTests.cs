using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class RunStatsTests
{
    private static readonly Goal[] Never = { new(GoalKind.Score, 999_999) };

    private static (GameEngine Engine, RunStats Stats) Watch(LevelDefinition level)
    {
        var engine = Story.Start(level);
        var stats = new RunStats();
        stats.Attach(engine);
        return (engine, stats);
    }

    private static void Drop(GameEngine e, int column)
    {
        Story.MoveTo(e, column);
        e.HardDrop();
        Story.Settle(e);
    }

    [Fact]
    public void A_chain_counts_every_wave_and_every_block_it_cleared()
    {
        var level = Story.Level(new[] { "...C....", ".RRRCCC." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.Chain, 2) }, moves: 3);
        var (e, stats) = Watch(level);
        stats.Attach(e);   // attaching again must not double count

        Drop(e, 0);

        Assert.Equal(GameState.LevelComplete, e.State);
        Assert.True(stats.Completed);
        Assert.Equal(2, stats.LongestChain);
        Assert.Equal(4, stats.LargestGroup);
        Assert.Equal(8, stats.MostClearedByOnePiece);
        Assert.Equal(8, stats.CellsPopped);
        Assert.Equal(2, stats.Waves);
        Assert.Equal(1, stats.PiecesPlaced);
        Assert.True(stats.BoardCleared);
        Assert.Equal(2, stats.MovesLeftAtFinish);
    }

    [Fact]
    public void The_most_cleared_by_one_piece_is_a_maximum_not_a_running_total()
    {
        var level = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:R", "Dot:C" }, Never, moves: 5);
        var (e, stats) = Watch(level);

        Drop(e, 3);
        Assert.Equal(4, stats.MostClearedByOnePiece);
        Drop(e, 3);

        Assert.Equal(4, stats.MostClearedByOnePiece);
        Assert.Equal(8, stats.CellsPopped);
        Assert.Equal(2, stats.PiecesPlaced);
        Assert.Equal(1, stats.LongestChain);
        Assert.False(stats.Completed);
    }

    [Fact]
    public void A_lost_level_is_recorded_as_failed()
    {
        var level = Story.Level(new[] { "RR......" }, new[] { "Dot:C" }, Never, moves: 1);
        var (e, stats) = Watch(level);
        Drop(e, 7);
        Assert.Equal(GameState.GameOver, e.State);
        Assert.True(stats.Failed);
        Assert.False(stats.Completed);
        Assert.Equal(0, stats.MovesLeftAtFinish);
    }

    [Fact]
    public void Moves_left_ignore_the_extra_moves_upgrade()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 4);
        var e = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 5 };
        e.StartLevel(level);
        var stats = new RunStats();
        stats.Attach(e);

        Drop(e, 3);

        Assert.Equal(GameState.LevelComplete, e.State);
        Assert.Equal(3, stats.MovesLeftAtFinish);
    }

    [Fact]
    public void The_colours_left_on_the_board_are_counted_at_the_finish()
    {
        var level = Story.Level(new[] { "...C....", "CC.RRR.Y" }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 3);
        var (e, stats) = Watch(level);
        Drop(e, 6);   // joins RRR and pops them; the C above them falls

        Assert.True(stats.Completed);
        Assert.False(stats.BoardCleared);
        Assert.Equal(3, stats.ColourCountsAtFinish[(int)CellColour.Cyan]);
        Assert.Equal(1, stats.ColourCountsAtFinish[(int)CellColour.Yellow]);
        Assert.Equal(0, stats.ColourCountsAtFinish[(int)CellColour.Coral]);
    }

    [Fact]
    public void A_restart_starts_the_run_over_and_detaching_stops_counting()
    {
        var level = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:R", "Dot:C" }, Never, moves: 5);
        var (e, stats) = Watch(level);
        Drop(e, 3);
        Assert.Equal(4, stats.CellsPopped);

        stats.Reset();
        Assert.Equal(0, stats.CellsPopped);
        Assert.Equal(0, stats.PiecesPlaced);

        stats.Detach();
        Drop(e, 3);
        Assert.Equal(0, stats.CellsPopped);
    }

    [Fact]
    public void Abilities_used_are_counted()
    {
        var stats = new RunStats();
        stats.RecordAbilityUsed();
        stats.RecordAbilityUsed();
        Assert.Equal(2, stats.AbilitiesUsed);
    }
}

public class LifetimeStatsTests
{
    private static RunStats Run(Action<GameEngine> play, LevelDefinition level)
    {
        var e = Story.Start(level);
        var stats = new RunStats();
        stats.Attach(e);
        play(e);
        return stats;
    }

    private static RunStats ChainRun() => Run(e => { Story.MoveTo(e, 0); e.HardDrop(); Story.Settle(e); },
        Story.Level(new[] { "...C....", ".RRRCCC." }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.Chain, 2) }, moves: 3));

    [Fact]
    public void A_new_save_starts_at_zero()
    {
        var life = new LifetimeStats(new InMemorySettings());
        Assert.Equal(0, life.LargestGroup);
        Assert.Equal(0, life.LongestChain);
        Assert.Equal(0, life.LevelsWon);
        Assert.Equal(0, life.BoardsCleared);
    }

    [Fact]
    public void A_won_run_raises_the_bests_and_the_counters()
    {
        var life = new LifetimeStats(new InMemorySettings());
        life.Add(ChainRun());

        Assert.Equal(4, life.LargestGroup);
        Assert.Equal(2, life.LongestChain);
        Assert.Equal(8, life.MostClearedByOnePiece);
        Assert.Equal(2, life.MostMovesLeft);
        Assert.Equal(8, life.CellsPopped);
        Assert.Equal(1, life.LevelsWon);
        Assert.Equal(1, life.BoardsCleared);
    }

    [Fact]
    public void Bests_never_fall_and_counters_keep_growing()
    {
        var life = new LifetimeStats(new InMemorySettings());
        life.Add(ChainRun());
        life.Add(new RunStats());   // an empty run

        Assert.Equal(2, life.LongestChain);
        Assert.Equal(8, life.CellsPopped);

        life.Add(ChainRun());
        Assert.Equal(16, life.CellsPopped);
        Assert.Equal(2, life.LevelsWon);
        Assert.Equal(2, life.LongestChain);
    }

    [Fact]
    public void A_lost_run_still_counts_its_bests_but_not_a_win()
    {
        var level = Story.Level(new[] { "RRR....." }, new[] { "Dot:R", "Dot:C" },
            new[] { new Goal(GoalKind.Score, 999_999) }, moves: 1);
        var run = Run(e => { Story.MoveTo(e, 3); e.HardDrop(); Story.Settle(e); }, level);
        Assert.True(run.Failed);

        var life = new LifetimeStats(new InMemorySettings());
        life.Add(run);
        Assert.Equal(4, life.LargestGroup);
        Assert.Equal(0, life.LevelsWon);
    }

    [Fact]
    public void Stats_survive_a_restart_and_use_their_own_keys()
    {
        var settings = new InMemorySettings();
        new LifetimeStats(settings).Add(ChainRun());

        var reopened = new LifetimeStats(settings);
        Assert.Equal(2, reopened.LongestChain);
        Assert.Equal(1, reopened.LevelsWon);

        foreach (var key in SaveKeys.StatKeys) Assert.Contains(key, SaveKeys.All);
        Assert.Equal(8 + 1 + 8 + 1, SaveKeys.StatKeys.Count);   // the eight originals, most-goals, one play counter per region, and rerolls (v1.1)
    }

    [Fact]
    public void Plays_in_a_region_and_most_goals_are_counted_when_a_level_is_given()
    {
        var settings = new InMemorySettings();
        var life = new LifetimeStats(settings);
        var level = LevelCatalog.Get(PopBlocks.Engine.Story.RegionCatalog.Get(3).FirstLevel + 2);

        life.Add(ChainRun(), level);
        life.Add(ChainRun(), level);

        Assert.Equal(2, life.PlaysInRegion(3));
        Assert.Equal(0, life.PlaysInRegion(4));
        Assert.Equal(level.Goals.Count, life.MostGoalsOnClearedLevel);

        var reopened = new LifetimeStats(settings);
        Assert.Equal(2, reopened.PlaysInRegion(3));
        Assert.Equal(level.Goals.Count, reopened.MostGoalsOnClearedLevel);
    }

    [Fact]
    public void A_run_with_no_level_given_adds_to_the_old_stats_only()
    {
        var life = new LifetimeStats(new InMemorySettings());
        life.Add(ChainRun());
        Assert.Equal(0, life.MostGoalsOnClearedLevel);
        for (int r = 1; r <= 8; r++) Assert.Equal(0, life.PlaysInRegion(r));
    }

    [Fact]
    public void Stats_can_be_cleared_for_testing()
    {
        var settings = new InMemorySettings();
        var life = new LifetimeStats(settings);
        life.Add(ChainRun());
        life.DevClear();

        Assert.Equal(0, life.LongestChain);
        Assert.Equal(0, new LifetimeStats(settings).LevelsWon);
    }

    [Fact]
    public void Raising_the_event_notifies_listeners()
    {
        var life = new LifetimeStats(new InMemorySettings());
        int calls = 0;
        life.Changed += () => calls++;
        life.Add(ChainRun());
        Assert.Equal(1, calls);
    }
}
