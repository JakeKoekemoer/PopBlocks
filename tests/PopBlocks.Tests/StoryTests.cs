using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Rules of Story mode: move limits, goals, penalties, pop pacing, previews and events.</summary>
public class StoryRuleTests
{
    private static readonly Goal[] Never = { new(GoalKind.Score, 999_999) };

    private static string[] Column(int x, params char[] colours) =>
        colours.Select(c => new string('.', x) + c + new string('.', GameConfig.Columns - x - 1)).ToArray();

    // ---------------------------------------------------------------- losing

    [Fact]
    public void Running_out_of_moves_fails_the_level()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C", "Dot:R", "Dot:Y" }, Never, moves: 2));

        Story.MoveTo(e, 0); e.HardDrop();
        Assert.Equal(GameState.Playing, e.State);
        Assert.Equal(1, e.MovesLeft);

        Story.MoveTo(e, 5); e.HardDrop();
        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.OutOfMoves, e.FailReason);
        Assert.Equal(0, e.MovesLeft);
    }

    [Fact]
    public void Clearing_the_goal_with_the_last_move_wins_instead_of_failing()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 1));

        Story.MoveTo(e, 3);
        e.HardDrop();

        Assert.Equal(GameState.LevelComplete, e.State);
        Assert.Equal(FailReason.None, e.FailReason);
    }

    [Fact]
    public void A_stack_reaching_the_spawn_rows_fails_the_level()
    {
        var tower = Column(3, 'C', 'R', 'C', 'R', 'C', 'R', 'C', 'R', 'C', 'R', 'C', 'R', 'C', 'R', 'C', 'R');
        var e = Story.Start(Story.Level(tower, new[] { "Dot:Y" }, Never));

        Assert.Equal(GameState.Playing, e.State);
        e.HardDrop();

        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.StackOverflow, e.FailReason);
    }

    [Fact]
    public void A_level_without_a_move_limit_never_runs_out()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C" }, Never, moves: 0));
        Assert.False(e.HasMoveLimit);
        Assert.Equal(0, e.MovesLeft);
    }

    [Fact]
    public void Finished_levels_ignore_input_and_ticks()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 1));
        Story.MoveTo(e, 3);
        e.HardDrop();

        Assert.False(e.MoveLeft());
        Assert.False(e.HardDrop());
        Assert.False(e.Tick());
        Assert.Equal(GameState.LevelComplete, e.State);
    }

    // ---------------------------------------------------------------- goals

    [Fact]
    public void Colour_goal_counts_only_that_colour()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C", "Dot:R" },
            new[] { new Goal(GoalKind.PopColour, 4, CellColour.Cyan) }));
        Story.MoveTo(e, 3);
        e.HardDrop();
        Assert.Equal(GameState.LevelComplete, e.State);
    }

    [Fact]
    public void Colour_goal_is_not_met_by_other_colours()
    {
        var e = Story.Start(Story.Level(new[] { "RRR....." }, new[] { "Dot:R" },
            new[] { new Goal(GoalKind.PopColour, 4, CellColour.Cyan) }));
        Story.MoveTo(e, 3);
        e.HardDrop();
        Assert.NotEqual(GameState.LevelComplete, e.State);
        Assert.Equal(0, e.Goals[0].Current);   // nothing cyan popped
    }

    [Fact]
    public void Score_goal_is_checked_against_the_score()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.Score, 5_000) }));
        Story.MoveTo(e, 3);
        e.HardDrop();
        Assert.NotEqual(GameState.LevelComplete, e.State);
        Assert.True(e.Goals[0].Fraction > 0 && e.Goals[0].Fraction < 1);
    }

    [Fact]
    public void Goal_tracker_handles_score_and_clear_down_without_waves()
    {
        var tracker = new GoalTracker(new[] { new Goal(GoalKind.Score, 100), new Goal(GoalKind.ClearDown, 3) }, 11);

        var low = tracker.Evaluate(60, 8);
        Assert.False(low[0].IsComplete);
        Assert.False(low[1].IsComplete);
        Assert.False(GoalTracker.AllComplete(low));

        var done = tracker.Evaluate(100, 3);
        Assert.True(done[0].IsComplete);
        Assert.True(done[1].IsComplete);
        Assert.True(GoalTracker.AllComplete(done));
        Assert.Equal(1.0, done[1].Fraction);
        Assert.Equal("3 left", done[1].ProgressText);
    }

    [Fact]
    public void Clear_down_progress_runs_from_the_start_count_to_the_target()
    {
        var tracker = new GoalTracker(new[] { new Goal(GoalKind.ClearDown, 0) }, 10);
        Assert.Equal(0.0, tracker.Evaluate(0, 10)[0].Fraction);
        Assert.Equal(0.5, tracker.Evaluate(0, 5)[0].Fraction);
        Assert.Equal(1.0, tracker.Evaluate(0, 0)[0].Fraction);
    }

    [Fact]
    public void No_goals_means_the_level_is_never_complete()
    {
        Assert.False(GoalTracker.AllComplete(new GoalTracker(Array.Empty<Goal>(), 0).Evaluate(0, 0)));
    }

    [Fact]
    public void Goal_text_reads_naturally()
    {
        Assert.Equal("Clear the board", new Goal(GoalKind.ClearDown, 0).Describe());
        Assert.Equal("Leave 5 blocks or fewer", new Goal(GoalKind.ClearDown, 5).Describe());
        Assert.Equal("Pop 12 blocks", new Goal(GoalKind.PopBlocks, 12).Describe());
        Assert.Equal("Pop 9 green blocks", new Goal(GoalKind.PopColour, 9, CellColour.Lime).Describe());
        Assert.Equal("Make a x3 chain", new Goal(GoalKind.Chain, 3).Describe());
    }

    // ---------------------------------------------------------------- penalty

    [Fact]
    public void A_piece_that_pops_nothing_costs_points_when_the_level_says_so()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C", "Dot:R" }, Never, penalty: 15));
        var events = new List<GameEvent>();
        e.EventRaised += events.Add;

        Story.MoveTo(e, 0);
        e.HardDrop();                         // earns hard-drop points, pops nothing
        int expected = e.LastPenalty;

        Assert.Equal(15, expected);
        Assert.Contains(GameEvent.PenaltyApplied, events);
    }

    [Fact]
    public void The_penalty_never_pushes_the_score_below_zero()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C" }, Never, penalty: 100_000));
        Story.MoveTo(e, 0);
        e.HardDrop();
        Assert.Equal(0, e.Score);
    }

    [Fact]
    public void A_piece_that_pops_something_is_not_penalised()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" }, Never, penalty: 50));
        Story.MoveTo(e, 3);
        e.HardDrop();
        Assert.Equal(0, e.LastPenalty);
    }

    [Fact]
    public void Levels_without_a_penalty_never_take_points()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C" }, Never, penalty: 0));
        Story.MoveTo(e, 0);
        e.HardDrop();
        Assert.Equal(0, e.LastPenalty);
        Assert.True(e.Score > 0);
    }

    // ---------------------------------------------------------------- pop pacing

    [Fact]
    public void Timed_pops_highlight_first_then_pop_then_settle()
    {
        var e = new GameEngine(new ScriptedGenerator()) { Timing = new PopTiming(300, 200) };
        e.StartLevel(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }));
        Story.MoveTo(e, 3);
        e.HardDrop();

        // Highlight: the match is known, the board has not changed yet.
        Assert.Equal(GameState.ResolvingBursts, e.State);
        Assert.True(e.IsHighlighting);
        Assert.Single(e.PendingGroups);
        Assert.Equal(4, e.PendingGroups[0].Size);
        Assert.Equal(4, e.Board.CountOccupied());
        Assert.Equal(300, e.TimerIntervalMs);

        // Pop: the blocks go.
        e.Tick();
        Assert.False(e.IsHighlighting);
        Assert.Empty(e.PendingGroups);
        Assert.Equal(0, e.Board.CountOccupied());
        Assert.Equal(GameState.ResolvingBursts, e.State);
        Assert.Equal(200, e.TimerIntervalMs);

        // Settle: nothing else to pop, so the level is complete.
        e.Tick();
        Assert.Equal(GameState.LevelComplete, e.State);
    }

    [Fact]
    public void Timed_chains_highlight_every_wave()
    {
        var e = new GameEngine(new ScriptedGenerator()) { Timing = new PopTiming(100, 100) };
        e.StartLevel(LevelCatalog.Lesson(3));
        Story.MoveTo(e, 0);
        e.HardDrop();

        int highlights = 0;
        for (int i = 0; i < 20 && e.State == GameState.ResolvingBursts; i++)
        {
            if (e.IsHighlighting) highlights++;
            e.Tick();
        }
        Assert.Equal(2, highlights);
        Assert.Equal(GameState.LevelComplete, e.State);
    }

    [Fact]
    public void Instant_timing_pops_at_once()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }));
        Story.MoveTo(e, 3);
        e.HardDrop();
        Assert.Equal(0, e.Board.CountOccupied());
        Assert.False(e.IsHighlighting);
    }

    [Fact]
    public void Pausing_during_a_highlight_keeps_the_pending_groups()
    {
        var e = new GameEngine(new ScriptedGenerator()) { Timing = new PopTiming(300, 200) };
        e.StartLevel(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" }, Never));
        Story.MoveTo(e, 3);
        e.HardDrop();

        e.Pause();
        Assert.Equal(GameState.Paused, e.State);
        Assert.False(e.Tick());
        e.Resume();
        Assert.True(e.IsHighlighting);
        Assert.Single(e.PendingGroups);
    }

    // ---------------------------------------------------------------- previews and shop bonuses

    [Fact]
    public void The_preview_queue_has_the_requested_length_and_stays_full()
    {
        var e = new GameEngine(new ScriptedGenerator()) { PreviewCount = 3 };
        e.StartLevel(Story.Level(new string[0], new[] { "Dot:C", "Dot:R", "Dot:Y", "Dot:P", "Dot:L" }, Never));

        Assert.Equal(3, e.Upcoming.Count);
        Assert.Equal(CellColour.Coral, e.Upcoming[0].Cells[0].Colour);
        Assert.Equal(CellColour.Yellow, e.Upcoming[1].Cells[0].Colour);
        Assert.Equal(CellColour.Purple, e.Upcoming[2].Cells[0].Colour);

        Story.MoveTo(e, 0);
        e.HardDrop();

        Assert.Equal(3, e.Upcoming.Count);
        Assert.Equal(CellColour.Coral, e.CurrentPiece!.Cells[0].Colour);
        Assert.Same(e.Upcoming[0], e.NextPiece);
    }

    [Fact]
    public void Preview_count_is_clamped()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.PreviewCount = 99;
        Assert.Equal(5, e.PreviewCount);
        e.PreviewCount = -3;
        Assert.Equal(1, e.PreviewCount);
    }

    [Fact]
    public void Extra_moves_apply_to_normal_levels_but_not_tutorials()
    {
        var normal = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 4 };
        normal.StartLevel(Story.Level(new string[0], new[] { "Dot:C" }, Never, moves: 3));
        Assert.Equal(7, normal.MoveLimit);

        var tutorial = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 4 };
        tutorial.StartLevel(Story.Level(new string[0], new[] { "Dot:C" }, Never, moves: 3, tutorial: true));
        Assert.Equal(3, tutorial.MoveLimit);
    }

    [Fact]
    public void Extra_moves_do_not_turn_an_unlimited_level_into_a_limited_one()
    {
        var e = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 4 };
        e.StartLevel(Story.Level(new string[0], new[] { "Dot:C" }, Never, moves: 0));
        Assert.False(e.HasMoveLimit);
    }

    // ---------------------------------------------------------------- events and restart

    [Fact]
    public void Events_are_raised_in_the_order_they_happen()
    {
        var e = Story.Start(Story.Level(new[] { "CCC....." }, new[] { "Dot:C" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }));
        var events = new List<GameEvent>();
        e.EventRaised += events.Add;

        e.MoveRight();
        e.MoveLeft();
        e.Rotate();
        e.HardDrop();

        Assert.Equal(new[]
        {
            GameEvent.Moved, GameEvent.Moved, GameEvent.Rotated, GameEvent.HardDropped, GameEvent.PieceLocked,
            GameEvent.Popped, GameEvent.LevelComplete,
        }, events.ToArray());
    }

    [Fact]
    public void Soft_dropping_raises_its_own_event()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C" }, Never));
        var events = new List<GameEvent>();
        e.EventRaised += events.Add;
        e.SoftDrop();
        Assert.Contains(GameEvent.SoftDropped, events);
    }

    [Fact]
    public void Failing_raises_level_failed()
    {
        var e = Story.Start(Story.Level(new string[0], new[] { "Dot:C" }, Never, moves: 1));
        var events = new List<GameEvent>();
        e.EventRaised += events.Add;
        e.HardDrop();
        Assert.Contains(GameEvent.LevelFailed, events);
    }

    [Fact]
    public void Restart_puts_the_same_pile_and_pieces_back()
    {
        var level = LevelCatalog.Get(20);
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(level);
        string before = e.Board.ToString()!;
        var firstColours = e.CurrentPiece!.Cells.Select(c => c.Colour).ToArray();

        e.HardDrop();
        e.Restart();

        Assert.Equal(before, e.Board.ToString()!);
        Assert.Equal(firstColours, e.CurrentPiece!.Cells.Select(c => c.Colour).ToArray());
        Assert.Equal(0, e.Score);
        Assert.Equal(0, e.PiecesPlaced);
        Assert.Equal(20, e.Setup.Level);
    }

    [Fact]
    public void Starting_classic_after_story_clears_the_story_state()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.Story(5));
        Assert.NotNull(e.Story);
        e.Start(GameSetup.Classic);
        Assert.Null(e.Story);
        Assert.Equal(0, e.Board.CountOccupied());
        Assert.Empty(e.Goals);
        Assert.False(e.HasMoveLimit);
    }

    [Fact]
    public void Setup_titles()
    {
        Assert.Equal("ENDLESS", GameSetup.Classic.Title);
        Assert.Equal("LEVEL 7", GameSetup.Story(7).Title);
        Assert.Equal("LESSON 3", GameSetup.Lesson(3).Title);
        Assert.Equal(1, GameSetup.Story(-4).Level);
        Assert.Equal(1, GameSetup.Lesson(0).Level);
    }
}

