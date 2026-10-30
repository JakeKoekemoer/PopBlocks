using System.Collections.Concurrent;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>v1.1 loss support: the never-resetting loss count, the "always a way through" note, and easing a level.</summary>
public class LossSupportTests
{
    private static readonly int Salt = SeedMix.Hash32("Tester", 4242);

    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public readonly PlayerProgress Progress;
        public readonly StoryLevels Levels;
        public readonly ConcurrentQueue<(int Level, int Reroll)> Rolls = new();

        public Rig(int highestCleared, int gold = 0)
        {
            Progress = new PlayerProgress(Settings);
            Progress.DevSetHighestCleared(highestCleared);
            Progress.DevSetGold(gold);
            var identity = new PlayerIdentity(Settings, () => 4242);
            if (identity.NeedsPrompt) identity.Choose("Tester");
            Levels = new StoryLevels(Settings, Progress, identity, (level, salt, reroll, ct) =>
            {
                Rolls.Enqueue((level, reroll));
                var seed = LevelSeed.Derive(salt, level, reroll, 0);
                return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
            });
        }

        public void Lose(int level, int times)
        {
            for (int i = 0; i < times; i++) Levels.RecordFailure(level);
        }
    }

    // ------------------------------------------------------------------ the loss count

    [Fact]
    public void Losses_count_every_failure_on_the_current_level_and_nowhere_else()
    {
        var rig = new Rig(39);
        rig.Lose(40, 3);
        Assert.Equal(3, rig.Levels.Losses(40));
        Assert.Equal(0, rig.Levels.RecordFailure(12));   // not the current level: nothing counted
        Assert.Equal(0, rig.Levels.Losses(12));
    }

    [Fact]
    public void Losses_survive_a_reroll_and_a_win()
    {
        var rig = new Rig(39, gold: 1000);
        rig.Lose(40, 2);
        Assert.True(rig.Levels.TryReroll(40));
        Assert.Equal(0, rig.Levels.Failures(40));   // the failure count started again...
        Assert.Equal(2, rig.Levels.Losses(40));      // ...the loss count did not

        rig.Lose(40, 1);
        Assert.Equal(3, rig.Levels.Losses(40));
        Assert.Equal(3, SeedStore.ReadLosses(rig.Settings, 40));   // it lives in the save

        rig.Progress.Complete(LevelCatalog.Get(40, LevelSeed.Derive(Salt, 40, 1, 0)));
        Assert.Equal(3, rig.Levels.Losses(40));      // winning does not clear it either
    }

    [Fact]
    public void The_solvable_note_appears_from_five_losses()
    {
        var rig = new Rig(39);
        rig.Lose(40, SeedingConfig.LossesForSolvableNote - 1);
        Assert.False(rig.Levels.ShowsSolvableNote(40));
        rig.Lose(40, 1);
        Assert.True(rig.Levels.ShowsSolvableNote(40));
    }

    [Fact]
    public void Losses_and_eased_levels_travel_in_a_save_code()
    {
        var rig = new Rig(39);
        rig.Lose(40, 4);
        SeedStore.SetEased(rig.Settings, 12, true);

        var other = new InMemorySettings();
        var result = SaveCode.Import(SaveCode.Export(rig.Settings), other);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(4, SeedStore.ReadLosses(other, 40));
        Assert.True(SeedStore.IsEased(other, 12));
        Assert.Contains(SaveKeys.LevelLosses, SaveCode.Keys);
        Assert.Contains(SaveKeys.EasedLevels, SaveCode.Keys);
    }

    // ------------------------------------------------------------------ easing

    [Fact]
    public void Easing_is_offered_from_fifteen_losses_on_the_current_level_only()
    {
        var rig = new Rig(39);
        rig.Lose(40, SeedingConfig.LossesForEase - 1);
        Assert.False(rig.Levels.CanEase(40));
        rig.Lose(40, 1);
        Assert.True(rig.Levels.CanEase(40));
        Assert.False(rig.Levels.CanEase(41));   // not the current level
        Assert.False(rig.Levels.CanEase(12));   // already cleared
    }

    [Fact]
    public void A_finale_can_never_be_eased()
    {
        foreach (var region in RegionCatalog.All)
        {
            var rig = new Rig(region.LastLevel - 1);
            rig.Lose(region.LastLevel, SeedingConfig.LossesForEase + 5);
            Assert.True(StoryLevels.IsFinale(region.LastLevel));
            Assert.False(rig.Levels.CanEase(region.LastLevel), $"level {region.LastLevel}");
            Assert.False(rig.Levels.TryEase(region.LastLevel));
        }
    }

    [Fact]
    public async Task Easing_marks_the_level_resets_failures_and_asks_for_the_eased_roll()
    {
        var rig = new Rig(39);
        rig.Lose(40, SeedingConfig.LossesForEase);

        Assert.True(rig.Levels.TryEase(40));

        Assert.True(rig.Levels.IsEased(40));
        Assert.True(rig.Levels.IsEasedPlay(40));
        Assert.Equal(0, rig.Levels.Failures(40));
        Assert.Equal(SeedingConfig.LossesForEase, rig.Levels.Losses(40));
        Assert.False(rig.Levels.CanEase(40));   // already eased
        Assert.Null(rig.Levels.KnownSeed(40));  // an eased level has to be found first

        var seed = await rig.Levels.ResolveAsync(40);
        Assert.False(seed.IsLegacy);
        Assert.Contains(rig.Rolls, r => r.Level == 40 && r.Reroll >= LevelRoller.EasedRerollOffset);
    }

    [Fact]
    public void A_reroll_of_an_eased_level_is_a_normal_level_again()
    {
        var rig = new Rig(39, gold: 1000);
        rig.Lose(40, SeedingConfig.LossesForEase);
        rig.Levels.TryEase(40);
        rig.Lose(40, 1);

        Assert.True(rig.Levels.TryReroll(40));
        Assert.False(rig.Levels.IsEased(40));
    }

    [Fact]
    public void An_eased_level_has_one_and_a_half_times_the_moves_rounded_up()
    {
        var level = LevelCatalog.Get(40, LevelSeed.Derive(1234, 40, 0, 0));
        var eased = level.ToEased();
        Assert.True(eased.Eased);
        Assert.False(level.Eased);
        Assert.Equal((int)Math.Ceiling(level.MoveLimit * 1.5), eased.MoveLimit);
        Assert.Equal(level.Goals.Count, eased.Goals.Count);
        Assert.Equal(level.Seed, eased.Seed);
    }

    [Fact]
    public void The_engine_plays_an_eased_setup_with_the_extra_moves_and_keeps_it_across_a_restart()
    {
        var seed = LevelSeed.Derive(1234, 40, 0, 0);
        var plain = LevelCatalog.Get(40, seed);

        var game = new GameEngine(new PieceGenerator());
        game.Start(GameSetup.Story(40, seed) with { Eased = true });
        Assert.True(game.Story!.Eased);
        Assert.Equal(plain.ToEased().MoveLimit, game.MoveLimit);

        game.Restart();
        Assert.True(game.Story!.Eased);
        Assert.True(game.Setup.Eased);
    }

    [Fact]
    public void The_eased_band_is_lower_and_an_eased_roll_still_returns_a_verified_level()
    {
        var (low, high) = DifficultyBand.For(60);
        var (easedLow, easedHigh) = DifficultyBand.EasedFor(60);
        Assert.Equal(1.0, easedLow);
        Assert.InRange(easedHigh, 1.0, Math.Max(1.0, high));
        Assert.True(easedHigh <= Math.Max(1.0, low + (high - low) / 2.0) + 1e-9);

        var rolled = LevelRoller.Roll(60, 4242, LevelRoller.EasedRerollOffset);
        var built = LevelCatalog.Build(60, rolled.Seed);
        Assert.True(built.Verified);   // the bot gate: the unchanged level is winnable, so the eased one (more moves) is too
        Assert.True(LevelSolver.PlanIsPlayable(built.Level, built.Plan));
    }

    // ------------------------------------------------------------------ what an eased clear pays

    [Fact]
    public void An_eased_clear_advances_the_story_and_pays_nothing()
    {
        var rig = new Rig(39);
        var eased = LevelCatalog.Get(40, LevelSeed.Derive(1234, 40, 0, 0)).ToEased();

        var result = rig.Progress.Complete(eased);

        Assert.True(result.FirstClear);
        Assert.Equal(0, result.GoldAwarded);
        Assert.Equal(40, rig.Progress.HighestCleared);
        Assert.Equal(0, rig.Progress.Gold);
        Assert.True(rig.Progress.IsEased(40));
    }

    [Fact]
    public void A_later_normal_win_pays_the_first_clear_once_and_removes_the_mark()
    {
        var rig = new Rig(39);
        var plain = LevelCatalog.Get(40, LevelSeed.Derive(1234, 40, 0, 0));
        rig.Progress.Complete(plain.ToEased());
        int owed = GoldRewards.For(40, false);
        Assert.True(owed > 0);

        var first = rig.Progress.Complete(plain);
        Assert.False(first.FirstClear);
        Assert.Equal(owed, first.GoldAwarded);
        Assert.Equal(owed, rig.Progress.Gold);
        Assert.False(rig.Progress.IsEased(40));

        var second = rig.Progress.Complete(plain);   // a plain replay pays nothing
        Assert.Equal(0, second.GoldAwarded);
        Assert.Equal(owed, rig.Progress.Gold);
    }

    [Fact]
    public void An_eased_replay_of_an_eased_level_does_not_remove_the_mark_or_pay()
    {
        var rig = new Rig(39);
        var plain = LevelCatalog.Get(40, LevelSeed.Derive(1234, 40, 0, 0));
        rig.Progress.Complete(plain.ToEased());

        var again = rig.Progress.Complete(plain.ToEased());
        Assert.Equal(0, again.GoldAwarded);
        Assert.True(rig.Progress.IsEased(40));
    }

    [Fact]
    public async Task A_replay_of_an_eased_level_is_rolled_fresh_instead_of_replaying_the_eased_seed()
    {
        var rig = new Rig(39);
        var plain = LevelCatalog.Get(40, LevelSeed.Derive(1234, 40, 0, 0));
        rig.Progress.Complete(plain.ToEased());

        Assert.Null(rig.Levels.KnownSeed(40));    // cleared + eased: needs a roll
        Assert.False(rig.Levels.IsEasedPlay(40)); // and it is a normal level, not the eased form
        var seed = await rig.Levels.ResolveAsync(40);
        Assert.False(seed.IsLegacy);
    }
}
