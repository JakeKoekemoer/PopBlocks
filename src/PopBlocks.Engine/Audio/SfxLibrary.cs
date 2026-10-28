using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Audio;

/// <summary>
/// The sounds that are the same everywhere in the game: movement, landing, colour pops, UI and a few story moments.
/// All of them are written in C and moved to the current region's key with the <c>semitones</c> argument of
/// <see cref="AudioEngine.PlayCue"/>, so they never clash with the music. Nothing here is a recording.
///
/// Rules these follow (from the audio spec): short, soft-edged, nothing long enough to clutter fast play, no harsh buzzers,
/// and no sound that relies on pitch alone to tell colours apart.
/// </summary>
public static class SfxLibrary
{
    // ---- small builders --------------------------------------------------------------------------------------------------

    internal static CueNote Note(InstrumentPatch patch, float midi, float delayMs, float durationMs, float velocity,
        float pan = 0f, float gain = 1f, bool transposes = true, float glideSemis = 0f, float glideMs = 0f) =>
        new()
        {
            Patch = patch, Midi = midi, DelayMs = delayMs, DurationMs = durationMs, Velocity = velocity,
            Pan = pan, Gain = gain, Transposes = transposes, GlideSemis = glideSemis, GlideMs = glideMs,
        };

    /// <summary>A sweep of filtered noise from one centre frequency to another (a whoosh). It never follows a key.</summary>
    internal static CueNote Noise(InstrumentPatch patch, float delayMs, float durationMs, float fromHz, float toHz,
        float velocity, float gain = 1f, float pan = 0f) =>
        new()
        {
            Patch = patch, Hz = 440f, DelayMs = delayMs, DurationMs = durationMs, Velocity = velocity, Gain = gain, Pan = pan,
            NoiseFromHz = fromHz, NoiseToHz = toHz, NoiseSweepMs = durationMs, Transposes = false,
        };

    // ---- movement and landing --------------------------------------------------------------------------------------------

    /// <summary>A tiny rounded tick.</summary>
    public static SfxCue Rotate { get; } = new()
    {
        Name = "rotate",
        Notes = new[] { Note(Patches.Tick, 84f, 0, 60, 0.72f, gain: 1.0f), Note(Patches.PureTone, 91f, 0, 70, 0.10f, gain: 0.7f) },
    };

    /// <summary>A short low thump with a faint tonal body, so it sits in the key.</summary>
    public static SfxCue Place { get; } = new()
    {
        Name = "place",
        Notes = new[]
        {
            Note(Patches.Thump, 43f, 0, 130, 0.62f, transposes: false),
            Note(Patches.WoodNote, 55f, 0, 160, 0.26f, gain: 0.8f),
        },
    };

    /// <summary>A quick falling whoosh that lands in the same thump as <see cref="Place"/>, only a little firmer.</summary>
    public static SfxCue HardDrop { get; } = new()
    {
        Name = "hard-drop",
        Notes = new[]
        {
            Noise(Patches.Whoosh, 0, 110, 2400f, 380f, 0.5f, gain: 0.9f),
            Note(Patches.Thump, 41f, 95, 150, 0.78f, transposes: false),
            Note(Patches.WoodNote, 53f, 95, 190, 0.32f, gain: 0.8f),
        },
    };

    // ---- colour pops ---------------------------------------------------------------------------------------------------

    private static readonly InstrumentPatch[] PopPatches =
    {
        Patches.PureTone,        // 0 = Empty, never used
        Patches.BrightPluck,     // Cyan:   clean, bright pluck
        Patches.RoundBell,       // Coral:  slightly rounded bell
        Patches.WoodNote,        // Yellow: warm, woody
        Patches.GlassChime,      // Purple: glassy, airy
        Patches.WoodenMarimba,   // Lime:   soft, lively marimba
    };

