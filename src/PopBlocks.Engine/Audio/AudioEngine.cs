namespace PopBlocks.Engine.Audio;

/// <summary>What the engine needs to start (or stop) a piece of music.</summary>
public sealed record ScoreRequest(
    MusicScore? Score,
    int StartBar,
    StateMask State,
    float[] Layers,
    float FadeOutSeconds,
    float FadeInSeconds);

/// <summary>
/// The whole real-time sound engine: voices, music clock, effect cues, ambience, three buses with their own reverb, volume
/// and mute, and a final limiter. The output (a phone's audio track, or an offline file writer) calls <see cref="Render"/>
/// to get the next block of stereo samples.
///
/// Threading: every method except <see cref="Render"/> is safe to call from any thread. They only post a small command;
/// <see cref="Render"/> picks the commands up at the start of the next block. Nothing in <see cref="Render"/> allocates,
/// reads a file, logs, or waits on a lock held by game code.
/// </summary>
public sealed class AudioEngine
{
    /// <summary>The engine works in chunks this long (about 0.7 ms), so notes start with sub-millisecond accuracy.</summary>
    public const int MaxChunk = 32;

    /// <summary>The most notes that can sound at once.</summary>
    public const int VoiceLimit = Synth.PoolSize;

    private const int QueueSize = 256;
    private const int MaxPendingNotes = 256;
    private const float Threshold = 0.9f;

    private enum Kind : byte { LoadScore, Layers, State, BusVolume, MusicDuck, AmbienceDuck, MasterFade, PlayCue, Reverb, Ambience, Panic }

    private struct Command
    {
        public Kind Kind;
        public Quantize Quantize;
        public int I1;
        public float F1, F2, F3;
        public object? Obj;
    }

    private struct PendingNote
    {
        public long Due;
        public CueNote Note;
        public Bus Bus;
        public float Volume;
        public float Semitones;
    }

    private readonly float _rate;
    private readonly Synth _synth;
    private readonly MusicSequencer _sequencer;
    private readonly AmbienceGenerator _ambience;
    private readonly Reverb[] _reverbs = new Reverb[3];

    private readonly float[][] _busL = new float[3][];
    private readonly float[][] _busR = new float[3][];
    private readonly float[][] _send = new float[3][];

    // Command queue: many producers (one at a time, under a short lock), one consumer (the audio thread, no lock).
    private readonly Command[] _queue = new Command[QueueSize];
    private readonly object _gate = new();
    private int _head, _tail;
    private int _dropped;

    // Smoothed levels. Each moves a little towards its target every sample, so no change ever steps.
    private readonly Smooth[] _user = { new(1f), new(1f), new(1f) };
    private readonly Smooth _musicDuck = new(1f);
    private readonly Smooth _ambienceDuck = new(1f);
    private readonly Smooth _crossfade = new(0f);
    private readonly Smooth _cueDuck = new(1f);
    private readonly Smooth _master = new(1f);

    // A change of piece waits here until the old one has faded out.
    private ScoreRequest? _waiting;
    private bool _hasWaiting;

    // Cue notes waiting for their moment.
    private readonly PendingNote[] _pending = new PendingNote[MaxPendingNotes];
    private int _pendingCount;
    private float _cueDuckGain = 1f;
    private long _cueDuckUntil;

    private long _now;
    private float _limiterGain = 1f;
    private readonly float _limiterRelease;
    private float _peak;
    private volatile int _barSnapshot;
    private volatile int _beatSnapshot;
    private volatile int _voiceSnapshot;
    private int _droppedNotes;

    private sealed class Smooth
    {
        public float Value, Target, Coeff;
        public Smooth(float v) { Value = v; Target = v; Coeff = 1f; }
        public void Set(float target, float seconds, float rate)
        {
            Target = target;
            Coeff = seconds <= 0.0005f ? 1f : Dsp.SmoothCoeff(seconds / 3f, rate);
        }
        public float Step()
        {
            Value += (Target - Value) * Coeff;
            return Value;
        }
    }

