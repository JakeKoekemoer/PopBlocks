using PopBlocks.Engine;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class LoadingPolicyTests
{
    [Fact]
    public void The_card_waits_three_hundred_milliseconds_and_only_if_the_level_is_not_ready()
    {
        Assert.False(LoadingPolicy.ShouldShowCard(TimeSpan.FromMilliseconds(299), levelReady: false));
        Assert.True(LoadingPolicy.ShouldShowCard(TimeSpan.FromMilliseconds(300), levelReady: false));
        Assert.True(LoadingPolicy.ShouldShowCard(TimeSpan.FromSeconds(2), levelReady: false));
        Assert.False(LoadingPolicy.ShouldShowCard(TimeSpan.FromSeconds(2), levelReady: true));
    }

    [Fact]
    public void Once_shown_the_card_stays_for_at_least_six_hundred_milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(600), LoadingPolicy.HoldRemaining(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromMilliseconds(250), LoadingPolicy.HoldRemaining(TimeSpan.FromMilliseconds(350)));
        Assert.Equal(TimeSpan.Zero, LoadingPolicy.HoldRemaining(TimeSpan.FromMilliseconds(600)));
        Assert.Equal(TimeSpan.Zero, LoadingPolicy.HoldRemaining(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void The_search_budget_and_the_card_fit_together_at_about_two_point_six_seconds()
    {
        var worst = SeedingConfig.LoadingCardDelay + SeedingConfig.LoadingCardMinimum + SeedingConfig.RollBudget;
        Assert.True(worst <= TimeSpan.FromSeconds(3));
    }
}

public class LoadingRecapTests
{
    [Fact]
    public void A_new_player_has_nothing_to_recap()
    {
        Assert.Empty(LoadingRecap.Build(new UnlockStore(new InMemorySettings())));
    }

    [Fact]
    public void Only_scenes_and_lore_the_player_has_seen_appear()
    {
        var unlocks = new UnlockStore(new InMemorySettings());
        var opening = StoryContent.Scenes.First(s => s.CanReplay);
        var unseen = StoryContent.Scenes.Last(s => s.CanReplay);

        unlocks.Unlock(CampaignService.SeenId(opening.Id));
        var recap = LoadingRecap.Build(unlocks);

        Assert.Single(recap);
        Assert.Equal(opening.Title, recap[0].Source);
        Assert.DoesNotContain(recap, r => r.Source == unseen.Title && unseen.Id != opening.Id);
    }

    [Fact]
    public void The_most_recent_story_comes_first_and_the_list_is_short()
    {
        var unlocks = new UnlockStore(new InMemorySettings());
        var replayable = StoryContent.Scenes.Where(s => s.CanReplay).OrderBy(s => s.UnlockLevel).ToList();
        foreach (var scene in replayable.Take(8)) unlocks.Unlock(CampaignService.SeenId(scene.Id));

        var recap = LoadingRecap.Build(unlocks);

        Assert.Equal(LoadingRecap.MaxItems, recap.Count);
        var latest = replayable.Take(8).Max(s => s.UnlockLevel);
        Assert.Contains(replayable.Take(8).Where(s => s.UnlockLevel == latest), s => s.Title == recap[0].Source);
    }

    [Fact]
    public void Skipping_a_scene_still_counts_as_having_seen_it_but_a_reaction_banner_never_appears()
    {
        var unlocks = new UnlockStore(new InMemorySettings());
        foreach (var scene in StoryContent.Scenes) unlocks.Unlock(CampaignService.SeenId(scene.Id));

        var recap = LoadingRecap.Build(unlocks, max: 200);

        var reactionTitles = StoryContent.Scenes.Where(s => !s.CanReplay).Select(s => s.Title).ToHashSet();
        Assert.NotEmpty(recap);
        Assert.All(recap, r => Assert.False(string.IsNullOrWhiteSpace(r.Text)));
        Assert.DoesNotContain(recap, r => reactionTitles.Contains(r.Source) && StoryContent.Scenes.Where(s => s.CanReplay).All(s => s.Title != r.Source));
    }

    [Fact]
    public void A_journal_entry_or_memory_appears_only_once_found()
    {
        var unlocks = new UnlockStore(new InMemorySettings());
        var memory = StoryContent.Lore.First(l => l.Kind == LoreKind.Memory);

        Assert.DoesNotContain(LoadingRecap.Build(unlocks, 50), r => r.Source == memory.Title);

        unlocks.Unlock(CampaignService.LoreId(memory.Id));
        Assert.Contains(LoadingRecap.Build(unlocks, 50), r => r.Source == memory.Title);
    }

    [Fact]
    public void Long_text_is_shortened_and_never_run_on()
    {
        var unlocks = new UnlockStore(new InMemorySettings());
        foreach (var scene in StoryContent.Scenes) unlocks.Unlock(CampaignService.SeenId(scene.Id));
        foreach (var lore in StoryContent.Lore) unlocks.Unlock(CampaignService.LoreId(lore.Id));

        foreach (var item in LoadingRecap.Build(unlocks, 500))
        {
            Assert.InRange(item.Text.Length, 1, 152);
            Assert.DoesNotContain('\n', item.Text);
        }
    }
}