/// <summary>The hand-made tutorial lessons can be played through with the moves their text asks for.</summary>
public class TutorialLevelTests
{
    private static GameEngine Play(int level, params (int Rotations, int Column)[] moves)
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(LevelCatalog.Lesson(level));
        foreach (var (rot, col) in moves)
        {
            Assert.Equal(GameState.Playing, e.State);
            Story.Rotate(e, rot);
            Story.MoveTo(e, col);
            e.HardDrop();
            Story.Settle(e);
        }
        return e;
    }

    [Fact] public void Level_1_pop_it() => Assert.Equal(GameState.LevelComplete, Play(1, (0, 4)).State);
    [Fact] public void Level_2_turn_it_around() => Assert.Equal(GameState.LevelComplete, Play(2, (1, 2)).State);
    [Fact] public void Level_3_chain_reaction() => Assert.Equal(GameState.LevelComplete, Play(3, (0, 0)).State);
    [Fact] public void Level_4_count_your_moves() => Assert.Equal(GameState.LevelComplete, Play(4, (0, 2), (0, 4)).State);
    [Fact] public void Level_5_bigger_is_better() => Assert.Equal(GameState.LevelComplete, Play(5, (0, 2)).State);
    [Fact] public void Level_6_speed_it_up() => Assert.Equal(GameState.LevelComplete, Play(6, (0, 0)).State);
    [Fact] public void Level_7_two_colours() => Assert.Equal(GameState.LevelComplete, Play(7, (0, 3)).State);
    [Fact] public void Level_8_plan_ahead() => Assert.Equal(GameState.LevelComplete, Play(8, (0, 2), (0, 4)).State);
    [Fact] public void Level_9_big_group() => Assert.Equal(GameState.LevelComplete, Play(9, (0, 2)).State);

    [Fact]
    public void Level_4_can_be_lost_by_wasting_moves()
    {
        var e = Play(4, (0, 0), (0, 0), (0, 0));
        Assert.Equal(GameState.GameOver, e.State);
        Assert.Equal(FailReason.OutOfMoves, e.FailReason);
    }

    [Fact]
    public void Tutorials_are_flagged_and_start_with_coaching()
    {
        for (int n = 1; n <= StoryConfig.LessonCount; n++)
        {
            var level = LevelCatalog.Lesson(n);
            Assert.Equal(n, level.Number);
            Assert.True(level.IsTutorial);
            Assert.True(level.Steps.Count > 0);
        }
        Assert.False(LevelCatalog.Get(11).IsTutorial);
        Assert.True(LevelCatalog.Get(11).Steps.Count > 0);
    }

    [Fact]
    public void Level_1_teaches_by_doing()
    {
        var steps = LevelCatalog.Lesson(1).Steps;
        Assert.Equal(StepMode.Tap, steps[0].Mode);
        Assert.Equal(StepMode.Do, steps[1].Mode);
        Assert.Equal(GameEvent.Moved, steps[1].Advance);
        Assert.Equal(GameEvent.HardDropped, steps[2].Advance);
    }

    [Fact]
    public void Level_11_brings_in_the_new_shapes()
    {
        var level = LevelCatalog.Get(11);
        Assert.True(level.ExoticShare > 0);
        Assert.Contains("Plus", level.Script[0]);
        Assert.Equal(StoryConfig.TierFor(11), Tier.Easy);
    }
}

