namespace PopBlocks.Engine.Audio;

/// <summary>Everything the synth needs to start one note, besides the instrument itself.</summary>
public struct NoteParams
{
    /// <summary>Pitch in Hz.</summary>
    public float Hz;

    /// <summary>0..1. Louder notes also reveal more of an instrument's upper partials.</summary>
    public float Velocity;

    /// <summary>-1 (left) to +1 (right).</summary>
    public float Pan;

    /// <summary>Extra level on top of the patch's own gain. 1 = as the patch is.</summary>
    public float Gain;

    /// <summary>Samples until the note is released on its own. 0 = never released (a ringing instrument just rings out).</summary>
    public int HoldSamples;

    /// <summary>The pitch starts this many semitones above (or below, if negative) the note and settles onto it.</summary>
    public float GlideSemis;

    public float GlideSeconds;

    /// <summary>Noise band sweep, in Hz. 0 = use the patch's own centre.</summary>
    public float NoiseHzFrom;

    public float NoiseHzTo;
    public float NoiseSweepSeconds;

    public static NoteParams Of(float hz, float velocity, float pan = 0f, int holdSamples = 0) => new()
    {
        Hz = hz, Velocity = velocity, Pan = pan, Gain = 1f, HoldSamples = holdSamples,
    };
}

/// <summary>
/// One sounding note. Voices are created once and reused: nothing in <see cref="Render"/> allocates or locks, so it is safe
/// to run on the audio thread. Every note fades in over at least ~1.5 ms and out over at least ~3 ms, so none clicks.
/// </summary>
internal sealed class Voice
{
    private enum Stage { Attack, Decay, Hold, Release }

    private const int Block = 16;               // pitch, vibrato and noise-filter settings are refreshed every 16 samples
    private const float NoiseMakeUp = 3.2f;     // a narrow band of noise is quiet; this brings NoiseAmount into a usable range
    private const float MinAttackSeconds = 0.0015f;

    public bool Active;
    public Bus Bus;
    public int Priority;
    public long Order;

    public bool Releasing => Active && _stage == Stage.Release;

    private InstrumentPatch _patch = Patches.PureTone;
    private float _rate = 44100f;
    private int _n;

    private readonly float[] _phase = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _inc = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _lvl = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _dec = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _gl = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _gr = new float[InstrumentPatch.MaxPartials];
    private readonly float[] _w = new float[InstrumentPatch.MaxPartials];

    // Envelope
    private Stage _stage;
    private float _env;
    private float _atkInc;
    private float _decCoeff;
    private float _sustain;
    private float _relCoeff;
    private int _holdLeft;       // samples until the automatic release; <0 = none

    // Pitch
    private float _pitchMul = 1f;
    private float _dropSemis, _dropSamples, _glideSemis, _glideSamples;
    private float _vibPhase, _vibInc, _vibDepth, _vibDelaySamples;
    private int _age;

    // Noise
    private uint _noiseState;
    private float _noiseAmt, _noiseEnv, _noiseDec;
    private float _nHzFrom, _nHzTo, _nSweepSamples;
    private float _ic1, _ic2, _a1, _a2, _a3, _k;
    private float _noiseL, _noiseR;

    private float _send;

