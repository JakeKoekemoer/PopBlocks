namespace PopBlocks.Engine.Audio;

/// <summary>How a patch's loudness behaves over the life of a note.</summary>
public enum EnvelopeShape
{
    /// <summary>Struck or plucked: a fast attack, then a natural decay. The note rings on by itself.</summary>
    Percussive,

    /// <summary>Held: attack, decay to a sustain level, then release when the note ends (pads, bass, air).</summary>
    Sustained,
}

/// <summary>The three mixer buses. Each has its own volume, mute and reverb.</summary>
public enum Bus
{
    Music = 0,
    Sfx = 1,
    Ambience = 2,
}

/// <summary>
/// A recipe for one instrument-like sound, made of a few sine partials plus an optional band of noise. All numbers are
/// plain data so a sound can be tuned without touching the synth. Nothing here is a sample: every sound is calculated.
/// </summary>
public sealed class InstrumentPatch
{
    public const int MaxPartials = 8;

    public string Id { get; init; } = "";

    /// <summary>Frequency of each partial as a multiple of the note's pitch. A ratio of 1.004 next to 1 gives a slow shimmer.</summary>
    public float[] Ratios { get; init; } = { 1f };

    /// <summary>Loudness of each partial (same length as <see cref="Ratios"/>).</summary>
    public float[] Amps { get; init; } = { 1f };

    /// <summary>How much faster than the first partial each one dies away (1 = same speed). Higher partials usually die sooner.</summary>
    public float[] DecayMul { get; init; } = { 1f };

    /// <summary>Which side each partial sits on, -1 left to +1 right, added to the note's pan. Lets a pad be wide.</summary>
    public float[] Spread { get; init; } = { 0f };

    public EnvelopeShape Shape { get; init; } = EnvelopeShape.Percussive;

    /// <summary>Seconds to reach full level. Never shorter than ~2 ms inside the synth, so nothing clicks.</summary>
    public float Attack { get; init; } = 0.003f;

    /// <summary>Percussive: seconds for the first partial to fall by 60 dB. Sustained: seconds to settle onto <see cref="Sustain"/>.</summary>
    public float Decay { get; init; } = 0.6f;

    public float Sustain { get; init; } = 0.7f;

    /// <summary>Seconds to fade out after the note ends. A percussive patch only uses it when <see cref="Damp"/> is set.</summary>
    public float Release { get; init; } = 0.12f;

    /// <summary>A percussive patch stops at the end of its written length instead of ringing out.</summary>
    public bool Damp { get; init; }

    public float VibratoHz { get; init; }

    /// <summary>Vibrato depth in cents (hundredths of a semitone).</summary>
    public float VibratoCents { get; init; }

    public float VibratoDelay { get; init; } = 0.25f;

    /// <summary>Loudness of the noise band (0 = none).</summary>
    public float NoiseAmount { get; init; }

    /// <summary>Seconds for the noise to fall by 60 dB. 0 = it follows the note's envelope instead.</summary>
    public float NoiseDecay { get; init; }

    /// <summary>Centre of the noise band in Hz.</summary>
    public float NoiseHz { get; init; } = 2000f;

    public float NoiseQ { get; init; } = 1.2f;

    /// <summary>The pitch starts this many semitones above the note and falls onto it (thumps, bloops).</summary>
    public float PitchDropSemis { get; init; }

    public float PitchDropSeconds { get; init; } = 0.05f;

    /// <summary>How much louder notes reveal their upper partials (0 = none). A soft note is rounder than a hard one.</summary>
    public float Brightness { get; init; } = 0.4f;

    /// <summary>Overall level, set so different patches feel equally loud at the same velocity.</summary>
    public float Gain { get; init; } = 1f;

    /// <summary>How much of the voice goes to its bus's reverb (0..1).</summary>
    public float ReverbSend { get; init; } = 0.2f;

    public int PartialCount => Ratios.Length;
}

/// <summary>The curated set of original timbres (spec section 4.1), plus the small UI and effect sounds built from the same parts.</summary>
public static class Patches
{
    // ---- Music and colour timbres ------------------------------------------------------------------------------------

    /// <summary>A plain sine with a short envelope: precise, for UI and subtle accents.</summary>
    public static readonly InstrumentPatch PureTone = new()
    {
        Id = "PureTone",
        Ratios = new[] { 1f }, Amps = new[] { 1f }, DecayMul = new[] { 1f }, Spread = new[] { 0f },
        Attack = 0.004f, Decay = 0.30f, Brightness = 0f, Gain = 0.55f, ReverbSend = 0.15f,
    };

