using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class RegionCatalogTests
{
    [Fact]
    public void There_are_eight_regions_of_25_levels_covering_1_to_200()
    {
        Assert.Equal(8, RegionCatalog.All.Count);
        Assert.Equal(1, RegionCatalog.All[0].FirstLevel);
        Assert.Equal(200, RegionCatalog.All[^1].LastLevel);
        Assert.All(RegionCatalog.All, r => Assert.Equal(25, r.LevelCount));
    }

    [Fact]
    public void Every_level_1_to_200_belongs_to_exactly_one_region()
    {
        for (int level = 1; level <= 200; level++)
            Assert.Single(RegionCatalog.All, r => r.Contains(level));
    }

    [Fact]
    public void Regions_are_contiguous_and_numbered_in_order()
    {
        for (int i = 0; i < RegionCatalog.All.Count; i++)
        {
            Assert.Equal(i + 1, RegionCatalog.All[i].Number);
            if (i > 0) Assert.Equal(RegionCatalog.All[i - 1].LastLevel + 1, RegionCatalog.All[i].FirstLevel);
        }
    }

    [Fact]
    public void Region_ids_and_titles_are_unique()
    {
        Assert.Equal(8, RegionCatalog.All.Select(r => r.Id).Distinct().Count());
        Assert.Equal(8, RegionCatalog.All.Select(r => r.Title).Distinct().Count());
    }

    [Fact]
    public void Levels_beyond_the_campaign_and_below_one_have_no_region()
    {
        Assert.Null(RegionCatalog.For(0));
        Assert.Null(RegionCatalog.For(201));
        Assert.Null(RegionCatalog.For(5000));
    }

    [Fact]
    public void Mid_level_is_the_13th_of_25()
    {
        Assert.Equal(13, RegionCatalog.Get(1).MidLevel);
        Assert.Equal(38, RegionCatalog.Get(2).MidLevel);
    }

    [Theory]
    [InlineData(0, RegionState.Faded, 0)]
    [InlineData(12, RegionState.Faded, 12)]
    [InlineData(13, RegionState.Recovering, 13)]
    [InlineData(24, RegionState.Recovering, 24)]
    [InlineData(25, RegionState.Restored, 25)]
    [InlineData(300, RegionState.Restored, 25)]
    public void Region_one_state_is_derived_from_highest_cleared(int highest, RegionState state, int cleared)
    {
        var p = RegionCatalog.Progress(RegionCatalog.Get(1), highest);
        Assert.Equal(state, p.State);
        Assert.Equal(cleared, p.LevelsCleared);
    }

    [Fact]
    public void A_region_unlocks_when_the_player_reaches_its_first_level()
    {
        var r2 = RegionCatalog.Get(2);
        Assert.False(RegionCatalog.Progress(r2, 24).IsUnlocked);
        Assert.True(RegionCatalog.Progress(r2, 25).IsUnlocked);
        Assert.Equal(26, RegionCatalog.Progress(r2, 25).NextLevel);
        Assert.Equal(RegionState.Faded, RegionCatalog.Progress(r2, 25).State);
    }

    [Fact]
    public void Later_regions_stay_faded_while_earlier_ones_progress()
    {
        Assert.All(RegionCatalog.All.Skip(1), r => Assert.Equal(RegionState.Faded, RegionCatalog.Progress(r, 25).State));
    }

    [Fact]
    public void Title_in_a_sentence_drops_the_capital_the()
    {
        Assert.Equal("the Crystal Caves", RegionCatalog.Get(2).TitleInSentence);
    }
}