public class TutorialControllerTests
{
    private static TutorialController Make() => new(new[]
    {
        new TutorialStep("Hello", "Tap me"),
        new TutorialStep("Move", "Slide it", StepMode.Do, GameEvent.Moved),
        new TutorialStep("Watch", "Look", StepMode.Watch, GameEvent.Popped),
    });

    [Fact]
    public void Tap_cards_wait_for_dismissal_and_freeze_the_game()
    {
        var t = Make();
        Assert.True(t.IsActive);
        Assert.True(t.FreezesGame);
        t.Notify(GameEvent.Moved);                // events do not advance a Tap card
        Assert.Equal(0, t.Index);
        t.Dismiss();
        Assert.Equal(1, t.Index);
    }

    [Fact]
    public void Do_steps_advance_only_on_their_event_and_freeze_the_game()
    {
        var t = Make();
        t.Dismiss();
        Assert.True(t.FreezesGame);
        t.Notify(GameEvent.Rotated);
        Assert.Equal(1, t.Index);
        t.Notify(GameEvent.Moved);
        Assert.Equal(2, t.Index);
    }

    [Fact]
    public void Watch_steps_let_the_game_run()
    {
        var t = Make();
        t.Dismiss();
        t.Notify(GameEvent.Moved);
        Assert.False(t.FreezesGame);
        t.Notify(GameEvent.Popped);
        Assert.False(t.IsActive);
        Assert.Null(t.Current);
    }