    /// <summary>Glassy bell: a pair of slightly detuned fundamentals and a few inharmonic partials that die away fast.</summary>
    public static readonly InstrumentPatch SpectrumBell = new()
    {
        Id = "SpectrumBell",
        Ratios = new[] { 1f, 1.0035f, 2.0f, 2.76f, 5.4f },
        Amps = new[] { 0.7f, 0.5f, 0.32f, 0.2f, 0.07f },
        DecayMul = new[] { 1f, 1f, 1.5f, 2.2f, 3.6f },
        Spread = new[] { -0.2f, 0.2f, 0f, -0.15f, 0.15f },
        Attack = 0.002f, Decay = 2.2f, Brightness = 0.55f, Gain = 0.50f, ReverbSend = 0.38f,
    };

    /// <summary>A softer, rounder bell: fewer, closer partials. For Coral bursts.</summary>
    public static readonly InstrumentPatch RoundBell = new()
    {
        Id = "RoundBell",
        Ratios = new[] { 1f, 1.0025f, 2.0f, 3.0f },
        Amps = new[] { 0.7f, 0.5f, 0.28f, 0.1f },
        DecayMul = new[] { 1f, 1f, 1.7f, 2.6f },
        Spread = new[] { -0.15f, 0.15f, 0f, 0f },
        Attack = 0.003f, Decay = 1.1f, Brightness = 0.35f, Gain = 0.55f, ReverbSend = 0.3f,
    };

    /// <summary>An airy glass chime: a high shimmer that fades quickly. For Purple bursts and crystals.</summary>
    public static readonly InstrumentPatch GlassChime = new()
    {
        Id = "GlassChime",
        Ratios = new[] { 1f, 1.005f, 2.76f, 4.07f },
        Amps = new[] { 0.6f, 0.5f, 0.22f, 0.1f },
        DecayMul = new[] { 1f, 1f, 2.0f, 3.2f },
        Spread = new[] { -0.3f, 0.3f, -0.1f, 0.1f },
        Attack = 0.002f, Decay = 1.4f, Brightness = 0.5f, Gain = 0.50f, ReverbSend = 0.42f,
    };

    /// <summary>Plucked string: upper partials die faster than the fundamental, so it darkens as it rings.</summary>
    public static readonly InstrumentPatch SoftPluck = new()
    {
        Id = "SoftPluck",
        Ratios = new[] { 1f, 2f, 3f, 4f, 5f, 6f },
        Amps = new[] { 1f, 0.55f, 0.30f, 0.16f, 0.09f, 0.04f },
        DecayMul = new[] { 1f, 1.5f, 2.2f, 3.2f, 4.5f, 6f },
        Spread = new[] { 0f, 0f, 0f, 0f, 0f, 0f },
        Attack = 0.002f, Decay = 1.0f, Damp = true, Release = 0.14f, Brightness = 0.6f, Gain = 0.45f, ReverbSend = 0.25f,
        NoiseAmount = 0.04f, NoiseDecay = 0.015f, NoiseHz = 3000f,
    };

    /// <summary>A brighter pluck for Cyan bursts.</summary>
    public static readonly InstrumentPatch BrightPluck = new()
    {
        Id = "BrightPluck",
        Ratios = new[] { 1f, 2f, 3f, 4f, 5f },
        Amps = new[] { 0.9f, 0.65f, 0.42f, 0.24f, 0.12f },
        DecayMul = new[] { 1f, 1.4f, 2.0f, 2.8f, 3.8f },
        Spread = new[] { 0f, 0f, 0f, 0f, 0f },
        Attack = 0.0015f, Decay = 0.55f, Damp = true, Release = 0.1f, Brightness = 0.7f, Gain = 0.42f, ReverbSend = 0.22f,
        NoiseAmount = 0.05f, NoiseDecay = 0.012f, NoiseHz = 3500f,
    };

    /// <summary>Rounded mallet note: near-harmonic partials (1 : 4 : 10) and a tiny wooden tap.</summary>
    public static readonly InstrumentPatch WoodenMarimba = new()
    {
        Id = "WoodenMarimba",
        Ratios = new[] { 1f, 4f, 9.9f },
        Amps = new[] { 1f, 0.42f, 0.10f },
        DecayMul = new[] { 1f, 3.5f, 6f },
        Spread = new[] { 0f, 0f, 0f },
        Attack = 0.0015f, Decay = 0.42f, Damp = true, Release = 0.08f, Brightness = 0.5f, Gain = 0.55f, ReverbSend = 0.22f,
        NoiseAmount = 0.10f, NoiseDecay = 0.010f, NoiseHz = 1800f,
    };

