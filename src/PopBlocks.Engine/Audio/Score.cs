namespace PopBlocks.Engine.Audio;

/// <summary>Which of the game's places a piece of music belongs to. 0 is the menu and the map.</summary>
public enum RegionId
{
    Menu = 0,
    ForgottenGarden = 1,
    CrystalCaves = 2,
    VerdantWilds = 3,
    SunkenCity = 4,
    ClockworkWastes = 5,
    FrozenExpanse = 6,
    ShatteredCitadel = 7,
    HeartOfSpectrum = 8,
}

/// <summary>
/// The strands of an arrangement. Each can be brought in or out on its own, on a musical boundary, so the music responds
/// to play without ever restarting.
/// </summary>
public enum Layer : byte
{
    Pad = 0,
    Bass = 1,
    Melody = 2,
    Motif = 3,
    Accent = 4,
    Counter = 5,
    Chain = 6,
    Pulse = 7,
    Tension = 8,
}

/// <summary>Which restoration states a note belongs to. A note plays only while the region is in one of its states.</summary>
[Flags]
public enum StateMask : byte
{
    None = 0,
    Faded = 1,
    Recovering = 2,
    Restored = 4,

    /// <summary>Plays while the region is still faded or recovering (hanging phrases, hollow chords).</summary>
    Calm = Faded | Recovering,

    /// <summary>Plays once colour has started to return.</summary>
    Warm = Recovering | Restored,

    All = Faded | Recovering | Restored,
}

/// <summary>When a change to the arrangement takes effect.</summary>
public enum Quantize : byte
{
    Immediate = 0,
    NextBeat = 1,
    NextBar = 2,
    NextPhrase = 3,
}

/// <summary>One note of a composed piece. Times are in ticks (<see cref="MusicScore.TicksPerBeat"/> per beat).</summary>
public readonly record struct NoteEvent(
    int Start,
    int Duration,
    byte Midi,
    byte Velocity,
    Layer Layer,
    byte Patch,
    sbyte Pan,
    StateMask States);

/// <summary>
/// A composed piece, ready to play: every note of every state, plus the instruments they use. Scores are built once by the
/// <see cref="MusicComposer"/> and never changed afterwards, so the audio thread can read one while the game builds the next.
/// </summary>
public sealed class MusicScore
{
    public const int TicksPerBeat = 96;
    public const int LayerCount = 9;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public RegionId Region { get; init; }
    public int TempoBpm { get; init; } = 70;
    public int BeatsPerBar { get; init; } = 4;
    public int Bars { get; init; } = 32;

    /// <summary>Bars in a phrase: the boundary used for the larger changes.</summary>
    public int PhraseBars { get; init; } = 4;

    public required NoteEvent[] Events { get; init; }
    public required InstrumentPatch[] Patches { get; init; }

    /// <summary>The key: the root note and the notes of the scale. The effects use them so they sound in key.</summary>
    public int RootMidi { get; init; } = 60;

    public int[] Scale { get; init; } = { 0, 2, 4, 7, 9 };

    public float ReverbRoom { get; init; } = 0.5f;
    public float ReverbDamping { get; init; } = 0.4f;
    public float ReverbWet { get; init; } = 0.25f;

    public int TicksPerBar => BeatsPerBar * TicksPerBeat;
    public int LoopTicks => Bars * TicksPerBar;
    public double LoopSeconds => LoopTicks / (double)TicksPerBeat * 60.0 / TempoBpm;
}
