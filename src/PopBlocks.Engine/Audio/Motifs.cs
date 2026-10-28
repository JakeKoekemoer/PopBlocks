namespace PopBlocks.Engine.Audio;

/// <summary>A short melodic idea: pitches in semitones above a root, when each note starts and how long it lasts (in beats).</summary>
public sealed record MotifShape(string Id, int[] Intervals, float[] OnsetBeats, float[] DurationBeats);

/// <summary>
/// The tunes the whole soundtrack is built from.
///
/// The <b>Spectrum motif</b> is five notes. Written as a distance from the root, it can be moved to any key and played by any
/// instrument. Its shape is: up a third, back a step, up a fourth to a note that hangs, and finally home an octave above the
/// start. When the world is faded the last note never arrives; when it is restored it does.
///
/// Every region also has a short <b>regional motif</b> of its own. They are only ever heard in their own region and, all
/// together, at the end. The <b>Warden's motif</b> is the Spectrum motif slowed, with its third lowered and its last note
/// left hanging a step short of home.
///
/// These pitches are original and are placeholders for listening tests: change the numbers here and every region follows.
/// </summary>
public static class Motifs
{
    /// <summary>Spectrum motif: root, up a major third, down a step, up a fourth, then up an octave from the root.</summary>
    public static readonly int[] SpectrumMajor = { 0, 4, 2, 7, 12 };

    /// <summary>The same shape with the third lowered, for the regions in a minor or modal colour.</summary>
    public static readonly int[] SpectrumMinor = { 0, 3, 2, 7, 12 };

    /// <summary>
    /// The Warden's motif: the Spectrum motif an octave down, third lowered, with the last note stopping on the fourth, a step short of
    /// where the Spectrum motif would come to rest.
    /// </summary>
    public static readonly int[] WardenIntervals = { -12, -9, -10, -5, -7 };

    public static MotifShape Garden { get; } = new("garden",
        new[] { 0, 4, 7, 4, 9 },
        new[] { 0f, 0.75f, 1.5f, 3f, 4f },
        new[] { 0.75f, 0.75f, 1.5f, 1f, 2.5f });

    public static MotifShape Caves { get; } = new("caves",
        new[] { 12, 7, 12, 9, 4 },
        new[] { 0f, 1f, 2.5f, 3.5f, 5f },
        new[] { 1f, 1.5f, 1f, 1.5f, 3f });

    public static MotifShape Wilds { get; } = new("wilds",
        new[] { 0, 2, 4, 9, 7 },
        new[] { 0f, 0.5f, 1f, 2.5f, 3.5f },
        new[] { 0.5f, 0.5f, 1.25f, 1f, 2.5f });

    public static MotifShape Sunken { get; } = new("sunken",
        new[] { 14, 12, 9, 4, 7 },
        new[] { 0f, 1.5f, 3f, 4.5f, 6f },
        new[] { 1.5f, 1.5f, 1.5f, 1.5f, 3f });

    public static MotifShape Clockwork { get; } = new("clockwork",
        new[] { 0, 0, 7, 0, 7 },
        new[] { 0f, 1f, 2f, 3f, 4f },
        new[] { 0.5f, 0.5f, 0.5f, 0.5f, 1.5f });

    public static MotifShape Frozen { get; } = new("frozen",
        new[] { 16, 12, 14, 9, 0 },
        new[] { 0f, 2.5f, 4f, 6f, 8f },
        new[] { 2f, 1.25f, 1.75f, 2f, 4f });

    public static MotifShape Warden { get; } = new("warden",
        WardenIntervals,
        new[] { 0f, 2.5f, 5f, 7.5f, 10f },
        new[] { 2.5f, 2.5f, 2.5f, 2.5f, 5f });

    /// <summary>The regional motifs in region order (Garden to Citadel), for the finale and the final region's theme.</summary>
    public static IReadOnlyList<MotifShape> Regional { get; } = new[] { Garden, Caves, Wilds, Sunken, Clockwork, Frozen, Warden };

    public static MotifShape? Find(string id) => Regional.FirstOrDefault(m => m.Id == id);
}
