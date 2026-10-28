namespace PopBlocks.Engine.Audio;

/// <summary>One note inside a cue (a short sequence of sounds). Times are in milliseconds from the start of the cue.</summary>
public sealed class CueNote
{
    public float DelayMs { get; init; }
    public InstrumentPatch Patch { get; init; } = Patches.PureTone;

    /// <summary>Pitch as a (possibly fractional) MIDI note. Used when <see cref="Hz"/> is 0.</summary>
    public float Midi { get; init; } = 72f;

    /// <summary>Pitch in Hz; overrides <see cref="Midi"/>. Used by noise sweeps.</summary>
    public float Hz { get; init; }

    public float Velocity { get; init; } = 0.7f;
    public float DurationMs { get; init; } = 200f;
    public float Pan { get; init; }
    public float Gain { get; init; } = 1f;
    public float GlideSemis { get; init; }
    public float GlideMs { get; init; }
    public float NoiseFromHz { get; init; }
    public float NoiseToHz { get; init; }
    public float NoiseSweepMs { get; init; }

    /// <summary>Whether this note answers to a transposition of the whole cue (a pitched note does; a noise sweep does not).</summary>
    public bool Transposes { get; init; } = true;
}

/// <summary>
/// A short, scripted sound: one note or many. Effects, story cues and the finale are all cues. A cue played on the
/// <see cref="Bus.Music"/> bus follows the music volume and mute; on <see cref="Bus.Sfx"/> it follows the effects volume.
/// </summary>
public sealed class SfxCue
{
    public required string Name { get; init; }
    public Bus Bus { get; init; } = Bus.Sfx;
    public required CueNote[] Notes { get; init; }

    /// <summary>How far to dip the music while the cue plays, in dB (0 = not at all).</summary>
    public float DuckDb { get; init; }

    /// <summary>How long the dip lasts, in milliseconds.</summary>
    public float DuckMs { get; init; }

    public float TotalMs
    {
        get
        {
            float end = 0f;
            foreach (var n in Notes) end = MathF.Max(end, n.DelayMs + n.DurationMs);
            return end;
        }
    }
}
