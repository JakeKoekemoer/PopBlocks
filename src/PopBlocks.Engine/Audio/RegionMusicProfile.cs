namespace PopBlocks.Engine.Audio;

public enum RhythmStyle { Sparse, Flowing, Playful, Mechanical, Slow }

public enum BassStyle { None, Root, RootFifth, Pulse, Drone }

public enum PulseStyle { None, SoftBeat, Ticks, Mechanical }

public enum CounterStyle { None, Interlock, Arpeggio, Glass, Ticks }

/// <summary>A chord: its root as semitones above the key's root, and its notes as semitones above that root.</summary>
public sealed record ChordSpec(int Root, int[] Tones, string Name)
{
    public static ChordSpec Of(string name, int root, params int[] tones) => new(root, tones, name);
}

/// <summary>
/// How one statement of a motif is played in one restoration state: which of its notes appear, when (in beats from the
/// start of the statement), for how long, and how softly. Echoes repeat the last notes more quietly.
/// </summary>
public sealed record MotifTreatment
{
    public int[] Notes { get; init; } = { 0, 1, 2, 3, 4 };
    public float[] OnsetBeats { get; init; } = { 0f, 1f, 2f, 3.5f, 5f };
    public float[] DurationBeats { get; init; } = { 1f, 1f, 1.5f, 1.5f, 3f };
    public float Velocity { get; init; } = 0.62f;

    /// <summary>How many of the last notes are repeated as quiet echoes (0 = none).</summary>
    public int EchoNotes { get; init; }
    public float EchoDelayBeats { get; init; } = 0.75f;
    public float EchoDecay { get; init; } = 0.45f;

    /// <summary>The motif is answered an octave higher, two beats later, by the accent instrument ("connected").</summary>
    public bool Answer { get; init; }

    /// <summary>A fragment, as the faded world remembers it: four notes, spaced out, the last left hanging.</summary>
    public static MotifTreatment Faded { get; } = new()
    {
        Notes = new[] { 0, 1, 2, 3 },
        OnsetBeats = new[] { 0f, 1.5f, 3f, 4.5f },
        DurationBeats = new[] { 1.5f, 1.5f, 1.5f, 3.5f },
        Velocity = 0.45f,
    };

    /// <summary>Remembered: the notes come back with echoes, and the fifth arrives tentatively.</summary>
    public static MotifTreatment Recovering { get; } = new()
    {
        Notes = new[] { 0, 1, 2, 3, 4 },
        OnsetBeats = new[] { 0f, 1.25f, 2.5f, 4f, 6f },
        DurationBeats = new[] { 1.25f, 1.25f, 1.5f, 2f, 2.5f },
        Velocity = 0.52f,
        EchoNotes = 2,
    };

    /// <summary>Resolved: all five notes, settling into a warm, held last note, answered an octave up.</summary>
    public static MotifTreatment Restored { get; } = new()
    {
        Notes = new[] { 0, 1, 2, 3, 4 },
        OnsetBeats = new[] { 0f, 1f, 2f, 3.5f, 5f },
        DurationBeats = new[] { 1f, 1f, 1.5f, 1.5f, 3f },
        Velocity = 0.66f,
        Answer = true,
    };
}

/// <summary>Where, and in what voice, a motif is stated in a piece.</summary>
public sealed record MotifPlan
{
    /// <summary>Which motif: "spectrum" (the profile's own five notes), "warden", or a regional motif id ("garden", "caves", ...).</summary>
    public string Kind { get; init; } = "spectrum";

    /// <summary>Overrides the motif's notes (semitones above the register's root). Null = the motif's own.</summary>
    public int[]? Intervals { get; init; }

    /// <summary>Bars (0-based, within the piece) where a statement starts. Statements start on a phrase (every 4th bar).</summary>
    public int[] Bars { get; init; } = { 0, 8, 24 };

    public string Patch { get; init; } = "SoftPluck";

    /// <summary>Whole octaves added to the melody register.</summary>
    public int Octave { get; init; }

