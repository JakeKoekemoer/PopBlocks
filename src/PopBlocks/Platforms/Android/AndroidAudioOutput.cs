using Android.Content;
using Android.Media;
using Android.OS;
using PopBlocks.Engine.Audio;

namespace PopBlocks;

/// <summary>
/// Carries the sound engine's samples to the speaker through an <see cref="AudioTrack"/>: 16 bits would do, but the engine
/// already mixes in floating point, so the track takes floats and Android converts. One background thread asks the engine for
/// the next 512 frames (about 12 ms) and writes them; the write waits until the track has room, which paces the thread.
///
/// It follows Android's audio rules: it asks for audio focus (so a phone call or an alarm stops the game's sound and the sound
/// comes back after), it stops when the headphones are unplugged, and it gives the device back when the app is paused. It needs
/// no permission, makes no network request and records nothing. Every call is wrapped so that a failure here only means silence.
///
/// NOTE: written without a device to test on. Check it on a real phone: start and stop, a call, a Bluetooth disconnect.
/// </summary>
public sealed class AndroidAudioOutput : IAudioOutput
{
    private const int SampleRate = 44100;
    private const int BlockFrames = 512;

    private readonly object _gate = new();
    private readonly ManualResetEventSlim _go = new(false);

    private AudioEngine? _engine;
    private AudioTrack? _track;
    private Thread? _thread;
    private AudioManager? _manager;
    private FocusListener? _focus;
    private AudioFocusRequestClass? _focusRequest;
    private NoisyReceiver? _noisy;
    private volatile bool _running;     // the output thread should keep going
    private volatile bool _paused;      // ...but is waiting (app in the background)
    private bool _disposed;

    public bool IsRunning => _running && !_paused && _track is not null;

    public event Action<AudioInterruption>? Interruption;

    // ---------------------------------------------------------------------------------------------------- start / stop

