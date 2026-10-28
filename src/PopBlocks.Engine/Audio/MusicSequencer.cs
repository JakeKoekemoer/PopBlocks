namespace PopBlocks.Engine.Audio;

/// <summary>
/// The musical clock. It steps through a <see cref="MusicScore"/> sample by sample's worth at a time, starts the notes that
/// belong to the current restoration state and to the layers that are switched on, and applies changes on musical boundaries
/// (the next beat, bar or phrase). It runs on the audio thread and never allocates while playing.
/// </summary>
internal sealed class MusicSequencer
{
    private readonly Synth _synth;
    private readonly float _rate;

    private MusicScore? _score;
    private int _next;
    private double _pos;                 // ticks into the loop
    private double _ticksPerSample;
    private double _samplesPerTick;

    private StateMask _state = StateMask.Faded;
    private readonly float[] _layerTarget = new float[MusicScore.LayerCount];
    private readonly float[] _layerGain = new float[MusicScore.LayerCount];

    private float[]? _pendingLayers;
    private Quantize _pendingLayersQ;
    private StateMask _pendingState;
    private bool _hasPendingState;
    private Quantize _pendingStateQ;

    public MusicSequencer(Synth synth, float sampleRate)
    {
        _synth = synth;
        _rate = sampleRate;
        for (int i = 0; i < _layerTarget.Length; i++) _layerTarget[i] = 1f;
        Array.Copy(_layerTarget, _layerGain, _layerGain.Length);
    }

    public bool HasScore => _score is not null;
    public MusicScore? Score => _score;
    public StateMask State => _state;

    /// <summary>Bar (0-based) the clock is in.</summary>
    public int CurrentBar => _score is null ? 0 : (int)(_pos / _score.TicksPerBar);

    /// <summary>Beat inside the bar, 0-based.</summary>
    public int CurrentBeat => _score is null ? 0 : (int)(_pos / MusicScore.TicksPerBeat) % _score.BeatsPerBar;

    /// <summary>Ticks into the current loop.</summary>
    public double PositionTicks => _pos;

    public double TicksPerSample => _ticksPerSample;

    public void SetScore(MusicScore? score, int startBar)
    {
        _score = score;
        _pendingLayers = null;
        _hasPendingState = false;
        if (score is null) return;

        _samplesPerTick = _rate * 60.0 / (score.TempoBpm * (double)MusicScore.TicksPerBeat);
        _ticksPerSample = 1.0 / _samplesPerTick;
        int bar = Math.Clamp(startBar, 0, Math.Max(0, score.Bars - 1));
        _pos = bar * (double)score.TicksPerBar;
        _next = FirstEventAtOrAfter(score.Events, (int)Math.Ceiling(_pos));
    }

    private static int FirstEventAtOrAfter(NoteEvent[] events, int tick)
    {
        int lo = 0, hi = events.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (events[mid].Start < tick) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>Sets the state immediately (used when a piece first starts).</summary>
    public void SetStateNow(StateMask state)
    {
        _state = state;
        _hasPendingState = false;
    }

    public void SetLayersNow(float[] targets, bool snap)
    {
        Array.Copy(targets, _layerTarget, Math.Min(targets.Length, _layerTarget.Length));
        if (snap) Array.Copy(_layerTarget, _layerGain, _layerGain.Length);
        _pendingLayers = null;
    }

    public void QueueLayers(float[] targets, Quantize q)
    {
        if (q == Quantize.Immediate || _score is null) { SetLayersNow(targets, snap: false); return; }
        _pendingLayers = targets;
        _pendingLayersQ = q;
    }

    public void QueueState(StateMask state, Quantize q)
    {
        if (q == Quantize.Immediate || _score is null) { SetStateNow(state); return; }
        _pendingState = state;
        _hasPendingState = true;
        _pendingStateQ = q;
    }

    /// <summary>Moves the clock on by <paramref name="frames"/> samples, starting every note that falls inside that time.</summary>
    public void Advance(int frames)
    {
        var score = _score;
        if (score is null) return;

        // Layer levels glide towards their targets (about a third of a second), so a layer fades in rather than appearing.
        float k = 1f - MathF.Exp(-frames / (0.30f * _rate));
        for (int i = 0; i < _layerGain.Length; i++)
            _layerGain[i] += (_layerTarget[i] - _layerGain[i]) * k;

        double start = _pos;
        double end = start + frames * _ticksPerSample;
        double loop = score.LoopTicks;

        ApplyBoundaries(score, start, end);

        if (end >= loop)
        {
            Fire(score, loop);
            _pos = end - loop;
            _next = 0;
            Fire(score, _pos);
        }
        else
        {
            _pos = end;
            Fire(score, end);
        }
    }

    private void Fire(MusicScore score, double untilTick)
    {
        var events = score.Events;
        while (_next < events.Length && events[_next].Start < untilTick)
        {
            ref readonly var ev = ref events[_next];
            _next++;

            if ((ev.States & _state) == 0) continue;
            float gain = _layerGain[(int)ev.Layer];
            if (gain < 0.01f) continue;

            var patch = score.Patches[ev.Patch];
            var np = new NoteParams
            {
                Hz = Dsp.MidiToHz(ev.Midi),
                Velocity = ev.Velocity * (1f / 127f) * gain,
                Pan = ev.Pan * 0.01f,
                Gain = 1f,
                HoldSamples = Math.Max(1, (int)(ev.Duration * _samplesPerTick)),
            };
            _synth.NoteOn(patch, Bus.Music, in np);
        }
    }

    private void ApplyBoundaries(MusicScore score, double start, double end)
    {
        if (_pendingLayers is null && !_hasPendingState) return;

        bool beat = (long)(end / MusicScore.TicksPerBeat) != (long)(start / MusicScore.TicksPerBeat);
        bool bar = (long)(end / score.TicksPerBar) != (long)(start / score.TicksPerBar);
        long phraseTicks = (long)score.TicksPerBar * Math.Max(1, score.PhraseBars);
        bool phrase = (long)(end / phraseTicks) != (long)(start / phraseTicks);

        if (_pendingLayers is not null && Reached(_pendingLayersQ, beat, bar, phrase))
        {
            Array.Copy(_pendingLayers, _layerTarget, Math.Min(_pendingLayers.Length, _layerTarget.Length));
            _pendingLayers = null;
        }

        if (_hasPendingState && Reached(_pendingStateQ, beat, bar, phrase))
        {
            _state = _pendingState;
            _hasPendingState = false;
        }
    }

    private static bool Reached(Quantize q, bool beat, bool bar, bool phrase) => q switch
    {
        Quantize.NextBeat => beat,
        Quantize.NextBar => bar,
        Quantize.NextPhrase => phrase,
        _ => true,
    };
}
