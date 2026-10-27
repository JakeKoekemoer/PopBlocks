namespace PopBlocks.Engine.Story;

/// <summary>
/// A picture beat for the stage strip above a dialogue panel. Cues are flags, so one line can ask for several
/// (a restoration sweep and a Prism shard together). They are data only: the app decides how each one is drawn.
/// </summary>
[Flags]
public enum StageCue
{
    None = 0,

    /// <summary>Pip is still behind the cracked stone: only a glow shows.</summary>
    PipHidden = 1 << 0,

    /// <summary>Pip drifts out from behind the cracked stone.</summary>
    PipReveal = 1 << 1,

    /// <summary>The Warden's mark is drawn on, then glows.</summary>
    Mark = 1 << 2,

    /// <summary>A wave of colour restores the picture (faded to recovering or restored).</summary>
    RestoreSweep = 1 << 3,

    /// <summary>A Prism shard rises and pulses.</summary>
    Shard = 1 << 4,

    /// <summary>Two Prism shards rise and pulse in step.</summary>
    ShardPair = 1 << 5,

    /// <summary>Light drawn toward one centre while the edges dim: the old world.</summary>
    FlowInward = 1 << 6,

    /// <summary>Light spreading out from a centre while the edges light up: the new plan.</summary>
    FlowOutward = 1 << 7,

    /// <summary>A memory crystal, pulsing while a recording plays.</summary>
    MemoryCrystal = 1 << 8,

    /// <summary>The Warden steps out of the dark.</summary>
    WardenEntrance = 1 << 9,

    /// <summary>The Warden is already there, standing.</summary>
    WardenStand = 1 << 10,

    /// <summary>The world holds still: ambient motion slows and the picture dims. For a pause in the story.</summary>
    Stillness = 1 << 11,
}

/// <summary>From <see cref="Line"/> of a scene onward, show <see cref="Cue"/> (until the next entry replaces it).</summary>
public sealed record StageEntry(int Line, StageCue Cue);

/// <summary>Everything the stage strip needs to draw one line of one scene.</summary>
/// <param name="SceneId">The scene being shown.</param>
/// <param name="RegionNumber">1 to 8: which region's picture to draw.</param>
/// <param name="World">How restored the picture is when the line starts. Before a sweep, the faded look.</param>
/// <param name="SweepTo">Where a <see cref="StageCue.RestoreSweep"/> ends up.</param>
/// <param name="Cue">The cues in force on this line.</param>
/// <param name="CueStartLine">The line where these cues began. One-shot animations play from there and are finished on later lines.</param>
/// <param name="Line">The line being shown.</param>
public sealed record StageInfo(
    string SceneId,
    int RegionNumber,
    RegionState World,
    RegionState SweepTo,
    StageCue Cue,
    int CueStartLine,
    int Line)
{
    public bool Has(StageCue cue) => (Cue & cue) != 0;
}