public class StoryContentTests
{
    [Fact]
    public void Scene_and_lore_ids_are_unique_and_safe_for_the_unlock_store()
    {
        var ids = StoryContent.Scenes.Select(s => s.Id).Concat(StoryContent.Lore.Select(l => l.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.DoesNotContain(',', id));
        Assert.All(ids, id => Assert.DoesNotContain(' ', id));
    }

    [Fact]
    public void Every_speaker_exists_in_the_cast()
    {
        foreach (var scene in StoryContent.Scenes)
            foreach (var line in scene.Lines)
                Assert.NotNull(Cast.Find(line.SpeakerId));
    }

    [Fact]
    public void Story_lines_fit_a_small_screen()
    {
        foreach (var scene in StoryContent.Scenes)
            foreach (var line in scene.Lines)
            {
                Assert.False(string.IsNullOrWhiteSpace(line.Text));
                Assert.True(line.Text.Length <= 200, $"{scene.Id}: line is {line.Text.Length} characters");
            }
    }

    [Fact]
    public void Region_one_scenes_have_the_expected_size()
    {
        int Spoken(string id) => StoryContent.FindScene(id)!.Lines.Count(l => !l.IsNarration);
        Assert.InRange(Spoken("r1_open"), 3, 7);
        Assert.InRange(StoryContent.FindScene("r1_first_burst")!.Lines.Count, 1, 3);
        Assert.InRange(StoryContent.FindScene("r1_first_chain")!.Lines.Count, 1, 3);
        Assert.InRange(StoryContent.FindScene("r1_complete")!.Lines.Count, 4, 8);
    }

    [Fact]
    public void Region_one_has_every_scene_the_catalog_names()
    {
        var r1 = RegionCatalog.Get(1);
        Assert.NotNull(StoryContent.FindScene(r1.OpeningSceneId));
        Assert.NotNull(StoryContent.FindScene(r1.MidSceneId));
        Assert.NotNull(StoryContent.FindScene(r1.CompletionSceneId));
        Assert.NotNull(StoryContent.FindLore(r1.FragmentId));
    }

    [Fact]
    public void Archive_entries_stay_short()
    {
        // The bible asks for roughly 40-100 words per journal entry; the bible's own journal entries run from about 24 to 48.
        foreach (var entry in StoryContent.Lore)
        {
            int words = entry.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            int min = entry.Kind == LoreKind.Journal ? 20 : 15;
            Assert.InRange(words, min, 100);
        }
    }

    [Fact]
    public void Reactions_are_not_listed_for_replay()
    {
        Assert.False(StoryContent.FindScene("r1_first_burst")!.CanReplay);
        Assert.True(StoryContent.FindScene("r1_open")!.CanReplay);
    }

    [Fact]
    public void Scenes_do_not_promise_mechanics_the_game_lacks()
    {
        string all = string.Join(" ", StoryContent.Scenes.SelectMany(s => s.Lines).Select(l => l.Text)).ToLowerInvariant();
        Assert.False(all.Contains("choose"), "story text must not offer a choice");
        Assert.False(all.Contains("secret level"), "no secret level exists");
        Assert.False(all.Contains("colour power"), "no colour power exists");
    }

    [Fact]
    public void Locked_placeholder_names_the_region_without_revealing_it()
    {
        Assert.Equal("More will be discovered in the Crystal Caves.", StoryContent.LockedPlaceholder(RegionCatalog.Get(2)));
    }
}

public class CampaignServiceTests
{
    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public UnlockStore Unlocks;
        public PlayerProgress Progress;
        public CampaignService Campaign;

        public Rig()
        {
            Unlocks = new UnlockStore(Settings);
            Progress = new PlayerProgress(Settings);
            Campaign = new CampaignService(Unlocks, Progress);
        }

        /// <summary>Recreates every service from saved settings, as an app restart would.</summary>
        public void Restart()
        {
            Unlocks = new UnlockStore(Settings);
            Progress = new PlayerProgress(Settings);
            Campaign = new CampaignService(Unlocks, Progress);
        }

        /// <summary>Clears levels 1..upTo through the real progress object and story hook.</summary>
        public CampaignOutcome ClearUpTo(int upTo)
        {
            var last = CampaignOutcome.None;
            for (int level = Progress.HighestCleared + 1; level <= upTo; level++)
            {
                var result = new CompletionResult(true, 0);
                // PlayerProgress.Complete needs a LevelDefinition; simulate the same state change through settings.
                Settings.SetInt(PlayerProgress.HighestClearedKey, level);
                Progress = new PlayerProgress(Settings);
                Campaign = new CampaignService(Unlocks, Progress);
                last = Campaign.OnLevelCompleted(level, result);
            }
            return last;
        }
    }

    [Fact]
    public void Opening_scene_is_offered_on_first_entry_to_level_one_only()
    {
        var rig = new Rig();
        var scene = rig.Campaign.OnLevelEntered(1);
        Assert.Equal("r1_open", scene?.Id);
        Assert.Null(rig.Campaign.OnLevelEntered(2));
        Assert.Equal("r2_open", rig.Campaign.OnLevelEntered(26)?.Id);   // each region opens once, on its first level
        Assert.Null(rig.Campaign.OnLevelEntered(27));
    }