    /// <summary>How the statement is played in each state. Null = the usual treatment for this kind of motif.</summary>
    public MotifTreatment? Faded { get; init; }
    public MotifTreatment? Recovering { get; init; }
    public MotifTreatment? Restored { get; init; }

    /// <summary>The melody keeps silent for this many bars after a statement starts, so the two do not talk over each other.</summary>
    public int MelodyRestBars { get; init; } = 2;
}

/// <summary>
/// Everything that makes one region's music its own. The composer reads only this, so a new region (or a retune) is a change
/// to data. The numbers below the shared basics are composition "knobs": densities, styles and registers.
/// </summary>
public sealed class RegionMusicProfile
{
    public RegionId Region { get; init; }
    public string FadedThemeId { get; init; } = string.Empty;
    public string RestoredThemeId { get; init; } = string.Empty;

    /// <summary>Beats per minute, inside the range the music spec gives this region.</summary>
    public int TempoBpm { get; init; } = 70;

    /// <summary>The key's root note (MIDI; 60 is middle C).</summary>
    public int RootMidiNote { get; init; } = 60;

    /// <summary>The notes of the melody's scale, in semitones above the root.</summary>
    public int[] ScaleIntervals { get; init; } = [];

    /// <summary>The five-note Spectrum motif in semitones above the root. The shape stays the same everywhere; the odd note bends to fit a region's mode.</summary>
    public int[] SpectrumMotifIntervals { get; init; } = [];

    public string MelodyPatch { get; init; } = "SoftPluck";
    public string HarmonyPatch { get; init; } = "WarmPad";
    public string AccentPatch { get; init; } = "SpectrumBell";
    public string BassPatch { get; init; } = "SoftBass";
    public string CounterPatch { get; init; } = "WoodenMarimba";
    public string PulsePatch { get; init; } = "ClockPulse";

    /// <summary>How busy the music is in ordinary play (0..1).</summary>
    public float BaseIntensity { get; init; } = 0.35f;

    public int VariationSeed { get; init; }

    // ---- Form and harmony
    public int Bars { get; init; } = 32;
    public ChordSpec[] ChordsA { get; init; } = [];
    public ChordSpec[] ChordsB { get; init; } = [];

    // ---- Melody
    public RhythmStyle Rhythm { get; init; } = RhythmStyle.Flowing;

    /// <summary>Lowest and highest scale degrees the melody may use (0 = the root at <see cref="MelodyBaseMidi"/>).</summary>
    public int MelodyLowDegree { get; init; } = -2;
    public int MelodyHighDegree { get; init; } = 7;
    public int MelodyBaseMidi { get; init; } = 60;

    /// <summary>Chance a whole bar of melody is left silent.</summary>
    public float RestBarChance { get; init; } = 0.12f;

    /// <summary>In the faded world, the chance a note is missing (so phrases have gaps).</summary>
    public float FadedDropChance { get; init; } = 0.25f;

    /// <summary>Chance of a leap of a fourth or more.</summary>
    public float LeapChance { get; init; } = 0.08f;

    /// <summary>The motif statements in this piece.</summary>
    public MotifPlan[] Motifs { get; init; } = [];

    // ---- Other strands
    public BassStyle Bass { get; init; } = BassStyle.Root;
    public PulseStyle Pulse { get; init; } = PulseStyle.None;
    public CounterStyle Counter { get; init; } = CounterStyle.Interlock;

    /// <summary>Single high notes per eight bars while faded ("distant fireflies"), and while restored.</summary>
    public int AccentsFaded { get; init; } = 3;
    public int AccentsRestored { get; init; } = 8;

    /// <summary>The pad changes every this many bars while faded (restored always changes every bar).</summary>
    public int PadBarsFaded { get; init; } = 2;

    // ---- Space and ambience
    public float ReverbRoom { get; init; } = 0.5f;
    public float ReverbDamping { get; init; } = 0.4f;
    public float ReverbWet { get; init; } = 0.25f;
    public AmbienceStyle? Ambience { get; init; }
}
