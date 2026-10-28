using System.Collections.Concurrent;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class RerollPricingTests
{
    [Theory]
    // level, failures, price. The examples from the spec, one per tier.
    [InlineData(12, 0, 12)] [InlineData(12, 1, 11)] [InlineData(12, 5, 6)] [InlineData(12, 9, 2)] [InlineData(12, 10, 0)] [InlineData(12, 25, 0)]
    [InlineData(60, 0, 33)] [InlineData(60, 1, 30)] [InlineData(60, 5, 17)] [InlineData(60, 9, 4)] [InlineData(60, 10, 0)]
    [InlineData(130, 0, 77)] [InlineData(130, 1, 70)] [InlineData(130, 5, 39)] [InlineData(130, 9, 8)] [InlineData(130, 10, 0)]
    [InlineData(180, 0, 114)] [InlineData(180, 1, 103)] [InlineData(180, 5, 57)] [InlineData(180, 9, 12)] [InlineData(180, 10, 0)]
    public void The_price_follows_the_table_including_rounding_up(int level, int failures, int expected) =>
        Assert.Equal(expected, RerollPricing.Price(level, failures));

    [Fact]
    public void The_opening_levels_pay_no_gold_so_their_rerolls_are_always_free()
    {
        for (int level = 1; level <= StoryConfig.TutorialLastLevel; level++)
            for (int failures = 0; failures <= 12; failures++)
                Assert.Equal(0, RerollPricing.Price(level, failures));
    }

    [Fact]
    public void The_price_never_rises_with_more_failures_and_is_free_from_the_tenth()
    {
        for (int level = 1; level <= 260; level++)
        {
            int previous = int.MaxValue;
            for (int failures = 0; failures <= 14; failures++)
            {
                int price = RerollPricing.Price(level, failures);
                Assert.InRange(price, 0, GoldRewards.For(level, false));
                Assert.True(price <= previous, $"level {level}, {failures} failures");
                previous = price;
                if (failures >= SeedingConfig.FailuresForFreeReroll) Assert.Equal(0, price);
            }
        }
    }

    [Fact]
    public void A_negative_failure_count_is_treated_as_none()
    {
        Assert.Equal(RerollPricing.Price(60, 0), RerollPricing.Price(60, -3));
        Assert.Equal(10, RerollPricing.FailuresUntilFree(0));
        Assert.Equal(1, RerollPricing.FailuresUntilFree(9));
        Assert.Equal(0, RerollPricing.FailuresUntilFree(10));
        Assert.Equal(0, RerollPricing.FailuresUntilFree(40));
    }
}

public class StoryLevelsTests
{
    /// <summary>The salt of the test player: the rig names them "Tester" and gives them install number 4242.</summary>
    private static readonly int Salt = SeedMix.Hash32("Tester", 4242);

    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public readonly PlayerProgress Progress;
        public readonly PlayerIdentity Identity;
        public readonly StoryLevels Levels;
        public readonly ConcurrentQueue<(int Level, int Salt, int Reroll)> Rolls = new();

        public Rig(InMemorySettings? settings = null, int salt = 4242, Func<int, int, int, CancellationToken, RolledLevel>? roll = null)
        {
            Settings = settings ?? Settings;
            Progress = new PlayerProgress(Settings);
            Identity = new PlayerIdentity(Settings, () => salt);
            if (Identity.NeedsPrompt) Identity.Choose("Tester");
            Levels = new StoryLevels(Settings, Progress, Identity, roll ?? FastRoll);
        }

        /// <summary>A roll that builds the real level for the seed but skips the search, and records that it was asked.</summary>
        public RolledLevel FastRoll(int level, int salt, int reroll, CancellationToken ct)
        {
            Rolls.Enqueue((level, salt, reroll));
            var seed = LevelSeed.Derive(salt, level, reroll, 0);
            return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
        }