    [Fact]
    public void Dismiss_does_nothing_on_a_do_step()
    {
        var t = Make();
        t.Dismiss();
        t.Dismiss();
        Assert.Equal(1, t.Index);
    }

    [Fact]
    public void Skip_ends_everything_and_reports_the_change_once()
    {
        var t = Make();
        int changes = 0;
        t.Changed += () => changes++;
        t.Skip();
        t.Skip();
        Assert.False(t.IsActive);
        Assert.False(t.FreezesGame);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void An_empty_tutorial_is_inactive()
    {
        var t = new TutorialController(Array.Empty<TutorialStep>());
        Assert.False(t.IsActive);
        t.Dismiss();
        t.Notify(GameEvent.Moved);
        Assert.False(t.FreezesGame);
    }

    [Fact]
    public void Level_1_can_be_coached_end_to_end_with_real_engine_events()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.StartLevel(LevelCatalog.Lesson(1));
        var t = new TutorialController(e.Story!.Steps);
        e.EventRaised += t.Notify;

        Assert.Equal(StepMode.Tap, t.Current!.Mode);
        t.Dismiss();
        Story.MoveTo(e, 4);
        Assert.Equal(2, t.Index);
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(GameState.LevelComplete, e.State);
        Assert.False(t.IsActive);
    }
}

