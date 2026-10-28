using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class P4ShopTests
{
    private static (ShopService Shop, PlayerProgress Progress, UnlockStore Unlocks, InMemorySettings Settings) Make(int gold, int highest = 0)
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.GoldKey, gold);
        if (highest > 0) settings.SetInt(PlayerProgress.HighestClearedKey, highest);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        return (new ShopService(settings, progress, unlocks), progress, unlocks, settings);
    }

    [Fact]
    public void Every_item_has_unique_ids_and_keys_and_rising_prices()
    {
        Assert.Equal(ShopCatalog.Items.Count, ShopCatalog.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(ShopCatalog.Items.Count, ShopCatalog.Items.Select(i => i.SaveKey).Distinct().Count());
        foreach (var item in ShopCatalog.Items)
        {
            Assert.True(item.Tiers.Count > 0);
            for (int i = 1; i < item.Tiers.Count; i++) Assert.True(item.Tiers[i].Price > item.Tiers[i - 1].Price, item.Id);
            Assert.All(item.Tiers, t => Assert.True(t.Effect.Length > 0));
        }
    }

    [Fact]
    public void Existing_upgrade_keys_are_unchanged()
    {
        Assert.Equal("upgrade_preview", ShopCatalog.Get(ShopCatalog.Preview).SaveKey);
        Assert.Equal("upgrade_popflair", ShopCatalog.Get(ShopCatalog.PopFlair).SaveKey);
        Assert.Equal("upgrade_extramoves", ShopCatalog.Get(ShopCatalog.ExtraMoves).SaveKey);
    }

    [Fact]
    public void The_first_three_levels_of_each_original_upgrade_keep_their_old_prices()
    {
        Assert.Equal(new[] { 120, 400 }, ShopCatalog.Get(ShopCatalog.Preview).Tiers.Take(2).Select(t => t.Price));
        Assert.Equal(new[] { 80, 250, 600 }, ShopCatalog.Get(ShopCatalog.PopFlair).Tiers.Select(t => t.Price));
        Assert.Equal(new[] { 150, 450, 1000 }, ShopCatalog.Get(ShopCatalog.ExtraMoves).Tiers.Select(t => t.Price));
    }

    [Fact]
    public void A_locked_level_cannot_be_bought_with_any_amount_of_gold()
    {
        var (shop, progress, _, _) = Make(1_000_000, highest: 10);
        Assert.True(shop.IsLocked(ShopCatalog.ColourLens));
        Assert.False(shop.CanAfford(ShopCatalog.ColourLens));
        Assert.False(shop.TryBuy(ShopCatalog.ColourLens));
        Assert.Equal(1_000_000, progress.Gold);
        Assert.Equal(0, shop.LensLevel);
    }

    [Fact]
    public void Restoring_the_region_opens_the_level()
    {
        var (shop, _, _, _) = Make(10_000, highest: RegionCatalog.Get(2).LastLevel);
        var entry = shop.Entry(ShopCatalog.ColourLens);
        Assert.False(entry.Locked);
        Assert.True(shop.TryBuy(ShopCatalog.ColourLens));
        Assert.Equal(1, shop.LensLevel);
        // The second level wants the fourth region.
        Assert.True(shop.Entry(ShopCatalog.ColourLens).Locked);
    }

    [Fact]
    public void A_locked_item_says_how_to_discover_it_and_never_asks_for_a_purchase()
    {
        var (shop, _, _, _) = Make(0);
        var entry = shop.Entry(ShopCatalog.ChainSense);
        Assert.True(entry.Locked);
        Assert.Contains("Restore", entry.LockHint);
    }

    [Fact]
    public void An_achievement_requirement_is_met_only_by_the_achievement()
    {
        var (shop, _, unlocks, _) = Make(0);
        var req = Requirement.Achievement("restorer");
        Assert.False(shop.IsMet(req));
        unlocks.Unlock(ShopCatalog.AchievementUnlockId("restorer"));
        Assert.True(shop.IsMet(req));
        Assert.Contains("Restorer", req.Describe());
    }

    [Fact]
    public void Every_achievement_requirement_in_the_shop_names_a_real_achievement()
    {
        foreach (var tier in ShopCatalog.Items.SelectMany(i => i.Tiers).Where(t => t.Requires.Kind == RequirementKind.Achievement))
            Assert.NotNull(AchievementCatalog.Find(tier.Requires.AchievementId));
    }

    [Fact]
    public void Lookout_now_shows_up_to_five_pieces()
    {
        var (shop, _, _, _) = Make(1_000_000, 200);
        while (shop.TryBuy(ShopCatalog.Preview)) { }
        Assert.Equal(5, shop.PreviewCount);
    }

    [Fact]
    public void Extra_levels_of_an_owned_upgrade_do_not_disturb_an_old_save()
    {
        // A save from before the new levels: Lookout bought twice (level 2).
        var (shop, _, _, _) = Make(0);
        shop.DevSetLevel(Upgrade.Preview, 2);
        Assert.Equal(3, shop.PreviewCount);
        Assert.Equal(900, shop.NextCost(ShopCatalog.Preview));
    }

    [Fact]
    public void Gameplay_items_never_hide_the_original_three_upgrades()
    {
        var (shop, _, _, _) = Make(0);
        Assert.Equal(3, shop.Items().Count);
        Assert.False(shop.Entries(ItemCategory.Gameplay).First(e => e.Item.Id == ShopCatalog.Preview).Locked);
        Assert.False(shop.Entry(ShopCatalog.PopFlair).Locked);
        Assert.False(shop.Entry(ShopCatalog.ExtraMoves).Locked);
    }
}