    public AudioEngine(int sampleRate = 44100)
    {
        _rate = Math.Clamp(sampleRate, 8000, 96000);
        _synth = new Synth(_rate);
        _sequencer = new MusicSequencer(_synth, _rate);
        _ambience = new AmbienceGenerator(_synth, _rate);
        for (int b = 0; b < 3; b++)
        {
            _busL[b] = new float[MaxChunk];
            _busR[b] = new float[MaxChunk];
            _send[b] = new float[MaxChunk];
            _reverbs[b] = new Reverb(_rate);
        }
        _reverbs[(int)Bus.Music].Configure(0.5f, 0.4f, 0.25f);
        _reverbs[(int)Bus.Sfx].Configure(0.25f, 0.55f, 0.16f);
        _reverbs[(int)Bus.Ambience].Configure(0.55f, 0.5f, 0.3f);
        _limiterRelease = Dsp.SmoothCoeff(0.12f, _rate);
        _crossfade.Set(1f, 0.01f, _rate);
    }

    public int SampleRate => (int)_rate;

    /// <summary>The loudest recent output sample (0..1), for meters and tests.</summary>
    public float PeakLevel => _peak;

    /// <summary>Voices sounding at the end of the last block.</summary>
    public int ActiveVoices => _voiceSnapshot;

    /// <summary>The bar and beat of the music clock (0-based), as of the last block.</summary>
    public int CurrentBar => _barSnapshot;
    public int CurrentBeat => _beatSnapshot;

    private int _cuesPlayed;

    /// <summary>How many cues have been asked for since the engine was made (for tests and the Music Lab).</summary>
    public int CuesPlayed => Volatile.Read(ref _cuesPlayed);

    /// <summary>Commands or notes that were dropped because a queue was full. Should stay 0.</summary>
    public int DroppedCount => Volatile.Read(ref _dropped) + _droppedNotes;

    // ------------------------------------------------------------------ control surface (any thread)

    /// <summary>Fades out what is playing (if anything) and starts <paramref name="request"/>'s piece, or silence if its score is null.</summary>
    public void LoadScore(ScoreRequest request) => Post(new Command { Kind = Kind.LoadScore, Obj = request });

    public void SetLayerTargets(float[] targets, Quantize quantize) =>
        Post(new Command { Kind = Kind.Layers, Obj = targets, Quantize = quantize });

    public void SetState(StateMask state, Quantize quantize) =>
        Post(new Command { Kind = Kind.State, I1 = (int)state, Quantize = quantize });

    /// <summary>Sets a bus's volume as a gain (0 = silent). Mute is a gain of 0, reached in a few milliseconds.</summary>
    public void SetBusGain(Bus bus, float gain, float rampSeconds = 0.03f) =>
        Post(new Command { Kind = Kind.BusVolume, I1 = (int)bus, F1 = gain, F2 = rampSeconds });

    public void SetMusicDuck(float gain, float rampSeconds) =>
        Post(new Command { Kind = Kind.MusicDuck, F1 = gain, F2 = rampSeconds });

    public void SetAmbienceDuck(float gain, float rampSeconds) =>
        Post(new Command { Kind = Kind.AmbienceDuck, F1 = gain, F2 = rampSeconds });

    /// <summary>Fades everything (used just before the output is paused, and when it resumes).</summary>
    public void SetMasterFade(float gain, float rampSeconds) =>
        Post(new Command { Kind = Kind.MasterFade, F1 = gain, F2 = rampSeconds });

    public void SetAmbience(AmbienceStyle? style) => Post(new Command { Kind = Kind.Ambience, Obj = style });

    public void ConfigureReverb(Bus bus, float room, float damping, float wet) =>
        Post(new Command { Kind = Kind.Reverb, I1 = (int)bus, F1 = room, F2 = damping, F3 = wet });

    /// <summary>
    /// Plays a cue. <paramref name="volume"/> scales its level, <paramref name="semitones"/> transposes its pitched notes,
    /// and <paramref name="quantizeTicks"/> (when above 0) waits for the next multiple of that many ticks of the music clock
    /// (never more than 0.2 s) so an accent lands on the beat.
    /// </summary>
    public void PlayCue(SfxCue cue, float volume = 1f, float semitones = 0f, int quantizeTicks = 0)
    {
        Interlocked.Increment(ref _cuesPlayed);
        Post(new Command { Kind = Kind.PlayCue, Obj = cue, F1 = volume, F2 = semitones, I1 = quantizeTicks });
    }

    /// <summary>Stops every sound with a very short fade.</summary>
    public void Panic() => Post(new Command { Kind = Kind.Panic });