public class ProgressAndShopTests
{
    [Fact]
    public void A_new_player_starts_at_level_one_with_no_gold()
    {
        var p = new PlayerProgress(new InMemorySettings());
        Assert.Equal(0, p.HighestCleared);
        Assert.Equal(0, p.Gold);
        Assert.Equal(1, p.NextLevel);
        Assert.True(p.IsUnlocked(1));
        Assert.False(p.IsUnlocked(2));
    }

    [Fact]
    public void Tutorial_levels_advance_progress_but_pay_nothing()
    {
        var p = new PlayerProgress(new InMemorySettings());
        var result = p.Complete(LevelCatalog.Get(1));
        Assert.True(result.FirstClear);
        Assert.Equal(0, result.GoldAwarded);
        Assert.Equal(1, p.HighestCleared);
        Assert.Equal(0, p.Gold);
    }

    [Fact]
    public void The_first_clear_pays_gold_and_a_replay_does_not()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.HighestClearedKey, 10);
        var p = new PlayerProgress(settings);

        var first = p.Complete(LevelCatalog.Get(11));
        Assert.True(first.FirstClear);
        Assert.True(first.GoldAwarded > 0);
        int gold = p.Gold;

        var again = p.Complete(LevelCatalog.Get(11));
        Assert.False(again.FirstClear);
        Assert.Equal(0, again.GoldAwarded);
        Assert.Equal(gold, p.Gold);
    }

    [Fact]
    public void Replaying_an_older_level_pays_nothing()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.HighestClearedKey, 30);
        var p = new PlayerProgress(settings);
        var result = p.Complete(LevelCatalog.Get(12));
        Assert.False(result.FirstClear);
        Assert.Equal(0, p.Gold);
        Assert.Equal(30, p.HighestCleared);
    }

    [Fact]
    public void Levels_cannot_be_cleared_out_of_order()
    {
        var p = new PlayerProgress(new InMemorySettings());
        var result = p.Complete(LevelCatalog.Get(15));
        Assert.False(result.FirstClear);
        Assert.Equal(0, p.HighestCleared);
        Assert.Equal(0, p.Gold);
    }

    [Fact]
    public void Progress_and_gold_survive_a_restart()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.HighestClearedKey, 10);
        var first = new PlayerProgress(settings);
        first.Complete(LevelCatalog.Get(11));

        var second = new PlayerProgress(settings);
        Assert.Equal(11, second.HighestCleared);
        Assert.Equal(first.Gold, second.Gold);
        Assert.True(second.IsCleared(11));
        Assert.False(second.IsCleared(12));
        Assert.True(second.IsUnlocked(12));
    }

    [Fact]
    public void Gold_grows_with_difficulty()
    {
        int easy = GoldRewards.For(30, false);
        int medium = GoldRewards.For(75, false);
        int hard = GoldRewards.For(125, false);
        int extreme = GoldRewards.For(175, false);
        Assert.True(easy > 0);
        Assert.True(medium > easy);
        Assert.True(hard > medium);
        Assert.True(extreme > hard);
        Assert.Equal(0, GoldRewards.For(5, true));
        Assert.Equal(0, GoldRewards.For(5, false));
    }

    [Fact]
    public void Gold_never_falls_as_levels_rise()
    {
        int previous = 0;
        for (int n = StoryConfig.TutorialLastLevel + 1; n <= 400; n++)
        {
            int gold = GoldRewards.For(n, false);
            Assert.True(gold >= previous);
            previous = gold;
        }
    }

    [Fact]
    public void Spending_needs_enough_gold()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.GoldKey, 50);
        var p = new PlayerProgress(settings);
        Assert.False(p.TrySpend(51));
        Assert.Equal(50, p.Gold);
        Assert.True(p.TrySpend(20));
        Assert.Equal(30, p.Gold);
        Assert.False(p.TrySpend(-5));
    }

    private static (ShopService Shop, PlayerProgress Progress, InMemorySettings Settings) Shop(int gold, int highestCleared = 0)
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.GoldKey, gold);
        if (highestCleared > 0) settings.SetInt(PlayerProgress.HighestClearedKey, highestCleared);
        var progress = new PlayerProgress(settings);
        return (new ShopService(settings, progress), progress, settings);
    }

    [Fact]
    public void The_shop_starts_with_no_upgrades()
    {
        var (shop, _, _) = Shop(0);
        Assert.Equal(1, shop.PreviewCount);
        Assert.Equal(0, shop.FlairLevel);
        Assert.Equal(0, shop.ExtraMoves);
        Assert.Equal(3, shop.Items().Count);
    }

    [Fact]
    public void Buying_spends_gold_and_raises_the_level()
    {
        var (shop, progress, _) = Shop(500);
        int cost = shop.NextCost(Upgrade.Preview)!.Value;

        Assert.True(shop.CanAfford(Upgrade.Preview));
        Assert.True(shop.TryBuy(Upgrade.Preview));
        Assert.Equal(500 - cost, progress.Gold);
        Assert.Equal(1, shop.Level(Upgrade.Preview));
        Assert.Equal(2, shop.PreviewCount);
    }

    [Fact]
    public void Buying_without_enough_gold_changes_nothing()
    {
        var (shop, progress, _) = Shop(10);
        Assert.False(shop.CanAfford(Upgrade.PopFlair));
        Assert.False(shop.TryBuy(Upgrade.PopFlair));
        Assert.Equal(10, progress.Gold);
        Assert.Equal(0, shop.FlairLevel);
    }

    [Fact]
    public void Each_upgrade_stops_at_its_maximum()
    {
        var (shop, _, _) = Shop(1_000_000);
        while (shop.TryBuy(Upgrade.ExtraMoves)) { }
        Assert.Equal(shop.MaxLevel(Upgrade.ExtraMoves), shop.Level(Upgrade.ExtraMoves));
        Assert.Null(shop.NextCost(Upgrade.ExtraMoves));
        Assert.False(shop.CanAfford(Upgrade.ExtraMoves));
        Assert.Equal(6, shop.ExtraMoves);
        Assert.True(shop.Items().First(i => i.Kind == Upgrade.ExtraMoves).IsMaxed);
    }

    [Fact]
    public void Upgrade_prices_rise_with_each_level()
    {
        foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
        {
            // Some later levels ask for a region to be restored, so give the player the whole campaign.
            var (shop, _, _) = Shop(1_000_000, 200);
            int previous = 0;
            while (shop.NextCost(u) is { } cost)
            {
                Assert.True(cost > previous);
                previous = cost;
                shop.TryBuy(u);
            }
        }
    }

    [Fact]
    public void Purchases_survive_a_restart()
    {
        var (shop, _, settings) = Shop(1000);
        shop.TryBuy(Upgrade.PopFlair);
        shop.TryBuy(Upgrade.PopFlair);

        var progress = new PlayerProgress(settings);
        var reopened = new ShopService(settings, progress);
        Assert.Equal(2, reopened.FlairLevel);
    }

    [Fact]
    public void The_shop_tells_the_player_what_each_level_does()
    {
        var (shop, _, _) = Shop(0);
        foreach (var item in shop.Items())
        {
            Assert.True(item.Title.Length > 0);
            Assert.True(item.Description.Length > 0);
            Assert.True(item.CurrentEffect.Length > 0);
            Assert.True(item.NextEffect.Length > 0);
            Assert.True(item.NextCost > 0);
        }
    }
}