    /// <summary>A warm, shorter wooden note for Amber bursts.</summary>
    public static readonly InstrumentPatch WoodNote = new()
    {
        Id = "WoodNote",
        Ratios = new[] { 1f, 2f, 4.2f },
        Amps = new[] { 1f, 0.35f, 0.08f },
        DecayMul = new[] { 1f, 2.5f, 5f },
        Spread = new[] { 0f, 0f, 0f },
        Attack = 0.002f, Decay = 0.30f, Damp = true, Release = 0.07f, Brightness = 0.35f, Gain = 0.60f, ReverbSend = 0.18f,
        NoiseAmount = 0.08f, NoiseDecay = 0.009f, NoiseHz = 1400f,
    };

    /// <summary>Several quiet, slightly detuned partials with a slow attack and release.</summary>
    public static readonly InstrumentPatch WarmPad = new()
    {
        Id = "WarmPad",
        Ratios = new[] { 1f, 1.0045f, 0.9955f, 2f, 2.004f, 0.5f },
        Amps = new[] { 0.40f, 0.30f, 0.30f, 0.12f, 0.09f, 0.16f },
        DecayMul = new[] { 1f, 1f, 1f, 1f, 1f, 1f },
        Spread = new[] { 0f, -0.6f, 0.6f, -0.35f, 0.35f, 0f },
        Shape = EnvelopeShape.Sustained,
        Attack = 0.9f, Decay = 1.5f, Sustain = 0.85f, Release = 1.4f,
        VibratoHz = 0.22f, VibratoCents = 3.5f, VibratoDelay = 0.8f,
        Brightness = 0.2f, Gain = 0.34f, ReverbSend = 0.40f,
    };

    /// <summary>A low sine with a little second and third harmonic so a phone speaker can still hear it.</summary>
    public static readonly InstrumentPatch SoftBass = new()
    {
        Id = "SoftBass",
        Ratios = new[] { 1f, 2f, 3f },
        Amps = new[] { 1f, 0.38f, 0.10f },
        DecayMul = new[] { 1f, 1.4f, 2.4f },
        Spread = new[] { 0f, 0f, 0f },
        Shape = EnvelopeShape.Sustained,
        Attack = 0.012f, Decay = 0.7f, Sustain = 0.62f, Release = 0.28f,
        Brightness = 0.25f, Gain = 0.42f, ReverbSend = 0.06f,
        NoiseAmount = 0.03f, NoiseDecay = 0.02f, NoiseHz = 400f,
    };

    /// <summary>Filtered noise with a slow swell: wind, breath, sweeps. Its pitch argument sets the centre of the band.</summary>
    public static readonly InstrumentPatch AirNoise = new()
    {
        Id = "AirNoise",
        Ratios = Array.Empty<float>(), Amps = Array.Empty<float>(), DecayMul = Array.Empty<float>(), Spread = Array.Empty<float>(),
        Shape = EnvelopeShape.Sustained,
        Attack = 0.8f, Decay = 0.5f, Sustain = 1f, Release = 1.2f,
        NoiseAmount = 1f, NoiseDecay = 0f, NoiseHz = 700f, NoiseQ = 0.8f,
        Brightness = 0f, Gain = 0.30f, ReverbSend = 0.30f,
    };

    /// <summary>A quick band of noise for a falling or rising whoosh. Short attack, short tail: use with a noise sweep.</summary>
    public static readonly InstrumentPatch Whoosh = new()
    {
        Id = "Whoosh",
        Ratios = Array.Empty<float>(), Amps = Array.Empty<float>(), DecayMul = Array.Empty<float>(), Spread = Array.Empty<float>(),
        Shape = EnvelopeShape.Sustained,
        Attack = 0.012f, Decay = 0.1f, Sustain = 1f, Release = 0.09f,
        NoiseAmount = 1f, NoiseDecay = 0f, NoiseHz = 1200f, NoiseQ = 0.9f,
        Brightness = 0f, Gain = 0.34f, ReverbSend = 0.12f,
    };

