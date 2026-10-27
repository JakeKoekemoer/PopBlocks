using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class FullStoryTests
{
    private static int Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    [Fact]
    public void Every_region_has_opening_completion_replay_journal_restored_text_and_fragment()
    {
        foreach (var r in RegionCatalog.All)
        {
            Assert.NotNull(StoryContent.FindScene(r.OpeningSceneId));
            Assert.NotNull(StoryContent.FindScene(r.CompletionSceneId));
            Assert.NotNull(StoryContent.FindScene(r.ReplaySceneId));
            Assert.NotNull(StoryContent.FindLore(r.FragmentId));
            Assert.Contains(StoryContent.LoreForRegion(r.Number), l => l.Kind == LoreKind.Journal && l.UnlockLevel == r.LastLevel);
            Assert.Contains(StoryContent.LoreForRegion(r.Number), l => l.Kind == LoreKind.RestoredWorld && l.UnlockLevel == r.LastLevel);
        }
    }

    [Fact]
    public void Each_region_has_two_memories_and_they_are_only_granted_by_finding_them()
    {
        foreach (var r in RegionCatalog.All)
        {
            var mem = StoryContent.MemoriesOf(r.Number).ToList();
            Assert.Equal(2, mem.Count);
            Assert.All(mem, m => Assert.Equal(-1, m.UnlockLevel));
            Assert.All(mem, m => Assert.InRange(Words(m.Text), 40, 100));
        }
    }

    [Fact]
    public void Scene_levels_sit_inside_their_region_and_opening_is_the_first_level()
    {
        foreach (var s in StoryContent.Scenes.Where(s => s.UnlockLevel > 0))
        {
            var region = RegionCatalog.Get(s.Region);
            Assert.True(region.Contains(s.UnlockLevel), $"{s.Id} level {s.UnlockLevel}");
            if (s.Trigger == SceneTrigger.RegionOpening) Assert.Equal(region.FirstLevel, s.UnlockLevel);
            if (s.Trigger == SceneTrigger.RegionCompletion) Assert.Equal(region.LastLevel, s.UnlockLevel);
            if (s.Trigger == SceneTrigger.MidDiscovery) Assert.Equal(region.MidLevel, s.UnlockLevel);
        }
    }

    [Fact]
    public void Two_scenes_never_fire_on_the_same_level_unless_they_are_the_ending_pair()
    {
        var clash = StoryContent.Scenes
            .Where(s => s.UnlockLevel > 0 && s.Trigger is not SceneTrigger.Reaction and not SceneTrigger.RegionOpening and not SceneTrigger.LevelIntro)
            .GroupBy(s => s.UnlockLevel).Where(g => g.Count() > 1).ToList();
        Assert.Empty(clash);
    }

    [Theory]
    [InlineData("Mara", 2)]
    [InlineData("Bram", 3)]
    [InlineData("Warden", 2)]
    public void Nobody_is_named_before_they_are_introduced(string name, int introRegion)
    {
        foreach (var s in StoryContent.Scenes.Where(s => s.Region < introRegion))
            foreach (var line in s.Lines)
                Assert.DoesNotContain(name, line.Text);
        foreach (var l in StoryContent.Lore.Where(l => l.Region is > 0 && l.Region < introRegion))
            Assert.DoesNotContain(name, l.Text);
    }

    [Fact]
    public void Speakers_only_speak_from_the_region_they_are_introduced_in()
    {
        foreach (var s in StoryContent.Scenes)
            foreach (var line in s.Lines)
            {
                var c = Cast.Find(line.SpeakerId)!;
                Assert.True(s.Region >= c.FirstRegion, $"{s.Id}: {c.Name} speaks before region {c.FirstRegion}");
            }
    }

    [Fact]
    public void The_warden_speaks_directly_only_from_region_seven()
    {
        foreach (var s in StoryContent.Scenes.Where(s => s.Region < 7))
            Assert.DoesNotContain(s.Lines, l => l.SpeakerId == Cast.Warden);
    }

    [Fact]
    public void Warden_character_entries_do_not_reveal_the_motive_early()
    {
        var early = StoryContent.FindLore("char_warden")!;
        Assert.DoesNotContain("overload", early.Text);
        Assert.DoesNotContain("shattered", early.Text);
        foreach (var s in StoryContent.Scenes.Where(s => s.Region < 4))
            foreach (var line in s.Lines)
                Assert.DoesNotContain("overload", line.Text);
    }

    [Fact]
    public void Every_scene_is_a_reasonable_length_and_every_line_fits()
    {
        foreach (var s in StoryContent.Scenes)
        {
            Assert.InRange(s.Lines.Count, 1, 20);
            Assert.All(s.Lines, l => Assert.InRange(l.Text.Length, 1, 200));
        }
    }

    [Fact]
    public void There_is_one_ending_and_no_text_promises_a_branch()
    {
        Assert.Single(StoryContent.Scenes, s => s.Trigger == SceneTrigger.Ending && s.UnlockLevel == 200);
        string all = string.Join(" ", StoryContent.Scenes.SelectMany(s => s.Lines).Select(l => l.Text)).ToLowerInvariant();
        Assert.DoesNotContain("your choice", all);
        Assert.DoesNotContain("alternate ending", all);
        Assert.DoesNotContain("secret level", all);
    }

    [Fact]
    public void Final_choices_are_three_distinct_cosmetics_with_an_epilogue_line_each()
    {
        Assert.Equal(3, StoryContent.FinalChoices.Count);
        Assert.Equal(3, StoryContent.FinalChoices.Select(c => c.Id).Distinct().Count());
        Assert.All(StoryContent.FinalChoices, c =>
        {
            Assert.InRange(c.EpilogueLine.Length, 5, 200);
            Assert.False(string.IsNullOrWhiteSpace(c.CosmeticName));
        });
    }

    [Fact]
    public void Region_text_in_the_archive_stays_locked_until_the_level_is_cleared()
    {
        var settings = new InMemorySettings();
        var unlocks = new UnlockStore(settings);
        var progress = new PlayerProgress(settings);
        var campaign = new CampaignService(unlocks, progress);

        var visible = campaign.UnlockedLore().Select(l => l.Id).ToList();
        Assert.Equal(new[] { "lore_spectrum", "char_pip" }, visible);
        Assert.All(StoryContent.Scenes.Where(s => s.CanReplay), s => Assert.False(campaign.IsSceneAvailable(s)));
    }

    [Fact]
    public void Clearing_a_milestone_level_fires_exactly_that_scene_and_lore()
    {
        var settings = new InMemorySettings();
        var unlocks = new UnlockStore(settings);
        var progress = new PlayerProgress(settings);
        var campaign = new CampaignService(unlocks, progress);

        var outcome = campaign.OnLevelCompleted(91, new CompletionResult(true, 0));
        Assert.Equal("r4_warden", outcome.Scene?.Id);
        Assert.Contains(outcome.NewLore, l => l.Id == "char_warden");
        Assert.False(outcome.FragmentAwarded);
    }

    [Fact]
    public void The_final_puzzle_intro_plays_once_when_level_200_starts()
    {
        var settings = new InMemorySettings();
        var campaign = new CampaignService(new UnlockStore(settings), new PlayerProgress(settings));
        Assert.Equal("r8_final", campaign.OnLevelEntered(200)?.Id);
        campaign.MarkSeen("r8_final");
        Assert.Null(campaign.OnLevelEntered(200));
    }

    [Fact]
    public void Replay_remark_appears_only_on_a_cleared_first_level_after_the_opening_was_seen()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var campaign = new CampaignService(new UnlockStore(settings), progress);

        Assert.Null(campaign.ReplayRemark(26));
        settings.SetInt(PlayerProgress.HighestClearedKey, 40);
        progress = new PlayerProgress(settings);
        campaign = new CampaignService(new UnlockStore(settings), progress);
        Assert.Null(campaign.ReplayRemark(26));          // opening not seen yet
        campaign.MarkSeen("r2_open");
        Assert.Equal("r2_replay", campaign.ReplayRemark(26)?.Id);
        Assert.Null(campaign.ReplayRemark(27));          // only a region's first level
    }

    [Fact]
    public void Reconcile_grants_all_lore_up_to_the_players_progress_and_nothing_beyond()
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.HighestClearedKey, 100);
        var unlocks = new UnlockStore(settings);
        var campaign = new CampaignService(unlocks, new PlayerProgress(settings));
        campaign.Reconcile();

        var ids = campaign.UnlockedLore().Select(l => l.Id).ToHashSet();
        Assert.Contains("char_mara", ids);
        Assert.Contains("char_bram", ids);
        Assert.Contains("r4_journal", ids);
        Assert.DoesNotContain("r5_journal", ids);
        Assert.DoesNotContain("mem_r1_1", ids);
        Assert.Equal(4, campaign.FragmentCount);
    }
}