public class ShapeTests
{
    private static readonly CellColour[] Cycle = { CellColour.Cyan, CellColour.Coral };

    private static Piece Make(ShapeDef shape) =>
        ShapeCatalog.Build(shape, Enumerable.Range(0, shape.CellCount).Select(i => Cycle[i % 2]).ToArray());

    private static string Signature(Piece p) =>
        string.Join(";", p.BoardCells().Select(c => $"{c.X},{c.Y}").OrderBy(s => s));

    [Fact]
    public void Every_shape_fits_on_the_empty_board_at_spawn()
    {
        foreach (var shape in ShapeCatalog.All)
            Assert.True(CollisionDetector.IsValid(new Board(), Make(shape)));
    }

    [Fact]
    public void Four_rotations_bring_every_shape_back_to_where_it_started()
    {
        foreach (var shape in ShapeCatalog.All)
        {
            var piece = Make(shape).Moved(0, 5);
            string start = Signature(piece);
            var board = new Board();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(CollisionDetector.TryRotate(board, piece, true, out var next));
                piece = next;
            }
            Assert.Equal(start, Signature(piece));
        }
    }

    [Fact]
    public void Rotating_never_loses_or_merges_cells()
    {
        foreach (var shape in ShapeCatalog.All)
        {
            var piece = Make(shape).Moved(0, 5);
            var board = new Board();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(CollisionDetector.TryRotate(board, piece, true, out var turned));
                piece = turned;
                Assert.Equal(shape.CellCount, piece.BoardCells().Select(c => (c.X, c.Y)).Distinct().Count());
            }
        }
    }

    [Fact]
    public void Shapes_can_be_found_by_name_and_parsed()
    {
        Assert.Equal("Plus", ShapeCatalog.Find("plus").Name);
        var piece = ShapeCatalog.Parse("Pair:RC");
        Assert.Equal(2, piece.Cells.Count);
        Assert.Equal(CellColour.Coral, piece.Cells[0].Colour);
        Assert.Equal(CellColour.Cyan, piece.Cells[1].Colour);
    }

    [Fact]
    public void Parsing_rejects_bad_specs()
    {
        bool threw = false;
        try { ShapeCatalog.Parse("Pair:R"); } catch (ArgumentException) { threw = true; }
        Assert.True(threw);

        threw = false;
        try { ShapeCatalog.Parse("Nope:RR"); } catch (ArgumentException) { threw = true; }
        Assert.True(threw);
    }

    [Fact]
    public void Classic_generation_never_makes_exotic_pieces()
    {
        var generator = new PieceGenerator(new Random(1));
        for (int i = 0; i < 300; i++)
            Assert.True(generator.Next(5).Cells.Count <= 4);
    }

    [Fact]
    public void Exotic_generation_makes_bigger_pieces_and_keeps_them_two_coloured()
    {
        var generator = new PieceGenerator(new Random(2), exoticShare: 1.0);
        bool sawFive = false;
        for (int i = 0; i < 300; i++)
        {
            var piece = generator.Next(5);
            if (piece.Cells.Count >= 5) sawFive = true;
            if (piece.Cells.Count >= 4)
                Assert.True(piece.Cells.Select(c => c.Colour).Distinct().Count() >= 2);
        }
        Assert.True(sawFive);
    }

    [Fact]
    public void The_script_plays_first_then_random_pieces_follow()
    {
        var generator = new ScriptedPieceGenerator(new[] { "Dot:C", "Pair:RR" }, new PieceGenerator(new Random(3)));
        Assert.Equal(1, generator.Next(4).Cells.Count);
        Assert.Equal(2, generator.Next(4).Cells.Count);
        Assert.NotNull(generator.Next(4));
    }
}