    // Written as notes of C (a root or a fifth) so any region's scale can pull them to a nearby note.
    private static readonly int[] PopBase = { 72, 79, 72, 67, 84, 60 };

    // Evens out how loud each colour sounds (measured as RMS) so no colour jumps out; the timbres differ, the loudness does not.
    private static readonly float[] PopGain = { 1f, 1.30f, 0.70f, 1.30f, 0.94f, 1.20f };

    private static readonly SfxCue[,] Pops = BuildPops();

    private static SfxCue[,] BuildPops()
    {
        var cues = new SfxCue[6, 3];
        for (int colour = 1; colour <= 5; colour++)
            for (int tier = 0; tier < 3; tier++)
            {
                var patch = PopPatches[colour];
                float m = PopBase[colour];
                float g = PopGain[colour];
                var notes = new List<CueNote>
                {
                    Note(patch, m, 0, 150, 0.64f, gain: g),
                    Note(Patches.PureTone, m + 12f, 0, 55, 0.16f, gain: 0.6f),   // a small spark on top, for every colour
                };
                if (tier >= 1) notes.Add(Note(patch, m + 7f, 12, 190, 0.30f, pan: -0.15f, gain: g));
                if (tier >= 2)
                {
                    notes.Add(Note(patch, m + 12f, 22, 240, 0.26f, pan: 0.15f, gain: g));
                    notes.Add(Note(Patches.WarmPad, m - 12f, 0, 260, 0.20f, gain: 0.6f));
                }
                cues[colour, tier] = new SfxCue { Name = $"pop-{(CellColour)colour}-{tier}", Notes = notes.ToArray() };
            }
        return cues;
    }

    /// <summary>The pop for a group of this colour and size: 1–3 blocks of a kind are a single note, bigger groups are fuller.</summary>
    public static SfxCue ColourPop(CellColour colour, int groupSize)
    {
        int index = (int)colour is >= 1 and <= 5 ? (int)colour : 1;
        int tier = groupSize >= 6 ? 2 : groupSize >= 4 ? 1 : 0;
        return Pops[index, tier];
    }

    /// <summary>The note a colour's pop is written on (in C). Used to find the shift that lands it in the scale.</summary>
    public static int PopBaseNote(CellColour colour) => PopBase[(int)colour is >= 1 and <= 5 ? (int)colour : 1];

    /// <summary>
    /// How far to move a colour pop so that it is a note of the region's scale, <paramref name="chainStep"/> steps up a short
    /// phrase. <paramref name="groupOrder"/> (0, 1, 2) harmonises several groups popping in one wave. The phrase climbs one
    /// scale step per chain and then hovers on its top two notes, so a long chain never climbs out of range.
    /// </summary>
    public static float PopSemitones(CellColour colour, IReadOnlyList<int> scale, int keyOffset, int chainStep, int groupOrder = 0)
    {
        int step = Math.Max(0, chainStep - 1);
        int phrase = step <= 5 ? step : (step % 2 == 0 ? 5 : 4);
        int degree = phrase + groupOrder * 2;
        int target = PopBaseNote(colour) + ScaleOffset(scale, degree);
        int snapped = SnapToScale(target, scale);
        return snapped - PopBaseNote(colour) + keyOffset;
    }

    /// <summary>Semitones above the root of scale degree <paramref name="degree"/> (0 = root; degrees past the top wrap up an octave).</summary>
    public static int ScaleOffset(IReadOnlyList<int> scale, int degree)
    {
        int n = scale.Count;
        int octave = degree >= 0 ? degree / n : -((-degree + n - 1) / n);
        int index = degree - octave * n;
        return octave * 12 + scale[index];
    }

