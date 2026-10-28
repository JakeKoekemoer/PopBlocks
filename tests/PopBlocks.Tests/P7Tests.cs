using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The rules that make the game fair and the save safe, kept as permanent tests (plan step "compat_and_fairness").</summary>
public class P7Tests
{
    // Every key that has ever been on a player's phone, as the literal string. A key may be added, never renamed or removed:
    // if this test fails, a player's saved data would be lost or misread.
    private static readonly string[] FrozenKeys =
    {
        "save_version", "progress_highest", "gold",
        "upgrade_preview", "upgrade_popflair", "upgrade_extramoves", "upgrade_colourlens", "upgrade_chainsense",
        "best_score", "blast_effects", "show_ghost", "controls_collapsed", "sound_effects", "landing_guide_style", "reduced_motion",
        "unlocks_v1", "cos_board", "cos_skin", "cos_burst", "cos_frame", "cos_pip", "ending_theme",
        "stat_largest_group", "stat_longest_chain", "stat_most_cleared", "stat_most_moves_left", "stat_abilities_used",
        "stat_boards_cleared", "stat_cells_popped", "stat_levels_won", "stat_most_goals",
        "stat_plays_r1", "stat_plays_r2", "stat_plays_r3", "stat_plays_r4", "stat_plays_r5", "stat_plays_r6", "stat_plays_r7", "stat_plays_r8",
        "upgrade_prismreserve", "consumable_second_chance", "consumable_prism_shift",
        // Seeded levels, rerolls and Classic gold.
        "player_name", "player_salt", "current_level_for", "current_level_seed", "current_level_rerolls", "current_level_failures",
        "first_clear_seeds", "classic_gold_carry",
        // Audio settings (the soundtrack update). Sound effects on/off stays the older "sound_effects" key.
        "audio_master", "audio_music", "audio_sfx", "audio_ambience", "audio_mute_master", "audio_mute_music", "audio_mute_ambience", "audio_reduced", "audio_track",
        // v1.1 (overnight build). Add-only.
        "stat_rerolls", "level_losses", "eased_levels",
    };

    [Fact]
    public void NoSaveKeyRenamed()
    {
        foreach (var key in FrozenKeys)
            Assert.Contains(key, SaveKeys.All);
        foreach (var key in SaveKeys.All)
            Assert.True(FrozenKeys.Contains(key), $"'{key}' is a new key: add it to the frozen list on purpose, in the release that ships it");
    }

    [Fact]
    public void Shop_items_use_the_keys_they_always_used()
    {
        Assert.Equal("upgrade_preview", ShopCatalog.Get(ShopCatalog.Preview).SaveKey);
        Assert.Equal("upgrade_popflair", ShopCatalog.Get(ShopCatalog.PopFlair).SaveKey);
        Assert.Equal("upgrade_extramoves", ShopCatalog.Get(ShopCatalog.ExtraMoves).SaveKey);
        Assert.Equal("upgrade_colourlens", ShopCatalog.Get(ShopCatalog.ColourLens).SaveKey);
        Assert.Equal("upgrade_chainsense", ShopCatalog.Get(ShopCatalog.ChainSense).SaveKey);
        Assert.Equal("upgrade_prismreserve", ShopCatalog.Get(ShopCatalog.PrismReserve).SaveKey);
        Assert.Equal("consumable_second_chance", ConsumableCatalog.Get(Consumable.SecondChance).SaveKey);
        Assert.Equal("consumable_prism_shift", ConsumableCatalog.Get(Consumable.PrismShift).SaveKey);
    }

    private sealed class Run
    {
        public readonly InMemorySettings Settings = new();
        public readonly PlayerProgress Progress;
        public readonly UnlockStore Unlocks;
        public readonly CampaignService Campaign;
        public readonly EndingService Ending;

        public Run()
        {
            Progress = new PlayerProgress(Settings);
            Unlocks = new UnlockStore(Settings);
            Campaign = new CampaignService(Unlocks, Progress);
            Ending = new EndingService(Settings, Progress, Unlocks, Campaign);
        }