/// <summary>
/// Which picture beats go with which lines. Kept apart from the story text so the words stay plain data, and a test
/// checks every entry points at a real scene and line. A cue stays in force until the next entry for that scene.
/// Scenes with no entry still get their region's picture and ambience.
/// </summary>
public static class StageDirections
{
    private static readonly Dictionary<string, StageEntry[]> Entries = new()
    {
        // Region 1: Pip waits behind the stone, then drifts out. The mark under the roots. The garden returns, and the first shard.
        ["r1_open"] = new[] { new StageEntry(0, StageCue.PipHidden), new StageEntry(1, StageCue.PipReveal), new StageEntry(2, StageCue.None) },
        ["r1_mid"] = new[] { new StageEntry(0, StageCue.Mark) },
        ["r1_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep | StageCue.Shard) },

        // Region 2: a record in a crystal, the inscription about the centre, the second shard answering the first.
        ["r2_record"] = new[] { new StageEntry(0, StageCue.MemoryCrystal) },
        ["r2_mid"] = new[] { new StageEntry(1, StageCue.FlowInward) },
        ["r2_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep | StageCue.ShardPair) },

        // Region 3: the pump brings the grove back; the workshop lamps come on.
        ["r3_mid"] = new[] { new StageEntry(0, StageCue.RestoreSweep) },
        ["r3_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep) },

        // Region 4: the recording crystal, the Warden's mark on the wall, the diagram of the old channels.
        ["r4_message"] = new[] { new StageEntry(0, StageCue.MemoryCrystal) },
        ["r4_warden"] = new[] { new StageEntry(0, StageCue.Mark) },
        ["r4_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep | StageCue.FlowInward) },

        // Region 5: everything linked to the Heart; the Warden's mark on the shutdown control; local power.
        ["r5_open"] = new[] { new StageEntry(0, StageCue.FlowInward) },
        ["r5_discovery"] = new[] { new StageEntry(0, StageCue.Mark) },
        ["r5_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep) },

        // Region 6: the memory in the ice; "every connection depends on one point"; the path opens.
        ["r6_ice"] = new[] { new StageEntry(0, StageCue.MemoryCrystal) },
        ["r6_mid"] = new[] { new StageEntry(3, StageCue.FlowInward) },
        ["r6_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep) },

        // Region 7: the Warden steps out; the confession, with its pause; the plan runs the other way.
        ["r7_open"] = new[] { new StageEntry(0, StageCue.WardenEntrance) },
        ["r7_confession"] = new[]
        {
            new StageEntry(0, StageCue.FlowInward | StageCue.WardenStand),
            new StageEntry(6, StageCue.WardenStand | StageCue.Stillness),   // "A pause."
            new StageEntry(7, StageCue.WardenStand),
        },
        ["r7_plan"] = new[] { new StageEntry(0, StageCue.FlowOutward) },
        ["r7_complete"] = new[] { new StageEntry(0, StageCue.RestoreSweep) },

        // Region 8: each region has its own light; then the light goes out to the regions.
        ["r8_prep"] = new[] { new StageEntry(0, StageCue.FlowOutward) },
        ["r8_complete"] = new[] { new StageEntry(1, StageCue.FlowOutward) },
    };

    /// <summary>Every scene that has picture beats, for tests and tools.</summary>
    public static IReadOnlyDictionary<string, StageEntry[]> Table => Entries;

    /// <summary>The cues in force on a line, and the line they began on. Unlisted scenes and early lines give none.</summary>
    public static (StageCue Cue, int StartLine) At(string sceneId, int line)
    {
        if (!Entries.TryGetValue(sceneId, out var entries)) return (StageCue.None, 0);

        var cue = StageCue.None;
        int start = 0;
        foreach (var entry in entries)
        {
            if (entry.Line > line) break;
            cue = entry.Cue;
            start = entry.Line;
        }
        return (cue, start);
    }

    /// <summary>What the stage should show for one line of a scene.</summary>
    public static StageInfo Describe(StoryScene scene, int line)
    {
        var (cue, start) = At(scene.Id, line);
        int number = Math.Clamp(scene.Region, 1, RegionCatalog.All.Count);
        var region = RegionCatalog.Get(number);
        bool sweeps = (cue & StageCue.RestoreSweep) != 0;

        RegionState world = scene.Trigger switch
        {
            // A completion starts faded and sweeps to restored; one with no sweep simply shows the restored place.
            SceneTrigger.RegionCompletion or SceneTrigger.Ending => sweeps ? RegionState.Faded : RegionState.Restored,
            SceneTrigger.LevelIntro => RegionState.Recovering,
            SceneTrigger.Milestone => scene.UnlockLevel >= region.MidLevel ? RegionState.Recovering : RegionState.Faded,
            _ => RegionState.Faded,   // openings and the mid-region discovery: the world is still faded
        };

        // The mid-region discovery only starts the recovery. A completion finishes it.
        var sweepTo = scene.Trigger == SceneTrigger.MidDiscovery ? RegionState.Recovering : RegionState.Restored;

        return new StageInfo(scene.Id, number, world, sweepTo, cue, start, line);
    }

    /// <summary>Recordings (a memory crystal, a message) are not people who are present, so they are never drawn as a character.</summary>
    public static bool IsRecording(string speakerId) =>
        speakerId is Cast.Voice or Cast.WardenRecording or Cast.WardenMessage;
}
