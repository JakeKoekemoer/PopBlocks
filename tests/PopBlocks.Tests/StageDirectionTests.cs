using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class StageDirectionTests
{
    [Fact]
    public void Every_mapped_scene_exists_and_every_entry_points_at_a_real_line()
    {
        foreach (var (id, entries) in StageDirections.Table)
        {
            var scene = StoryContent.FindScene(id);
            Assert.NotNull(scene);
            Assert.NotEmpty(entries);
            Assert.All(entries, e => Assert.InRange(e.Line, 0, scene!.Lines.Count - 1));
        }
    }

    [Fact]
    public void Entries_run_in_line_order_with_no_line_repeated()
    {
        foreach (var entries in StageDirections.Table.Values)
            for (int i = 1; i < entries.Length; i++)
                Assert.True(entries[i].Line > entries[i - 1].Line);
    }

    [Fact]
    public void No_entry_asks_for_cues_that_contradict_each_other()
    {
        foreach (var e in StageDirections.Table.Values.SelectMany(x => x))
        {
            Assert.False(e.Cue.HasFlag(StageCue.FlowInward) && e.Cue.HasFlag(StageCue.FlowOutward));
            Assert.False(e.Cue.HasFlag(StageCue.Shard) && e.Cue.HasFlag(StageCue.ShardPair));
            Assert.False(e.Cue.HasFlag(StageCue.WardenEntrance) && e.Cue.HasFlag(StageCue.WardenStand));
            Assert.False(e.Cue.HasFlag(StageCue.PipHidden) && e.Cue.HasFlag(StageCue.PipReveal));
        }
    }

    [Fact]
    public void Region_completions_1_to_7_restore_with_a_sweep_from_their_first_line()
    {
        for (int n = 1; n <= 7; n++)
        {
            var scene = StoryContent.SceneFor(RegionCatalog.Get(n), SceneTrigger.RegionCompletion)!;
            var (cue, start) = StageDirections.At(scene.Id, 0);
            Assert.True(cue.HasFlag(StageCue.RestoreSweep), scene.Id);
            Assert.Equal(0, start);
        }
    }

    [Theory]
    [InlineData("r2_record")]
    [InlineData("r4_message")]
    [InlineData("r6_ice")]
    public void Recording_scenes_show_a_memory_crystal(string id) =>
        Assert.True(StageDirections.At(id, 0).Cue.HasFlag(StageCue.MemoryCrystal));

    [Theory]
    [InlineData("r1_mid")]
    [InlineData("r4_warden")]
    [InlineData("r5_discovery")]
    public void The_mark_scenes_draw_the_mark(string id) =>
        Assert.True(StageDirections.At(id, 0).Cue.HasFlag(StageCue.Mark));

    [Fact]
    public void The_warden_is_only_staged_in_the_citadel()
    {
        foreach (var (id, entries) in StageDirections.Table)
        {
            if (entries.Any(e => e.Cue.HasFlag(StageCue.WardenEntrance) || e.Cue.HasFlag(StageCue.WardenStand)))
                Assert.Equal(7, StoryContent.FindScene(id)!.Region);
        }
    }

    [Fact]
    public void Pip_waits_behind_the_stone_then_is_revealed_on_the_narration_line()
    {
        var scene = StoryContent.FindScene("r1_open")!;
        Assert.Equal(StageCue.PipHidden, StageDirections.At(scene.Id, 0).Cue);

        var (cue, start) = StageDirections.At(scene.Id, 1);
        Assert.Equal(StageCue.PipReveal, cue);
        Assert.True(scene.Lines[start].IsNarration);

        Assert.Equal(StageCue.None, StageDirections.At(scene.Id, 2).Cue);
    }

    [Fact]
    public void The_confession_holds_still_for_exactly_the_pause_line()
    {
        var scene = StoryContent.FindScene("r7_confession")!;
        int pause = scene.Lines.ToList().FindIndex(l => l.IsNarration && l.Text == "A pause.");
        Assert.True(pause > 0);

        Assert.False(StageDirections.At(scene.Id, pause - 1).Cue.HasFlag(StageCue.Stillness));
        Assert.True(StageDirections.At(scene.Id, pause).Cue.HasFlag(StageCue.Stillness));
        Assert.False(StageDirections.At(scene.Id, pause + 1).Cue.HasFlag(StageCue.Stillness));
    }

    [Fact]
    public void Unmapped_scenes_and_early_lines_have_no_cue()
    {
        Assert.Equal((StageCue.None, 0), StageDirections.At("r2_open", 3));
        Assert.Equal((StageCue.None, 0), StageDirections.At("no_such_scene", 0));
        Assert.Equal(StageCue.None, StageDirections.At("r6_mid", 2).Cue);
        Assert.Equal(StageCue.FlowInward, StageDirections.At("r6_mid", 3).Cue);
    }

    [Fact]
    public void A_completion_starts_faded_and_sweeps_to_restored_and_the_ending_is_simply_restored()
    {
        var done = StageDirections.Describe(StoryContent.FindScene("r1_complete")!, 0);
        Assert.Equal(RegionState.Faded, done.World);
        Assert.Equal(RegionState.Restored, done.SweepTo);
        Assert.Equal(1, done.RegionNumber);

        var ending = StageDirections.Describe(StoryContent.FindScene("r8_complete")!, 1);
        Assert.Equal(RegionState.Restored, ending.World);
        Assert.True(ending.Has(StageCue.FlowOutward));
        Assert.Equal(1, ending.CueStartLine);
    }

    [Fact]
    public void The_mid_region_discovery_only_recovers_the_picture()
    {
        var pump = StageDirections.Describe(StoryContent.FindScene("r3_mid")!, 0);
        Assert.Equal(RegionState.Faded, pump.World);
        Assert.Equal(RegionState.Recovering, pump.SweepTo);
    }

    [Fact]
    public void Milestones_look_faded_before_the_middle_of_a_region_and_recovering_after_it()
    {
        Assert.Equal(RegionState.Faded, StageDirections.Describe(StoryContent.FindScene("r2_record")!, 0).World);        // level 31 of 26-50
        Assert.Equal(RegionState.Recovering, StageDirections.Describe(StoryContent.FindScene("r5_discovery")!, 0).World); // level 116 of 101-125
    }

    [Fact]
    public void Recordings_are_never_drawn_as_a_character()
    {
        Assert.True(StageDirections.IsRecording(Cast.Voice));
        Assert.True(StageDirections.IsRecording(Cast.WardenRecording));
        Assert.True(StageDirections.IsRecording(Cast.WardenMessage));
        Assert.False(StageDirections.IsRecording(Cast.Warden));
        Assert.False(StageDirections.IsRecording(Cast.Pip));
    }

    [Fact]
    public void Every_panel_scene_can_be_described_on_every_line()
    {
        foreach (var scene in StoryContent.Scenes.Where(s => s.Trigger != SceneTrigger.Reaction))
            for (int line = 0; line < scene.Lines.Count; line++)
            {
                var info = StageDirections.Describe(scene, line);
                Assert.InRange(info.RegionNumber, 1, 8);
                Assert.True(info.CueStartLine <= line);
            }
    }
}