    [Fact]
    public void Opening_is_not_repeated_once_seen_even_after_restart()
    {
        var rig = new Rig();
        rig.Campaign.MarkSeen("r1_open");
        Assert.Null(rig.Campaign.OnLevelEntered(1));
        rig.Restart();
        Assert.Null(rig.Campaign.OnLevelEntered(1));
    }

    [Fact]
    public void A_skipped_opening_counts_as_seen_the_same_way()
    {
        var rig = new Rig();
        var scene = rig.Campaign.OnLevelEntered(1)!;
        rig.Campaign.MarkSeen(scene.Id); // skip and finish both call this
        Assert.True(rig.Campaign.HasSeen("r1_open"));
    }

    [Fact]
    public void Mid_discovery_fires_on_the_first_clear_of_level_13()
    {
        var rig = new Rig();
        rig.ClearUpTo(12);
        var outcome = rig.ClearUpTo(13);
        Assert.Equal("r1_mid", outcome.Scene?.Id);
        Assert.True(outcome.RegionRecovering);
        Assert.False(outcome.FragmentAwarded);
    }

    [Fact]
    public void Ordinary_levels_produce_no_story_output()
    {
        var rig = new Rig();
        for (int level = 1; level <= 24; level++)
        {
            var outcome = rig.ClearUpTo(level);
            if (level != 13) Assert.False(outcome.HasAnything, $"level {level}");
        }
    }

    [Fact]
    public void Clearing_level_25_restores_the_region_and_awards_everything_once()
    {
        var rig = new Rig();
        rig.ClearUpTo(24);
        var outcome = rig.ClearUpTo(25);

        Assert.True(outcome.RegionRestored);
        Assert.True(outcome.FragmentAwarded);
        Assert.Equal("r1_complete", outcome.Scene?.Id);
        Assert.Contains(outcome.NewLore, l => l.Id == "r1_journal");
        Assert.Equal(CampaignService.CosmeticGarden, outcome.CosmeticUnlocked);
        Assert.Equal(1, rig.Campaign.FragmentCount);
        Assert.Equal(1, rig.Campaign.RestoredRegionCount);
    }

    [Fact]
    public void Skipping_the_completion_scene_still_keeps_every_reward()
    {
        var rig = new Rig();
        rig.ClearUpTo(25); // never call MarkSeen: the player skipped, or the app was killed
        rig.Restart();

        Assert.True(rig.Campaign.HasFragment(RegionCatalog.Get(1)));
        Assert.Contains(rig.Campaign.UnlockedLore(), l => l.Id == "r1_journal");
        Assert.True(rig.Unlocks.Has(CampaignService.CosmeticGarden));
    }

    [Fact]
    public void Replaying_a_cleared_level_never_repeats_a_reward()
    {
        var rig = new Rig();
        rig.ClearUpTo(25);
        int unlocksBefore = rig.Unlocks.Count;

        var replay = rig.Campaign.OnLevelCompleted(25, new CompletionResult(false, 0));
        Assert.False(replay.HasAnything);
        Assert.Equal(unlocksBefore, rig.Unlocks.Count);
    }

    [Fact]
    public void Even_a_repeated_first_clear_signal_cannot_pay_the_fragment_twice()
    {
        var rig = new Rig();
        rig.ClearUpTo(25);
        var again = rig.Campaign.OnLevelCompleted(25, new CompletionResult(true, 0));
        Assert.False(again.FragmentAwarded);
        Assert.Empty(again.NewLore);
        Assert.Null(again.CosmeticUnlocked);
        Assert.Equal(1, rig.Campaign.FragmentCount);
    }

    [Fact]
    public void A_seen_completion_scene_is_not_offered_again()
    {
        var rig = new Rig();
        rig.Campaign.MarkSeen("r1_complete");
        Assert.Null(rig.ClearUpTo(25).Scene);
    }

    [Fact]
    public void Levels_outside_the_campaign_give_nothing()
    {
        var rig = new Rig();
        Assert.False(rig.Campaign.OnLevelCompleted(201, new CompletionResult(true, 100)).HasAnything);
    }