    private void Post(in Command c)
    {
        lock (_gate)
        {
            int next = (_tail + 1) & (QueueSize - 1);
            if (next == Volatile.Read(ref _head))
            {
                Interlocked.Increment(ref _dropped);
                return;
            }
            _queue[_tail] = c;
            Volatile.Write(ref _tail, next);
        }
    }

    // ------------------------------------------------------------------ audio thread

    /// <summary>
    /// Fills <paramref name="frames"/> stereo frames (interleaved left, right) into <paramref name="output"/> starting at
    /// <paramref name="offset"/>. Call from the audio thread only.
    /// </summary>
    public void Render(float[] output, int offset, int frames)
    {
        Drain();
        int done = 0;
        while (done < frames)
        {
            int n = Math.Min(MaxChunk, frames - done);
            Chunk(output, offset + done * 2, n);
            done += n;
        }
        _barSnapshot = _sequencer.CurrentBar;
        _beatSnapshot = _sequencer.CurrentBeat;
        _voiceSnapshot = _synth.ActiveCount;
    }

    private void Drain()
    {
        while (_head != Volatile.Read(ref _tail))
        {
            var c = _queue[_head];
            _queue[_head] = default;
            Volatile.Write(ref _head, (_head + 1) & (QueueSize - 1));
            Execute(in c);
        }
    }

    private void Execute(in Command c)
    {
        switch (c.Kind)
        {
            case Kind.LoadScore:
                _waiting = (ScoreRequest)c.Obj!;
                _hasWaiting = true;
                _crossfade.Set(0f, _sequencer.HasScore ? _waiting.FadeOutSeconds : 0f, _rate);
                break;

            case Kind.Layers:
                _sequencer.QueueLayers((float[])c.Obj!, c.Quantize);
                break;

            case Kind.State:
                _sequencer.QueueState((StateMask)c.I1, c.Quantize);
                break;

            case Kind.BusVolume:
                _user[Math.Clamp(c.I1, 0, 2)].Set(Math.Max(0f, c.F1), c.F2, _rate);
                break;

            case Kind.MusicDuck:
                _musicDuck.Set(Math.Clamp(c.F1, 0f, 1f), c.F2, _rate);
                break;

            case Kind.AmbienceDuck:
                _ambienceDuck.Set(Math.Clamp(c.F1, 0f, 1f), c.F2, _rate);
                break;

            case Kind.MasterFade:
                _master.Set(Math.Clamp(c.F1, 0f, 1f), c.F2, _rate);
                break;

            case Kind.Ambience:
                _ambience.SetStyle((AmbienceStyle?)c.Obj);
                break;

            case Kind.Reverb:
                _reverbs[Math.Clamp(c.I1, 0, 2)].Configure(c.F1, c.F2, c.F3);
                break;

            case Kind.PlayCue:
                ScheduleCue((SfxCue)c.Obj!, c.F1, c.F2, c.I1);
                break;

            case Kind.Panic:
                _synth.KillAll(0.02f);
                _pendingCount = 0;
                break;
        }
    }

    private void ScheduleCue(SfxCue cue, float volume, float semitones, int quantizeTicks)
    {
        long start = _now;
        if (quantizeTicks > 0 && _sequencer.HasScore)
        {
            double rem = _sequencer.PositionTicks % quantizeTicks;
            if (rem > 0.5)
            {
                double wait = (quantizeTicks - rem) / _sequencer.TicksPerSample;
                start += (long)Math.Min(wait, 0.2 * _rate);
            }
        }

        foreach (var note in cue.Notes)
        {
            if (_pendingCount >= MaxPendingNotes) { _droppedNotes++; continue; }
            _pending[_pendingCount++] = new PendingNote
            {
                Due = start + (long)(note.DelayMs * _rate / 1000f),
                Note = note,
                Bus = cue.Bus,
                Volume = volume,
                Semitones = semitones,
            };
        }

        if (cue.DuckDb > 0f)
        {
            float gain = Dsp.DbToGain(-cue.DuckDb);
            _cueDuckGain = _cueDuckUntil > _now ? MathF.Min(_cueDuckGain, gain) : gain;
            _cueDuckUntil = Math.Max(_cueDuckUntil, start + (long)(cue.DuckMs * _rate / 1000f));
        }
    }

