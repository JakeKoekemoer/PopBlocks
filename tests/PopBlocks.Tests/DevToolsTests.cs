using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class DevToolsTests
{
    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public readonly UnlockStore Unlocks;
        public readonly PlayerProgress Progress;
        public readonly ShopService Shop;
        public readonly CampaignService Campaign;
        public readonly CosmeticLoadout Loadout;
        public readonly DevTools Dev;

        public Rig()
        {
            Unlocks = new UnlockStore(Settings);
            Progress = new PlayerProgress(Settings);
            Shop = new ShopService(Settings, Progress);
            Campaign = new CampaignService(Unlocks, Progress);
            Loadout = new CosmeticLoadout(Settings, Unlocks);
            Dev = new DevTools(Progress, Shop, Unlocks, Campaign, Loadout);
        }
    }

    [Fact]
    public void Gold_can_be_added_and_set_and_never_goes_negative()
    {
        var r = new Rig();
        r.Dev.AddGold(1000);
        Assert.Equal(1000, r.Progress.Gold);
        r.Dev.AddGold(-5000);
        Assert.Equal(0, r.Progress.Gold);
        r.Dev.SetGold(250);
        Assert.Equal(250, new PlayerProgress(r.Settings).Gold);   // saved
    }

    [Fact]
    public void Set_next_level_makes_that_level_the_next_one_and_saves_it()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(25);
        Assert.Equal(25, r.Progress.NextLevel);
        Assert.Equal(25, new PlayerProgress(r.Settings).NextLevel);
    }

    [Fact]
    public void Jumping_to_level_25_leaves_region_one_unrestored_so_its_ending_can_be_tested()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(25);
        Assert.Equal(0, r.Campaign.FragmentCount);
        Assert.Equal(RegionState.Recovering, r.Campaign.ProgressOf(RegionCatalog.Get(1)).State);

        var outcome = r.Campaign.OnLevelCompleted(25, new CompletionResult(true, 0));
        Assert.True(outcome.FragmentAwarded);
    }

    [Fact]
    public void Jumping_past_region_one_grants_its_rewards_but_keeps_the_scenes_readable()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(40);
        Assert.Equal(1, r.Campaign.FragmentCount);
        Assert.True(r.Unlocks.Has(CampaignService.CosmeticGarden));
        Assert.False(r.Campaign.HasSeen("r1_open"));
    }

    [Fact]
    public void Jumping_back_resets_story_flags_so_the_opening_plays_again()
    {
        var r = new Rig();
        r.Campaign.MarkSeen("r1_open");
        r.Dev.SetNextLevel(1);
        Assert.NotNull(r.Campaign.OnLevelEntered(1));
        Assert.Equal(0, r.Campaign.FragmentCount);
    }

    [Fact]
    public void Max_shop_buys_every_upgrade_without_gold()
    {
        var r = new Rig();
        r.Dev.MaxShop();
        Assert.All(Enum.GetValues<Upgrade>(), u => Assert.Equal(r.Shop.MaxLevel(u), r.Shop.Level(u)));
        Assert.Equal(0, r.Progress.Gold);
    }

    [Fact]
    public void Max_shop_buys_every_catalogue_item_including_the_new_helpers()
    {
        var r = new Rig();
        r.Dev.MaxShop();
        Assert.All(ShopCatalog.Items, i => Assert.Equal(i.MaxLevel, r.Shop.Level(i.Id)));
        r.Dev.ResetEverything();
        Assert.All(ShopCatalog.Items, i => Assert.Equal(0, r.Shop.Level(i.Id)));
    }

    [Fact]
    public void Unlock_all_looks_makes_every_cosmetic_wearable_and_reset_takes_them_off()
    {
        var r = new Rig();
        r.Dev.UnlockAllLooks();
        Assert.All(CosmeticCatalog.All, c => Assert.True(r.Loadout.IsOwned(c), c.Id));
        Assert.True(r.Loadout.Select(CosmeticSlot.Skin, "skin_citadel"));

        r.Dev.ResetEverything();
        Assert.Equal(CosmeticCatalog.DefaultSkin, r.Loadout.Selected(CosmeticSlot.Skin));
        Assert.False(r.Loadout.IsOwned("skin_citadel"));
    }

    [Fact]
    public void Reset_everything_returns_to_a_fresh_save()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(60);
        r.Dev.AddGold(500);
        r.Dev.MaxShop();
        r.Loadout.Select("garden");

        r.Dev.ResetEverything();

        Assert.Equal(1, r.Progress.NextLevel);
        Assert.Equal(0, r.Progress.Gold);
        Assert.Equal(0, r.Unlocks.Count);
        Assert.Equal(CosmeticLoadout.DefaultId, r.Loadout.SelectedBoard);
        Assert.All(Enum.GetValues<Upgrade>(), u => Assert.Equal(0, r.Shop.Level(u)));
    }

    [Fact]
    public void Reset_story_flags_keeps_levels_and_gold()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(30);
        r.Dev.SetGold(77);
        r.Dev.ResetStoryFlags();
        Assert.Equal(30, r.Progress.NextLevel);
        Assert.Equal(77, r.Progress.Gold);
        Assert.Equal(0, r.Campaign.FragmentCount);
    }

    [Fact]
    public void Describe_mentions_the_current_state()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(25);
        string text = r.Dev.Describe();
        Assert.Contains("Next level 25", text);
        Assert.Contains("Forgotten Garden", text);
    }

    [Fact]
    public void Force_complete_ends_a_story_level_as_won_and_raises_the_event()
    {
        var game = new GameEngine(new PieceGenerator());
        var events = new List<GameEvent>();
        game.EventRaised += events.Add;
        game.Start(GameSetup.Story(12));

        Assert.True(game.ForceComplete());
        Assert.Equal(GameState.LevelComplete, game.State);
        Assert.Contains(GameEvent.LevelComplete, events);
        Assert.False(game.ForceComplete());   // already complete
    }

    [Fact]
    public void Force_complete_does_nothing_in_classic_or_before_a_game_starts()
    {
        var game = new GameEngine(new PieceGenerator());
        Assert.False(game.ForceComplete());
        game.Start(GameSetup.Classic);
        Assert.False(game.ForceComplete());
        Assert.NotEqual(GameState.LevelComplete, game.State);
    }

    [Fact]
    public void A_forced_win_pays_gold_and_story_rewards_through_the_normal_path()
    {
        var r = new Rig();
        r.Dev.SetNextLevel(25);
        var level = LevelCatalog.Get(25);

        var result = r.Progress.Complete(level);
        var outcome = r.Campaign.OnLevelCompleted(25, result);

        Assert.True(result.FirstClear);
        Assert.True(outcome.RegionRestored);
        Assert.Equal(26, r.Progress.NextLevel);
    }
}