    [Fact]
    public void Reconcile_grants_rewards_to_a_player_who_finished_region_one_before_the_story_existed()
    {
        var rig = new Rig();
        rig.Settings.SetInt(PlayerProgress.HighestClearedKey, 40);
        rig.Restart();
        Assert.Equal(0, rig.Campaign.FragmentCount);

        rig.Campaign.Reconcile();
        rig.Campaign.Reconcile(); // idempotent

        Assert.Equal(1, rig.Campaign.FragmentCount);
        Assert.True(rig.Unlocks.Has(CampaignService.CosmeticGarden));
        Assert.Contains(rig.Campaign.UnlockedLore(), l => l.Id == "r1_journal");
        Assert.False(rig.Campaign.HasSeen("r1_complete")); // the story is still theirs to read
        Assert.True(rig.Campaign.IsSceneAvailable(StoryContent.FindScene("r1_complete")!));
    }

    [Fact]
    public void Reconcile_does_nothing_for_a_new_player()
    {
        var rig = new Rig();
        rig.Campaign.Reconcile();
        Assert.Equal(0, rig.Unlocks.Count);
    }

    [Fact]
    public void Archive_scene_is_hidden_until_seen_or_reached()
    {
        var rig = new Rig();
        var complete = StoryContent.FindScene("r1_complete")!;
        Assert.False(rig.Campaign.IsSceneAvailable(complete));
        rig.Campaign.MarkSeen("r1_complete");
        Assert.True(rig.Campaign.IsSceneAvailable(complete));
    }

    [Fact]
    public void Archive_starts_with_the_overview_and_pip_only()
    {
        var rig = new Rig();
        var ids = rig.Campaign.UnlockedLore().Select(l => l.Id).ToList();
        Assert.Equal(new[] { "lore_spectrum", "char_pip" }, ids);
    }

    [Fact]
    public void Burst_reaction_comes_once_then_never_again()
    {
        var rig = new Rig();
        Assert.Equal("r1_first_burst", rig.Campaign.OnBurst(1)?.Id);
        Assert.Null(rig.Campaign.OnBurst(1));
        rig.Restart();
        Assert.Null(rig.Campaign.OnBurst(1));
    }

    [Fact]
    public void Chain_reaction_follows_the_burst_reaction_once()
    {
        var rig = new Rig();
        rig.Campaign.OnBurst(1);
        Assert.Equal("r1_first_chain", rig.Campaign.OnBurst(2)?.Id);
        Assert.Null(rig.Campaign.OnBurst(3));
    }

    [Fact]
    public void A_first_burst_that_is_already_a_chain_gives_only_the_chain_line()
    {
        var rig = new Rig();
        Assert.Equal("r1_first_chain", rig.Campaign.OnBurst(2)?.Id);
        Assert.Null(rig.Campaign.OnBurst(1));
    }

    [Fact]
    public void Region_state_matches_saved_progress_after_restart()
    {
        var rig = new Rig();
        rig.ClearUpTo(14);
        rig.Restart();
        Assert.Equal(RegionState.Recovering, rig.Campaign.ProgressOf(RegionCatalog.Get(1)).State);
    }
}

public class CosmeticLoadoutTests
{
    [Fact]
    public void Garden_is_not_selectable_until_unlocked()
    {
        var settings = new InMemorySettings();
        var unlocks = new UnlockStore(settings);
        var loadout = new CosmeticLoadout(settings, unlocks);

        Assert.False(loadout.Select("garden"));
        Assert.Equal(CosmeticLoadout.DefaultId, loadout.SelectedBoard);

        unlocks.Unlock(CampaignService.CosmeticGarden);
        Assert.True(loadout.Select("garden"));
        Assert.Equal("garden", loadout.SelectedBoard);
    }

    [Fact]
    public void Selection_survives_a_restart_and_a_stale_value_falls_back()
    {
        var settings = new InMemorySettings();
        var unlocks = new UnlockStore(settings);
        unlocks.Unlock(CampaignService.CosmeticGarden);
        new CosmeticLoadout(settings, unlocks).Select("garden");

        Assert.Equal("garden", new CosmeticLoadout(settings, new UnlockStore(settings)).SelectedBoard);

        settings.SetString(SaveKeys.CosmeticBoard, "hacked");
        Assert.Equal(CosmeticLoadout.DefaultId, new CosmeticLoadout(settings, new UnlockStore(settings)).SelectedBoard);
    }
}
