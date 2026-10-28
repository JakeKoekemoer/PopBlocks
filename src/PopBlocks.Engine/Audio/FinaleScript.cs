namespace PopBlocks.Engine.Audio;

/// <summary>
/// The Prism Heart restoration, written out note by note so its timing never depends on chance. It follows the audio spec's
/// five steps:
/// <list type="number">
/// <item>the backing thins (the director does this) and the music pauses for a breath;</item>
/// <item>the five-note Spectrum motif is stated in full;</item>
/// <item>the seven regional motifs return one at a time, each joined by a held note that builds a chord;</item>
/// <item>everything settles into one warm, stable chord;</item>
/// <item>the last note rings out and fades away by itself.</item>
/// </list>
/// After <see cref="RestoreAtMs"/> the director moves the Heart score to its restored state, so the game's own music grows back
/// underneath the last note instead of restarting. All of it is in C, the Heart theme's key.
/// </summary>
public static class FinaleScript
{
    /// <summary>When the music underneath should start returning (the resolution is already ringing).</summary>
    public const float RestoreAtMs = 36_500f;

    /// <summary>When the last note has died away.</summary>
    public const float EndMs = 43_000f;

    private const float MotifStartMs = 10_500f;
    private const float MotifSlotMs = 3_200f;
    private const float ResolveAtMs = 34_000f;
    private const float BeatMs = 250f;

    public static SfxCue Cue { get; } = Build();

    private static SfxCue Build()
    {
        var notes = new List<CueNote>();
        void Add(InstrumentPatch p, float midi, float at, float len, float vel, float pan = 0f, float gain = 1f) =>
            notes.Add(SfxLibrary.Note(p, midi, at, len, vel, pan, gain, transposes: false));

        // 2. The five-note Spectrum motif, stated plainly and left to ring.
        float t = 2_400f;
        int[] spectrum = Motifs.SpectrumMajor;
        float[] spectrumGap = { 1_000f, 1_000f, 1_000f, 1_300f, 0f };
        for (int i = 0; i < spectrum.Length; i++)
        {
            bool last = i == spectrum.Length - 1;
            Add(Patches.SoftPluck, 72 + spectrum[i], t, last ? 3_200 : 900, 0.50f);
            Add(Patches.SpectrumBell, 72 + spectrum[i], t, last ? 4_200 : 1_100, last ? 0.52f : 0.40f, gain: 0.9f);
            t += spectrumGap[i];
        }
        Add(Patches.WarmPad, 48, 2_400, 7_000, 0.34f, gain: 0.8f);        // a low C underneath
        Add(Patches.WarmPad, 55, 2_900, 6_500, 0.26f, gain: 0.8f);

        // 3. The regional motifs return one by one. Each is followed by a held note that joins the chord.
        (string id, InstrumentPatch patch, int octaveBase, float? holdMidi)[] order =
        {
            ("garden", Patches.SoftPluck, 72, 64f),      // E4
            ("caves", Patches.SpectrumBell, 72, 67f),    // G4
            ("wilds", Patches.WoodenMarimba, 72, 62f),   // D4 (the ninth)
            ("sunken", Patches.RoundBell, 72, 69f),      // A4
            ("clockwork", Patches.WoodNote, 72, 52f),    // E3
            ("frozen", Patches.GlassChime, 72, 60f),     // C4
            ("warden", Patches.SoftPluck, 72, 71f),      // B4
        };
        float start = MotifStartMs;
        foreach (var (id, patch, baseMidi, hold) in order)
        {
            var shape = Motifs.Find(id)!;
            for (int i = 0; i < shape.Intervals.Length; i++)
            {
                bool last = i == shape.Intervals.Length - 1;
                float at = start + shape.OnsetBeats[i] * BeatMs;
                float len = MathF.Max(240f, shape.DurationBeats[i] * BeatMs);
                float pan = id switch { "garden" => -0.3f, "caves" => 0.35f, "wilds" => -0.2f, "sunken" => 0.25f, "clockwork" => -0.1f, "frozen" => 0.4f, _ => 0f };
                Add(patch, baseMidi + shape.Intervals[i], at, last ? len + 500f : len, last ? 0.48f : 0.42f, pan);
            }
            // The held note that keeps the chord growing until the resolution.
            float holdAt = start + 1_800f;
            Add(Patches.WarmPad, hold ?? 60f, holdAt, ResolveAtMs - holdAt + 800f, 0.20f, gain: 0.7f);
            start += MotifSlotMs;
        }

        // 4. Everything settles into one warm chord (C, G, E, D: a major chord with its ninth).
        float r = ResolveAtMs;
        foreach (var (midi, vel) in new[] { (36f, 0.34f), (48f, 0.34f), (55f, 0.30f), (64f, 0.26f), (62f, 0.20f), (72f, 0.22f) })
            Add(Patches.WarmPad, midi, r, 5_500f, vel, gain: 0.85f);
        Add(Patches.SoftPluck, 72, r, 800f, 0.42f);
        Add(Patches.SoftPluck, 76, r + 160f, 800f, 0.38f);
        Add(Patches.SoftPluck, 79, r + 320f, 800f, 0.36f);

        // 5. The last note rings out and decays on its own.
        Add(Patches.SpectrumBell, 84, r + 480f, 5_000f, 0.56f);
        Add(Patches.RoundBell, 72, r + 480f, 4_500f, 0.36f, gain: 0.9f);

        return new SfxCue { Name = "finale-prism-heart", Bus = Bus.Music, Notes = notes.ToArray() };
    }
}
