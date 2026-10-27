using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The text the levels page shows for a seed, and the dev-menu helpers for seeds and failures.</summary>
public class SeededDevAndPageTests
{
    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public readonly UnlockStore Unlocks;
        public readonly PlayerProgress Progress;
        public readonly PlayerIdentity Identity;
        public readonly StoryLevels Levels;
        public readonly DevTools Dev;

        public Rig()
        {
            Unlocks = new UnlockStore(Settings);
            Progress = new PlayerProgress(Settings);
            Identity = new PlayerIdentity(Settings, () => 77);
            Levels = new StoryLevels(Settings, Progress, Identity);
            var shop = new ShopService(Settings, Progress);
            var campaign = new CampaignService(Unlocks, Progress);
            var loadout = new CosmeticLoadout(Settings, Unlocks);
            Dev = new DevTools(Progress, shop, Unlocks, campaign, loadout, levels: Levels, identity: Identity);
        }
    }

    [Fact]
    public async Task The_levels_page_shows_a_seed_for_cleared_and_current_levels_and_nothing_for_locked_ones()
    {
        var r = new Rig();
        r.Identity.Choose("Tester");
        r.Progress.DevSetHighestCleared(12);
        r.Settings.SetString(SaveKeys.FirstClearSeeds, "12:483920117");

        Assert.Equal("Seed 483920117", r.Levels.SeedText(12));
        Assert.Equal(483920117, r.Levels.SeedNumber(12));
        Assert.Equal("Original", r.Levels.SeedText(5));          // cleared before seeds existed
        Assert.Null(r.Levels.SeedNumber(5));
        Assert.Equal("Seed given when you start", r.Levels.SeedText(13));   // current, not assigned yet
        Assert.Equal(string.Empty, r.Levels.SeedText(14));       // locked
        Assert.Equal(string.Empty, r.Levels.SeedText(500));

        var seed = await r.Levels.ResolveAsync(13);
        Assert.Equal("Seed " + seed.Value, r.Levels.SeedText(13));
        Assert.Equal(seed.Value, r.Levels.SeedNumber(13));
    }

    [Fact]
    public void The_dev_menu_can_set_failures_on_the_current_level()
    {
        var r = new Rig();
        r.Progress.DevSetHighestCleared(59);

        r.Dev.SetFailures(9);
        Assert.Equal(9, r.Levels.Failures(60));
        r.Dev.SetFailures(10);
        Assert.Equal(0, r.Levels.RerollPrice(60));
        r.Dev.SetFailures(-4);
        Assert.Equal(0, r.Levels.Failures(60));
    }

    [Fact]
    public void Reset_everything_gives_a_fresh_install_that_asks_for_a_name_again()
    {
        var r = new Rig();
        r.Identity.Choose("Someone");
        r.Progress.DevSetHighestCleared(40);
        r.Settings.SetString(SaveKeys.FirstClearSeeds, "5:55,6:66");
        r.Dev.SetFailures(3);

        r.Dev.ResetEverything();

        Assert.True(r.Identity.NeedsPrompt);
        Assert.Equal(string.Empty, r.Settings.GetString(SaveKeys.FirstClearSeeds, "x"));
        Assert.Equal(0, r.Settings.GetInt(SaveKeys.CurrentLevelFailures, -1));
        Assert.Equal(0, r.Progress.HighestCleared);
        Assert.Equal(0, r.Settings.GetInt(SaveKeys.PlayerSalt, -1));
    }

    [Fact]
    public void The_dev_description_mentions_the_name_seed_and_failures()
    {
        var r = new Rig();
        r.Identity.Choose("Describer");
        string text = r.Dev.Describe();
        Assert.Contains("Describer", text);
        Assert.Contains("Failures 0", text);
    }

    [Fact]
    public void Timing_the_gate_reports_every_tier()
    {
        var timings = GateTiming.Measure(rollsPerTier: 2);
        Assert.Equal(4, timings.Count);
        Assert.All(timings, t =>
        {
            Assert.Equal(2, t.Rolls);
            Assert.True(t.P50Ms >= 0 && t.MaxMs >= t.P50Ms);
        });
        string report = GateTiming.Describe(timings);
        Assert.Contains("Extreme", report);
        Assert.Contains("median", report);
    }
}
