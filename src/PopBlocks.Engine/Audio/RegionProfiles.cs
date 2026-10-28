namespace PopBlocks.Engine.Audio;

/// <summary>
/// The soundtrack as data: one profile for the menu and one for each of the eight regions. Each says what a region sounds
/// like (key, tempo, scale, instruments, chords, how busy it is, how roomy) and the <see cref="MusicComposer"/> does the rest.
/// Tempos stay inside the ranges the audio spec gives for each region.
/// </summary>
public static class RegionProfiles
{
    // Chord shorthand: root in semitones above the key, notes above that root.
    private static ChordSpec C(string name, int root, params int[] tones) => ChordSpec.Of(name, root, tones);

    private static readonly int[] Major = { 0, 4, 7 };
    private static readonly int[] MajorAdd9 = { 0, 4, 7, 14 };
    private static readonly int[] Major7 = { 0, 4, 7, 11 };
    private static readonly int[] Minor = { 0, 3, 7 };
    private static readonly int[] Minor7 = { 0, 3, 7, 10 };
    private static readonly int[] Sus2 = { 0, 2, 7 };
    private static readonly int[] Sus4 = { 0, 5, 7 };
    private static readonly int[] PentMajor = { 0, 2, 4, 7, 9 };

    // ---- Menu and the map ---------------------------------------------------------------------------------------------

    public static RegionMusicProfile Menu { get; } = new()
    {
        Region = RegionId.Menu,
        FadedThemeId = "Spectrum",
        RestoredThemeId = "Spectrum",
        TempoBpm = 66,
        RootMidiNote = 60,
        MelodyBaseMidi = 60,
        ScaleIntervals = PentMajor,
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        BaseIntensity = 0.25f,
        VariationSeed = 11,
        Bars = 16,
        ChordsA = new[]
        {
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gadd9", 7, MajorAdd9),
        },
        ChordsB = new[]
        {
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gadd9", 7, MajorAdd9),
        },
        Rhythm = RhythmStyle.Slow,
        MelodyLowDegree = -2, MelodyHighDegree = 7,
        RestBarChance = 0.3f, FadedDropChance = 0.1f,
        Motifs = new[] { new MotifPlan { Bars = new[] { 0, 8 }, Patch = "SoftPluck", MelodyRestBars = 2 } },
        Bass = BassStyle.None,
        Counter = CounterStyle.Glass,
        Pulse = PulseStyle.None,
        AccentsFaded = 3, AccentsRestored = 4,
        PadBarsFaded = 1,
        ReverbRoom = 0.6f, ReverbDamping = 0.45f, ReverbWet = 0.3f,
    };

    // ---- 1. The Forgotten Garden --------------------------------------------------------------------------------------