        /// <summary>Clears one level the way the game does when it is won (first clear, or a replay of a cleared level).</summary>
        public CampaignOutcome Clear(int level)
        {
            var result = Progress.Complete(LevelCatalog.Get(level));
            return Campaign.OnLevelCompleted(level, result);
        }
    }

    [Fact]
    public void EndingReachable_WithNoPurchasesAndNoOptionalCollectibles()
    {
        var run = new Run();
        CampaignOutcome? last = null;
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++) last = run.Clear(n);

        Assert.Equal(RegionCatalog.LastCampaignLevel, run.Progress.HighestCleared);
        Assert.Contains(last!.Scenes, s => s.Id == "r8_complete");
        Assert.True(run.Ending.NeedsChoice);
        Assert.Equal(0, run.Campaign.TotalMemoriesFound);          // not one optional memory
        Assert.Equal(0, run.Settings.GetInt(SaveKeys.UpgradePreview, 0));   // not one purchase
        Assert.True(run.Ending.Choose(StoryContent.FinalChoices[0].Id));
    }

    [Fact]
    public void ReplayedLevels_NeverDuplicateRewards()
    {
        var run = new Run();
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++) run.Clear(n);
        int gold = run.Progress.Gold, unlocks = run.Unlocks.Count, highest = run.Progress.HighestCleared;

        for (int pass = 0; pass < 2; pass++)
            for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++)
            {
                var outcome = run.Clear(n);
                Assert.False(outcome.HasAnything, $"replaying level {n} produced a reward");
            }

        Assert.Equal(gold, run.Progress.Gold);
        Assert.Equal(unlocks, run.Unlocks.Count);
        Assert.Equal(highest, run.Progress.HighestCleared);
    }

    [Fact]
    public void A_scene_skipped_or_a_reconcile_run_twice_never_pays_again()
    {
        var run = new Run();
        for (int n = 1; n <= 75; n++) run.Clear(n);
        int gold = run.Progress.Gold;
        run.Campaign.Reconcile();
        run.Campaign.Reconcile();
        Assert.Equal(gold, run.Progress.Gold);
    }

    [Fact]
    public void ShopPricesAndIncome_AllItemsAffordableByLevel200_NoItemRequiresReplay()
    {
        var run = new Run();
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++) run.Clear(n);
        int earned = run.Progress.Gold;   // level gold and region rewards only: achievements are a bonus on top

        int shop = ShopCatalog.Items.SelectMany(i => i.Tiers).Sum(t => t.Price);
        int looks = CosmeticCatalog.TotalPrice;
        Assert.True(shop + looks <= earned, $"everything for sale costs {shop + looks}, the campaign pays {earned}");

        // Every requirement is met by finishing the campaign: nothing waits on a replay or an optional collectible.
        int highest = run.Progress.HighestCleared;
        foreach (var tier in ShopCatalog.Items.SelectMany(i => i.Tiers))
            Assert.True(tier.Requires.Kind != RequirementKind.Achievement || AchievementCatalog.Find(tier.Requires.AchievementId) is not null);
        foreach (var tier in ShopCatalog.Items.SelectMany(i => i.Tiers).Where(t => t.Requires.Kind != RequirementKind.Achievement))
            Assert.True(tier.Requires.IsMetBy(highest, run.Unlocks));
        foreach (var info in ConsumableCatalog.All)
            Assert.True(info.Requires.IsMetBy(highest, run.Unlocks));
        foreach (var c in CosmeticCatalog.All.Where(c => c.Discover.Kind != RequirementKind.Achievement))
            Assert.True(c.Discover.IsMetBy(highest, run.Unlocks), c.Id);
    }

    [Fact]
    public void Every_achievement_a_shop_item_or_look_asks_for_exists()
    {
        foreach (var req in ShopCatalog.Items.SelectMany(i => i.Tiers).Select(t => t.Requires)
                     .Concat(CosmeticCatalog.All.Select(c => c.Discover))
                     .Concat(ConsumableCatalog.All.Select(c => c.Requires))
                     .Where(r => r.Kind == RequirementKind.Achievement))
            Assert.NotNull(AchievementCatalog.Find(req.AchievementId));
    }

    [Fact]
    public void Helpers_and_looks_are_never_needed_to_play_a_level()
    {
        // A fresh install holds nothing and a level starts with the rules the level file gives it.
        var engine = new GameEngine(new ScriptedGenerator());
        engine.StartLevel(LevelCatalog.Get(60));
        Assert.Equal(0, engine.SecondChanceStock);
        Assert.Equal(0, engine.PrismShiftStock);
        Assert.Equal(0, engine.PrismFreeCharges);
        Assert.Equal(0, engine.ExtraMoves);
        Assert.Equal(1, engine.PreviewCount);
        Assert.Equal(LevelCatalog.Get(60).MoveLimit, engine.MoveLimit);
    }

    // ------------------------------------------------------------------ gold never gates the story (seeded levels and rerolls)

    [Fact]
    public void Every_level_1_to_220_has_a_free_reroll_at_ten_failures_and_a_price_that_never_exceeds_its_reward()
    {
        for (int level = 1; level <= 220; level++)
        {
            Assert.Equal(0, RerollPricing.Price(level, SeedingConfig.FailuresForFreeReroll));
            Assert.Equal(0, RerollPricing.Price(level, SeedingConfig.FailuresForFreeReroll + 25));
            Assert.InRange(RerollPricing.Price(level, 0), 0, GoldRewards.For(level, false));
        }
    }

    [Fact]
    public void A_player_with_no_gold_gets_through_any_level_by_failing_it_ten_times()
    {
        foreach (int level in new[] { 1, 10, 11, 12, 25, 50, 75, 100, 125, 150, 175, 199, 200, 201, 345 })
        {
            var settings = new InMemorySettings();
            var progress = new PlayerProgress(settings);
            progress.DevSetHighestCleared(level - 1);
            progress.DevSetGold(0);
            var identity = new PlayerIdentity(settings, () => 5);
            identity.Choose("Broke");
            var levels = new StoryLevels(settings, progress, identity);

            for (int i = 1; i < SeedingConfig.FailuresForFreeReroll; i++)
            {
                levels.RecordFailure(level);
                if (RerollPricing.Price(level, i) > 0) Assert.False(levels.CanAffordReroll(level), $"level {level}, {i} failures");
            }
            levels.RecordFailure(level);

            Assert.True(levels.CanAffordReroll(level), $"level {level} should be free after ten failures");
            Assert.True(levels.TryReroll(level));
            Assert.Equal(0, progress.Gold);
        }
    }

    [Fact]
    public void Failing_on_purpose_is_allowed_and_nothing_stops_a_cleared_level_being_rerolled_for_free()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        progress.DevSetHighestCleared(120);
        var identity = new PlayerIdentity(settings, () => 5);
        identity.Choose("Tester");
        var levels = new StoryLevels(settings, progress, identity);

        for (int level = 1; level <= 120; level += 7)
        {
            Assert.True(levels.CanOfferReroll(level));
            Assert.Equal(0, levels.RerollPrice(level));
        }
    }

    [Fact]
    public void Nothing_in_the_story_costs_gold_to_continue()
    {
        // The only thing that spends gold on the way is the reroll, and it is free from the tenth failure.
        // A level's completion never asks for gold and the ending needs no purchase (see EndingReachable_...).
        for (int level = 1; level <= 220; level++)
        {
            var settings = new InMemorySettings();
            var progress = new PlayerProgress(settings);
            progress.DevSetHighestCleared(level - 1);
            Assert.True(progress.IsUnlocked(level));
            Assert.Equal(level, progress.NextLevel);
        }
    }
}
