namespace PopBlocks.Engine.Audio;

/// <summary>Small, allocation-free signal helpers shared by the synth, the mixer and the composer.</summary>
public static class Dsp
{
    private const int SineSize = 2048;
    private static readonly float[] SineTable = BuildSine();

    private static float[] BuildSine()
    {
        var table = new float[SineSize + 1];
        for (int i = 0; i <= SineSize; i++) table[i] = (float)Math.Sin(2.0 * Math.PI * i / SineSize);
        return table;
    }

    /// <summary>Sine of a phase given in cycles. <paramref name="phase"/> must be in [0, 1).</summary>
    public static float Sin(float phase)
    {
        float x = phase * SineSize;
        int i = (int)x;
        float f = x - i;
        float a = SineTable[i];
        return a + (SineTable[i + 1] - a) * f;
    }

    /// <summary>Frequency of a (possibly fractional) MIDI note number. 69 = A4 = 440 Hz.</summary>
    public static float MidiToHz(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);

    public static float SemitonesToRatio(float semitones) => MathF.Pow(2f, semitones / 12f);

    public static float DbToGain(float db) => MathF.Pow(10f, db / 20f);

    /// <summary>A 0..1 slider as a gain. Squared, so the middle of the slider is about -12 dB rather than -6.</summary>
    public static float VolumeToGain(float volume)
    {
        float v = Math.Clamp(volume, 0f, 1f);
        return v * v;
    }

    /// <summary>
    /// A gentle safety net for the very last stage: untouched below 0.8, then rolls off smoothly towards 1.0.
    /// It is a guard against a rare peak, not part of the sound.
    /// </summary>
    public static float SoftClip(float x)
    {
        float a = MathF.Abs(x);
        if (a <= 0.8f) return x;
        float over = (a - 0.8f) / 0.2f;
        // tanh-like knee: 0.8 + 0.2 * (1 - 1/(1+over+over^2/2)) stays below 1.0 for every input
        float shaped = 0.8f + 0.2f * (over * (2f + over)) / (2f + 2f * over + over * over);
        return x < 0 ? -shaped : shaped;
    }

    /// <summary>One-pole smoothing coefficient: after <paramref name="seconds"/> the value has covered about 63% of the way.</summary>
    public static float SmoothCoeff(float seconds, float rate)
    {
        if (seconds <= 0f) return 1f;
        return 1f - MathF.Exp(-1f / (seconds * rate));
    }

    /// <summary>Flushes tiny values to zero so feedback loops never fall into slow denormal arithmetic.</summary>
    public static float Flush(float x) => MathF.Abs(x) < 1e-18f ? 0f : x;
}

/// <summary>
/// A small deterministic random source (xorshift32 seeded through SplitMix). The same seed always gives the same
/// sequence on every platform, which System.Random does not promise across runtime versions.
/// </summary>
public sealed class Rng
{
    private uint _state;

    public Rng(int seed)
    {
        uint z = (uint)seed + 0x9E3779B9u;
        z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
        z = (z ^ (z >> 13)) * 0xC2B2AE35u;
        z ^= z >> 16;
        _state = z == 0 ? 0x1234ABCDu : z;
    }

    public uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>0 (inclusive) to 1 (exclusive).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>0 (inclusive) to <paramref name="maxExclusive"/> (exclusive).</summary>
    public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

    /// <summary>minInclusive to maxInclusive.</summary>
    public int Range(int minInclusive, int maxInclusive) => minInclusive + Next(maxInclusive - minInclusive + 1);

    public bool Chance(float probability) => NextFloat() < probability;

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}
