namespace PopBlocks.Engine.Audio;

/// <summary>
/// Runs an <see cref="AudioEngine"/> without a sound card and returns what it would have played. Used by the tests, by the
/// PC preview tool (so the music can be heard without a phone) and by the Debug-only Music Lab.
/// </summary>
public static class OfflineRenderer
{
    /// <summary>One thing to do at a moment in the render: a call on the engine.</summary>
    public readonly record struct Step(double AtSeconds, Action<AudioEngine> Do);

    /// <summary>Renders <paramref name="seconds"/> of stereo audio (interleaved left/right floats).</summary>
    public static float[] Render(AudioEngine engine, double seconds, IEnumerable<Step>? script = null, int blockFrames = 512)
    {
        int total = (int)Math.Ceiling(seconds * engine.SampleRate);
        var output = new float[total * 2];
        var steps = (script ?? Array.Empty<Step>()).OrderBy(s => s.AtSeconds).ToList();
        int next = 0;

        int done = 0;
        while (done < total)
        {
            double now = done / (double)engine.SampleRate;
            while (next < steps.Count && steps[next].AtSeconds <= now + 1e-9)
                steps[next++].Do(engine);

            int n = Math.Min(blockFrames, total - done);
            engine.Render(output, done * 2, n);
            done += n;
        }
        return output;
    }

    /// <summary>Renders a single cue followed by <paramref name="tailSeconds"/> of its natural decay.</summary>
    public static float[] RenderCue(SfxCue cue, int sampleRate = 44100, float tailSeconds = 1.5f, float volume = 1f, float semitones = 0f)
    {
        var engine = new AudioEngine(sampleRate);
        double seconds = cue.TotalMs / 1000.0 + tailSeconds;
        return Render(engine, seconds, new[] { new Step(0.05, e => e.PlayCue(cue, volume, semitones)) });
    }
}

/// <summary>Writes rendered audio as 16-bit PCM WAV files.</summary>
public static class WavWriter
{
    public static void WriteStereo16(Stream stream, float[] interleaved, int sampleRate)
    {
        int dataBytes = interleaved.Length * 2;
        using var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)2);
        w.Write(sampleRate);
        w.Write(sampleRate * 4);
        w.Write((short)4);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (float f in interleaved)
            w.Write((short)Math.Round(Math.Clamp(f, -1f, 1f) * short.MaxValue));
        w.Flush();
    }

    public static byte[] ToStereo16(float[] interleaved, int sampleRate)
    {
        using var ms = new MemoryStream();
        WriteStereo16(ms, interleaved, sampleRate);
        return ms.ToArray();
    }
}
