namespace PopBlocks.Engine.Audio;

/// <summary>Things that happen to the phone's audio which the game should answer.</summary>
public enum AudioInterruption
{
    /// <summary>Another app (a call, an alarm, a video) took the audio. Stop making sound until it ends.</summary>
    FocusLost,

    /// <summary>Another app asked us to be quieter for a moment (a navigation prompt, say).</summary>
    FocusDucked,

    /// <summary>The audio is ours again.</summary>
    FocusGained,

    /// <summary>The headphones came out or the Bluetooth speaker went away: don't suddenly play out loud.</summary>
    RouteBecameNoisy,
}

/// <summary>
/// Whatever carries the engine's samples to the speaker. On Android it is an <c>AudioTrack</c> on its own thread; in tests
/// and on other platforms it is <see cref="NullAudioOutput"/>. Everything here must be safe to call from any thread and must
/// never throw: sound is never essential to playing the game.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Starts pulling audio from the engine. False when the device cannot play it (the game carries on silently).</summary>
    bool Start(AudioEngine engine);

    /// <summary>Stops pulling audio and gives the audio device back (the app went to the background).</summary>
    void Pause();

    /// <summary>Starts again after <see cref="Pause"/>.</summary>
    void Resume();

    bool IsRunning { get; }

    event Action<AudioInterruption>? Interruption;
}

/// <summary>Plays nothing. Used by tests and by platforms with no audio backend.</summary>
public sealed class NullAudioOutput : IAudioOutput
{
    public bool IsRunning { get; private set; }

    /// <summary>What <see cref="Start"/> should answer, so a test can pretend the device failed.</summary>
    public bool CanStart { get; set; } = true;

    public event Action<AudioInterruption>? Interruption;

    public bool Start(AudioEngine engine) => IsRunning = CanStart;
    public void Pause() => IsRunning = false;
    public void Resume() => IsRunning = CanStart;
    public void Dispose() => IsRunning = false;

    /// <summary>For tests: act as if the phone interrupted us.</summary>
    public void Raise(AudioInterruption what) => Interruption?.Invoke(what);
}