    /// <summary>A slower swell of noise for a restoration sweep: it blooms in, then lets go gently.</summary>
    public static readonly InstrumentPatch Swell = new()
    {
        Id = "Swell",
        Ratios = Array.Empty<float>(), Amps = Array.Empty<float>(), DecayMul = Array.Empty<float>(), Spread = Array.Empty<float>(),
        Shape = EnvelopeShape.Sustained,
        Attack = 0.3f, Decay = 0.3f, Sustain = 1f, Release = 0.55f,
        NoiseAmount = 1f, NoiseDecay = 0f, NoiseHz = 600f, NoiseQ = 1.6f,
        Brightness = 0f, Gain = 0.30f, ReverbSend = 0.35f,
    };

    /// <summary>A short tonal tick with a breath of noise.</summary>
    public static readonly InstrumentPatch ClockPulse = new()
    {
        Id = "ClockPulse",
        Ratios = new[] { 1f, 2.95f },
        Amps = new[] { 1f, 0.35f },
        DecayMul = new[] { 1f, 1.8f },
        Spread = new[] { 0f, 0f },
        Attack = 0.001f, Decay = 0.07f, Brightness = 0.3f, Gain = 0.40f, ReverbSend = 0.10f,
        NoiseAmount = 0.30f, NoiseDecay = 0.012f, NoiseHz = 3200f, NoiseQ = 1.6f,
    };

    // ---- Effect and UI building blocks -------------------------------------------------------------------------------

    /// <summary>A low-mid thump: a sine that falls onto its note, with a little body above it.</summary>
    public static readonly InstrumentPatch Thump = new()
    {
        Id = "Thump",
        Ratios = new[] { 1f, 2f },
        Amps = new[] { 1f, 0.45f },
        DecayMul = new[] { 1f, 1.6f },
        Spread = new[] { 0f, 0f },
        Attack = 0.002f, Decay = 0.16f, PitchDropSemis = 9f, PitchDropSeconds = 0.05f,
        Brightness = 0.2f, Gain = 0.62f, ReverbSend = 0.05f,
        NoiseAmount = 0.10f, NoiseDecay = 0.025f, NoiseHz = 500f,
    };

    /// <summary>A small rounded tick (rotation, taps).</summary>
    public static readonly InstrumentPatch Tick = new()
    {
        Id = "Tick",
        Ratios = new[] { 1f, 2.1f },
        Amps = new[] { 1f, 0.25f },
        DecayMul = new[] { 1f, 2f },
        Spread = new[] { 0f, 0f },
        Attack = 0.0015f, Decay = 0.05f, Brightness = 0.2f, Gain = 0.50f, ReverbSend = 0.06f,
        NoiseAmount = 0.12f, NoiseDecay = 0.006f, NoiseHz = 2600f,
    };

    /// <summary>A sine that falls onto its note: the body of a soft "bloop" or ripple.</summary>
    public static readonly InstrumentPatch Bloop = new()
    {
        Id = "Bloop",
        Ratios = new[] { 1f, 2f },
        Amps = new[] { 1f, 0.2f },
        DecayMul = new[] { 1f, 2f },
        Spread = new[] { 0f, 0f },
        Attack = 0.003f, Decay = 0.22f, PitchDropSemis = 7f, PitchDropSeconds = 0.09f,
        Brightness = 0f, Gain = 0.50f, ReverbSend = 0.25f,
    };

    /// <summary>Every patch by id, for profiles that name their instruments as text.</summary>
    public static IReadOnlyDictionary<string, InstrumentPatch> ById { get; } = new Dictionary<string, InstrumentPatch>
    {
        [PureTone.Id] = PureTone,
        [SpectrumBell.Id] = SpectrumBell,
        [RoundBell.Id] = RoundBell,
        [GlassChime.Id] = GlassChime,
        [SoftPluck.Id] = SoftPluck,
        [BrightPluck.Id] = BrightPluck,
        [WoodenMarimba.Id] = WoodenMarimba,
        [WoodNote.Id] = WoodNote,
        [WarmPad.Id] = WarmPad,
        [SoftBass.Id] = SoftBass,
        [AirNoise.Id] = AirNoise,
        [Whoosh.Id] = Whoosh,
        [Swell.Id] = Swell,
        [ClockPulse.Id] = ClockPulse,
        [Thump.Id] = Thump,
        [Tick.Id] = Tick,
        [Bloop.Id] = Bloop,
    };

    public static InstrumentPatch Get(string id) =>
        ById.TryGetValue(id, out var patch) ? patch : throw new KeyNotFoundException($"No instrument patch named '{id}'.");
}
