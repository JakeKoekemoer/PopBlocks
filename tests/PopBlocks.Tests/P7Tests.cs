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
}
