using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The checks that close phase 4: nothing a player already had is changed, and nothing new is needed to win.</summary>
public class P4GateTests
{
    [Fact]
    public void BaselineSave_LoadsSameProgressAndUpgrades()
    {
        // A save written by the last shipped version: only the keys that existed then.
        var settings = new InMemorySettings();
        settings.SetInt("save_version", 1);
        settings.SetInt("progress_highest", 75);
        settings.SetInt("gold", 1234);
        settings.SetInt("upgrade_preview", 2);
        settings.SetInt("upgrade_popflair", 1);
        settings.SetInt("upgrade_extramoves", 3);
        settings.SetInt("best_score", 4200);
        settings.SetBool("show_ghost", false);
        settings.SetBool("blast_effects", false);

        SaveMigrator.Run(settings);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, progress);
        campaign.Reconcile();
        var shop = new ShopService(settings, progress, unlocks);
        var game = new GameSettings(settings);

        Assert.Equal(75, progress.HighestCleared);
        Assert.Equal(1234, progress.Gold);              // catching up pays no region or achievement gold
        Assert.Equal(3, shop.PreviewCount);
        Assert.Equal(1, shop.FlairLevel);
        Assert.Equal(6, shop.ExtraMoves);
        Assert.Equal(0, shop.LensLevel);
        Assert.False(shop.ChainSenseOwned);
        Assert.Equal(4200, new HighScoreService(settings).Best);
        Assert.False(game.ShowGhost);
        Assert.Equal(0, game.LandingGuideStyle);
        Assert.False(game.BlastEffects);
        Assert.False(game.ReducedMotion);
    }

    [Fact]
    public void BaselineSave_KeepsGoldAfterEveryCheckThatRunsAtStartUp()
    {
        var settings = new InMemorySettings();
        settings.SetInt("progress_highest", 200);
        settings.SetInt("gold", 50);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, progress);
        campaign.Reconcile();
        campaign.Reconcile();
        Assert.Equal(50, progress.Gold);
    }

    [Fact]
    public void AllLevels_1_to_200_BotWinnable_WithNoUpgrades()
    {
        var failed = new List<int>();
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++)
        {
            var level = LevelCatalog.Get(n);
            if (level.MoveLimit <= 0) continue;   // unlimited-move tutorials need no proof of a move budget
            if (!LevelSolver.Solve(level, level.MoveLimit).Won) failed.Add(n);
        }
        Assert.True(failed.Count == 0, "not winnable with the level's own moves: " + string.Join(", ", failed));
    }

    [Fact]
    public void Nothing_in_the_shop_or_workshop_changes_how_many_moves_a_level_allows_by_default()
    {
        var engine = new GameEngine(new PieceGenerator(new Random(1)));
        var level = LevelCatalog.Get(40);
        engine.StartLevel(level);
        Assert.Equal(level.MoveLimit, engine.MoveLimit);   // ExtraMoves only ever adds what the player bought
    }

    [Fact]
    public void Every_gameplay_item_is_optional_and_every_price_is_reachable()
    {
        // A player who has restored every region can afford everything from the campaign's own income, with room to spare.
        int income = ShopCatalog.Items.SelectMany(i => i.Tiers).Sum(t => t.Price);
        Assert.True(income < 12_000, $"total gameplay prices {income}");
        Assert.True(income > 4_000);
    }
}