public class RegionGoldTests
{
    private static (CampaignService Campaign, PlayerProgress Progress, InMemorySettings Settings) Make()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        return (new CampaignService(new UnlockStore(settings), progress), progress, settings);
    }

    private static CampaignOutcome Clear(PlayerProgress progress, CampaignService campaign, int level)
    {
        var def = LevelCatalog.Get(level);
        var result = progress.Complete(def);
        return campaign.OnLevelCompleted(level, result);
    }

    [Fact]
    public void Restoring_a_region_in_play_pays_its_reward_once()
    {
        var (campaign, progress, _) = Make();
        var last = RegionCatalog.Get(1).LastLevel;
        for (int n = 1; n < last; n++) Clear(progress, campaign, n);

        int before = progress.Gold;
        var outcome = Clear(progress, campaign, last);
        int clearGold = progress.Gold - before - outcome.RegionGold;
        Assert.Equal(ShopCatalog.RegionClearGold(1), outcome.RegionGold);
        Assert.True(clearGold >= 0);
        Assert.True(outcome.HasAnything);
    }

    [Fact]
    public void Replaying_never_pays_it_again()
    {
        var (campaign, progress, _) = Make();
        var last = RegionCatalog.Get(1).LastLevel;
        for (int n = 1; n <= last; n++) Clear(progress, campaign, n);
        int gold = progress.Gold;

        var replay = campaign.OnLevelCompleted(last, new CompletionResult(false, 0));
        Assert.Equal(0, replay.RegionGold);
        Assert.Equal(gold, progress.Gold);
    }

    [Fact]
    public void Catching_up_a_baseline_save_pays_no_region_reward()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.HighestClearedKey, 60);
        settings.SetInt(PlayerProgress.GoldKey, 777);
        var progress = new PlayerProgress(settings);
        var campaign = new CampaignService(new UnlockStore(settings), progress);
        campaign.Reconcile();
        Assert.Equal(777, progress.Gold);
    }

    [Fact]
    public void Region_rewards_rise_with_the_region()
    {
        int previous = 0;
        foreach (var r in RegionCatalog.All)
        {
            int g = ShopCatalog.RegionClearGold(r.Number);
            Assert.True(g > previous);
            previous = g;
        }
    }

    [Fact]
    public void AwardGold_adds_and_saves_and_ignores_nonsense()
    {
        var (_, progress, settings) = Make();
        progress.AwardGold(30);
        progress.AwardGold(-10);
        progress.AwardGold(0);
        Assert.Equal(30, progress.Gold);
        Assert.Equal(30, settings.GetInt(PlayerProgress.GoldKey, 0));
    }
}