    public void Start(InstrumentPatch patch, Bus bus, int priority, long order, in NoteParams np, float sampleRate)
    {
        _patch = patch;
        _rate = sampleRate;
        Bus = bus;
        Priority = priority;
        Order = order;
        _age = 0;

        float vel = Math.Clamp(np.Velocity, 0f, 1f);
        float amp = MathF.Pow(vel, 1.3f) * patch.Gain * (np.Gain <= 0f ? 1f : np.Gain);
        _n = Math.Min(patch.PartialCount, InstrumentPatch.MaxPartials);

        float basePan = Math.Clamp(np.Pan, -1f, 1f);
        for (int i = 0; i < _n; i++)
        {
            float spread = i < patch.Spread.Length ? patch.Spread[i] : 0f;
            float pan = Math.Clamp(basePan + spread, -1f, 1f);
            float angle = (pan + 1f) * 0.25f * MathF.PI;
            float a = patch.Amps[i] * MathF.Pow(vel, patch.Brightness * i * 0.5f);
            float inc = np.Hz * patch.Ratios[i] / sampleRate;
            if (inc >= 0.45f) a = 0f;   // above the audible band: leave it out rather than alias

            _gl[i] = a * amp * MathF.Cos(angle) * 1.4142135f;
            _gr[i] = a * amp * MathF.Sin(angle) * 1.4142135f;
            _w[i] = MathF.Max(_gl[i], _gr[i]);
            _inc[i] = inc;
            _lvl[i] = 1f;
            _phase[i] = patch.Shape == EnvelopeShape.Sustained ? (order * 0.61803f + i * 0.37f) % 1f : 0f;

            float mul = i < patch.DecayMul.Length ? patch.DecayMul[i] : 1f;
            float seconds;
            if (patch.Shape == EnvelopeShape.Percussive) seconds = patch.Decay / MathF.Max(0.05f, mul);
            else seconds = mul > 1.001f ? patch.Decay / (mul - 1f) : 0f;   // sustained: only the upper partials fade
            _dec[i] = seconds <= 0f ? 1f : MathF.Exp(-6.9078f / (seconds * sampleRate));   // -60 dB over `seconds`
        }

        // Envelope.
        float attack = MathF.Max(MinAttackSeconds, patch.Attack);
        _atkInc = 1f / (attack * sampleRate);
        _env = 0f;
        _stage = Stage.Attack;
        _sustain = patch.Shape == EnvelopeShape.Sustained ? Math.Clamp(patch.Sustain, 0f, 1f) : 1f;
        _decCoeff = patch.Shape == EnvelopeShape.Sustained ? MathF.Exp(-1f / (MathF.Max(0.02f, patch.Decay / 4.6f) * sampleRate)) : 1f;
        _relCoeff = ReleaseCoeff(MathF.Max(0.01f, patch.Release));
        _holdLeft = np.HoldSamples > 0 ? np.HoldSamples : -1;

        // Pitch movement.
        _pitchMul = 1f;
        _dropSemis = patch.PitchDropSemis;
        _dropSamples = MathF.Max(1f, patch.PitchDropSeconds * sampleRate);
        _glideSemis = np.GlideSemis;
        _glideSamples = MathF.Max(1f, np.GlideSeconds * sampleRate);
        _vibPhase = (order * 0.37f) % 1f;
        _vibInc = patch.VibratoHz / sampleRate / 1f;
        _vibDepth = patch.VibratoCents;
        _vibDelaySamples = patch.VibratoDelay * sampleRate;

        // Noise.
        _noiseAmt = patch.NoiseAmount * amp * NoiseMakeUp;
        _noiseEnv = 1f;
        _noiseDec = patch.NoiseDecay > 0f ? MathF.Exp(-6.9078f / (patch.NoiseDecay * sampleRate)) : 1f;
        _nHzFrom = np.NoiseHzFrom > 0f ? np.NoiseHzFrom : patch.NoiseHz;
        _nHzTo = np.NoiseHzTo > 0f ? np.NoiseHzTo : _nHzFrom;
        _nSweepSamples = MathF.Max(1f, np.NoiseSweepSeconds * sampleRate);
        _noiseState = (uint)(order * 2654435761L) | 1u;
        _ic1 = _ic2 = 0f;
        _k = 1f / MathF.Max(0.3f, patch.NoiseQ);
        SetNoiseFilter(_nHzFrom);
        float pan2 = Math.Clamp(basePan, -1f, 1f);
        float a2 = (pan2 + 1f) * 0.25f * MathF.PI;
        _noiseL = MathF.Cos(a2) * 1.4142135f;
        _noiseR = MathF.Sin(a2) * 1.4142135f;

        _send = patch.ReverbSend;
        Active = true;
        UpdateModulation();
    }

    /// <summary>Starts a fast fade-out (used when a note is stolen or a bus is cleared). The voice frees itself when silent.</summary>
    public void Kill(float seconds)
    {
        if (!Active) return;
        _relCoeff = ReleaseCoeff(MathF.Max(0.003f, seconds));
        _stage = Stage.Release;
    }

    private float ReleaseCoeff(float seconds) => MathF.Exp(-6.9078f / (seconds * _rate));

    private void NoteOff()
    {
        if (_stage == Stage.Release) return;
        if (_patch.Shape == EnvelopeShape.Sustained || _patch.Damp)
        {
            _relCoeff = ReleaseCoeff(MathF.Max(0.01f, _patch.Release));
            _stage = Stage.Release;
        }
    }