    /// <summary>The nearest note of <paramref name="scale"/> (taking the lower one on a tie), given a note counted up from C.</summary>
    public static int SnapToScale(int semitonesFromC, IReadOnlyList<int> scale)
    {
        int best = semitonesFromC, bestDistance = int.MaxValue;
        int octave = (int)MathF.Floor(semitonesFromC / 12f);
        for (int o = octave - 1; o <= octave + 1; o++)
            foreach (int s in scale)
            {
                int candidate = o * 12 + s;
                int d = Math.Abs(candidate - semitonesFromC);
                if (d < bestDistance || (d == bestDistance && candidate < best)) { best = candidate; bestDistance = d; }
            }
        return best;
    }

    /// <summary>
    /// Shifts a note written in C to the key of <paramref name="rootMidi"/>, as the smallest move (−6..+5 semitones), so a cue
    /// never jumps by more than half an octave.
    /// </summary>
    public static int KeyOffset(int rootMidi) => ((rootMidi % 12) + 18) % 12 - 6;

    // ---- user interface ---------------------------------------------------------------------------------------------------

    public static SfxCue ButtonTap { get; } = new()
    {
        Name = "button-tap",
        Notes = new[] { Note(Patches.Tick, 86f, 0, 45, 0.60f, gain: 0.9f), Note(Patches.PureTone, 79f, 0, 70, 0.14f, gain: 0.7f) },
    };

    public static SfxCue MenuOpen { get; } = new()
    {
        Name = "menu-open",
        Notes = new[] { Note(Patches.SoftPluck, 72f, 0, 130, 0.38f), Note(Patches.SoftPluck, 79f, 80, 200, 0.40f) },
    };

    public static SfxCue MenuClose { get; } = new()
    {
        Name = "menu-close",
        Notes = new[] { Note(Patches.SoftPluck, 79f, 0, 120, 0.34f), Note(Patches.SoftPluck, 72f, 80, 190, 0.34f) },
    };

    /// <summary>A muted double tap. Low and soft on purpose; never an error buzz.</summary>
    public static SfxCue Locked { get; } = new()
    {
        Name = "locked",
        Notes = new[] { Note(Patches.WoodNote, 50f, 0, 80, 0.42f, gain: 0.8f), Note(Patches.WoodNote, 50f, 95, 90, 0.34f, gain: 0.8f) },
    };

    /// <summary>One quiet confirmation tone, played when a setting changes.</summary>
    public static SfxCue SettingsChange { get; } = new()
    {
        Name = "settings-change",
        Notes = new[] { Note(Patches.PureTone, 84f, 0, 140, 0.30f), Note(Patches.PureTone, 91f, 60, 160, 0.14f, gain: 0.7f) },
    };

    public static SfxCue Pause { get; } = new()
    {
        Name = "pause",
        Notes = new[] { Note(Patches.RoundBell, 76f, 0, 220, 0.30f), Note(Patches.RoundBell, 69f, 110, 300, 0.26f) },
    };

    public static SfxCue Resume { get; } = new()
    {
        Name = "resume",
        Notes = new[] { Note(Patches.RoundBell, 69f, 0, 200, 0.26f), Note(Patches.RoundBell, 76f, 110, 300, 0.30f) },
    };

    // ---- story moments ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The Warden is revealed: one restrained low resonance and then silence. Nothing in the music is added under it, and the
    /// music is lowered while it lasts.
    /// </summary>
    public static SfxCue WardenReveal { get; } = new()
    {
        Name = "warden-reveal",
        Notes = new[]
        {
            Note(Patches.WarmPad, 36f, 0, 2600, 0.42f, transposes: false),
            Note(Patches.PureTone, 48f, 120, 1800, 0.12f, transposes: false, gain: 0.8f),
        },
        DuckDb = 9f, DuckMs = 4200f,
    };

    /// <summary>All the cues that do not depend on a region, for tests and the Music Lab.</summary>
    public static IReadOnlyList<SfxCue> Globals { get; } = new[]
    {
        Rotate, Place, HardDrop, ButtonTap, MenuOpen, MenuClose, Locked, SettingsChange, Pause, Resume, WardenReveal,
    };
}