        public void ClearUpTo(int level)
        {
            Progress.DevSetHighestCleared(level);
        }
    }

    private static Rig At(int highestCleared, int gold = 0)
    {
        var rig = new Rig();
        rig.Progress.DevSetHighestCleared(highestCleared);
        rig.Progress.DevSetGold(gold);
        return rig;
    }

    // ------------------------------------------------------------------ failures

    [Fact]
    public void A_successful_reroll_sends_the_failure_count_back_to_zero_and_counts_the_reroll()
    {
        var rig = At(11, gold: 500);
        rig.Levels.RecordFailure(12);
        rig.Levels.RecordFailure(12);
        Assert.Equal(2, rig.Levels.Failures(12));

        Assert.True(rig.Levels.TryReroll(12));

        Assert.Equal(0, rig.Levels.Failures(12));
        Assert.Equal(0, rig.Settings.GetInt(SaveKeys.CurrentLevelFailures, -1));
        Assert.Equal(1, rig.Levels.Rerolls(12));
        Assert.False(rig.Levels.CanOfferReroll(12));   // a fresh level: the reroll is offered again after its first failure
    }

    [Fact]
    public void A_reroll_that_is_refused_keeps_the_failure_count()
    {
        var rig = At(179, gold: 0);   // level 180 costs gold and there is none
        rig.Levels.RecordFailure(180);
        Assert.False(rig.Levels.TryReroll(180));
        Assert.Equal(1, rig.Levels.Failures(180));
    }

    [Fact]
    public void Winning_the_level_leaves_the_next_level_with_no_failures()
    {
        var rig = At(11, gold: 0);
        rig.Levels.RecordFailure(12);
        rig.Progress.Complete(LevelCatalog.Get(12, LevelSeed.Derive(Salt, 12, 0, 0)));
        Assert.Equal(0, rig.Levels.Failures(13));
    }

    [Fact]
    public void Rerolls_are_a_lifetime_stat_that_survives_a_restart_and_travels_in_a_save_code()
    {
        var settings = new InMemorySettings();
        var stats = new LifetimeStats(settings);
        stats.AddReroll();
        stats.AddReroll();
        Assert.Equal(2, stats.Rerolls);
        Assert.Equal(2, new LifetimeStats(settings).Rerolls);
        Assert.Contains(SaveKeys.StatRerolls, SaveKeys.StatKeys);
        Assert.Contains(SaveKeys.StatRerolls, SaveCode.Keys);

        stats.DevClear();
        Assert.Equal(0, stats.Rerolls);
    }

    [Fact]
    public void Failures_count_only_on_the_current_level()
    {
        var rig = At(39);   // level 40 is current

        Assert.Equal(1, rig.Levels.RecordFailure(40));
        Assert.Equal(2, rig.Levels.RecordFailure(40));
        Assert.Equal(0, rig.Levels.RecordFailure(12));   // cleared: a replay never counts
        Assert.Equal(0, rig.Levels.RecordFailure(41));   // locked
        Assert.Equal(2, rig.Levels.Failures(40));
        Assert.Equal(0, rig.Levels.Failures(12));
    }

    [Fact]
    public void Failures_survive_an_app_restart()
    {
        var rig = At(39);
        rig.Levels.RecordFailure(40);
        rig.Levels.RecordFailure(40);
        rig.Levels.RecordFailure(40);

        var again = new Rig(rig.Settings);
        Assert.Equal(3, again.Levels.Failures(40));
        Assert.Equal(7, again.Levels.RecordFailure(40) + 3);
    }

    [Fact]
    public void Failures_are_saved_on_every_increment()
    {
        var rig = At(39);
        rig.Levels.RecordFailure(40);
        Assert.Equal(1, rig.Settings.GetInt(SaveKeys.CurrentLevelFailures, 0));
        Assert.Equal(40, rig.Settings.GetInt(SaveKeys.CurrentLevelFor, 0));
        rig.Levels.RecordFailure(40);
        Assert.Equal(2, rig.Settings.GetInt(SaveKeys.CurrentLevelFailures, 0));
    }

    [Fact]
    public void Failures_from_an_earlier_level_are_ignored_once_progress_moves_on()
    {
        var rig = At(39);
        rig.Levels.RecordFailure(40);
        rig.Progress.DevSetHighestCleared(40);   // level 41 is current now; the saved values are for level 40
        Assert.Equal(0, rig.Levels.Failures(41));
        Assert.Null(rig.Levels.KnownSeed(41));
    }

    [Fact]
    public void Clearing_the_level_resets_failures_rerolls_and_the_stored_seed()
    {
        var rig = At(11, gold: 100);
        rig.Levels.RecordFailure(12);
        Assert.True(rig.Levels.TryReroll(12));
        var level = LevelCatalog.Get(12, LevelSeed.Derive(Salt, 12, 1, 0));

        rig.Progress.Complete(level);

        Assert.Equal(0, rig.Settings.GetInt(SaveKeys.CurrentLevelFailures, -1));
        Assert.Equal(0, rig.Settings.GetInt(SaveKeys.CurrentLevelRerolls, -1));
        Assert.Equal(0, rig.Settings.GetInt(SaveKeys.CurrentLevelSeed, -1));
        Assert.Equal(0, rig.Levels.Failures(13));
        Assert.Equal(0, rig.Levels.Rerolls(13));
    }

    // ------------------------------------------------------------------ reroll

    [Fact]
    public void A_reroll_is_not_offered_before_the_first_failure_but_is_after_it()
    {
        var rig = At(39, gold: 500);
        Assert.False(rig.Levels.CanOfferReroll(40));
        Assert.False(rig.Levels.TryReroll(40));

        rig.Levels.RecordFailure(40);
        Assert.True(rig.Levels.CanOfferReroll(40));
        Assert.False(rig.Levels.CanOfferReroll(41));   // not unlocked
    }

    [Fact]
    public void A_reroll_takes_the_price_adds_one_clears_the_seed_and_resets_the_failures()
    {
        var rig = At(59, gold: 100);   // level 60: 33 gold
        rig.Levels.RecordFailure(60);
        rig.Levels.RecordFailure(60);   // price after 2 failures = (33*8+9)/10 = 27
        Assert.Equal(27, rig.Levels.RerollPrice(60));
        Assert.Equal(8, rig.Levels.FailuresUntilFree(60));
        rig.Settings.SetInt(SaveKeys.CurrentLevelSeed, 5555);

        Assert.True(rig.Levels.TryReroll(60));

        Assert.Equal(73, rig.Progress.Gold);
        Assert.Equal(73, rig.Settings.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(1, rig.Levels.Rerolls(60));
        Assert.Equal(0, rig.Levels.Failures(60));
        Assert.Equal(0, rig.Settings.GetInt(SaveKeys.CurrentLevelSeed, -1));
        Assert.Null(rig.Levels.KnownSeed(60));
    }

    [Fact]
    public void Too_little_gold_changes_nothing()
    {
        var rig = At(59, gold: 10);
        rig.Levels.RecordFailure(60);
        string before = rig.Settings.Dump();

        Assert.False(rig.Levels.CanAffordReroll(60));
        Assert.False(rig.Levels.TryReroll(60));

        Assert.Equal(before, rig.Settings.Dump());
        Assert.Equal(10, rig.Progress.Gold);
    }

    [Fact]
    public void The_reroll_is_free_at_ten_failures_even_with_no_gold()
    {
        var rig = At(179, gold: 0);   // level 180: Extreme
        for (int i = 0; i < 9; i++) rig.Levels.RecordFailure(180);
        Assert.False(rig.Levels.CanAffordReroll(180));
        Assert.Equal(1, rig.Levels.FailuresUntilFree(180));

        rig.Levels.RecordFailure(180);
        Assert.Equal(0, rig.Levels.RerollPrice(180));
        Assert.True(rig.Levels.CanAffordReroll(180));
        Assert.True(rig.Levels.TryReroll(180));
        Assert.Equal(0, rig.Progress.Gold);
        Assert.Equal(0, rig.Levels.Failures(180));   // a reroll starts the new level's failure count again
    }

    [Fact]
    public void The_opening_levels_reroll_for_free_after_one_failure()
    {
        var rig = At(4, gold: 0);
        rig.Levels.RecordFailure(5);
        Assert.Equal(0, rig.Levels.RerollPrice(5));
        Assert.True(rig.Levels.TryReroll(5));
    }

    [Fact]
    public void A_cleared_level_can_always_be_rerolled_for_free_and_nothing_is_stored()
    {
        var rig = At(39, gold: 0);
        Assert.True(rig.Levels.CanOfferReroll(30));
        string before = rig.Settings.Dump();

        Assert.True(rig.Levels.TryReroll(30));

        Assert.Equal(before, rig.Settings.Dump());
        Assert.Equal(0, rig.Levels.RerollPrice(30));
        Assert.Null(rig.Levels.KnownSeed(30));   // needs a roll now
    }

    [Fact]
    public void Every_level_can_be_rerolled_including_finales_and_the_last_level()
    {
        foreach (int level in new[] { 25, 50, 100, 150, 175, 199, 200, 201 })
        {
            var rig = At(level - 1, gold: 0);
            for (int i = 0; i < 10; i++) rig.Levels.RecordFailure(level);
            Assert.True(rig.Levels.TryReroll(level), $"level {level}");
        }
    }

    // ------------------------------------------------------------------ which seed a level plays

    [Fact]
    public async Task The_first_entry_to_the_current_level_rolls_and_stores_the_seed()
    {
        var rig = At(39);
        Assert.Null(rig.Levels.KnownSeed(40));

        var seed = await rig.Levels.ResolveAsync(40);

        Assert.Equal(LevelSeed.Derive(Salt, 40, 0, 0), seed);
        Assert.Equal(seed.Value, rig.Settings.GetInt(SaveKeys.CurrentLevelSeed, 0));
        Assert.Equal(40, rig.Settings.GetInt(SaveKeys.CurrentLevelFor, 0));
        Assert.Single(rig.Rolls);
    }

    [Fact]
    public async Task Later_entries_use_the_stored_seed_without_rolling_again()
    {
        var rig = At(39);
        var first = await rig.Levels.ResolveAsync(40);

        var restarted = new Rig(rig.Settings);
        Assert.Equal(first, restarted.Levels.KnownSeed(40));
        Assert.Equal(first, await restarted.Levels.ResolveAsync(40));
        Assert.Empty(restarted.Rolls);
        Assert.True(restarted.Levels.TryGetReady(40, out var ready));
        Assert.Equal(first, ready);
    }

    [Fact]
    public async Task A_reroll_gives_a_different_level_and_it_is_the_same_one_every_time_for_that_reroll_count()
    {
        var rig = At(59, gold: 500);
        var first = await rig.Levels.ResolveAsync(60);
        rig.Levels.RecordFailure(60);
        Assert.True(rig.Levels.TryReroll(60));

        var second = await rig.Levels.ResolveAsync(60);

        Assert.NotEqual(first, second);
        Assert.Equal(LevelSeed.Derive(Salt, 60, 1, 0), second);
        Assert.Equal(second.Value, rig.Settings.GetInt(SaveKeys.CurrentLevelSeed, 0));
    }

    [Fact]
    public async Task A_crash_after_paying_still_gives_the_same_new_level_on_restart()
    {
        var rig = At(59, gold: 500);
        await rig.Levels.ResolveAsync(60);
        rig.Levels.RecordFailure(60);
        rig.Levels.TryReroll(60);   // paid; the app dies before the new level is rolled

        var restarted = new Rig(rig.Settings);
        Assert.Equal(LevelSeed.Derive(Salt, 60, 1, 0), await restarted.Levels.ResolveAsync(60));
    }

    [Fact]
    public async Task A_level_cleared_before_seeds_existed_replays_the_original_level()
    {
        var rig = At(39);
        Assert.Equal(LevelSeed.Legacy, rig.Levels.KnownSeed(12));
        Assert.Equal(LevelSeed.Legacy, await rig.Levels.ResolveAsync(12));
        Assert.Empty(rig.Rolls);
    }

    [Fact]
    public async Task A_cleared_level_replays_the_seed_it_was_first_cleared_with()
    {
        var rig = At(39);
        rig.Settings.SetString(SaveKeys.FirstClearSeeds, "12:777,30:888");
        Assert.Equal(new LevelSeed(777), rig.Levels.KnownSeed(12));
        Assert.Equal(new LevelSeed(888), await rig.Levels.ResolveAsync(30));
        Assert.Equal(LevelSeed.Legacy, rig.Levels.KnownSeed(13));   // cleared, no record
        Assert.Empty(rig.Rolls);
    }

    [Fact]
    public async Task A_rerolled_replay_uses_a_fresh_seed_this_session_and_returns_to_the_record_afterwards()
    {
        var rig = At(39);
        rig.Settings.SetString(SaveKeys.FirstClearSeeds, "30:888");
        string before = rig.Settings.Dump();

        Assert.True(rig.Levels.TryReroll(30));
        var replay = await rig.Levels.ResolveAsync(30);

        Assert.NotEqual(new LevelSeed(888), replay);
        Assert.False(replay.IsLegacy);
        Assert.Equal(replay, rig.Levels.KnownSeed(30));   // stays for the session
        Assert.Equal(before, rig.Settings.Dump());         // never stored

        var nextSession = new Rig(rig.Settings);
        Assert.Equal(new LevelSeed(888), nextSession.Levels.KnownSeed(30));
    }

    [Fact]
    public async Task Rerolling_a_replay_twice_gives_two_different_seeds()
    {
        var rig = At(39);
        rig.Levels.TryReroll(30);
        var a = await rig.Levels.ResolveAsync(30);
        rig.Levels.TryReroll(30);
        var b = await rig.Levels.ResolveAsync(30);
        Assert.NotEqual(a, b);
    }

    // ------------------------------------------------------------------ first clear

    [Fact]
    public void The_first_clear_records_the_seed_once_and_never_overwrites_it()
    {
        var rig = At(39);
        var seedA = LevelSeed.Derive(Salt, 40, 0, 0);
        var seedB = LevelSeed.Derive(Salt, 40, 3, 0);

        var result = rig.Progress.Complete(LevelCatalog.Get(40, seedA));
        Assert.True(result.FirstClear);
        Assert.Equal(seedA.Value, SeedStore.ReadFirstClear(rig.Settings)[40]);

        // A replay of a cleared level pays nothing and records nothing.
        Assert.False(rig.Progress.Complete(LevelCatalog.Get(40, seedB)).FirstClear);
        Assert.Equal(seedA.Value, SeedStore.ReadFirstClear(rig.Settings)[40]);

        // Even forced, the store refuses to overwrite.
        Assert.False(SeedStore.RecordFirstClear(rig.Settings, 40, seedB.Value));
        Assert.Equal(seedA.Value, SeedStore.ReadFirstClear(rig.Settings)[40]);
    }

    [Fact]
    public void Clearing_with_the_legacy_seed_records_nothing_so_the_level_still_replays_as_the_original()
    {
        var rig = At(39);
        rig.Progress.Complete(LevelCatalog.Get(40));
        Assert.Empty(SeedStore.ReadFirstClear(rig.Settings));
        Assert.Equal(LevelSeed.Legacy, rig.Levels.KnownSeed(40));
    }

    [Fact]
    public void Gold_is_paid_on_the_first_clear_and_the_next_level_starts_clean()
    {
        var rig = At(59, gold: 0);
        rig.Levels.RecordFailure(60);
        var seed = LevelSeed.Derive(Salt, 60, 0, 0);

        var result = rig.Progress.Complete(LevelCatalog.Get(60, seed));

        Assert.Equal(33, result.GoldAwarded);
        Assert.Equal(33, rig.Progress.Gold);
        Assert.Equal(61, rig.Progress.NextLevel);
        Assert.Equal(0, rig.Levels.Failures(61));
        Assert.Null(rig.Levels.KnownSeed(61));
    }

    // ------------------------------------------------------------------ prefetch

    [Fact]
    public async Task Nothing_is_prefetched_for_a_reroll_before_the_first_failure()
    {
        var rig = At(39);
        await rig.Levels.ResolveAsync(40);
        rig.Rolls.Clear();

        rig.Levels.PrefetchReroll(40);
        await Task.Delay(100);
        Assert.Empty(rig.Rolls);
    }

    [Fact]
    public async Task After_a_failure_the_next_reroll_is_rolled_ahead_and_used_at_once()
    {
        var rig = At(59, gold: 500);
        await rig.Levels.ResolveAsync(60);
        rig.Levels.RecordFailure(60);
        rig.Rolls.Clear();

        rig.Levels.PrefetchReroll(60);
        await WaitUntil(() => !rig.Rolls.IsEmpty);
        await WaitUntil(() => rig.Levels.HasRoll(60, 1));

        Assert.True(rig.Levels.TryReroll(60));
        await WaitUntil(() => { rig.Levels.TryGetReady(60, out _); return rig.Levels.KnownSeed(60) is not null; });

        Assert.Equal(LevelSeed.Derive(Salt, 60, 1, 0), rig.Levels.KnownSeed(60));
        Assert.Single(rig.Rolls);   // the prefetch was the only roll
    }

    [Fact]
    public async Task The_next_level_is_rolled_ahead_and_found_ready_when_it_becomes_current()
    {
        var rig = At(39);
        await rig.Levels.ResolveAsync(40);
        rig.Rolls.Clear();

        rig.Levels.PrefetchAfter(40);
        await WaitUntil(() => rig.Levels.HasRoll(41, 0));

        rig.Progress.Complete(LevelCatalog.Get(40, rig.Levels.KnownSeed(40)!.Value));
        await WaitUntil(() => rig.Levels.TryGetReady(41, out _));

        Assert.Equal(LevelSeed.Derive(Salt, 41, 0, 0), rig.Levels.KnownSeed(41));
        Assert.Single(rig.Rolls);
    }

    [Fact]
    public async Task Two_people_asking_for_the_same_roll_share_one_search()
    {
        int calls = 0;
        var gate = new ManualResetEventSlim();
        var rig = new Rig(roll: (level, salt, reroll, ct) =>
        {
            Interlocked.Increment(ref calls);
            gate.Wait(TimeSpan.FromSeconds(5));
            var seed = LevelSeed.Derive(salt, level, reroll, 0);
            return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
        });
        rig.Progress.DevSetHighestCleared(39);

        var a = rig.Levels.ResolveAsync(40);
        var b = rig.Levels.ResolveAsync(40);
        gate.Set();

        Assert.Equal(await a, await b);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancelling_prefetches_stops_the_ones_nobody_is_waiting_for()
    {
        var started = new ManualResetEventSlim();
        var cancelled = new ManualResetEventSlim();
        var rig = new Rig(roll: (level, salt, reroll, ct) =>
        {
            started.Set();
            try { Task.Delay(TimeSpan.FromSeconds(10), ct).Wait(ct); }
            catch (OperationCanceledException) { cancelled.Set(); throw; }
            catch (AggregateException) { cancelled.Set(); throw new OperationCanceledException(); }
            var seed = LevelSeed.Derive(salt, level, reroll, 0);
            return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
        });
        rig.Progress.DevSetHighestCleared(39);

        rig.Levels.PrefetchAfter(40);
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        rig.Levels.CancelPrefetches();

        Assert.True(cancelled.Wait(TimeSpan.FromSeconds(5)));
        await WaitUntil(() => !rig.Levels.HasRoll(41, 0));
    }

    [Fact]
    public async Task A_roll_somebody_is_waiting_for_is_not_cancelled()
    {
        var rig = new Rig(roll: (level, salt, reroll, ct) =>
        {
            Thread.Sleep(150);
            ct.ThrowIfCancellationRequested();
            var seed = LevelSeed.Derive(salt, level, reroll, 0);
            return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
        });
        rig.Progress.DevSetHighestCleared(39);

        var pending = rig.Levels.ResolveAsync(40);
        rig.Levels.CancelPrefetches();

        Assert.Equal(LevelSeed.Derive(Salt, 40, 0, 0), await pending);
    }

    [Fact]
    public async Task Rolling_with_the_real_gate_gives_a_verified_level()
    {
        var rig = new Rig(roll: null);
        rig.Progress.DevSetHighestCleared(11);

        // Level 12, the real search (no fake): the level it stores is the one the engine will build for that seed.
        var realLevels = new StoryLevels(rig.Settings, rig.Progress, rig.Identity);
        var seed = await realLevels.ResolveAsync(12);
        var level = LevelCatalog.Get(12, seed);

        Assert.Equal(seed.Value, level.Seed);
        Assert.True(LevelCatalog.IsCached(12, seed));
        Assert.True(LevelSolver.Solve(level with { MoveLimit = LevelCatalog.SolverCap }, LevelCatalog.SolverCap).Won);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > end) throw new TimeoutException("Condition not met in time.");
            await Task.Delay(10);
        }
    }
}