    private void SetNoiseFilter(float hz)
    {
        float fc = Math.Clamp(hz, 40f, _rate * 0.45f);
        float g = MathF.Tan(MathF.PI * fc / _rate);
        _a1 = 1f / (1f + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    private void UpdateModulation()
    {
        // Pitch: a falling "drop" built into the patch, plus an optional glide asked for by the caller.
        float semis = 0f;
        if (_age < _dropSamples && _dropSemis != 0f) semis += _dropSemis * (1f - _age / _dropSamples);
        if (_age < _glideSamples && _glideSemis != 0f) semis += _glideSemis * (1f - _age / _glideSamples);
        float mul = semis == 0f ? 1f : MathF.Pow(2f, semis / 12f);

        if (_vibDepth > 0f && _vibInc > 0f)
        {
            float depth = _vibDepth * MathF.Min(1f, _age / MathF.Max(1f, _vibDelaySamples));
            mul *= 1f + depth * 0.000578f * Dsp.Sin(_vibPhase);
            _vibPhase += _vibInc * Block;
            if (_vibPhase >= 1f) _vibPhase -= 1f;
        }
        _pitchMul = mul;

        if (_noiseAmt > 0f && _nHzTo != _nHzFrom)
        {
            float t = MathF.Min(1f, _age / _nSweepSamples);
            SetNoiseFilter(_nHzFrom * MathF.Pow(_nHzTo / _nHzFrom, t));
        }
    }

    /// <summary>Adds this voice's next <paramref name="frames"/> samples to the bus buffers.</summary>
    public void Render(int frames, float[] busL, float[] busR, float[] send)
    {
        int done = 0;
        while (done < frames && Active)
        {
            int n = Math.Min(Block, frames - done);
            for (int s = 0; s < n; s++)
            {
                // Envelope
                switch (_stage)
                {
                    case Stage.Attack:
                        _env += _atkInc;
                        if (_env >= 1f)
                        {
                            _env = 1f;
                            _stage = _patch.Shape == EnvelopeShape.Sustained ? Stage.Decay : Stage.Hold;
                        }
                        break;
                    case Stage.Decay:
                        _env = _sustain + (_env - _sustain) * _decCoeff;
                        if (_env - _sustain < 0.002f) { _env = _sustain; _stage = Stage.Hold; }
                        break;
                    case Stage.Release:
                        _env *= _relCoeff;
                        break;
                }

                float l = 0f, r = 0f;
                for (int i = 0; i < _n; i++)
                {
                    float ph = _phase[i];
                    float v = Dsp.Sin(ph) * _lvl[i];
                    l += v * _gl[i];
                    r += v * _gr[i];
                    ph += _inc[i] * _pitchMul;
                    if (ph >= 1f) ph -= 1f;
                    _phase[i] = ph;
                    _lvl[i] *= _dec[i];
                }

                if (_noiseAmt > 0f)
                {
                    _noiseState ^= _noiseState << 13;
                    _noiseState ^= _noiseState >> 17;
                    _noiseState ^= _noiseState << 5;
                    float white = (_noiseState >> 8) * (2f / 16777216f) - 1f;

                    float v3 = white - _ic2;
                    float v1 = _a1 * _ic1 + _a2 * v3;
                    float v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
                    _ic1 = 2f * v1 - _ic1;
                    _ic2 = 2f * v2 - _ic2;

                    float nz = v1 * _k * _noiseAmt * _noiseEnv;
                    l += nz * _noiseL;
                    r += nz * _noiseR;
                    _noiseEnv *= _noiseDec;
                }

                l *= _env;
                r *= _env;
                int at = done + s;
                busL[at] += l;
                busR[at] += r;
                send[at] += (l + r) * 0.5f * _send;
            }

            done += n;
            _age += n;
            if (_holdLeft >= 0)
            {
                _holdLeft -= n;
                if (_holdLeft < 0) { _holdLeft = -1; NoteOff(); }
            }
            UpdateModulation();
            Flush();

            // Safety: a held note whose owner never ended it does not play for ever.
            if (_holdLeft < 0 && _patch.Shape == EnvelopeShape.Sustained && _stage != Stage.Release && _age > 60f * _rate)
                NoteOff();

            if (_stage == Stage.Release)
            {
                if (_env < 0.0004f) Active = false;
            }
            else if (_patch.Shape == EnvelopeShape.Percussive && _stage == Stage.Hold)
            {
                // A ringing instrument is finished when everything in it has died away (about -80 dB).
                float left = _noiseAmt * _noiseEnv;
                for (int i = 0; i < _n; i++) left += _lvl[i] * _w[i];
                if (left < 0.0001f) Active = false;
            }
        }
    }

    private void Flush()
    {
        for (int i = 0; i < _n; i++)
            if (_lvl[i] < 1e-12f) _lvl[i] = 0f;
        if (_noiseEnv < 1e-12f) _noiseEnv = 0f;
        _ic1 = Dsp.Flush(_ic1);
        _ic2 = Dsp.Flush(_ic2);
    }
}