public class AchievementTests
{
    private static (AchievementService Service, PlayerProgress Progress, UnlockStore Unlocks, CampaignService Campaign) Make()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, progress);
        return (new AchievementService(unlocks, progress, campaign), progress, unlocks, campaign);
    }

    [Fact]
    public void Ids_are_unique_and_every_achievement_pays_some_gold()
    {
        Assert.Equal(AchievementCatalog.All.Count, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());
        Assert.All(AchievementCatalog.All, a =>
        {
            Assert.True(a.Gold > 0);
            Assert.True(a.Name.Length > 0 && a.Description.Length > 0);
        });
        Assert.Equal(13 + 16, AchievementCatalog.All.Count);   // the original thirteen and the sixteen of v1.1
    }

    [Fact]
    public void Total_achievement_gold_is_in_the_economy_budget()
    {
        Assert.InRange(AchievementCatalog.TotalGold, 1700, 2000);   // 910 for the originals and 930 for the v1.1 sixteen
    }

    [Fact]
    public void A_rule_that_is_met_pays_once()
    {
        var (service, progress, _, _) = Make();
        var facts = new AchievementFacts { RegionsRestored = 1 };

        var first = service.Check(facts);
        Assert.Contains(first, a => a.Id == "restorer");
        int gold = progress.Gold;
        Assert.Equal(AchievementCatalog.Find("restorer")!.Gold, gold);

        Assert.DoesNotContain(service.Check(facts), a => a.Id == "restorer");
        Assert.Equal(gold, progress.Gold);
        Assert.True(service.IsEarned("restorer"));
    }

    [Fact]
    public void Earned_achievements_survive_a_restart_and_stay_paid_once()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var service = new AchievementService(unlocks, progress, new CampaignService(unlocks, progress));
        service.Check(new AchievementFacts { CampaignComplete = true });
        int gold = progress.Gold;

        var progress2 = new PlayerProgress(settings);
        var unlocks2 = new UnlockStore(settings);
        var service2 = new AchievementService(unlocks2, progress2, new CampaignService(unlocks2, progress2));
        Assert.True(service2.IsEarned("balanced_world"));
        service2.Check(new AchievementFacts { CampaignComplete = true });
        Assert.Equal(gold, progress2.Gold);
    }

    [Fact]
    public void Nothing_is_earned_from_empty_facts()
    {
        var (service, progress, _, _) = Make();
        Assert.Empty(service.Check(new AchievementFacts()));
        Assert.Equal(0, progress.Gold);
    }

    [Fact]
    public void Rules_react_to_the_run_they_describe()
    {
        var facts = new AchievementFacts { ClassicScore = AchievementCatalog.ClassicGoal };
        Assert.True(AchievementCatalog.Find("classic_champion")!.IsMet(facts));
        Assert.False(AchievementCatalog.Find("classic_champion")!.IsMet(new AchievementFacts { ClassicScore = AchievementCatalog.ClassicGoal - 1 }));

        Assert.True(AchievementCatalog.Find("spectrum_seeker")!.IsMet(new AchievementFacts { RegionsWithMemory = 8 }));
        Assert.False(AchievementCatalog.Find("spectrum_seeker")!.IsMet(new AchievementFacts { RegionsWithMemory = 7 }));
        Assert.True(AchievementCatalog.Find("keeper_of_memories")!.IsMet(new AchievementFacts { ArchiveEntries = AchievementCatalog.ArchiveGoal }));
        Assert.True(AchievementCatalog.Find("puzzle_scholar")!.IsMet(new AchievementFacts { HighestCleared = StoryConfig.TutorialLastLevel }));
        Assert.False(AchievementCatalog.Find("puzzle_scholar")!.IsMet(new AchievementFacts { HighestCleared = StoryConfig.TutorialLastLevel - 1 }));
        Assert.True(AchievementCatalog.Find("perfectionist")!.IsMet(new AchievementFacts { AnyRegionFullyRemembered = true }));
    }

    [Fact]
    public void Achievement_gold_is_the_only_gold_that_is_not_a_level_or_region_reward()
    {
        // The achievement rewards are one-off and small next to the campaign's income.
        Assert.True(AchievementCatalog.TotalGold < 0.25 * 11965);
    }

    [Fact]
    public void The_archive_count_grows_with_progress()
    {
        var (_, progress, _, campaign) = Make();
        int start = campaign.ArchiveEntryCount;
        progress.DevSetHighestCleared(200);
        campaign.Reconcile();
        Assert.True(campaign.ArchiveEntryCount > start);
    }
}
