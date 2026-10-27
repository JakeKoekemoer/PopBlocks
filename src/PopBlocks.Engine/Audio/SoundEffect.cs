namespace PopBlocks.Engine.Audio;

/// <summary>The few short sounds the game makes. All are single-voice (mono-tone) beeps and sweeps.</summary>
public enum SoundEffect
{
    /// <summary>The piece was turned: a short, high tick.</summary>
    Rotate,

    /// <summary>The piece came to rest on its own: a low thud.</summary>
    Place,

    /// <summary>The piece was dropped hard: a falling whoosh that ends in the thud.</summary>
    HardDrop,

    /// <summary>Blocks popped: a quick falling "bloop". Played faster (higher) for each step of a chain.</summary>
    Pop,
}

/// <summary>Plays <see cref="SoundEffect"/>s. The app supplies the real player; tests and other platforms use <see cref="NullSoundPlayer"/>.</summary>
public interface ISoundPlayer
{
    /// <summary>Plays the effect now. <paramref name="pitch"/> is a playback-rate multiplier (1 = as made, 2 = an octave up).</summary>
    void Play(SoundEffect effect, float pitch = 1f);
}

/// <summary>Plays nothing.</summary>
public sealed class NullSoundPlayer : ISoundPlayer
{
    public void Play(SoundEffect effect, float pitch = 1f) { }
}
