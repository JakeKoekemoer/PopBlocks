namespace PopBlocks.Engine.Audio;

/// <summary>
/// A small stereo room (four combs and two all-pass filters per side). It adds space without a cloud of high notes:
/// <c>Room</c> sets how long it rings and <c>Damping</c> how quickly the highs fade inside it.
/// All its memory is allocated once; <see cref="Process"/> never allocates.
/// </summary>
public sealed class Reverb
{
    private static readonly int[] CombLeft = { 1116, 1188, 1277, 1356 };
    private static readonly int[] CombRight = { 1139, 1211, 1300, 1379 };
    private static readonly int[] AllLeft = { 556, 441 };
    private static readonly int[] AllRight = { 579, 464 };

    private sealed class Comb
    {
        public readonly float[] Buffer;
        public int Index;
        public float Store;
        public Comb(int size) => Buffer = new float[size];
    }

    private sealed class AllPass
    {
        public readonly float[] Buffer;
        public int Index;
        public AllPass(int size) => Buffer = new float[size];
    }

    private readonly Comb[] _cl, _cr;
    private readonly AllPass[] _al, _ar;
    private float _feedback = 0.84f;
    private float _damp = 0.35f;
    private float _wet = 0.2f;

    public Reverb(float sampleRate)
    {
        float scale = sampleRate / 44100f;
        _cl = new Comb[CombLeft.Length];
        _cr = new Comb[CombRight.Length];
        for (int i = 0; i < _cl.Length; i++)
        {
            _cl[i] = new Comb(Math.Max(8, (int)(CombLeft[i] * scale)));
            _cr[i] = new Comb(Math.Max(8, (int)(CombRight[i] * scale)));
        }
        _al = new AllPass[AllLeft.Length];
        _ar = new AllPass[AllRight.Length];
        for (int i = 0; i < _al.Length; i++)
        {
            _al[i] = new AllPass(Math.Max(8, (int)(AllLeft[i] * scale)));
            _ar[i] = new AllPass(Math.Max(8, (int)(AllRight[i] * scale)));
        }
    }

    /// <summary>Room 0..1 (about 0.7 s to 4 s of ringing), damping 0..1 (higher = duller), wet 0..1 (level of the reverb).</summary>
    public void Configure(float room, float damping, float wet)
    {
        _feedback = 0.76f + 0.19f * Math.Clamp(room, 0f, 1f);
        _damp = 0.1f + 0.7f * Math.Clamp(damping, 0f, 1f);
        _wet = Math.Clamp(wet, 0f, 1f);
    }

    /// <summary>Adds the reverb of <paramref name="send"/> (mono) to the left and right buffers.</summary>
    public void Process(float[] send, float[] left, float[] right, int frames)
    {
        float fb = _feedback, damp = _damp, keep = 1f - _damp, wet = _wet * 1.6f;
        for (int i = 0; i < frames; i++)
        {
            float input = send[i] * 0.03f;
            float sl = 0f, sr = 0f;

            for (int c = 0; c < _cl.Length; c++)
            {
                var cl = _cl[c];
                float outL = cl.Buffer[cl.Index];
                cl.Store = Dsp.Flush(outL * keep + cl.Store * damp);
                cl.Buffer[cl.Index] = input + cl.Store * fb;
                if (++cl.Index >= cl.Buffer.Length) cl.Index = 0;
                sl += outL;

                var cr = _cr[c];
                float outR = cr.Buffer[cr.Index];
                cr.Store = Dsp.Flush(outR * keep + cr.Store * damp);
                cr.Buffer[cr.Index] = input + cr.Store * fb;
                if (++cr.Index >= cr.Buffer.Length) cr.Index = 0;
                sr += outR;
            }

            for (int a = 0; a < _al.Length; a++)
            {
                var al = _al[a];
                float bl = al.Buffer[al.Index];
                al.Buffer[al.Index] = sl + bl * 0.5f;
                sl = bl - sl;
                if (++al.Index >= al.Buffer.Length) al.Index = 0;

                var ar = _ar[a];
                float br = ar.Buffer[ar.Index];
                ar.Buffer[ar.Index] = sr + br * 0.5f;
                sr = br - sr;
                if (++ar.Index >= ar.Buffer.Length) ar.Index = 0;
            }

            left[i] += sl * wet;
            right[i] += sr * wet;
        }
    }
}
