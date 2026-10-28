namespace PopBlocks.Engine.Audio;

/// <summary>One kind of small, occasional sound in a region's ambience (a firefly chime, a bubble, a tick).</summary>
public sealed class AmbientEvent
{
    public InstrumentPatch Patch { get; init; } = Patches.PureTone;

    /// <summary>MIDI notes it may use, always from the region's key.</summary>
    public int[] Midis { get; init; } = { 84 };

    public float MinGapSeconds { get; init; } = 6f;
    public float MaxGapSeconds { get; init; } = 14f;
    public float MinVelocity { get; init; } = 0.12f;
    public float MaxVelocity { get; init; } = 0.3f;
    public float HoldMs { get; init; } = 120f;
    public float GlideSemis { get; init; }
    public float GlideMs { get; init; }
    public float Pan { get; init; } = 0.8f;
}

/// <summary>
/// A region's ambience: a very quiet, dark bed of air that swells in occasional gusts (so it is never a steady hiss), and
/// a few sparse accents. Everything is on the ambience bus, which has its own volume.
/// </summary>
public sealed class AmbienceStyle
{
    /// <summary>Centre of the bed in Hz. Low, so it reads as air and not as hiss.</summary>
    public float BedHz { get; init; } = 500f;

    /// <summary>Loudness of the bed at the peak of a gust.</summary>
    public float BedLevel { get; init; } = 0.12f;

    /// <summary>The share of the peak that is left between gusts.</summary>
    public float BedFloor { get; init; } = 0.06f;

    public float GustMinSeconds { get; init; } = 7f;
    public float GustMaxSeconds { get; init; } = 14f;
    public AmbientEvent[] Events { get; init; } = Array.Empty<AmbientEvent>();
}

/// <summary>Runs a region's ambience on the audio thread.</summary>
internal sealed class AmbienceGenerator
{
    private readonly Synth _synth;
    private readonly float _rate;
    private readonly Rng _rng = new(7);

    private AmbienceStyle? _style;
    private double[] _eventDue = Array.Empty<double>();
    private double _time;

    // Bed
    private float _gustPhase, _gustInc;
    private float _lfoPhase;
    private float _l1, _l2, _r1, _r2;       // two one-pole low-pass stages per channel
    private uint _nl = 0x9E3779B1u, _nr = 0x85EBCA6Bu;
    private float _fade;                    // fades the whole ambience in and out when a region changes

    public AmbienceGenerator(Synth synth, float sampleRate)
    {
        _synth = synth;
        _rate = sampleRate;
    }

    public void SetStyle(AmbienceStyle? style)
    {
        _style = style;
        _time = 0;
        _eventDue = new double[style?.Events.Length ?? 0];
        if (style is not null)
        {
            for (int i = 0; i < _eventDue.Length; i++)
                _eventDue[i] = _rng.Range(1f, style.Events[i].MaxGapSeconds);
            NextGust();
        }
    }

    private void NextGust()
    {
        var s = _style!;
        float seconds = _rng.Range(s.GustMinSeconds, s.GustMaxSeconds);
        _gustInc = 1f / (seconds * _rate);
        _gustPhase = 0f;
    }

    /// <summary>Starts any accents that are due. Call once per chunk.</summary>
    public void Advance(int frames)
    {
        var style = _style;
        if (style is null) return;

        double dt = frames / (double)_rate;
        _time += dt;
        for (int i = 0; i < _eventDue.Length; i++)
        {
            if (_time < _eventDue[i]) continue;
            var e = style.Events[i];
            _eventDue[i] = _time + _rng.Range(e.MinGapSeconds, e.MaxGapSeconds);

            int midi = e.Midis[_rng.Next(e.Midis.Length)];
            float side = _rng.Chance(0.5f) ? 1f : -1f;
            var np = new NoteParams
            {
                Hz = Dsp.MidiToHz(midi),
                Velocity = _rng.Range(e.MinVelocity, e.MaxVelocity),
                Pan = side * e.Pan * _rng.Range(0.4f, 1f),
                Gain = 1f,
                HoldSamples = Math.Max(1, (int)(e.HoldMs * _rate / 1000f)),
                GlideSemis = e.GlideSemis,
                GlideSeconds = e.GlideMs / 1000f,
            };
            _synth.NoteOn(e.Patch, Bus.Ambience, in np);
        }
    }

    /// <summary>Adds the bed to the ambience bus.</summary>
    public void RenderBed(int frames, float[] left, float[] right)
    {
        var style = _style;
        float target = style is null ? 0f : 1f;
        if (style is null && _fade < 0.0005f) return;

        float fadeK = 1f - MathF.Exp(-1f / (1.5f * _rate));
        float cutoff = style?.BedHz ?? 500f;
        float baseCoeff = Math.Clamp(2f * MathF.PI * cutoff / _rate, 0.001f, 0.5f);
        float floor = style?.BedFloor ?? 0f;
        float level = style?.BedLevel ?? 0f;

        for (int i = 0; i < frames; i++)
        {
            _fade += (target - _fade) * fadeK;

            // Gust shape: a raised cosine, so the bed rises and falls smoothly; between gusts only the floor remains.
            float gust = 0f;
            if (_gustPhase < 1f)
            {
                gust = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * _gustPhase);
                _gustPhase += _gustInc;
            }
            else if (style is not null) NextGust();

            _lfoPhase += 0.11f / _rate;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
            float coeff = baseCoeff * (0.75f + 0.25f * Dsp.Sin(_lfoPhase));

            _nl ^= _nl << 13; _nl ^= _nl >> 17; _nl ^= _nl << 5;
            _nr ^= _nr << 13; _nr ^= _nr >> 17; _nr ^= _nr << 5;
            float wl = (_nl >> 8) * (2f / 16777216f) - 1f;
            float wr = (_nr >> 8) * (2f / 16777216f) - 1f;

            _l1 += (wl - _l1) * coeff; _l2 += (_l1 - _l2) * coeff;
            _r1 += (wr - _r1) * coeff; _r2 += (_r1 - _r2) * coeff;

            // A pair of one-pole stages takes a lot of level off white noise; 7 brings the bed back up to a useful amount.
            float amp = (floor + (1f - floor) * gust) * level * _fade * 7f;
            left[i] += _l2 * amp;
            right[i] += _r2 * amp;
        }
        _l1 = Dsp.Flush(_l1); _l2 = Dsp.Flush(_l2); _r1 = Dsp.Flush(_r1); _r2 = Dsp.Flush(_r2);
    }
}