    private void Chunk(float[] output, int at, int n)
    {
        // A change of piece: once the old one has faded out, swap, then fade the new one in.
        if (_hasWaiting && _crossfade.Value < 0.004f)
        {
            var req = _waiting!;
            _hasWaiting = false;
            _waiting = null;
            _synth.KillBus(Bus.Music, 0.02f);
            _sequencer.SetScore(req.Score, req.StartBar);
            _sequencer.SetStateNow(req.State);
            _sequencer.SetLayersNow(req.Layers, snap: true);
            if (req.Score is not null)
            {
                _reverbs[(int)Bus.Music].Configure(req.Score.ReverbRoom, req.Score.ReverbDamping, req.Score.ReverbWet);
                _crossfade.Set(1f, req.FadeInSeconds, _rate);
            }
        }

        _sequencer.Advance(n);
        FirePending(n);
        _ambience.Advance(n);

        for (int b = 0; b < 3; b++)
        {
            Array.Clear(_busL[b], 0, n);
            Array.Clear(_busR[b], 0, n);
            Array.Clear(_send[b], 0, n);
        }

        _synth.Render(n, _busL, _busR, _send);
        _ambience.RenderBed(n, _busL[(int)Bus.Ambience], _busR[(int)Bus.Ambience]);

        for (int b = 0; b < 3; b++)
            _reverbs[b].Process(_send[b], _busL[b], _busR[b], n);

        // Mix the buses to the master, with every level moving smoothly.
        var musicL = _busL[0]; var musicR = _busR[0];
        var sfxL = _busL[1]; var sfxR = _busR[1];
        var ambL = _busL[2]; var ambR = _busR[2];
        float duckTarget = _now < _cueDuckUntil ? _cueDuckGain : 1f;
        _cueDuck.Set(duckTarget, 0.08f, _rate);

        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float music = _user[0].Step() * _musicDuck.Step() * _crossfade.Step() * _cueDuck.Step();
            float sfx = _user[1].Step();
            float amb = _user[2].Step() * _ambienceDuck.Step();
            float master = _master.Step();

            float l = (musicL[i] * music + sfxL[i] * sfx + ambL[i] * amb) * master;
            float r = (musicR[i] * music + sfxR[i] * sfx + ambR[i] * amb) * master;

            // Final limiter: pulls the level down smoothly if the mix would pass the threshold, then a soft clip as a last guard.
            float p = MathF.Max(MathF.Abs(l), MathF.Abs(r));
            float want = p > Threshold ? Threshold / p : 1f;
            if (want < _limiterGain) _limiterGain = want;
            else _limiterGain += (1f - _limiterGain) * _limiterRelease;

            l = Dsp.SoftClip(l * _limiterGain);
            r = Dsp.SoftClip(r * _limiterGain);
            output[at + i * 2] = l;
            output[at + i * 2 + 1] = r;

            float m = MathF.Max(MathF.Abs(l), MathF.Abs(r));
            if (m > peak) peak = m;
        }

        _peak = MathF.Max(peak, _peak * 0.9995f);
        _now += n;
    }

    private void FirePending(int n)
    {
        if (_pendingCount == 0) return;
        long until = _now + n;
        for (int i = 0; i < _pendingCount;)
        {
            ref var p = ref _pending[i];
            if (p.Due >= until) { i++; continue; }

            var note = p.Note;
            var np = new NoteParams
            {
                Hz = note.Hz > 0f ? note.Hz : Dsp.MidiToHz(note.Midi + (note.Transposes ? p.Semitones : 0f)),
                Velocity = note.Velocity,
                Pan = note.Pan,
                Gain = note.Gain * p.Volume,
                HoldSamples = Math.Max(1, (int)(note.DurationMs * _rate / 1000f)),
                GlideSemis = note.GlideSemis,
                GlideSeconds = note.GlideMs / 1000f,
                NoiseHzFrom = note.NoiseFromHz,
                NoiseHzTo = note.NoiseToHz,
                NoiseSweepSeconds = note.NoiseSweepMs / 1000f,
            };
            _synth.NoteOn(note.Patch, p.Bus, in np);

            _pending[i] = _pending[--_pendingCount];
            _pending[_pendingCount] = default;
        }
    }
}
