namespace PopBlocks.Engine.Audio;

/// <summary>
/// Makes the game's sound effects in code, so there are no audio files to ship. Each effect is one voice:
/// a triangle or sine wave whose pitch glides from one note to another under a quick fade. The output is
/// 16-bit mono PCM, or a complete WAV file for players that load files.
///
/// Every sound starts and ends at silence, so none of them clicks.
/// </summary>
public static class ToneSynth
{
    public const int SampleRate = 22050;

    /// <summary>Loudest sample allowed, as a share of full scale. Leaves headroom so several sounds can overlap.</summary>
    private const double Headroom = 0.8;

    private enum Wave { Sine, Triangle }

    /// <summary>The effect as 16-bit mono samples at <see cref="SampleRate"/>.</summary>
    public static short[] Render(SoundEffect effect)
    {
        var samples = effect switch
        {
            // Short high tick, rising a little.
            SoundEffect.Rotate => Glide(45, 700, 1100, Wave.Triangle, attackMs: 2, fadePower: 1.5, gain: 0.55),

            // Low thud: a quick drop in pitch.
            SoundEffect.Place => Glide(110, 190, 70, Wave.Triangle, attackMs: 2, fadePower: 2.5, gain: 0.85),

            // Whoosh: a long fall from high to low that swells in, then the thud.
            SoundEffect.HardDrop => Join(
                Glide(120, 1300, 220, Wave.Triangle, attackMs: 25, fadePower: 0.6, gain: 0.5),
                Glide(110, 190, 70, Wave.Triangle, attackMs: 2, fadePower: 2.5, gain: 0.85)),

            // Pop: a quick falling bloop.
            SoundEffect.Pop => Glide(110, 1200, 480, Wave.Sine, attackMs: 2, fadePower: 2.0, gain: 0.7),

            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };

        var pcm = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            pcm[i] = (short)Math.Round(Math.Clamp(samples[i], -1.0, 1.0) * Headroom * short.MaxValue);
        return pcm;
    }

    /// <summary>The effect as a complete WAV file (16-bit, mono, <see cref="SampleRate"/> Hz).</summary>
    public static byte[] Wav(SoundEffect effect) => ToWav(Render(effect));

    /// <summary>Wraps 16-bit mono samples in a WAV header.</summary>
    public static byte[] ToWav(short[] samples)
    {
        int dataBytes = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);                  // size of the fmt chunk
        w.Write((short)1);            // PCM
        w.Write((short)1);            // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);      // bytes per second
        w.Write((short)2);            // bytes per sample frame
        w.Write((short)16);           // bits per sample
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// One note gliding from <paramref name="fromHz"/> to <paramref name="toHz"/> (exponentially, so it sounds
    /// even). It fades in over <paramref name="attackMs"/> and out over the whole length; a bigger
    /// <paramref name="fadePower"/> makes the fade-out faster at the start.
    /// </summary>
    private static double[] Glide(int ms, double fromHz, double toHz, Wave wave, double attackMs, double fadePower, double gain)
    {
        int n = ms * SampleRate / 1000;
        int attack = Math.Max(1, (int)(attackMs * SampleRate / 1000));
        var result = new double[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)n;
            double hz = fromHz * Math.Pow(toHz / fromHz, t);
            phase += hz / SampleRate;
            phase -= Math.Floor(phase);

            double osc = wave == Wave.Sine
                ? Math.Sin(2 * Math.PI * phase)
                : 4 * Math.Abs(phase - 0.5) - 1;   // triangle, -1..1

            double fadeIn = Math.Min(1.0, i / (double)attack);
            double fadeOut = Math.Pow(1.0 - t, fadePower);
            result[i] = osc * fadeIn * fadeOut * gain;
        }
        return result;
    }

    private static double[] Join(params double[][] parts)
    {
        var all = new double[parts.Sum(p => p.Length)];
        int at = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, all, at, p.Length);
            at += p.Length;
        }
        return all;
    }
}