public class BoardGeneratorTests
{
    private static readonly DifficultyProfile Profile = new(5, 5, 2, 7, 0.5, 0.2);

    [Fact]
    public void The_same_seed_gives_the_same_board()
    {
        Assert.Equal(BoardGenerator.Generate(42, Profile).ToString()!, BoardGenerator.Generate(42, Profile).ToString()!);
    }

    [Fact]
    public void Different_seeds_give_different_boards()
    {
        Assert.NotEqual(BoardGenerator.Generate(1, Profile).ToString()!, BoardGenerator.Generate(2, Profile).ToString()!);
    }

    [Fact]
    public void Generated_boards_are_settled_with_nothing_to_pop_and_a_clear_top()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var board = BoardGenerator.Generate(seed, Profile);
            Assert.True(board.CountOccupied() > 0);
            Assert.True(Story.IsSettled(board));
            Assert.Empty(ColourMatcher.FindGroups(board));
            Assert.False(board.AnyInRows(0, GameConfig.HiddenRows + 2));
        }
    }

    [Fact]
    public void Boards_only_use_the_colours_they_were_asked_for()
    {
        var four = new DifficultyProfile(4, 5, 2, 7, 0.5, 0.2);
        for (int seed = 0; seed < 50; seed++)
        {
            var board = BoardGenerator.Generate(seed, four);
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.True((int)board[x, y] <= 4);
        }
    }

    [Fact]
    public void Taller_profiles_make_bigger_piles()
    {
        int low = 0, high = 0;
        for (int seed = 0; seed < 30; seed++)
        {
            low += BoardGenerator.Generate(seed, new DifficultyProfile(4, 2, 1, 4, 0.5, 0)).CountOccupied();
            high += BoardGenerator.Generate(seed, new DifficultyProfile(4, 7, 1, 9, 0.5, 0)).CountOccupied();
        }
        Assert.True(high > low);
    }
}

/// <summary>Whole-catalogue checks: every level must be playable and winnable.</summary>
public class LevelCatalogueTests
{
    [Fact]
    public void Tiers_follow_the_documented_bands()
    {
        Assert.Equal(Tier.Easy, StoryConfig.TierFor(1));
        Assert.Equal(Tier.Easy, StoryConfig.TierFor(50));
        Assert.Equal(Tier.Medium, StoryConfig.TierFor(51));
        Assert.Equal(Tier.Medium, StoryConfig.TierFor(100));
        Assert.Equal(Tier.Hard, StoryConfig.TierFor(101));
        Assert.Equal(Tier.Hard, StoryConfig.TierFor(150));
        Assert.Equal(Tier.Extreme, StoryConfig.TierFor(151));
        Assert.Equal(Tier.Extreme, StoryConfig.TierFor(5000));
        Assert.Equal(0.0, StoryConfig.ProgressInTier(51));
        Assert.Equal(1.0, StoryConfig.ProgressInTier(100));
    }

    [Fact]
    public void Levels_are_the_same_every_time_they_are_asked_for()
    {
        Assert.Same(LevelCatalog.Get(33), LevelCatalog.Get(33));
        Assert.Equal(1, LevelCatalog.Get(0).Number);
        Assert.Equal(1, LevelCatalog.Get(-9).Number);
    }

    [Fact]
    public void Levels_record_their_tier()
    {
        foreach (int n in new[] { 1, 25, 50, 51, 99, 101, 150, 151, 300 })
            Assert.Equal(StoryConfig.TierFor(n), LevelCatalog.Get(n).Tier);
    }

