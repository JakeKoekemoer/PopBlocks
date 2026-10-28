using System.Text;

namespace PopBlocks.Engine.Audio;

/// <summary>
/// Turns a <see cref="RegionMusicProfile"/> into a finished <see cref="MusicScore"/>. The same profile always gives the same
/// score. The composer follows musical rules instead of picking notes at random: melodies walk by steps with the odd leap
/// (and answer a leap by turning back), land on chord notes on the strong beats, rise and fall in a shape across a phrase, and
/// end phrases either hanging or at rest depending on whether the world is faded or restored.
///
/// One score holds every state at once. Each note carries the states it belongs to, so a region changing from faded to
/// restored is just a different filter on the same piece: the music never restarts and always stays in time.
/// </summary>
public static class MusicComposer
{
    public static MusicScore Compose(RegionMusicProfile profile)
    {
        var problems = Validate(profile);
        if (problems.Count > 0)
            throw new ArgumentException($"Music profile for {profile.Region} is not valid: " + string.Join("; ", problems));
        return new Builder(profile).Build();
    }

    /// <summary>Checks a profile so a bad scale, tempo, range or missing instrument is reported instead of crashing playback.</summary>
    public static IReadOnlyList<string> Validate(RegionMusicProfile p)
    {
        var bad = new List<string>();
        if (p.TempoBpm is < 40 or > 160) bad.Add("tempo must be 40 to 160 BPM");
        if (p.RootMidiNote is < 24 or > 84) bad.Add("root note must be MIDI 24 to 84");

        if (p.ScaleIntervals.Length is < 5 or > 8) bad.Add("scale needs 5 to 8 notes");
        else
        {
            if (p.ScaleIntervals[0] != 0) bad.Add("scale must start on 0");
            for (int i = 0; i < p.ScaleIntervals.Length; i++)
            {
                if (p.ScaleIntervals[i] is < 0 or > 11) bad.Add("scale notes must be 0 to 11");
                if (i > 0 && p.ScaleIntervals[i] <= p.ScaleIntervals[i - 1]) bad.Add("scale notes must rise");
            }
        }

        if (p.SpectrumMotifIntervals.Length != 5) bad.Add("the Spectrum motif needs exactly 5 notes");
        if ((p.MelodyBaseMidi - p.RootMidiNote) % 12 != 0) bad.Add("melody base must be a whole number of octaves from the root");
        if (p.MelodyBaseMidi is < 36 or > 96) bad.Add("melody base out of range");
        if (p.MelodyLowDegree >= p.MelodyHighDegree) bad.Add("melody range is empty");

        foreach (var (what, id) in new[]
        {
            ("melody", p.MelodyPatch), ("harmony", p.HarmonyPatch), ("accent", p.AccentPatch),
            ("bass", p.BassPatch), ("counter", p.CounterPatch), ("pulse", p.PulsePatch),
        })
            if (!Patches.ById.ContainsKey(id)) bad.Add($"unknown {what} patch '{id}'");

        if (p.Bars < 8 || p.Bars > 64 || p.Bars % 8 != 0) bad.Add("bars must be a multiple of 8, from 8 to 64");
        CheckChords(p.ChordsA, "A", bad);
        CheckChords(p.ChordsB, "B", bad);

        foreach (var m in p.Motifs)
        {
            if (!Patches.ById.ContainsKey(m.Patch)) bad.Add($"unknown motif patch '{m.Patch}'");
            if (m.Kind != "spectrum" && m.Kind != "warden" && Motifs.Find(m.Kind) is null) bad.Add($"unknown motif '{m.Kind}'");
            if (m.Intervals is { Length: < 3 }) bad.Add("a motif needs at least 3 notes");
            foreach (int bar in m.Bars)
                if (bar < 0 || bar > p.Bars - 4 || bar % 4 != 0) bad.Add($"motif bar {bar} must start a phrase inside the piece");
        }

        if (p.ReverbRoom is < 0 or > 1 || p.ReverbDamping is < 0 or > 1 || p.ReverbWet is < 0 or > 1) bad.Add("reverb values must be 0 to 1");
        return bad;
    }

