using Android.Media;
using Microsoft.Maui.Storage;
using PopBlocks.Engine.Audio;

// Android.Media also has a SoundEffect type; this alias says which one is meant.
using SoundEffect = PopBlocks.Engine.Audio.SoundEffect;

namespace PopBlocks;

/// <summary>
/// Plays the synthesised effects through Android's <see cref="SoundPool"/>, which is made for short game sounds:
/// low delay, several sounds at once, and a pitch (rate) control. The sounds are written once to the app's cache
/// folder as small WAV files and loaded at start-up. Nothing is downloaded and no permission is needed.
///
/// Sound is never essential: if anything here fails the game carries on silently.
/// </summary>
public sealed class AndroidSoundPlayer : ISoundPlayer
{
    private readonly SoundPool? _pool;
    private readonly Dictionary<SoundEffect, int> _ids = new();
    private readonly HashSet<int> _loaded = new();
    private readonly object _gate = new();

    public AndroidSoundPlayer()
    {
        try
        {
            var attributes = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!;

            _pool = new SoundPool.Builder()!
                .SetMaxStreams(6)!
                .SetAudioAttributes(attributes)!
                .Build()!;

            _pool.LoadComplete += (_, e) =>
            {
                if (e.Status == 0)
                    lock (_gate) _loaded.Add(e.SampleId);
            };

            string folder = Path.Combine(FileSystem.CacheDirectory, "sfx");
            Directory.CreateDirectory(folder);
            foreach (var effect in Enum.GetValues<SoundEffect>())
            {
                string path = Path.Combine(folder, effect.ToString().ToLowerInvariant() + ".wav");
                File.WriteAllBytes(path, ToneSynth.Wav(effect));
                _ids[effect] = _pool.Load(path, 1);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Sound start-up", ex);
            _pool?.Release();
            _pool = null;
        }
    }

    public void Play(SoundEffect effect, float pitch = 1f)
    {
        if (_pool is null || !_ids.TryGetValue(effect, out int id)) return;
        lock (_gate)
            if (!_loaded.Contains(id)) return;   // still loading (only in the first moments after launch)

        try
        {
            _pool.Play(id, 0.8f, 0.8f, 1, 0, Math.Clamp(pitch, 0.5f, 2f));
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Sound play", ex);
        }
    }
}