    [Fact]
    public void Difficulty_ramps_up_across_the_tiers()
    {
        Assert.True(LevelCatalog.Get(12).FallIntervalMs > LevelCatalog.Get(60).FallIntervalMs);
        Assert.True(LevelCatalog.Get(60).FallIntervalMs > LevelCatalog.Get(120).FallIntervalMs);
        Assert.True(LevelCatalog.Get(120).FallIntervalMs > LevelCatalog.Get(180).FallIntervalMs);
        Assert.True(LevelCatalog.Get(51).Colours >= LevelCatalog.Get(12).Colours);
    }

    [Fact]
    public void Multiple_goals_arrive_later_in_the_story()
    {
        Assert.True(Enumerable.Range(12, 19).All(n => LevelCatalog.Get(n).Goals.Count == 1));
        Assert.True(Enumerable.Range(32, 40).Any(n => LevelCatalog.Get(n).Goals.Count >= 2));
        Assert.True(Enumerable.Range(161, 40).Any(n => LevelCatalog.Get(n).Goals.Count >= 3));
    }

    [Fact]
    public void New_features_get_an_introduction_card()
    {
        foreach (int n in new[] { 13, 16, 20, 26, 32, 51, 101, 128, 134, 151, 161 })
            Assert.True(LevelCatalog.Get(n).Steps.Count > 0);
    }

    [Fact]
    public void Every_level_up_to_220_has_a_fair_start_and_the_bot_can_win_it()
    {
        for (int n = 1; n <= 220; n++)
        {
            var level = LevelCatalog.Get(n);
            string label = $"level {n}";

            var board = level.CreateBoard();
            Assert.True(board.CountOccupied() > 0, label);
            Assert.True(Story.IsSettled(board), label);
            Assert.False(board.AnyInRows(0, GameConfig.HiddenRows + 1), label);
            Assert.True(ColourMatcher.FindGroups(board).Count == 0, label + " starts with a ready-made group");
            Assert.True(level.Goals.Count > 0, label);
            Assert.True(level.FallIntervalMs >= 300, label);

            int cap = level.MoveLimit > 0 ? level.MoveLimit : 12;
            var plan = LevelSolver.Solve(level, cap);
            Assert.True(plan.Won, label + " is not winnable by the bot");

            var engine = new GameEngine(new ScriptedGenerator());
            engine.StartLevel(level);
            var end = Story.Replay(engine, plan);
            Assert.Equal(GameState.LevelComplete, end);
            if (level.MoveLimit > 0) Assert.True(engine.PiecesPlaced <= level.MoveLimit, label);
        }
    }

    [Fact]
    public void Generated_levels_leave_some_slack_in_their_move_limit()
    {
        foreach (int n in new[] { 14, 40, 75, 120, 170 })
        {
            var level = LevelCatalog.Get(n);
            var plan = LevelSolver.Solve(level, level.MoveLimit);
            Assert.True(plan.Won);
            Assert.True(level.MoveLimit >= plan.Moves);
        }
    }

    [Fact]
    public void Winning_the_extra_moves_upgrade_makes_a_level_easier_not_harder()
    {
        var level = LevelCatalog.Get(60);
        var plan = LevelSolver.Solve(level, level.MoveLimit);
        var e = new GameEngine(new ScriptedGenerator()) { ExtraMoves = 6 };
        e.StartLevel(level);
        Assert.Equal(level.MoveLimit + 6, e.MoveLimit);
        Assert.Equal(GameState.LevelComplete, Story.Replay(e, plan));
    }
}

/// <summary>Random play must never break the engine, whatever level it is on.</summary>
public class StoryFuzzTests
{
    [Fact]
    public void Random_play_keeps_the_board_valid_on_many_levels()
    {
        var random = new Random(77);
        var levels = new List<int>();
        levels.AddRange(Enumerable.Range(1, 30));
        levels.AddRange(new[] { 51, 77, 101, 133, 151, 152, 156, 161, 190, 250 });

        foreach (int n in levels)
        {
            var e = new GameEngine(new ScriptedGenerator())
            {
                Timing = n % 2 == 0 ? new PopTiming(80, 80) : PopTiming.Instant,
                PreviewCount = 1 + n % 3,
                ExtraMoves = n % 4,
            };
            e.StartLevel(LevelCatalog.Get(n));

            for (int step = 0; step < 400; step++)
            {
                switch (random.Next(7))
                {
                    case 0: e.MoveLeft(); break;
                    case 1: e.MoveRight(); break;
                    case 2: e.Rotate(random.Next(2) == 0); break;
                    case 3: e.SoftDrop(); break;
                    case 4: if (random.Next(4) == 0) e.HardDrop(); break;
                    default: e.Tick(); break;
                }

                if (e.State == GameState.Playing)
                {
                    Assert.NotNull(e.CurrentPiece);
                    Assert.True(CollisionDetector.IsValid(e.Board, e.CurrentPiece!), $"level {n} step {step}: piece invalid");
                    Assert.Equal(e.PreviewCount, e.Upcoming.Count);
                }
                if (e.State is GameState.GameOver or GameState.LevelComplete) break;
            }
            Assert.True(e.Score >= 0, $"level {n}: negative score");
        }
    }
}