    public bool Start(AudioEngine engine)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed) return false;
                if (_track is not null) return true;

                _engine = engine;
                _track = CreateTrack();
                if (_track is null) return false;

                RequestFocus();
                RegisterNoisyReceiver();
                _track.Play();

                _paused = false;
                _running = true;
                _go.Set();
                _thread = new Thread(Run) { IsBackground = true, Name = "PopBlocks.AudioOut" };
                _thread.Start();
                return true;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio output start", ex);
            Teardown();
            return false;
        }
    }

    public void Pause()
    {
        try
        {
            lock (_gate)
            {
                if (_track is null || _paused) return;
                _paused = true;
                _go.Reset();
                _track.Pause();
                _track.Flush();
                AbandonFocus();
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio output pause", ex);
        }
    }

    public void Resume()
    {
        try
        {
            lock (_gate)
            {
                if (_track is null || _disposed || !_paused) return;
                RequestFocus();
                _track.Play();
                _paused = false;
                _go.Set();
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio output resume", ex);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Teardown();
    }

    private void Teardown()
    {
        try
        {
            _running = false;
            _paused = false;
            _go.Set();
            try { _thread?.Join(400); } catch { /* the thread is a background thread: it cannot keep the app alive */ }

            lock (_gate)
            {
                try { _track?.Stop(); } catch { }
                try { _track?.Release(); } catch { }
                _track = null;
                AbandonFocus();
                UnregisterNoisyReceiver();
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio output teardown", ex);
        }
    }

    // ---------------------------------------------------------------------------------------------------- the output thread

    private void Run()
    {
        try
        {
            Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.Audio);
        }
        catch
        {
            // A normal priority still works; it only makes underruns a little more likely.
        }

        var buffer = new float[BlockFrames * 2];
        int failures = 0;

        while (_running)
        {
            try
            {
                if (_paused) { _go.Wait(100); continue; }

                var engine = _engine;
                var track = _track;
                if (engine is null || track is null) break;

                engine.Render(buffer, 0, BlockFrames);
                int written = track.Write(buffer, 0, buffer.Length, WriteMode.Blocking);

                if (written < 0)
                {
                    // The audio service was restarted or the device went away. Try a few times to build a new track.
                    if (++failures > 5 || !Rebuild()) { _running = false; break; }
                }
                else
                {
                    failures = 0;
                }
            }
            catch (Exception ex)
            {
                Diagnostics.Error("Audio output thread", ex);
                if (++failures > 5) { _running = false; break; }
                Thread.Sleep(50);
            }
        }
    }

    private bool Rebuild()
    {
        lock (_gate)
        {
            try { _track?.Release(); } catch { }
            _track = CreateTrack();
            if (_track is null) return false;
            _track.Play();
            return true;
        }
    }

    private static AudioAttributes BuildAttributes() =>
        new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;

    private static AudioTrack? CreateTrack()
    {
        int minBytes = AudioTrack.GetMinBufferSize(SampleRate, ChannelOut.Stereo, Encoding.PcmFloat);
        if (minBytes <= 0) return null;

        // Room for about 40 ms: small enough to feel immediate, large enough to ride out a busy moment on a slow phone.
        int bytes = Math.Max(minBytes * 2, BlockFrames * 2 * sizeof(float) * 4);

        var format = new AudioFormat.Builder()!
            .SetEncoding(Encoding.PcmFloat)!
            .SetSampleRate(SampleRate)!
            .SetChannelMask(ChannelOut.Stereo)!
            .Build()!;

        var builder = new AudioTrack.Builder()!
            .SetAudioAttributes(BuildAttributes())!
            .SetAudioFormat(format)!
            .SetBufferSizeInBytes(bytes)!
            .SetTransferMode(AudioTrackMode.Stream)!;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder.SetPerformanceMode(AudioTrackPerformanceMode.LowLatency);

        var track = builder.Build();
        if (track is null || track.State != AudioTrackState.Initialized)
        {
            track?.Release();
            return null;
        }
        return track;
    }

    // ---------------------------------------------------------------------------------------------------- audio focus

    private void RequestFocus()
    {
        try
        {
            _manager ??= Android.App.Application.Context.GetSystemService(Context.AudioService) as AudioManager;
            if (_manager is null) return;
            _focus ??= new FocusListener(this);

            AudioFocusRequest result;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                    .SetAudioAttributes(BuildAttributes())!
                    .SetOnAudioFocusChangeListener(_focus)!
                    .Build()!;
                result = _manager.RequestAudioFocus(_focusRequest);
            }
            else
            {
#pragma warning disable CA1422 // the old overload is the only one before Android 8
                result = _manager.RequestAudioFocus(_focus, Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422
            }

            // Refused (most likely a call is in progress): stay silent until the phone gives the audio back.
            if (result != AudioFocusRequest.Granted) Interruption?.Invoke(AudioInterruption.FocusLost);
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio focus request", ex);
        }
    }

    private void AbandonFocus()
    {
        try
        {
            if (_manager is null) return;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_focusRequest is not null) _manager.AbandonAudioFocusRequest(_focusRequest);
            }
            else if (_focus is not null)
            {
#pragma warning disable CA1422
                _manager.AbandonAudioFocus(_focus);
#pragma warning restore CA1422
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio focus abandon", ex);
        }
    }

    private void OnFocusChange(AudioFocus change)
    {
        switch (change)
        {
            case AudioFocus.Gain:
                Interruption?.Invoke(AudioInterruption.FocusGained);
                break;
            case AudioFocus.LossTransientCanDuck:
                Interruption?.Invoke(AudioInterruption.FocusDucked);
                break;
            case AudioFocus.Loss:
            case AudioFocus.LossTransient:
                Interruption?.Invoke(AudioInterruption.FocusLost);
                break;
        }
    }

    private sealed class FocusListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        private readonly AndroidAudioOutput _owner;
        public FocusListener(AndroidAudioOutput owner) => _owner = owner;
        public void OnAudioFocusChange(AudioFocus focusChange) => _owner.OnFocusChange(focusChange);
    }

    // ---------------------------------------------------------------------------------------------------- headphones unplugged

    private void RegisterNoisyReceiver()
    {
        try
        {
            if (_noisy is not null) return;
            _noisy = new NoisyReceiver(this);
            var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
            var context = Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                context.RegisterReceiver(_noisy, filter, ReceiverFlags.NotExported);
            else
                context.RegisterReceiver(_noisy, filter);
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Audio noisy receiver", ex);
            _noisy = null;
        }
    }

    private void UnregisterNoisyReceiver()
    {
        try
        {
            if (_noisy is not null) Android.App.Application.Context.UnregisterReceiver(_noisy);
        }
        catch
        {
            // Already unregistered: nothing to do.
        }
        _noisy = null;
    }

    private sealed class NoisyReceiver : BroadcastReceiver
    {
        private readonly AndroidAudioOutput _owner;
        public NoisyReceiver(AndroidAudioOutput owner) => _owner = owner;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
                _owner.Interruption?.Invoke(AudioInterruption.RouteBecameNoisy);
        }
    }
}