    private static void CheckChords(ChordSpec[] chords, string name, List<string> bad)
    {
        if (chords.Length != 8) { bad.Add($"chords {name} needs 8 bars"); return; }
        foreach (var c in chords)
        {
            if (c.Root is < 0 or > 11) bad.Add($"chord {c.Name}: root must be 0 to 11");
            if (c.Tones.Length < 2) bad.Add($"chord {c.Name}: needs at least 2 notes");
            for (int i = 0; i < c.Tones.Length; i++)
                if (c.Tones[i] is < 0 or > 24 || (i > 0 && c.Tones[i] <= c.Tones[i - 1])) bad.Add($"chord {c.Name}: notes must rise from 0 to 24");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------

    private sealed class Builder
    {
        private const int Beat = MusicScore.TicksPerBeat;
        private const int Step = Beat / 4;   // a sixteenth note
        private const int BarTicks = Beat * 4;

        private readonly RegionMusicProfile _p;
        private readonly int[] _scale;
        private readonly int _bars;
        private readonly int _loop;
        private readonly List<NoteEvent> _events = new();
        private readonly List<InstrumentPatch> _patches = new();
        private readonly Dictionary<string, int> _patchIndex = new();
        private readonly HashSet<int> _melodyRest = new();

        public Builder(RegionMusicProfile profile)
        {
            _p = profile;
            _scale = profile.ScaleIntervals;
            _bars = profile.Bars;
            _loop = _bars * BarTicks;
        }

        public MusicScore Build()
        {
            BuildMotifs();
            BuildPad();
            BuildBass();
            BuildMelody();
            BuildAccents();
            BuildCounter();
            BuildChain();
            BuildPulse();
            BuildTension();

            _events.Sort((a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) return c;
                c = a.Layer.CompareTo(b.Layer);
                return c != 0 ? c : a.Midi.CompareTo(b.Midi);
            });

            return new MusicScore
            {
                Id = $"{_p.Region}",
                Title = $"{_p.FadedThemeId} / {_p.RestoredThemeId}",
                Region = _p.Region,
                TempoBpm = _p.TempoBpm,
                BeatsPerBar = 4,
                Bars = _bars,
                PhraseBars = 4,
                Events = _events.ToArray(),
                Patches = _patches.ToArray(),
                RootMidi = _p.RootMidiNote,
                Scale = (int[])_scale.Clone(),
                ReverbRoom = _p.ReverbRoom,
                ReverbDamping = _p.ReverbDamping,
                ReverbWet = _p.ReverbWet,
            };
        }

        // ---- helpers ------------------------------------------------------------------------------------------------

        private int PatchIndex(string id)
        {
            if (_patchIndex.TryGetValue(id, out int i)) return i;
            i = _patches.Count;
            _patches.Add(Patches.Get(id));
            _patchIndex[id] = i;
            return i;
        }

        private void Add(int start, int duration, int midi, float velocity, Layer layer, string patch, int pan, StateMask states)
        {
            if (start < 0 || start >= _loop || states == StateMask.None) return;
            _events.Add(new NoteEvent(
                start,
                Math.Max(12, duration),
                (byte)Math.Clamp(midi, 12, 120),
                (byte)Math.Clamp((int)MathF.Round(velocity * 127f), 1, 127),
                layer,
                (byte)PatchIndex(patch),
                (sbyte)Math.Clamp(pan, -100, 100),
                states));
        }

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q;
        }

        private static int Mod12(int x) => ((x % 12) + 12) % 12;

        /// <summary>The MIDI note of a scale degree (0 = the root at the melody register; negative goes below).</summary>
        private int DegreeMidi(int degree)
        {
            int n = _scale.Length;
            int oct = FloorDiv(degree, n);
            return _p.MelodyBaseMidi + oct * 12 + _scale[degree - oct * n];
        }

        private ChordSpec ChordAt(int bar)
        {
            int section = bar / 8;
            var set = (section == 2 && _p.ChordsB.Length == 8) ? _p.ChordsB : _p.ChordsA;
            return set[bar % 8];
        }

        private bool InChord(int midi, ChordSpec chord)
        {
            int pc = Mod12(midi - _p.RootMidiNote);
            foreach (int t in chord.Tones)
                if (Mod12(chord.Root + t) == pc) return true;
            return false;
        }

        /// <summary>A chord's note nearest to a target pitch, from the chord's notes in any octave.</summary>
        private int NearestChordNote(ChordSpec chord, int target, int toneLimit = int.MaxValue)
        {
            int best = target, bestDist = int.MaxValue;
            for (int i = 0; i < chord.Tones.Length && i < toneLimit; i++)
            {
                int pc = Mod12(_p.RootMidiNote + chord.Root + chord.Tones[i]);
                int baseNote = target - Mod12(target - pc);
                foreach (int cand in new[] { baseNote, baseNote + 12 })
                {
                    int d = Math.Abs(cand - target);
                    if (d < bestDist) { bestDist = d; best = cand; }
                }
            }
            return best;
        }

        private int PadFold(int midi, int low)
        {
            while (midi < low) midi += 12;
            while (midi >= low + 12) midi -= 12;
            return midi;
        }

        // ---- pad ----------------------------------------------------------------------------------------------------

        private void BuildPad()
        {
            string patch = _p.HarmonyPatch;
            int low = _p.RootMidiNote - 14;

            for (int bar = 0; bar < _bars; bar++)
            {
                var chord = ChordAt(bar);
                int root = PadFold(_p.RootMidiNote + chord.Root, low);

                // Colour returning: every chord has its full voicing, one bar each.
                int count = Math.Min(chord.Tones.Length, 4);
                for (int i = 0; i < count; i++)
                {
                    int note = root + chord.Tones[i];
                    if (i > 0 && note > root + 17) note -= 12;
                    float vel = i == 0 ? 0.50f : 0.40f;
                    Add(bar * BarTicks, BarTicks + 48, note, vel, Layer.Pad, patch, i % 2 == 0 ? -12 : 12, StateMask.Warm);
                }

                // Faded: a hollow root and fifth, changing slowly.
                if (_p.PadBarsFaded <= 1 || bar % _p.PadBarsFaded == 0)
                {
                    int bars = Math.Max(1, _p.PadBarsFaded);
                    int fifth = chord.Tones.Length > 2 ? chord.Tones[2] : 7;
                    Add(bar * BarTicks, bars * BarTicks + 48, root, 0.42f, Layer.Pad, patch, -8, StateMask.Faded);
                    Add(bar * BarTicks, bars * BarTicks + 48, root + fifth, 0.32f, Layer.Pad, patch, 8, StateMask.Faded);
                }
            }
        }

        // ---- bass ---------------------------------------------------------------------------------------------------

        private void BuildBass()
        {
            if (_p.Bass == BassStyle.None) return;
            string patch = _p.BassPatch;
            int low = _p.RootMidiNote - 25;

            for (int bar = 0; bar < _bars; bar++)
            {
                var chord = ChordAt(bar);
                int root = PadFold(_p.RootMidiNote + chord.Root, low);
                int fifth = root + 7;
                int t0 = bar * BarTicks;

                switch (_p.Bass)
                {
                    case BassStyle.Root:
                        Add(t0, BarTicks - 24, root, 0.55f, Layer.Bass, patch, 0, StateMask.Warm);
                        if (bar % 2 == 0) Add(t0, 2 * BarTicks - 24, root, 0.40f, Layer.Bass, patch, 0, StateMask.Faded);
                        break;

                    case BassStyle.RootFifth:
                        Add(t0, 2 * Beat - 12, root, 0.55f, Layer.Bass, patch, 0, StateMask.Warm);
                        Add(t0 + 2 * Beat, 2 * Beat - 12, fifth, 0.45f, Layer.Bass, patch, 0, StateMask.Warm);
                        if (bar % 2 == 0) Add(t0, 2 * BarTicks - 24, root, 0.38f, Layer.Bass, patch, 0, StateMask.Faded);
                        break;

                    case BassStyle.Pulse:
                        foreach (int s in new[] { 0, 4, 8, 12 })
                            Add(t0 + s * Step, 3 * Step, (s == 8 ? fifth : root), s == 0 ? 0.55f : 0.42f, Layer.Bass, patch, 0, StateMask.Warm);
                        Add(t0, 2 * Beat, root, 0.36f, Layer.Bass, patch, 0, StateMask.Faded);
                        if (bar % 2 == 1) Add(t0 + 2 * Beat, 2 * Beat, fifth, 0.28f, Layer.Bass, patch, 0, StateMask.Faded);
                        break;

                    case BassStyle.Drone:
                        if (bar % 2 == 0)
                            Add(t0, 2 * BarTicks - 24, root, 0.46f, Layer.Bass, patch, 0, StateMask.All);
                        break;
                }
            }
        }

        // ---- motif statements ---------------------------------------------------------------------------------------

        private void BuildMotifs()
        {
            foreach (var plan in _p.Motifs)
                foreach (int bar in plan.Bars)
                {
                    AddMotif(plan, bar);
                    for (int r = 0; r < plan.MelodyRestBars; r++) _melodyRest.Add(bar + r);
                }
        }

        private (int[] Intervals, MotifShape? Shape) ShapeOf(MotifPlan plan)
        {
            if (plan.Kind == "spectrum") return (plan.Intervals ?? _p.SpectrumMotifIntervals, null);
            var shape = plan.Kind == "warden" ? Motifs.Warden : Motifs.Find(plan.Kind)!;
            return (plan.Intervals ?? shape.Intervals, shape);
        }

        private static MotifTreatment Derive(MotifShape shape, StateMask state)
        {
            int n = shape.Intervals.Length;
            float stretch = state == StateMask.Faded ? 1.6f : state == StateMask.Recovering ? 1.25f : 1f;
            int count = state == StateMask.Faded ? Math.Max(2, n - 1) : n;
            var notes = Enumerable.Range(0, count).ToArray();
            return new MotifTreatment
            {
                Notes = notes,
                OnsetBeats = notes.Select(i => shape.OnsetBeats[i] * stretch).ToArray(),
                DurationBeats = notes.Select(i => shape.DurationBeats[i] * stretch).ToArray(),
                Velocity = state == StateMask.Faded ? 0.42f : state == StateMask.Recovering ? 0.5f : 0.62f,
                EchoNotes = state == StateMask.Faded ? 0 : 1,
                EchoDelayBeats = 0.75f,
                EchoDecay = 0.4f,
            };
        }

        private MotifTreatment TreatmentFor(MotifPlan plan, MotifShape? shape, StateMask state)
        {
            var given = state == StateMask.Faded ? plan.Faded : state == StateMask.Recovering ? plan.Recovering : plan.Restored;
            if (given is not null) return given;
            if (shape is null)
                return state == StateMask.Faded ? MotifTreatment.Faded : state == StateMask.Recovering ? MotifTreatment.Recovering : MotifTreatment.Restored;
            return Derive(shape, state);
        }

        private void AddMotif(MotifPlan plan, int bar)
        {
            var (intervals, shape) = ShapeOf(plan);
            int baseMidi = _p.MelodyBaseMidi + 12 * plan.Octave + (plan.Kind == "warden" ? 12 : 0);
            int start = bar * BarTicks;

            foreach (var state in new[] { StateMask.Faded, StateMask.Recovering, StateMask.Restored })
            {
                var t = TreatmentFor(plan, shape, state);
                for (int k = 0; k < t.Notes.Length; k++)
                {
                    int idx = t.Notes[k];
                    if (idx >= intervals.Length || k >= t.OnsetBeats.Length || k >= t.DurationBeats.Length) continue;

                    int at = start + (int)(t.OnsetBeats[k] * Beat);
                    int dur = (int)(t.DurationBeats[k] * Beat);
                    int midi = baseMidi + intervals[idx];
                    float vel = t.Velocity * (0.94f + 0.03f * k) + (k == t.Notes.Length - 1 ? 0.04f : 0f);

                    Add(at, dur, midi, vel, Layer.Motif, plan.Patch, -6, state);

                    if (t.Answer)
                        Add(at + 2 * Beat, dur, midi + 12, vel * 0.55f, Layer.Accent, _p.AccentPatch, 28, state);

                    if (k >= t.Notes.Length - t.EchoNotes)
                        for (int e = 1; e <= 2; e++)
                            Add(at + (int)(t.EchoDelayBeats * e * Beat), dur, midi, vel * MathF.Pow(t.EchoDecay, e), Layer.Motif, plan.Patch, e % 2 == 1 ? 32 : -32, state);
                }
            }
        }

        // ---- melody -------------------------------------------------------------------------------------------------

        private static readonly (int Step, int Len)[][] Sparse =
        {
            new[] { (0, 6) },
            new[] { (0, 4), (6, 6) },
            new[] { (4, 4), (8, 8) },
            new[] { (0, 8), (10, 6) },
            new[] { (2, 3), (6, 2), (8, 8) },
            new[] { (0, 4), (4, 4), (8, 8) },
            new[] { (6, 10) },
        };

        private static readonly (int Step, int Len)[][] Flowing =
        {
            new[] { (0, 4), (4, 2), (6, 2), (8, 4), (12, 4) },
            new[] { (0, 2), (2, 2), (4, 4), (8, 2), (10, 2), (12, 4) },
            new[] { (0, 6), (6, 2), (8, 4), (12, 4) },
            new[] { (0, 4), (4, 4), (8, 2), (10, 2), (12, 4) },
            new[] { (2, 2), (4, 2), (6, 2), (8, 8) },
            new[] { (0, 2), (2, 2), (4, 2), (6, 2), (8, 4), (12, 4) },
            new[] { (0, 8), (8, 2), (10, 2), (12, 4) },
        };

        private static readonly (int Step, int Len)[][] Playful =
        {
            new[] { (0, 3), (3, 3), (6, 2), (8, 4), (12, 4) },
            new[] { (0, 2), (3, 2), (6, 3), (10, 2), (12, 4) },
            new[] { (2, 2), (4, 3), (7, 3), (10, 2), (12, 2), (14, 2) },
            new[] { (0, 3), (3, 3), (6, 4), (10, 6) },
            new[] { (0, 4), (5, 3), (8, 3), (11, 5) },
            new[] { (1, 2), (4, 2), (6, 2), (8, 3), (11, 2), (14, 2) },
        };

        private static readonly (int Step, int Len)[][] Mechanical =
        {
            new[] { (0, 2), (4, 2), (8, 2), (12, 2) },
            new[] { (0, 2), (4, 2), (8, 2), (10, 2), (12, 2) },
            new[] { (2, 2), (6, 2), (8, 2), (12, 2), (14, 2) },
            new[] { (0, 2), (2, 2), (4, 2), (8, 2) },
            new[] { (0, 2), (4, 2), (6, 2), (8, 2), (12, 2) },
        };

        private static readonly (int Step, int Len)[][] Slow =
        {
            new[] { (0, 12) },
            new[] { (0, 8), (8, 8) },
            new[] { (2, 6), (8, 8) },
            new[] { (0, 6), (8, 6) },
            new[] { (4, 12) },
        };

        private (int Step, int Len)[][] Pool() => _p.Rhythm switch
        {
            RhythmStyle.Sparse => Sparse,
            RhythmStyle.Playful => Playful,
            RhythmStyle.Mechanical => Mechanical,
            RhythmStyle.Slow => Slow,
            _ => Flowing,
        };

        private void BuildMelody()
        {
            int sections = _bars / 8;
            for (int section = 0; section < sections; section++)
            {
                // A sections reuse a seed so the tune comes back; the second one is a variation; the B section is new.
                int kind = section == 2 ? 99 : section == 1 ? 1 : 0;
                var rng = new Rng(_p.VariationSeed * 31 + kind * 7919 + (int)_p.Region);
                bool isB = section == 2;
                for (int phrase = 0; phrase < 2; phrase++)
                {
                    int bar0 = section * 8 + phrase * 4;
                    BuildPhrase(rng, bar0, section, phrase, isB, bar0 + 4 >= _bars);
                }
            }
        }

        private void BuildPhrase(Rng rng, int bar0, int section, int phrase, bool isB, bool finalPhrase)
        {
            int low = _p.MelodyLowDegree, high = _p.MelodyHighDegree;
            int mid = (low + high) / 2;
            int range = isB ? 5 : 4;
            int lift = isB ? 2 : 0;
            var pool = Pool();

            // Start on a chord note near the middle of the range.
            var firstChord = ChordAt(bar0);
            int deg = Math.Clamp(NearestDegreeTo(NearestChordNote(firstChord, DegreeMidi(mid + lift - 1)), low, high), low, high);
            int apex = Math.Min(high, deg + range);
            int prevMove = 0;
            int lastTemplate = -1;

            for (int i = 0; i < 4; i++)
            {
                int bar = bar0 + i;
                if (_melodyRest.Contains(bar)) continue;
                var chord = ChordAt(bar);
                bool cadenceBar = i == 3;

                bool restBar = !cadenceBar && !(i == 0 && phrase == 0) && rng.Chance(_p.RestBarChance);
                bool fadedRest = !cadenceBar && i is 1 or 2 && rng.Chance(_p.FadedDropChance * 0.7f);

                int t;
                do { t = rng.Next(pool.Length); } while (pool.Length > 1 && t == lastTemplate);
                lastTemplate = t;
                var template = pool[t];

                int desired = i switch
                {
                    0 => deg,
                    1 => deg + (apex - deg) * 6 / 10,
                    2 => apex,
                    _ => Math.Max(low, deg - 1),
                };

                for (int j = 0; j < template.Length; j++)
                {
                    bool last = cadenceBar && j == template.Length - 1;
                    var (step, len) = template[j];
                    int at = bar * BarTicks + step * Step;
                    if (last) len = 16 - step;                       // the cadence note is held to the end of the bar
                    int dur = len * Step - (len >= 4 ? 10 : 6);
                    bool strong = step % 8 == 0;

                    if (last)
                    {
                        AddCadence(rng, at, dur, chord, deg, isB && phrase == 1);
                        continue;
                    }
                    if (restBar) continue;

                    int next = NextDegree(rng, deg, desired, strong, chord, prevMove, low, high);
                    prevMove = next - deg;
                    deg = next;

                    bool structural = i == 0 && j == 0;
                    StateMask mask = (fadedRest || (!structural && rng.Chance(_p.FadedDropChance))) ? StateMask.Warm : StateMask.All;

                    float vel = 0.56f + (strong ? 0.06f : 0f) + (deg == apex ? 0.05f : 0f) + rng.Range(-0.04f, 0.04f);
                    int jitter = (i == 0 && j == 0) ? 0 : rng.Range(-3, 3);
                    Add(Math.Max(0, at + jitter), dur, DegreeMidi(deg), vel, Layer.Melody, _p.MelodyPatch, rng.Range(-10, 10), mask);
                }

                if (cadenceBar) deg = Math.Clamp(deg, low, high);
            }
        }

        private int NearestDegreeTo(int midi, int low, int high)
        {
            int best = low, bestDist = int.MaxValue;
            for (int d = low; d <= high; d++)
            {
                int dist = Math.Abs(DegreeMidi(d) - midi);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best;
        }

        private int NextDegree(Rng rng, int cur, int desired, bool strong, ChordSpec chord, int prevMove, int low, int high)
        {
            double total = 0;
            var weights = new double[9];
            for (int k = 0; k < weights.Length; k++)
            {
                int cand = cur + k - 4;
                if (cand < low || cand > high) continue;
                int delta = cand - cur;
                int ad = Math.Abs(delta);

                double leap = _p.LeapChance;
                double w = ad switch { 0 => 0.3, 1 => 1.0, 2 => 0.55, 3 => 0.2, 4 => leap, _ => 0 };
                w *= Math.Exp(-Math.Abs(cand - desired) * 0.45);
                bool inChord = InChord(DegreeMidi(cand), chord);
                w *= strong ? (inChord ? 4.0 : 0.15) : (inChord ? 1.2 : 1.0);
                if (Math.Abs(prevMove) >= 3 && Math.Sign(delta) == Math.Sign(prevMove)) w *= 0.25;   // a leap is answered by turning back
                weights[k] = w;
                total += w;
            }
            if (total <= 0) return Math.Clamp(cur, low, high);

            double pick = rng.NextFloat() * total;
            for (int k = 0; k < weights.Length; k++)
            {
                pick -= weights[k];
                if (pick <= 0 && weights[k] > 0) return cur + k - 4;
            }
            return cur;
        }

        /// <summary>A phrase ending: it hangs (a step beyond a chord note) while faded, and rests on a chord note once restored.</summary>
        private void AddCadence(Rng rng, int at, int dur, ChordSpec chord, int fromDegree, bool hangEverywhere)
        {
            int low = _p.MelodyLowDegree, high = _p.MelodyHighDegree;
            int hangPc1 = Mod12(chord.Root + 2), hangPc2 = Mod12(chord.Root + 9);
            int restPc1 = Mod12(chord.Root), restPc2 = Mod12(chord.Root + 7);

            int Pick(int pcA, int pcB)
            {
                int best = -1, bestDist = int.MaxValue;
                for (int d = low; d <= high; d++)
                {
                    int pc = Mod12(DegreeMidi(d) - _p.RootMidiNote);
                    if (pc != pcA && pc != pcB) continue;
                    int dist = Math.Abs(d - fromDegree);
                    if (dist < bestDist) { bestDist = dist; best = d; }
                }
                return best;
            }

            int hang = Pick(hangPc1, hangPc2);
            int rest = Pick(restPc1, restPc2);
            if (hang < 0) hang = Math.Clamp(fromDegree + 1, low, high);
            if (rest < 0) rest = Math.Clamp(fromDegree, low, high);

            float vel = 0.52f + rng.Range(-0.03f, 0.03f);
            if (hangEverywhere)
            {
                Add(at, dur, DegreeMidi(hang), vel, Layer.Melody, _p.MelodyPatch, 0, StateMask.All);
                return;
            }
            Add(at, dur, DegreeMidi(hang), vel, Layer.Melody, _p.MelodyPatch, 0, StateMask.Calm);
            Add(at, dur + 24, DegreeMidi(rest), vel + 0.05f, Layer.Melody, _p.MelodyPatch, 0, StateMask.Restored);
        }

        // ---- accents ("fireflies") and answers -------------------------------------------------------------------------

        private void BuildAccents()
        {
            int sections = _bars / 8;
            for (int section = 0; section < sections; section++)
            {
                var rng = new Rng(_p.VariationSeed * 17 + section * 313 + 5);
                int total = Math.Max(_p.AccentsFaded, _p.AccentsRestored);
                for (int k = 0; k < total; k++)
                {
                    int bar = section * 8 + rng.Next(8);
                    int step = new[] { 2, 6, 10, 14 }[rng.Next(4)];
                    var chord = ChordAt(bar);
                    int target = _p.MelodyBaseMidi + 12 + rng.Range(0, 11);
                    int midi = NearestChordNote(chord, target);
                    StateMask mask = k < _p.AccentsFaded ? StateMask.All : (k % 2 == 0 ? StateMask.Warm : StateMask.Restored);
                    Add(bar * BarTicks + step * Step, 2 * Beat, midi, rng.Range(0.24f, 0.38f), Layer.Accent, _p.AccentPatch, rng.Range(-60, 60), mask);
                }

                // Once restored, a phrase is answered by two high notes just before the next one begins.
                for (int phrase = 0; phrase < 2; phrase++)
                {
                    int bar = section * 8 + phrase * 4 + 3;
                    var chord = ChordAt(bar);
                    int a = NearestChordNote(chord, _p.MelodyBaseMidi + 16);
                    Add(bar * BarTicks + 12 * Step, Beat, a, 0.34f, Layer.Accent, _p.AccentPatch, -20, StateMask.Restored);
                    Add(bar * BarTicks + 14 * Step, 2 * Beat, a + 7, 0.30f, Layer.Accent, _p.AccentPatch, 20, StateMask.Restored);
                }
            }
        }

        // ---- counter-melody (what the restored world adds) ---------------------------------------------------------------

        private void BuildCounter()
        {
            if (_p.Counter == CounterStyle.None) return;
            string patch = _p.CounterPatch;
            int register = _p.MelodyBaseMidi - 5;

            for (int bar = 0; bar < _bars; bar++)
            {
                var rng = new Rng(_p.VariationSeed * 13 + bar * 101 + 3);
                var chord = ChordAt(bar);
                int t0 = bar * BarTicks;
                StateMask mask = bar % 2 == 0 ? StateMask.Warm : StateMask.Restored;

                switch (_p.Counter)
                {
                    case CounterStyle.Interlock:
                    {
                        var patterns = new[] { new[] { 2, 6, 10, 14 }, new[] { 3, 7, 11, 15 }, new[] { 2, 5, 10, 13 }, new[] { 1, 6, 9, 14 } };
                        var steps = patterns[rng.Next(patterns.Length)];
                        for (int k = 0; k < steps.Length; k++)
                        {
                            if (rng.Chance(0.18f)) continue;
                            int midi = NearestChordNote(chord, register + rng.Range(0, 12));
                            Add(t0 + steps[k] * Step, 3 * Step, midi, rng.Range(0.30f, 0.40f), Layer.Counter, patch, k % 2 == 0 ? -35 : 35, mask);
                        }
                        break;
                    }

                    case CounterStyle.Arpeggio:
                    {
                        for (int s = 0; s < 16; s += 2)
                        {
                            int toneIndex = (s / 2) % 4;
                            int note = _p.RootMidiNote + chord.Root + chord.Tones[Math.Min(toneIndex, chord.Tones.Length - 1)];
                            note = PadFold(note, register);
                            if (toneIndex == 3) note += 12;
                            Add(t0 + s * Step, 4 * Step, note, 0.26f + (s % 8 == 0 ? 0.05f : 0f), Layer.Counter, patch, (s / 2) % 2 == 0 ? -25 : 25, StateMask.Restored);
                        }
                        break;
                    }

                    case CounterStyle.Glass:
                    {
                        if (bar % 2 == 1 && rng.Chance(0.7f))
                        {
                            int s = new[] { 4, 8, 10 }[rng.Next(3)];
                            int top = NearestChordNote(chord, _p.MelodyBaseMidi + 19);
                            Add(t0 + s * Step, 2 * Beat, top, 0.26f, Layer.Counter, patch, 30, StateMask.Restored);
                            Add(t0 + (s + 3) * Step, 2 * Beat, top - 5, 0.22f, Layer.Counter, patch, -30, StateMask.Restored);
                        }
                        break;
                    }

                    case CounterStyle.Ticks:
                    {
                        // Two small patterns that interlock without competing: one on the beats, one between them.
                        int midi = NearestChordNote(chord, register + 7);
                        foreach (int s in new[] { 0, 8 })
                            Add(t0 + s * Step, 2 * Step, midi, 0.32f, Layer.Counter, patch, -30, StateMask.Restored);
                        foreach (int s in new[] { 4, 12 })
                            Add(t0 + s * Step, 2 * Step, midi + 7, 0.26f, Layer.Counter, patch, 30, StateMask.Warm);
                        break;
                    }
                }
            }
        }

        // ---- chain layer (brought in by play) -------------------------------------------------------------------------------

        private void BuildChain()
        {
            string patch = _p.CounterPatch;
            int register = _p.MelodyBaseMidi - 5;
            int[] order = { 0, 1, 2, 3, 2, 1, 2, 1 };

            for (int bar = 0; bar < _bars; bar++)
            {
                var chord = ChordAt(bar);
                for (int s = 0; s < 16; s += 2)
                {
                    int idx = Math.Min(order[(s / 2) % order.Length], chord.Tones.Length - 1);
                    int note = PadFold(_p.RootMidiNote + chord.Root + chord.Tones[idx], register + 12);
                    Add(bar * BarTicks + s * Step, 3 * Step, note, 0.24f + (s % 8 == 0 ? 0.06f : 0f), Layer.Chain, patch, (s / 2) % 2 == 0 ? -30 : 30, StateMask.All);
                }
            }
        }

        // ---- pulse ---------------------------------------------------------------------------------------------------------

        private void BuildPulse()
        {
            if (_p.Pulse == PulseStyle.None) return;
            string patch = _p.PulsePatch;
            int tick = _p.RootMidiNote + 36;

            for (int bar = 0; bar < _bars; bar++)
            {
                var rng = new Rng(_p.VariationSeed * 29 + bar * 211 + 1);
                int t0 = bar * BarTicks;

                switch (_p.Pulse)
                {
                    case PulseStyle.SoftBeat:
                        Add(t0, Beat, _p.RootMidiNote - 12, 0.34f, Layer.Pulse, patch, 0, StateMask.Warm);
                        Add(t0 + 8 * Step, Beat, _p.RootMidiNote - 12, 0.26f, Layer.Pulse, patch, 0, StateMask.Warm);
                        break;

                    case PulseStyle.Ticks:
                        // Restored: steady on every beat. Faded: the third beat is missing, and others sometimes fail to come.
                        for (int b = 0; b < 4; b++)
                        {
                            float v = b == 0 ? 0.34f : 0.26f;
                            Add(t0 + b * Beat, Step, tick + (b % 2 == 0 ? 0 : 7), v, Layer.Pulse, patch, b % 2 == 0 ? -25 : 25, StateMask.Restored);
                            if (b != 2 && !rng.Chance(0.2f))
                                Add(t0 + b * Beat, Step, tick + (b % 2 == 0 ? 0 : 7), v * 0.8f, Layer.Pulse, patch, b % 2 == 0 ? -25 : 25, StateMask.Calm);
                        }
                        break;

                    case PulseStyle.Mechanical:
                        for (int s = 0; s < 16; s += 2)
                        {
                            float v = s % 4 == 0 ? 0.30f : 0.18f;
                            Add(t0 + s * Step, Step, tick + (s % 8 == 0 ? 0 : 7), v, Layer.Pulse, patch, s % 4 == 0 ? -25 : 25, StateMask.Restored);
                            if (s != 8 && s != 10 && !rng.Chance(0.25f))
                                Add(t0 + s * Step, Step, tick + (s % 8 == 0 ? 0 : 7), v * 0.8f, Layer.Pulse, patch, s % 4 == 0 ? -25 : 25, StateMask.Calm);
                        }
                        break;
                }
            }
        }

        // ---- tension (optional, near the top of the board) ----------------------------------------------------------------

        private void BuildTension()
        {
            int low = _p.RootMidiNote - 25;
            for (int bar = 0; bar < _bars; bar++)
            {
                var chord = ChordAt(bar);
                int root = PadFold(_p.RootMidiNote + chord.Root, low);
                for (int b = 0; b < 4; b++)
                    Add(bar * BarTicks + b * Beat, Beat - 12, root + 7, b == 0 ? 0.30f : 0.20f, Layer.Tension, "SoftBass", 0, StateMask.All);
            }
        }
    }
}

/// <summary>Plain-text view of a score, for tests, the Music Lab and for reading what the composer wrote.</summary>
public static class ScoreDump
{
    private static readonly string[] Names = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };

    public static string NoteName(int midi) => Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);

    public static string Describe(MusicScore score, StateMask state, Layer? only = null, int fromBar = 0, int toBar = int.MaxValue)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{score.Id}  {score.TempoBpm} BPM  {score.Bars} bars  state {state}");
        foreach (var e in score.Events)
        {
            if ((e.States & state) == 0) continue;
            if (only.HasValue && e.Layer != only.Value) continue;
            int bar = e.Start / score.TicksPerBar;
            if (bar < fromBar || bar > toBar) continue;
            double beat = (e.Start % score.TicksPerBar) / (double)MusicScore.TicksPerBeat + 1;
            sb.AppendLine($"  bar {bar + 1,2} beat {beat,4:F2}  {e.Layer,-7} {NoteName(e.Midi),-4} len {e.Duration / (double)MusicScore.TicksPerBeat,4:F2}b vel {e.Velocity,3}  {score.Patches[e.Patch].Id}");
        }
        return sb.ToString();
    }
}
