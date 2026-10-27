using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>One ending, then a purely cosmetic choice of how the world looks.</summary>
public class EndingTests
{
    private sealed record Rig(InMemorySettings Settings, PlayerProgress Progress, UnlockStore Unlocks,
        CampaignService Campaign, EndingService Ending);

    private static Rig Make(int highest = 0)
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        progress.DevSetHighestCleared(highest);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, progress);
        return new Rig(settings, progress, unlocks, campaign, new EndingService(settings, progress, unlocks, campaign));
    }

    [Fact]
    public void Nothing_can_be_chosen_before_the_campaign_is_complete()
    {
        var rig = Make(199);
        Assert.False(rig.Ending.CampaignComplete);
        Assert.False(rig.Ending.NeedsChoice);
        Assert.Empty(rig.Ending.Available);
        Assert.False(rig.Ending.Choose("lanterns"));
        Assert.False(rig.Ending.HasChosen);
    }

    [Fact]
    public void Clearing_level_200_asks_for_a_choice()
    {
        var rig = Make(200);
        Assert.True(rig.Ending.CampaignComplete);
        Assert.True(rig.Ending.NeedsChoice);
        Assert.Equal(3, rig.Ending.Available.Count);
    }

    [Fact]
    public void A_choice_is_saved_and_unlocks_its_cosmetic()
    {
        var rig = Make(200);
        Assert.True(rig.Ending.Choose("aurora"));

        Assert.True(rig.Ending.HasChosen);
        Assert.False(rig.Ending.NeedsChoice);
        Assert.Equal("aurora", rig.Ending.Choice!.Id);
        Assert.Equal("aurora", rig.Settings.GetString(SaveKeys.EndingTheme, ""));
        Assert.True(rig.Unlocks.Has("cosmetic_aurora"));
        Assert.False(rig.Unlocks.Has("cosmetic_lanterns"));
    }

    [Fact]
    public void The_choice_can_be_changed_later_and_every_look_stays_available()
    {
        var rig = Make(200);
        rig.Ending.Choose("aurora");
        int changes = 0;
        rig.Ending.Changed += () => changes++;

        Assert.True(rig.Ending.Choose("meadow"));
        Assert.Equal("meadow", rig.Ending.Choice!.Id);
        Assert.Equal(1, changes);

        Assert.True(rig.Ending.Choose("meadow"));   // picking the same look again changes nothing
        Assert.Equal(1, changes);

        Assert.True(rig.Ending.Choose("aurora"));   // and back: nobody is locked out
        Assert.True(rig.Ending.OwnsCosmetic(StoryContent.FindFinalChoice("meadow")!));
        Assert.True(rig.Ending.OwnsCosmetic(StoryContent.FindFinalChoice("aurora")!));
    }

    [Fact]
    public void An_unknown_look_is_refused_and_a_bad_saved_value_is_ignored()
    {
        var rig = Make(200);
        Assert.False(rig.Ending.Choose("rainbow"));
        Assert.False(rig.Ending.HasChosen);

        rig.Settings.SetString(SaveKeys.EndingTheme, "rainbow");
        Assert.Null(rig.Ending.Choice);
        Assert.True(rig.Ending.NeedsChoice);
    }

    [Fact]
    public void The_choice_survives_a_restart()
    {
        var rig = Make(200);
        rig.Ending.Choose("lanterns");

        var progress = new PlayerProgress(rig.Settings);
        var unlocks = new UnlockStore(rig.Settings);
        var again = new EndingService(rig.Settings, progress, unlocks, new CampaignService(unlocks, progress));
        Assert.Equal("lanterns", again.Choice!.Id);
        Assert.True(again.OwnsCosmetic(again.Choice));
    }

    [Fact]
    public void There_is_one_ending_whatever_the_choice()
    {
        // Every look has the same shape, and none of the text implies a different outcome.
        Assert.Equal(3, StoryContent.FinalChoices.Count);
        Assert.Equal(3, StoryContent.FinalChoices.Select(c => c.Id).Distinct().Count());
        var ending = Assert.Single(StoryContent.Scenes, s => s.Trigger == SceneTrigger.Ending && s.UnlockLevel == 200);
        Assert.Equal("r8_complete", ending.Id);

        string[] branching = { "bad ending", "good ending", "alternate", "if you chose", "other path", "had you chosen" };
        foreach (var c in StoryContent.FinalChoices)
        {
            string text = (c.Name + " " + c.Description + " " + c.EpilogueLine).ToLowerInvariant();
            foreach (var word in branching) Assert.DoesNotContain(word, text);
            Assert.True(c.EpilogueLine.Length > 10);
        }
    }

    [Fact]
    public void The_ending_does_not_depend_on_any_optional_collectible()
    {
        var rig = Make(199);
        var level = LevelCatalog.Get(200);
        var result = rig.Progress.Complete(level);
        Assert.True(result.FirstClear);

        var outcome = rig.Campaign.OnLevelCompleted(200, result);
        Assert.Contains(outcome.Scenes, s => s.Id == "r8_complete");
        Assert.True(outcome.RegionRestored);
        Assert.Equal(0, rig.Campaign.TotalMemoriesFound);
        Assert.True(rig.Ending.NeedsChoice);
    }

    [Fact]
    public void The_epilogue_is_due_only_after_a_choice_and_a_memory_in_every_region()
    {
        var rig = Make(200);
        Assert.Null(rig.Ending.EpilogueDue());

        foreach (var region in RegionCatalog.All)
            rig.Campaign.GrantLoreEntry(StoryContent.MemoriesOf(region.Number).First().Id);
        Assert.True(rig.Campaign.EpilogueUnlocked);
        Assert.Null(rig.Ending.EpilogueDue());   // not until the player has chosen

        rig.Ending.Choose("meadow");
        var due = rig.Ending.EpilogueDue();
        Assert.Equal(StoryContent.EpilogueSceneId, due!.Id);

        rig.Campaign.MarkSeen(due.Id);
        Assert.Null(rig.Ending.EpilogueDue());
        Assert.True(rig.Campaign.IsSceneAvailable(due));   // still readable in the Archive
    }

    [Fact]
    public void Levels_above_200_stay_endless_with_no_story_scenes()
    {
        var rig = Make(200);
        for (int n = 201; n <= 230; n++)
        {
            Assert.Null(rig.Campaign.OnLevelEntered(n));
            Assert.Null(rig.Campaign.ReplayRemark(n));
            var result = rig.Progress.Complete(LevelCatalog.Get(n));
            Assert.True(result.FirstClear);
            Assert.False(rig.Campaign.OnLevelCompleted(n, result).HasAnything);
            Assert.False(LevelCatalog.Get(n).IsFinale);
        }
    }

    [Fact]
    public void The_choice_can_be_cleared_for_testing()
    {
        var rig = Make(200);
        rig.Ending.Choose("aurora");
        rig.Ending.DevClear();
        Assert.False(rig.Ending.HasChosen);
        Assert.True(rig.Ending.NeedsChoice);
    }
}

public class EndingPageContractTests
{
    [Fact]
    public void The_page_offers_exactly_these_three_looks()
    {
        // GamePage.xaml has one button per look with these ids; the ids must stay in step with the content.
        Assert.Equal(new[] { "lanterns", "aurora", "meadow" }, StoryContent.FinalChoices.Select(c => c.Id).ToArray());
    }
}

public class EndingDevToolsTests
{
    [Fact]
    public void Resetting_the_story_forgets_the_choice_and_resetting_everything_forgets_the_stats()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var shop = new ShopService(settings, progress);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, progress);
        var loadout = new CosmeticLoadout(settings, unlocks);
        var life = new LifetimeStats(settings);
        var ending = new EndingService(settings, progress, unlocks, campaign);
        var dev = new DevTools(progress, shop, unlocks, campaign, loadout, life, ending);

        dev.SetNextLevel(201);
        ending.Choose("aurora");
        Assert.True(ending.HasChosen);

        dev.SetNextLevel(150);
        Assert.False(ending.HasChosen);

        var run = new RunStats();
        life.Add(run);
        dev.ResetEverything();
        Assert.Equal(0, progress.HighestCleared);
        Assert.Equal(0, life.LevelsWon);
    }
}
