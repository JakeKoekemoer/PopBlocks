namespace PopBlocks.Engine.Audio;

/// <summary>
/// The cues that belong to one region: they are written in the region's own key and scale, so they join the music instead of
/// sitting on top of it. Built once from the region's <see cref="RegionMusicProfile"/>; play them with no transposition.
/// </summary>
public sealed class RegionCues
{
    private static readonly Dictionary<RegionId, RegionCues> Cache = new();

    public static RegionCues For(RegionId region)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(region, out var cues)) Cache[region] = cues = new RegionCues(RegionProfiles.Get(region));
            return cues;
        }
    }

    public RegionId Region { get; }

    /// <summary>The region's scale, as semitones above its root. Colour pops and chains use it.</summary>
    public IReadOnlyList<int> Scale { get; }

    /// <summary>How far the C-based global cues must move to sit in this region's key.</summary>
    public int KeyOffset { get; }

    /// <summary>A short welcoming phrase drawn from the region's own motif.</summary>
    public SfxCue LevelStart { get; }

    /// <summary>Two or three notes that resolve clearly, with a quiet chord under the last.</summary>
    public SfxCue LevelComplete { get; }

    /// <summary>A gentle descent that settles. No buzzer, nothing punishing.</summary>
    public SfxCue LevelFailed { get; }

    /// <summary>The region's motif in miniature, played when the region is unlocked.</summary>
    public SfxCue Unlocked { get; }

    /// <summary>A soft glassy chime, a softened Spectrum motif and a ripple of descending notes.</summary>
    public SfxCue MemoryCrystal { get; }

    /// <summary>A short ascending flourish that resolves (reserved for meaningful chains).</summary>
    public SfxCue ComboSmall { get; }

    /// <summary>A longer flourish for a big chain.</summary>
    public SfxCue ComboBig { get; }

    /// <summary>The Spectrum motif revealed a little more with each Prism fragment (3, 4 then all 5 notes).</summary>
    public IReadOnlyList<SfxCue> PrismFragments { get; }

    /// <summary>Three ticks that speed up gently into a resonant chord (the Clockwork machine waking).</summary>
    public SfxCue MachineActivation { get; }

    /// <summary>A three-part chime for finding a crystal shard: soft low, bright middle, high resolving note.</summary>
    public SfxCue ShardDiscovery { get; }

    /// <summary>The region coming back to life: a sweep from low to high, small accents, then a musical resolution.</summary>
    public SfxCue Restoration { get; }

    private readonly int _baseMidi;
    private readonly int[] _scale;

    /// <summary>The third of the region's chord: major where the scale has one, minor where it has that, else a fifth.</summary>
    private int Third => Array.IndexOf(_scale, 4) >= 0 ? 4 : Array.IndexOf(_scale, 3) >= 0 ? 3 : 7;

    private int Deg(int degree) => _baseMidi + SfxLibrary.ScaleOffset(_scale, degree);

    private RegionCues(RegionMusicProfile p)
    {
        Region = p.Region;
        _scale = p.ScaleIntervals;
        _baseMidi = p.MelodyBaseMidi;
        Scale = _scale;
        KeyOffset = SfxLibrary.KeyOffset(p.RootMidiNote);

        var melody = Patches.Get(p.MelodyPatch);
        var accent = Patches.Get(p.AccentPatch);
        var pad = Patches.Get(p.HarmonyPatch);
        int n = _scale.Length;

        LevelStart = BuildLevelStart(p, melody);

        LevelComplete = new SfxCue
        {
            Name = $"{p.Region}-level-complete",
            Notes = new[]
            {
                SfxLibrary.Note(melody, Deg(0) + 12, 0, 220, 0.50f),
                SfxLibrary.Note(melody, Deg(2) + 12, 170, 240, 0.52f),
                SfxLibrary.Note(accent, Deg(n) + 12, 360, 850, 0.58f),
                SfxLibrary.Note(pad, Deg(0) - 12, 330, 900, 0.34f, gain: 0.7f),
                SfxLibrary.Note(pad, _baseMidi + Third, 330, 900, 0.26f, gain: 0.7f),
            },
        };

        LevelFailed = new SfxCue
        {
            Name = $"{p.Region}-level-failed",
            Notes = new[]
            {
                SfxLibrary.Note(accent, Deg(2) + 12, 0, 300, 0.30f),
                SfxLibrary.Note(accent, Deg(1) + 12, 230, 340, 0.27f),
                SfxLibrary.Note(accent, Deg(0) + 12, 470, 650, 0.24f),
                SfxLibrary.Note(pad, Deg(0) - 12, 400, 700, 0.20f, gain: 0.6f),
            },
        };

        Unlocked = BuildUnlocked(p);

        // Memory crystal: a soft chime, the Spectrum motif softened, then a ripple of descending notes.
        {
            var notes = new List<CueNote> { SfxLibrary.Note(Patches.GlassChime, Deg(n - 1) + 12, 0, 700, 0.40f) };
            float t = 420f;
            foreach (int interval in p.SpectrumMotifIntervals.Take(4))
            {
                notes.Add(SfxLibrary.Note(Patches.RoundBell, _baseMidi + interval, t, 360, 0.30f, gain: 0.85f));
                t += 260f;
            }
            for (int i = 0; i < 4; i++)
            {
                notes.Add(SfxLibrary.Note(Patches.GlassChime, Deg(n - i) + 12, t + i * 130f, 420, 0.22f - i * 0.04f,
                    pan: i % 2 == 0 ? -0.3f : 0.3f, gain: 0.8f));
            }
            MemoryCrystal = new SfxCue { Name = $"{p.Region}-memory-crystal", Notes = notes.ToArray(), DuckDb = 6f, DuckMs = t + 900f };
        }

        ComboSmall = BuildCombo(accent, 4, big: false);
        ComboBig = BuildCombo(accent, n, big: true);

        // Prism fragments: three, four or all five notes of the Spectrum motif.
        {
            var list = new List<SfxCue>();
            foreach (int count in new[] { 3, 4, 5 })
            {
                var notes = new List<CueNote>();
                float t = 0f;
                for (int i = 0; i < count; i++)
                {
                    bool last = i == count - 1;
                    notes.Add(SfxLibrary.Note(Patches.SpectrumBell, _baseMidi + 12 + p.SpectrumMotifIntervals[i], t, last ? 900 : 260, last ? 0.52f : 0.44f));
                    t += 220f;
                }
                if (count == 5) notes.Add(SfxLibrary.Note(pad, _baseMidi - 12, t - 220f, 1100, 0.30f, gain: 0.7f));
                list.Add(new SfxCue { Name = $"{p.Region}-prism-fragment-{count}", Notes = notes.ToArray(), DuckDb = 4f, DuckMs = t + 600f });
            }
            PrismFragments = list;
        }

        MachineActivation = new SfxCue
        {
            Name = $"{p.Region}-machine",
            Notes = new[]
            {
                SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 0, 70, 0.42f),
                SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 320, 70, 0.46f),
                SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 560, 70, 0.50f),
                SfxLibrary.Note(Patches.RoundBell, Deg(0), 740, 1100, 0.46f),
                SfxLibrary.Note(Patches.RoundBell, Deg(2), 740, 1000, 0.34f),
                SfxLibrary.Note(pad, Deg(0) - 12, 740, 1300, 0.34f, gain: 0.7f),
            },
        };

        ShardDiscovery = new SfxCue
        {
            Name = $"{p.Region}-shard",
            Notes = new[]
            {
                SfxLibrary.Note(Patches.RoundBell, Deg(0), 0, 160, 0.34f),
                SfxLibrary.Note(Patches.GlassChime, Deg(2) + 12, 110, 180, 0.46f),
                SfxLibrary.Note(Patches.SpectrumBell, Deg(n) + 12, 230, 420, 0.52f),
            },
        };

        Restoration = BuildRestoration(p, melody, accent, pad);
    }

    // ---- builders -------------------------------------------------------------------------------------------------------

    /// <summary>Plays the first three notes of the region's own tune, quickly: it says "this place" before the level starts.</summary>
    private SfxCue BuildLevelStart(RegionMusicProfile p, InstrumentPatch melody)
    {
        int[] intervals = RegionalIntervals(p);
        var notes = new List<CueNote>();
        float t = 0f;
        for (int i = 0; i < 3; i++)
        {
            bool last = i == 2;
            notes.Add(SfxLibrary.Note(melody, _baseMidi + 12 + intervals[i], t, last ? 600 : 200, last ? 0.50f : 0.42f));
            t += 150f;
        }
        return new SfxCue { Name = $"{p.Region}-level-start", Notes = notes.ToArray() };
    }

    /// <summary>The region's identifying tune played quickly (about 1.3 seconds), in its own key.</summary>
    private SfxCue BuildUnlocked(RegionMusicProfile p)
    {
        var shape = Motifs.Find(KindFor(p.Region)) ?? new MotifShape("spectrum", p.SpectrumMotifIntervals,
            new[] { 0f, 1f, 2f, 3f, 4f }, new[] { 1f, 1f, 1f, 1f, 2f });
        float scaleMs = 170f;   // one beat of the motif in milliseconds
        var patch = Patches.Get(p.MelodyPatch);
        var notes = new List<CueNote>();
        float last = 0f;
        for (int i = 0; i < shape.Intervals.Length; i++)
        {
            float at = shape.OnsetBeats[i] * scaleMs;
            float len = MathF.Max(180f, shape.DurationBeats[i] * scaleMs);
            bool final = i == shape.Intervals.Length - 1;
            notes.Add(SfxLibrary.Note(i % 2 == 0 ? patch : Patches.SpectrumBell, _baseMidi + 12 + shape.Intervals[i], at, final ? 800 : len, final ? 0.50f : 0.42f));
            last = at;
        }
        return new SfxCue { Name = $"{p.Region}-unlocked", Notes = notes.ToArray(), DuckDb = 4f, DuckMs = last + 1200f };
    }

    private static string KindFor(RegionId region) => region switch
    {
        RegionId.ForgottenGarden => "garden",
        RegionId.CrystalCaves => "caves",
        RegionId.VerdantWilds => "wilds",
        RegionId.SunkenCity => "sunken",
        RegionId.ClockworkWastes => "clockwork",
        RegionId.FrozenExpanse => "frozen",
        RegionId.ShatteredCitadel => "warden",
        _ => "",
    };

    private static int[] RegionalIntervals(RegionMusicProfile p) =>
        Motifs.Find(KindFor(p.Region))?.Intervals ?? p.SpectrumMotifIntervals;

    /// <summary>An upward run through the scale that lands on a settled, ringing note.</summary>
    private SfxCue BuildCombo(InstrumentPatch accent, int steps, bool big)
    {
        var notes = new List<CueNote>();
        float t = 0f;
        float spacing = big ? 70f : 85f;
        int count = big ? Math.Min(steps + 1, 6) : 4;
        for (int i = 0; i < count; i++)
        {
            notes.Add(SfxLibrary.Note(i % 2 == 0 ? accent : Patches.SoftPluck, Deg(i) + 12, t, 180, 0.40f + 0.02f * i));
            t += spacing;
        }
        // The resolving note: the root an octave above the run, with its fifth below it.
        notes.Add(SfxLibrary.Note(accent, Deg(count) + 12, t + 20f, big ? 900 : 600, big ? 0.58f : 0.50f));
        notes.Add(SfxLibrary.Note(Patches.PureTone, Deg(count) + 12 + 7, t + 40f, big ? 700 : 400, 0.14f, gain: 0.7f));
        return new SfxCue { Name = $"{Region}-combo-{(big ? "big" : "small")}", Notes = notes.ToArray(), DuckDb = big ? 2f : 0f, DuckMs = 800f };
    }

    private SfxCue BuildRestoration(RegionMusicProfile p, InstrumentPatch melody, InstrumentPatch accent, InstrumentPatch pad)
    {
        int n = _scale.Length;
        var notes = new List<CueNote>();
        float chordAt;
        string name = $"{p.Region}-restoration";

        // Every region starts the same way: a warm sweep from low to high. What follows is the region's own.
        float sweepMs = p.Region == RegionId.HeartOfSpectrum ? 1400f : 950f;
        notes.Add(SfxLibrary.Noise(Patches.Swell, 0, sweepMs, 220f, 2600f, 0.55f));

        switch (p.Region)
        {
            case RegionId.ForgottenGarden:
                // Three small bells, then the last note of the Spectrum motif.
                for (int i = 0; i < 3; i++)
                    notes.Add(SfxLibrary.Note(Patches.SpectrumBell, Deg(i * 2) + 12, 640 + i * 190, 520, 0.44f + i * 0.03f, pan: (i - 1) * 0.25f));
                notes.Add(SfxLibrary.Note(Patches.SpectrumBell, _baseMidi + 12 + p.SpectrumMotifIntervals[4], 1280, 1400, 0.58f));
                chordAt = 1280;
                break;

            case RegionId.CrystalCaves:
                // Crystals wake one after another, then a bright high chime resolves them.
                for (int i = 0; i < 6; i++)
                    notes.Add(SfxLibrary.Note(Patches.GlassChime, Deg(i) + 12, 560 + i * 95, 520, 0.36f + 0.02f * i, pan: (i % 2 == 0 ? -0.35f : 0.35f)));
                notes.Add(SfxLibrary.Note(Patches.SpectrumBell, Deg(n) + 12, 1180, 1300, 0.56f));
                chordAt = 1180;
                break;

            case RegionId.VerdantWilds:
                // Soft wooden taps, then a rising arpeggio, then a warm chord.
                for (int i = 0; i < 3; i++)
                    notes.Add(SfxLibrary.Note(Patches.WoodNote, Deg(0), 300 + i * (260 - i * 50), 90, 0.40f));
                for (int i = 0; i < 6; i++)
                    notes.Add(SfxLibrary.Note(Patches.WoodenMarimba, Deg(i), 800 + i * 115, 300, 0.42f + 0.02f * i));
                chordAt = 1500;
                break;

            case RegionId.SunkenCity:
                // Bubbles rise as the sweep opens; the city's bell settles on its chord.
                for (int i = 0; i < 4; i++)
                    notes.Add(SfxLibrary.Note(Patches.PureTone, 76 + i * 3, 450 + i * 210, 80, 0.14f, glideSemis: -5f, glideMs: 70f, pan: i % 2 == 0 ? -0.4f : 0.4f));
                notes.Add(SfxLibrary.Note(Patches.RoundBell, Deg(n) , 1250, 1500, 0.52f));
                notes.Add(SfxLibrary.Note(Patches.RoundBell, Deg(2), 1330, 1400, 0.34f));
                chordAt = 1250;
                break;

            case RegionId.ClockworkWastes:
                // Three ticks speed up gently into a resonant chord.
                notes.Add(SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 400, 70, 0.40f));
                notes.Add(SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 720, 70, 0.44f));
                notes.Add(SfxLibrary.Note(Patches.ClockPulse, Deg(n) + 12, 960, 70, 0.48f));
                notes.Add(SfxLibrary.Note(Patches.RoundBell, Deg(0) + 12, 1130, 1500, 0.50f));
                notes.Add(SfxLibrary.Note(Patches.RoundBell, Deg(2) + 12, 1130, 1400, 0.34f));
                chordAt = 1130;
                break;

            case RegionId.FrozenExpanse:
                // One delicate high note descends into a warmer chord; a faint shimmer follows.
                notes.Add(SfxLibrary.Note(Patches.GlassChime, Deg(n) + 24, 600, 700, 0.34f, glideSemis: -5f, glideMs: 600f));
                notes.Add(SfxLibrary.Note(Patches.SpectrumBell, Deg(n) + 12, 1200, 1400, 0.46f));
                for (int i = 0; i < 4; i++)
                    notes.Add(SfxLibrary.Note(Patches.GlassChime, Deg(n + i % 3) + 24, 1700 + i * 170, 300, 0.10f, gain: 0.6f, pan: i % 2 == 0 ? -0.6f : 0.6f));
                chordAt = 1200;
                break;

            case RegionId.ShatteredCitadel:
                // The Warden's motif, which always stopped short, is allowed to arrive home.
                {
                    int[] w = Motifs.WardenIntervals;
                    float t = 450f;
                    for (int i = 0; i < w.Length; i++)
                    {
                        notes.Add(SfxLibrary.Note(Patches.RoundBell, p.RootMidiNote + 12 + w[i], t, 600, 0.38f));
                        t += 280f;
                    }
                    // the missing resolution: home, with a gentle upper note
                    notes.Add(SfxLibrary.Note(Patches.RoundBell, p.RootMidiNote, t, 1700, 0.54f));
                    notes.Add(SfxLibrary.Note(Patches.SpectrumBell, p.RootMidiNote + 24, t + 120, 1500, 0.28f));
                    chordAt = t;
                }
                break;

            default:
                // Heart: the full five-note motif, stated plainly and left to ring.
                {
                    float t = 700f;
                    for (int i = 0; i < 5; i++)
                    {
                        bool last = i == 4;
                        notes.Add(SfxLibrary.Note(Patches.SpectrumBell, _baseMidi + 12 + p.SpectrumMotifIntervals[i], t, last ? 1800 : 400, last ? 0.58f : 0.46f));
                        t += 380f;
                    }
                    chordAt = t - 380f;
                }
                break;
        }

        // The resolution under every one: a warm, stable chord that rings out.
        notes.Add(SfxLibrary.Note(pad, _baseMidi - 12, chordAt, 1800, 0.36f, gain: 0.7f));
        notes.Add(SfxLibrary.Note(pad, _baseMidi, chordAt, 1800, 0.28f, gain: 0.7f));
        notes.Add(SfxLibrary.Note(pad, _baseMidi + Third, chordAt, 1800, 0.24f, gain: 0.7f));
        notes.Add(SfxLibrary.Note(pad, _baseMidi + 7, chordAt, 1800, 0.20f, gain: 0.7f));

        return new SfxCue { Name = name, Notes = notes.ToArray(), DuckDb = 5f, DuckMs = chordAt + 1400f };
    }
}