    public static RegionMusicProfile ForgottenGarden { get; } = new()
    {
        Region = RegionId.ForgottenGarden,
        FadedThemeId = "Where the Flowers Slept",
        RestoredThemeId = "A Thousand Little Lights",
        TempoBpm = 72,
        RootMidiNote = 60, MelodyBaseMidi = 60,
        ScaleIntervals = PentMajor,
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        MelodyPatch = "SoftPluck", HarmonyPatch = "WarmPad", AccentPatch = "SpectrumBell",
        BassPatch = "SoftBass", CounterPatch = "WoodenMarimba",
        BaseIntensity = 0.30f,
        VariationSeed = 101,
        ChordsA = new[]
        {
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Cadd9", 0, MajorAdd9), C("Em7", 4, Minor7), C("Fadd9", 5, MajorAdd9), C("Gsus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Cadd9", 0, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Dm7", 2, Minor7), C("G", 7, Major),
        },
        Rhythm = RhythmStyle.Sparse,
        MelodyLowDegree = -2, MelodyHighDegree = 7,
        RestBarChance = 0.15f, FadedDropChance = 0.30f, LeapChance = 0.06f,
        Motifs = new[]
        {
            new MotifPlan { Bars = new[] { 0, 8, 24 }, Patch = "SoftPluck", MelodyRestBars = 2 },
            new MotifPlan { Kind = "garden", Bars = new[] { 16 }, Patch = "SpectrumBell", Octave = 1, MelodyRestBars = 2 },
        },
        Bass = BassStyle.Root, Counter = CounterStyle.Interlock, Pulse = PulseStyle.None,
        AccentsFaded = 3, AccentsRestored = 9, PadBarsFaded = 2,
        ReverbRoom = 0.55f, ReverbDamping = 0.45f, ReverbWet = 0.26f,
        Ambience = new AmbienceStyle
        {
            BedHz = 420f, BedLevel = 0.12f, BedFloor = 0.05f, GustMinSeconds = 6f, GustMaxSeconds = 13f,
            Events = new[]
            {
                // Distant fireflies: a single soft high bell now and then.
                new AmbientEvent { Patch = Patches.SpectrumBell, Midis = new[] { 84, 86, 88, 91, 93 }, MinGapSeconds = 7f, MaxGapSeconds = 16f, MinVelocity = 0.10f, MaxVelocity = 0.22f, HoldMs = 100f, Pan = 0.8f },
            },
        },
    };

    // ---- 2. The Crystal Caves -----------------------------------------------------------------------------------------

    public static RegionMusicProfile CrystalCaves { get; } = new()
    {
        Region = RegionId.CrystalCaves,
        FadedThemeId = "Echoes Without Answers",
        RestoredThemeId = "The Crystals Remember",
        TempoBpm = 66,
        RootMidiNote = 62, MelodyBaseMidi = 62,
        ScaleIntervals = new[] { 0, 2, 4, 6, 7, 9 },
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        MelodyPatch = "SoftPluck", HarmonyPatch = "WarmPad", AccentPatch = "GlassChime",
        BassPatch = "SoftBass", CounterPatch = "BrightPluck", PulsePatch = "PureTone",
        BaseIntensity = 0.30f,
        VariationSeed = 202,
        ChordsA = new[]
        {
            C("Dadd9", 0, MajorAdd9), C("E", 2, Major), C("Bm7", 9, Minor7), C("Asus4", 7, Sus4),
            C("Dadd9", 0, MajorAdd9), C("E", 2, Major), C("F#m7", 4, Minor7), C("Asus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("Bm7", 9, Minor7), C("Asus4", 7, Sus4), C("Dadd9", 0, MajorAdd9), C("E", 2, Major),
            C("Bm7", 9, Minor7), C("F#m7", 4, Minor7), C("E", 2, Major), C("Asus4", 7, Sus4),
        },
        Rhythm = RhythmStyle.Sparse,
        MelodyLowDegree = -3, MelodyHighDegree = 8,
        RestBarChance = 0.25f, FadedDropChance = 0.35f, LeapChance = 0.10f,
        Motifs = new[]
        {
            // The caves remember only part of the Spectrum motif, and answer it after a pause.
            new MotifPlan
            {
                Bars = new[] { 4, 12, 28 }, Patch = "SpectrumBell", MelodyRestBars = 2,
                Faded = new MotifTreatment { Notes = new[] { 0, 1, 2 }, OnsetBeats = new[] { 0f, 2f, 4f }, DurationBeats = new[] { 2f, 2f, 4f }, Velocity = 0.42f, EchoNotes = 1, EchoDelayBeats = 1.5f, EchoDecay = 0.5f },
                Recovering = new MotifTreatment { Notes = new[] { 0, 1, 2, 3 }, OnsetBeats = new[] { 0f, 1.5f, 3f, 5f }, DurationBeats = new[] { 1.5f, 1.5f, 2f, 3f }, Velocity = 0.5f, EchoNotes = 2, EchoDelayBeats = 1.0f },
            },
            new MotifPlan { Kind = "caves", Bars = new[] { 0, 20 }, Patch = "GlassChime", Octave = 0, MelodyRestBars = 2 },
        },
        Bass = BassStyle.Root, Counter = CounterStyle.Interlock, Pulse = PulseStyle.SoftBeat,
        AccentsFaded = 4, AccentsRestored = 10, PadBarsFaded = 2,
        ReverbRoom = 0.85f, ReverbDamping = 0.35f, ReverbWet = 0.36f,
        Ambience = new AmbienceStyle
        {
            BedHz = 260f, BedLevel = 0.09f, BedFloor = 0.08f, GustMinSeconds = 10f, GustMaxSeconds = 18f,
            Events = new[]
            {
                new AmbientEvent { Patch = Patches.GlassChime, Midis = new[] { 86, 88, 90, 93, 95 }, MinGapSeconds = 8f, MaxGapSeconds = 20f, MinVelocity = 0.08f, MaxVelocity = 0.18f, HoldMs = 80f, Pan = 0.85f },
            },
        },
    };

    // ---- 3. The Verdant Wilds -----------------------------------------------------------------------------------------

    public static RegionMusicProfile VerdantWilds { get; } = new()
    {
        Region = RegionId.VerdantWilds,
        FadedThemeId = "Roots of Silence",
        RestoredThemeId = "Everything Grows Together",
        TempoBpm = 78,
        RootMidiNote = 55, MelodyBaseMidi = 67,
        ScaleIntervals = new[] { 0, 2, 3, 5, 7, 9, 10 },
        SpectrumMotifIntervals = Motifs.SpectrumMinor,
        MelodyPatch = "WoodenMarimba", HarmonyPatch = "WarmPad", AccentPatch = "RoundBell",
        BassPatch = "SoftBass", CounterPatch = "SoftPluck", PulsePatch = "WoodNote",
        BaseIntensity = 0.35f,
        VariationSeed = 303,
        ChordsA = new[]
        {
            C("Gm7", 0, Minor7), C("C", 5, Major), C("Gm7", 0, Minor7), C("F", 10, Major),
            C("Gm7", 0, Minor7), C("C", 5, Major), C("Bbmaj7", 3, Major7), C("F", 10, Major),
        },
        ChordsB = new[]
        {
            C("Eb", 8, Major), C("Bb", 3, Major), C("F", 10, Major), C("Gm7", 0, Minor7),
            C("Eb", 8, Major), C("Bb", 3, Major), C("C", 5, Major), C("F", 10, Major),
        },
        Rhythm = RhythmStyle.Playful,
        MelodyLowDegree = -3, MelodyHighDegree = 7,
        RestBarChance = 0.18f, FadedDropChance = 0.28f, LeapChance = 0.10f,
        Motifs = new[]
        {
            // The motif is buried in the middle register while faded.
            new MotifPlan { Bars = new[] { 4, 12, 28 }, Patch = "SoftPluck", Octave = -1, MelodyRestBars = 2 },
            new MotifPlan { Kind = "wilds", Bars = new[] { 0, 20 }, Patch = "WoodenMarimba", Octave = 0, MelodyRestBars = 2 },
        },
        Bass = BassStyle.RootFifth, Counter = CounterStyle.Interlock, Pulse = PulseStyle.SoftBeat,
        AccentsFaded = 2, AccentsRestored = 8, PadBarsFaded = 2,
        ReverbRoom = 0.35f, ReverbDamping = 0.5f, ReverbWet = 0.2f,
        Ambience = new AmbienceStyle
        {
            BedHz = 380f, BedLevel = 0.09f, BedFloor = 0.06f, GustMinSeconds = 5f, GustMaxSeconds = 11f,
            Events = new[]
            {
                // Soft insect- and bird-like chirps: tiny rising glides, nothing realistic enough to be irritating.
                new AmbientEvent { Patch = Patches.PureTone, Midis = new[] { 91, 93, 96 }, MinGapSeconds = 5f, MaxGapSeconds = 12f, MinVelocity = 0.06f, MaxVelocity = 0.12f, HoldMs = 70f, GlideSemis = -4f, GlideMs = 60f, Pan = 0.7f },
            },
        },
    };

    // ---- 4. The Sunken City -------------------------------------------------------------------------------------------

    public static RegionMusicProfile SunkenCity { get; } = new()
    {
        Region = RegionId.SunkenCity,
        FadedThemeId = "Beneath the Quiet",
        RestoredThemeId = "The City Remembers",
        TempoBpm = 62,
        RootMidiNote = 51, MelodyBaseMidi = 63,
        ScaleIntervals = PentMajor,
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        MelodyPatch = "SoftPluck", HarmonyPatch = "WarmPad", AccentPatch = "RoundBell",
        BassPatch = "SoftBass", CounterPatch = "SoftPluck",
        BaseIntensity = 0.25f,
        VariationSeed = 404,
        ChordsA = new[]
        {
            C("Ebadd9", 0, MajorAdd9), C("Cm7", 9, Minor7), C("Abmaj7", 5, Major7), C("Bbsus4", 7, Sus4),
            C("Ebadd9", 0, MajorAdd9), C("Gm7", 4, Minor7), C("Abmaj7", 5, Major7), C("Bbsus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("Cm7", 9, Minor7), C("Abmaj7", 5, Major7), C("Ebadd9", 0, MajorAdd9), C("Bbsus4", 7, Sus4),
            C("Cm7", 9, Minor7), C("Fm7", 2, Minor7), C("Abmaj7", 5, Major7), C("Bbsus4", 7, Sus4),
        },
        Rhythm = RhythmStyle.Flowing,
        MelodyLowDegree = -2, MelodyHighDegree = 7,
        RestBarChance = 0.30f, FadedDropChance = 0.45f, LeapChance = 0.05f,
        Motifs = new[]
        {
            // Softened: lower velocity, a gentle descending ripple of echoes after each statement.
            new MotifPlan
            {
                Bars = new[] { 0, 8, 24 }, Patch = "RoundBell", MelodyRestBars = 2,
                Faded = new MotifTreatment { Notes = new[] { 0, 1, 2, 3 }, OnsetBeats = new[] { 0f, 1.5f, 3f, 4.5f }, DurationBeats = new[] { 1.5f, 1.5f, 1.5f, 3.5f }, Velocity = 0.38f, EchoNotes = 1, EchoDelayBeats = 1.0f, EchoDecay = 0.5f },
                Recovering = new MotifTreatment { Notes = new[] { 0, 1, 2, 3, 4 }, OnsetBeats = new[] { 0f, 1.25f, 2.5f, 4f, 6f }, DurationBeats = new[] { 1.25f, 1.25f, 1.5f, 2f, 2.5f }, Velocity = 0.46f, EchoNotes = 3, EchoDelayBeats = 0.75f },
                Restored = new MotifTreatment { Notes = new[] { 0, 1, 2, 3, 4 }, OnsetBeats = new[] { 0f, 1f, 2f, 3.5f, 5f }, DurationBeats = new[] { 1f, 1f, 1.5f, 1.5f, 3f }, Velocity = 0.58f, EchoNotes = 2, EchoDelayBeats = 0.75f, Answer = true },
            },
            new MotifPlan { Kind = "sunken", Bars = new[] { 16 }, Patch = "SoftPluck", MelodyRestBars = 2 },
        },
        Bass = BassStyle.Drone, Counter = CounterStyle.Arpeggio, Pulse = PulseStyle.None,
        AccentsFaded = 3, AccentsRestored = 8, PadBarsFaded = 2,
        ReverbRoom = 0.8f, ReverbDamping = 0.5f, ReverbWet = 0.38f,
        Ambience = new AmbienceStyle
        {
            BedHz = 300f, BedLevel = 0.08f, BedFloor = 0.07f, GustMinSeconds = 8f, GustMaxSeconds = 15f,
            Events = new[]
            {
                // Subtle bubbles: a quick rising blip, now and then.
                new AmbientEvent { Patch = Patches.PureTone, Midis = new[] { 72, 76, 79, 84 }, MinGapSeconds = 5f, MaxGapSeconds = 11f, MinVelocity = 0.08f, MaxVelocity = 0.16f, HoldMs = 60f, GlideSemis = -6f, GlideMs = 70f, Pan = 0.7f },
            },
        },
    };

    // ---- 5. The Clockwork Wastes --------------------------------------------------------------------------------------

    public static RegionMusicProfile ClockworkWastes { get; } = new()
    {
        Region = RegionId.ClockworkWastes,
        FadedThemeId = "A Machine Waiting",
        RestoredThemeId = "A Different Kind of Order",
        TempoBpm = 82,
        RootMidiNote = 57, MelodyBaseMidi = 69,
        ScaleIntervals = new[] { 0, 3, 5, 7, 10 },
        SpectrumMotifIntervals = Motifs.SpectrumMinor,
        MelodyPatch = "BrightPluck", HarmonyPatch = "WarmPad", AccentPatch = "SpectrumBell",
        BassPatch = "SoftBass", CounterPatch = "WoodenMarimba", PulsePatch = "ClockPulse",
        BaseIntensity = 0.40f,
        VariationSeed = 505,
        ChordsA = new[]
        {
            C("Am7", 0, Minor7), C("Fmaj7", 8, Major7), C("C", 3, Major), C("G", 10, Major),
            C("Am7", 0, Minor7), C("Fmaj7", 8, Major7), C("Dm7", 5, Minor7), C("Esus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("Fmaj7", 8, Major7), C("C", 3, Major), C("G", 10, Major), C("Am7", 0, Minor7),
            C("Fmaj7", 8, Major7), C("Dm7", 5, Minor7), C("Esus4", 7, Sus4), C("Esus4", 7, Sus4),
        },
        Rhythm = RhythmStyle.Mechanical,
        MelodyLowDegree = -2, MelodyHighDegree = 6,
        RestBarChance = 0.20f, FadedDropChance = 0.22f, LeapChance = 0.12f,
        Motifs = new[]
        {
            // The motif almost locks into place: four steady notes, then an empty beat where the fifth should be.
            new MotifPlan
            {
                Bars = new[] { 0, 8, 24 }, Patch = "WoodenMarimba", MelodyRestBars = 2,
                Faded = new MotifTreatment { Notes = new[] { 0, 1, 2, 3 }, OnsetBeats = new[] { 0f, 1f, 2f, 3f }, DurationBeats = new[] { 0.75f, 0.75f, 0.75f, 0.75f }, Velocity = 0.42f },
                Recovering = new MotifTreatment { Notes = new[] { 0, 1, 2, 3, 4 }, OnsetBeats = new[] { 0f, 1f, 2f, 3f, 4.5f }, DurationBeats = new[] { 0.75f, 0.75f, 0.75f, 0.75f, 1.5f }, Velocity = 0.5f },
                Restored = new MotifTreatment { Notes = new[] { 0, 1, 2, 3, 4 }, OnsetBeats = new[] { 0f, 1f, 2f, 3f, 4f }, DurationBeats = new[] { 0.75f, 0.75f, 0.75f, 0.75f, 2.5f }, Velocity = 0.6f },
            },
            new MotifPlan { Kind = "clockwork", Bars = new[] { 16 }, Patch = "WoodNote", MelodyRestBars = 2 },
        },
        Bass = BassStyle.Pulse, Counter = CounterStyle.Ticks, Pulse = PulseStyle.Ticks,
        AccentsFaded = 2, AccentsRestored = 6, PadBarsFaded = 4,
        ReverbRoom = 0.3f, ReverbDamping = 0.6f, ReverbWet = 0.15f,
        Ambience = new AmbienceStyle
        {
            BedHz = 600f, BedLevel = 0.06f, BedFloor = 0.05f, GustMinSeconds = 4f, GustMaxSeconds = 9f,
            Events = new[]
            {
                new AmbientEvent { Patch = Patches.ClockPulse, Midis = new[] { 88, 95 }, MinGapSeconds = 3f, MaxGapSeconds = 7f, MinVelocity = 0.05f, MaxVelocity = 0.10f, HoldMs = 40f, Pan = 0.6f },
            },
        },
    };

    // ---- 6. The Frozen Expanse ----------------------------------------------------------------------------------------

    public static RegionMusicProfile FrozenExpanse { get; } = new()
    {
        Region = RegionId.FrozenExpanse,
        FadedThemeId = "The Memory Beneath the Ice",
        RestoredThemeId = "When Winter Lets Go",
        TempoBpm = 58,
        RootMidiNote = 52, MelodyBaseMidi = 64,
        ScaleIntervals = PentMajor,
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        MelodyPatch = "SpectrumBell", HarmonyPatch = "WarmPad", AccentPatch = "GlassChime",
        BassPatch = "SoftBass", CounterPatch = "SoftPluck",
        BaseIntensity = 0.22f,
        VariationSeed = 606,
        ChordsA = new[]
        {
            C("Esus2", 0, Sus2), C("C#m7", 9, Minor7), C("Aadd9", 5, MajorAdd9), C("Bsus4", 7, Sus4),
            C("Esus2", 0, Sus2), C("C#m7", 9, Minor7), C("Aadd9", 5, MajorAdd9), C("Bsus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("C#m7", 9, Minor7), C("Aadd9", 5, MajorAdd9), C("Esus2", 0, Sus2), C("Bsus4", 7, Sus4),
            C("C#m7", 9, Minor7), C("Aadd9", 5, MajorAdd9), C("Esus2", 0, Sus2), C("Bsus4", 7, Sus4),
        },
        Rhythm = RhythmStyle.Slow,
        MelodyLowDegree = -2, MelodyHighDegree = 6,
        RestBarChance = 0.35f, FadedDropChance = 0.40f, LeapChance = 0.06f,
        Motifs = new[]
        {
            // Fragments with missing notes while faded; the same notes joined up as the ice thaws.
            new MotifPlan
            {
                Bars = new[] { 0, 8, 24 }, Patch = "SpectrumBell", MelodyRestBars = 2,
                Faded = new MotifTreatment { Notes = new[] { 0, 3 }, OnsetBeats = new[] { 0f, 4f }, DurationBeats = new[] { 3f, 5f }, Velocity = 0.36f },
                Recovering = new MotifTreatment { Notes = new[] { 0, 1, 3, 4 }, OnsetBeats = new[] { 0f, 2.5f, 5f, 8f }, DurationBeats = new[] { 2.5f, 2.5f, 3f, 4f }, Velocity = 0.44f, EchoNotes = 1, EchoDelayBeats = 1.5f },
                Restored = new MotifTreatment { Notes = new[] { 0, 1, 2, 3, 4 }, OnsetBeats = new[] { 0f, 2f, 4f, 6f, 8f }, DurationBeats = new[] { 2f, 2f, 2f, 2f, 5f }, Velocity = 0.5f },
            },
            new MotifPlan { Kind = "frozen", Bars = new[] { 16 }, Patch = "GlassChime", MelodyRestBars = 2 },
        },
        Bass = BassStyle.Drone, Counter = CounterStyle.Glass, Pulse = PulseStyle.None,
        AccentsFaded = 2, AccentsRestored = 6, PadBarsFaded = 4,
        ReverbRoom = 0.75f, ReverbDamping = 0.55f, ReverbWet = 0.34f,
        Ambience = new AmbienceStyle
        {
            BedHz = 350f, BedLevel = 0.07f, BedFloor = 0.03f, GustMinSeconds = 9f, GustMaxSeconds = 16f,
            Events = new[]
            {
                new AmbientEvent { Patch = Patches.GlassChime, Midis = new[] { 93, 96, 98 }, MinGapSeconds = 9f, MaxGapSeconds = 22f, MinVelocity = 0.05f, MaxVelocity = 0.10f, HoldMs = 60f, Pan = 0.85f },
            },
        },
    };

    // ---- 7. The Shattered Citadel -------------------------------------------------------------------------------------

    public static RegionMusicProfile ShatteredCitadel { get; } = new()
    {
        Region = RegionId.ShatteredCitadel,
        FadedThemeId = "The Weight of the Guardian",
        RestoredThemeId = "The Courage to Let Go",
        TempoBpm = 70,
        RootMidiNote = 48, MelodyBaseMidi = 60,
        ScaleIntervals = new[] { 0, 3, 5, 7, 10 },
        SpectrumMotifIntervals = Motifs.SpectrumMinor,
        MelodyPatch = "RoundBell", HarmonyPatch = "WarmPad", AccentPatch = "SpectrumBell",
        BassPatch = "SoftBass", CounterPatch = "SpectrumBell", PulsePatch = "PureTone",
        BaseIntensity = 0.28f,
        VariationSeed = 707,
        ChordsA = new[]
        {
            C("Cm", 0, 0, 3, 7, 14), C("Abmaj7", 8, Major7), C("Eb", 3, Major), C("Gsus4", 7, Sus4),
            C("Cm", 0, 0, 3, 7, 14), C("Ab", 8, Major), C("Fm", 5, Minor), C("Gsus4", 7, Sus4),
        },
        ChordsB = new[]
        {
            C("Abmaj7", 8, Major7), C("Eb", 3, Major), C("Bb", 10, Major), C("Fm", 5, Minor),
            C("Abmaj7", 8, Major7), C("Eb", 3, Major), C("Bb", 10, MajorAdd9), C("Gsus4", 7, Sus4),
        },
        Rhythm = RhythmStyle.Slow,
        MelodyLowDegree = -2, MelodyHighDegree = 6,
        RestBarChance = 0.45f, FadedDropChance = 0.35f, LeapChance = 0.05f,
        Motifs = new[]
        {
            // The Warden's motif: the Spectrum motif slowed, lowered and left a step short of home. Heard first, and loudest.
            new MotifPlan { Kind = "warden", Bars = new[] { 0, 8, 24 }, Patch = "SoftPluck", MelodyRestBars = 4 },

            // The Spectrum motif is only faintly present until the Citadel has let go.
            new MotifPlan
            {
                Bars = new[] { 16 }, Patch = "SpectrumBell", Octave = 1, MelodyRestBars = 2,
                Faded = new MotifTreatment { Notes = Array.Empty<int>(), OnsetBeats = Array.Empty<float>(), DurationBeats = Array.Empty<float>() },
                Recovering = new MotifTreatment { Notes = new[] { 0, 1, 2 }, OnsetBeats = new[] { 0f, 2f, 4f }, DurationBeats = new[] { 2f, 2f, 4f }, Velocity = 0.34f, EchoNotes = 1 },
            },
        },
        Bass = BassStyle.Drone, Counter = CounterStyle.Glass, Pulse = PulseStyle.SoftBeat,
        AccentsFaded = 2, AccentsRestored = 6, PadBarsFaded = 2,
        ReverbRoom = 0.8f, ReverbDamping = 0.5f, ReverbWet = 0.33f,
        Ambience = new AmbienceStyle
        {
            BedHz = 240f, BedLevel = 0.08f, BedFloor = 0.07f, GustMinSeconds = 9f, GustMaxSeconds = 17f,
            Events = new[]
            {
                new AmbientEvent { Patch = Patches.SpectrumBell, Midis = new[] { 72, 79 }, MinGapSeconds = 12f, MaxGapSeconds = 24f, MinVelocity = 0.06f, MaxVelocity = 0.12f, HoldMs = 500f, Pan = 0.7f },
            },
        },
    };

    // ---- 8. The Heart of Spectrum -------------------------------------------------------------------------------------

    public static RegionMusicProfile HeartOfSpectrum { get; } = new()
    {
        Region = RegionId.HeartOfSpectrum,
        FadedThemeId = "The Space Between Colours",
        RestoredThemeId = "Same Colours, New Connections",
        TempoBpm = 70,
        RootMidiNote = 60, MelodyBaseMidi = 60,
        ScaleIntervals = PentMajor,
        SpectrumMotifIntervals = Motifs.SpectrumMajor,
        MelodyPatch = "SoftPluck", HarmonyPatch = "WarmPad", AccentPatch = "SpectrumBell",
        BassPatch = "SoftBass", CounterPatch = "WoodenMarimba", PulsePatch = "PureTone",
        BaseIntensity = 0.32f,
        VariationSeed = 808,
        ChordsA = new[]
        {
            C("Cadd9", 0, MajorAdd9), C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Cadd9", 0, MajorAdd9), C("Em7", 4, Minor7), C("Fadd9", 5, MajorAdd9), C("Gadd9", 7, MajorAdd9),
        },
        ChordsB = new[]
        {
            C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Cadd9", 0, MajorAdd9), C("Gsus4", 7, Sus4),
            C("Am7", 9, Minor7), C("Fadd9", 5, MajorAdd9), C("Dm7", 2, Minor7), C("Gadd9", 7, MajorAdd9),
        },
        Rhythm = RhythmStyle.Slow,
        MelodyLowDegree = -2, MelodyHighDegree = 6,
        RestBarChance = 0.25f, FadedDropChance = 0.30f, LeapChance = 0.06f,
        Motifs = new[]
        {
            // The five-note motif is stated clearly first; then each region's own tune returns, one at a time, in this one key.
            new MotifPlan { Bars = new[] { 0 }, Patch = "SoftPluck", MelodyRestBars = 2 },
            new MotifPlan { Kind = "garden", Bars = new[] { 4 }, Patch = "BrightPluck", MelodyRestBars = 2 },
            new MotifPlan { Kind = "caves", Bars = new[] { 8 }, Patch = "SpectrumBell", MelodyRestBars = 2 },
            new MotifPlan { Kind = "wilds", Bars = new[] { 12 }, Patch = "WoodenMarimba", MelodyRestBars = 2 },
            new MotifPlan { Kind = "sunken", Bars = new[] { 16 }, Patch = "RoundBell", MelodyRestBars = 2 },
            new MotifPlan { Kind = "clockwork", Bars = new[] { 20 }, Patch = "WoodNote", MelodyRestBars = 2 },
            new MotifPlan { Kind = "frozen", Bars = new[] { 24 }, Patch = "GlassChime", MelodyRestBars = 2 },
            new MotifPlan { Kind = "warden", Bars = new[] { 28 }, Patch = "SoftPluck", MelodyRestBars = 2 },
        },
        Bass = BassStyle.RootFifth, Counter = CounterStyle.Interlock, Pulse = PulseStyle.SoftBeat,
        AccentsFaded = 3, AccentsRestored = 8, PadBarsFaded = 2,
        ReverbRoom = 0.6f, ReverbDamping = 0.4f, ReverbWet = 0.3f,
        Ambience = new AmbienceStyle
        {
            BedHz = 300f, BedLevel = 0.06f, BedFloor = 0.05f, GustMinSeconds = 8f, GustMaxSeconds = 15f,
            Events = new[]
            {
                // Small light-like chimes, wide in the stereo field.
                new AmbientEvent { Patch = Patches.SpectrumBell, Midis = new[] { 79, 84, 88, 91 }, MinGapSeconds = 5f, MaxGapSeconds = 11f, MinVelocity = 0.08f, MaxVelocity = 0.18f, HoldMs = 100f, Pan = 0.9f },
            },
        },
    };

    // ---- Lookup -------------------------------------------------------------------------------------------------------

    public static IReadOnlyList<RegionMusicProfile> All { get; } = new[]
    {
        Menu, ForgottenGarden, CrystalCaves, VerdantWilds, SunkenCity, ClockworkWastes, FrozenExpanse, ShatteredCitadel, HeartOfSpectrum,
    };

    public static RegionMusicProfile Get(RegionId region) => All[(int)region];

    /// <summary>Every profile's problems (empty when all are valid). A test keeps this empty.</summary>
    public static IEnumerable<string> ValidateAll() =>
        All.SelectMany(p => MusicComposer.Validate(p).Select(problem => $"{p.Region}: {problem}"));
}
