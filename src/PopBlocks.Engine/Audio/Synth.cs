namespace PopBlocks.Engine.Audio;

/// <summary>
/// A fixed pool of voices. Notes start immediately and fade out on their own; when the pool is busy the quietest kind of
/// sound (ambience, then music) gives way to the more important one (effects), always through a quick fade rather than a cut.
/// </summary>
internal sealed class Synth
{
    public const int PoolSize = 48;
    public const int SoftLimit = 30;

    private readonly Voice[] _voices = new Voice[PoolSize];
    private readonly float _rate;
    private long _order;

    public Synth(float sampleRate)
    {
        _rate = sampleRate;
        for (int i = 0; i < PoolSize; i++) _voices[i] = new Voice();
    }

    public int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (var v in _voices) if (v.Active) n++;
            return n;
        }
    }

    public static int PriorityOf(Bus bus) => bus switch { Bus.Sfx => 2, Bus.Music => 1, _ => 0 };

    /// <summary>Starts a note. Returns false if it was dropped because the pool was full of more important sounds.</summary>
    public bool NoteOn(InstrumentPatch patch, Bus bus, in NoteParams np)
    {
        int priority = PriorityOf(bus);
        Voice? free = null;
        Voice? victim = null;
        int sounding = 0;

        for (int i = 0; i < PoolSize; i++)
        {
            var v = _voices[i];
            if (!v.Active) { free ??= v; continue; }
            if (v.Releasing) continue;
            sounding++;
            if (victim is null || v.Priority < victim.Priority || (v.Priority == victim.Priority && v.Order < victim.Order))
                victim = v;
        }

        if (sounding >= SoftLimit && victim is not null)
        {
            if (victim.Priority > priority) return false;
            victim.Kill(0.006f);
        }

        if (free is null) return false;
        free.Start(patch, bus, priority, ++_order, in np, _rate);
        return true;
    }

    /// <summary>Fades out every voice on a bus (used when the music changes to another piece).</summary>
    public void KillBus(Bus bus, float seconds)
    {
        foreach (var v in _voices)
            if (v.Active && v.Bus == bus) v.Kill(seconds);
    }

    public void KillAll(float seconds)
    {
        foreach (var v in _voices)
            if (v.Active) v.Kill(seconds);
    }

    public void Render(int frames, float[][] busL, float[][] busR, float[][] send)
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var v = _voices[i];
            if (!v.Active) continue;
            int b = (int)v.Bus;
            v.Render(frames, busL[b], busR[b], send[b]);
        }
    }
}
