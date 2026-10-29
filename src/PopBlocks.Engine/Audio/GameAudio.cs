using System.Collections.Concurrent;
using System.Diagnostics;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Audio;

/// <summary>The small user-interface sounds.</summary>
public enum UiSound { Tap, MenuOpen, MenuClose, Locked, SettingsChange, Coin }

/// <summary>What happened to the Orb (see <see cref="GameAudio.OrbSound"/>).</summary>
public enum OrbSoundKind { Appeared, Broke, Corrupted }

/// <summary>
/// The one object the app talks to for sound. It owns the engine, the director and the platform output, turns game events into
/// music changes and effects, applies the player's volumes and mutes, and looks after the phone's audio rules (backgrounding,
/// calls, unplugged headphones).
///
/// Every public method is safe to call from the UI thread: it only queues a small job for the audio-control thread and returns.
/// If the device cannot play audio at all, the effects fall back to <paramref name="fallback"/> (the older SoundPool player)
/// and the music is simply absent; the game never depends on sound.
/// </summary>
public sealed class GameAudio : IDisposable
{
    private const double TickSeconds = 0.1;
    private const double PopGapSeconds = 0.035;
    private const double OrbGapSeconds = 0.25;
    private const double RotateGapSeconds = 0.045;
    private const double PlaceGapSeconds = 0.06;
    private const double ComboGapSeconds = 2.5;

    private readonly AudioSettings _settings;
    private readonly IAudioOutput _output;
    private readonly ISoundPlayer? _fallback;
    private readonly Func<double> _clock;
    private readonly BlockingCollection<Action>? _jobs;
    private readonly Thread? _thread;
    private readonly List<(double Due, Action Act)> _timed = new();
    private readonly CancellationTokenSource _stop = new();

    private bool _available;
    private bool _background;            // the app is not on screen
    private bool _interrupted;           // another app has the audio
    private bool _routePaused;           // headphones came out: wait for the player to do something
    private bool _outputPaused;
    private float _focusGain = 1f;
    private double _lastPop = -99, _lastRotate = -99, _lastPlace = -99, _lastCombo = -99, _lastTick, _lastOrb = -99;
    private bool _disposed;

    public GameAudio(AudioSettings settings, IAudioOutput output, ISoundPlayer? fallback = null,
        bool useWorkerThread = true, Func<double>? clock = null, AudioEngine? engine = null)
    {
        _settings = settings;
        _output = output;
        _fallback = fallback;
        Engine = engine ?? new AudioEngine(44100);
        Director = new MusicDirector(Engine, settings);

        var sw = Stopwatch.StartNew();
        _clock = clock ?? (() => sw.Elapsed.TotalSeconds);

        _output.Interruption += OnInterruption;
        _settings.Changed += OnSettingsChanged;

        if (useWorkerThread)
        {
            _jobs = new BlockingCollection<Action>(512);
            _thread = new Thread(Loop) { IsBackground = true, Name = "PopBlocks.Audio", Priority = ThreadPriority.BelowNormal };
            _thread.Start();
        }
    }

    public AudioEngine Engine { get; }
    public MusicDirector Director { get; }

    /// <summary>True once the device's audio output is running. False means effects use the fallback player and there is no music.</summary>
    public bool IsAvailable => _available;

    /// <summary>Raised on the audio-control thread when something in the sound system failed. The game carries on.</summary>
    public event Action<string, Exception>? Failed;

    // ---------------------------------------------------------------------------------------------------- plumbing

    private void Post(Action job)
    {
        if (_disposed) return;
        if (_jobs is null) { Run(job); return; }
        try { if (!_jobs.TryAdd(job)) { /* queue full: dropping a sound is better than blocking the UI */ } }
        catch (InvalidOperationException) { }
    }

    private void Run(Action job)
    {
        try { job(); }
        catch (Exception ex) { Failed?.Invoke("audio job", ex); }
    }

    private void Loop()
    {
        var token = _stop.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_jobs!.TryTake(out var job, 100, token)) Run(job);
                Tick();
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Runs timers. The worker thread calls this about ten times a second; tests call it by hand.</summary>
    public void Tick()
    {
        double now = _clock();
        if (now - _lastTick < TickSeconds * 0.5 && _jobs is not null) return;
        _lastTick = now;
        try
        {
            Director.Update(now);
            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                if (_timed[i].Due > now) continue;
                var act = _timed[i].Act;
                _timed.RemoveAt(i);
                act();
            }
        }
        catch (Exception ex) { Failed?.Invoke("audio tick", ex); }
    }

    private void Later(double seconds, Action act) => _timed.Add((_clock() + seconds, act));

    // ---------------------------------------------------------------------------------------------------- lifecycle

    /// <summary>Starts the audio output. Safe to call more than once.</summary>
    public void Start() => Post(() =>
    {
        if (_available) return;
        _available = SafeStart();
        ApplyGains(immediate: true);
    });

    private bool SafeStart()
    {
        try { return _output.Start(Engine); }
        catch (Exception ex) { Failed?.Invoke("audio start", ex); return false; }
    }

    /// <summary>The app left the screen: fade out, then give the audio device back.</summary>
    public void OnSleep() => Post(() =>
    {
        _background = true;
        Quiet();
    });

    /// <summary>The app is back on screen.</summary>
    public void OnResume() => Post(() =>
    {
        _background = false;
        _routePaused = false;
        _interrupted = false;          // coming back is a fresh chance to take the audio; the output asks the phone again
        _focusGain = 1f;
        Wake();
    });

    private void Quiet()
    {
        Engine.SetMasterFade(0f, 0.15f);
        Later(0.25, () =>
        {
            if ((_background || _interrupted || _routePaused) && _available && !_outputPaused)
            {
                try { _output.Pause(); } catch (Exception ex) { Failed?.Invoke("audio pause", ex); }
                _outputPaused = true;
            }
        });
    }

    /// <summary>Brings sound back after any pause (backgrounded, call, unplugged headphones).</summary>
    private void Wake()
    {
        if (_background || _interrupted) return;
        _routePaused = false;
        if (_outputPaused)
        {
            try { _output.Resume(); } catch (Exception ex) { Failed?.Invoke("audio resume", ex); }
            _outputPaused = false;
        }
        Engine.SetMasterFade(_focusGain, 0.3f);
    }

    private void OnInterruption(AudioInterruption what) => Post(() =>
    {
        switch (what)
        {
            case AudioInterruption.FocusLost:
                _interrupted = true;
                Quiet();
                break;
            case AudioInterruption.FocusDucked:
                _focusGain = 0.3f;
                if (!_outputPaused) Engine.SetMasterFade(_focusGain, 0.2f);
                break;
            case AudioInterruption.FocusGained:
                _interrupted = false;
                _focusGain = 1f;
                Wake();
                break;
            case AudioInterruption.RouteBecameNoisy:
                _routePaused = true;
                Quiet();
                break;
        }
    });

    // ---------------------------------------------------------------------------------------------------- settings

    private void OnSettingsChanged() => Post(() =>
    {
        ApplyGains(immediate: false);
        Director.SettingsChanged();
        if (_scene is Scene.Classic or Scene.Region && _settings.SelectedTrack != _appliedTrack) ApplyScene();   // a new song was picked
    });

    /// <summary>Pushes the volumes and mutes to the engine. A mute is a very short ramp (immediate to the ear, no click).</summary>
    private void ApplyGains(bool immediate)
    {
        foreach (var bus in new[] { Bus.Music, Bus.Sfx, Bus.Ambience })
        {
            float gain = _settings.GainFor(bus);
            Engine.SetBusGain(bus, gain, immediate ? 0f : gain == 0f ? 0.012f : 0.05f);
        }
    }

    /// <summary>Call after changing <see cref="AudioSettings"/> in code that does not raise its event (tests, the Music Lab).</summary>
    public void ApplySettings() => OnSettingsChanged();

    // ---------------------------------------------------------------------------------------------------- music

    public void ShowMenu() => Post(() => { _scene = Scene.Menu; ApplyScene(); });
    public void ShowClassic() => Post(() => { _scene = Scene.Classic; ApplyScene(); });

    /// <summary>A Story level or scene in this region (1 to 8), in the state the region is in.</summary>
    public void ShowRegion(int regionNumber, RegionState state) =>
        Post(() => { _scene = Scene.Region; _sceneRegion = regionNumber; _sceneState = state; ApplyScene(); });

    public void StopMusic() => Post(() => { _scene = Scene.None; Director.Stop(); });

    // The music of the place the game is in (the "scene") and the song the player may have chosen instead. A chosen song plays
    // during games (Classic, Story and lessons); the menu and the map always keep the Spectrum theme.
    private enum Scene { None, Menu, Classic, Region }
    private Scene _scene;
    private int _sceneRegion;
    private RegionState _sceneState;
    private int _appliedTrack;      // the chosen song in force right now (0 = automatic)

    /// <summary>Plays what the scene calls for: the chosen song during a game, otherwise the music of the place.</summary>
    private void ApplyScene()
    {
        if (_scene == Scene.None) return;
        Wake();

        var chosen = _scene is Scene.Classic or Scene.Region ? Soundtrack.Get(_settings.SelectedTrack) : null;
        _appliedTrack = chosen?.Number ?? 0;

        if (chosen is not null)
        {
            if (chosen.Region == RegionId.Menu) Director.ShowMenu();
            else Director.ShowRegion(chosen.Region, chosen.State);
            return;
        }

        switch (_scene)
        {
            case Scene.Menu: Director.ShowMenu(); break;
            case Scene.Classic: Director.ShowClassic(); break;
            default: Director.ShowRegion(ScoreLibrary.FromNumber(_sceneRegion), _sceneState); break;
        }
    }

    /// <summary>The story panel opened (true) or closed (false).</summary>
    public void DialogueChanged(bool visible) => Post(() => Director.SetDialogue(visible));

    /// <summary>0 to 1: how close the player is to running out of moves. Quiet and optional.</summary>
    public void Tension(float level) => Post(() => Director.SetTension(level));

    // ---------------------------------------------------------------------------------------------------- effects

    private bool SfxOn => !_settings.SfxMuted;

    private RegionCues Cues => RegionCues.For(Director.Region);

    /// <summary>Whether a sound made now would actually be heard (not while the app is away or another app has the audio).</summary>
    private bool Audible => _available && !_background && !_interrupted && !_outputPaused;

    private void Play(SfxCue cue, float volume = 1f, float semitones = 0f, int quantizeTicks = 0)
    {
        if (!Audible || !SfxOn) return;
        Engine.PlayCue(cue, volume, semitones, quantizeTicks);
    }

    /// <summary>A piece was turned.</summary>
    public void Rotated() => Post(() =>
    {
        Wake();
        double now = _clock();
        if (now - _lastRotate < RotateGapSeconds) return;
        _lastRotate = now;
        if (_available) Play(SfxLibrary.Rotate, 1f, Cues.KeyOffset);
        else FallbackPlay(SoundEffect.Rotate);
    });

    /// <summary>A piece came to rest (<paramref name="hard"/> after a hard drop).</summary>
    public void Placed(bool hard) => Post(() =>
    {
        Wake();
        double now = _clock();
        if (now - _lastPlace < PlaceGapSeconds) return;
        _lastPlace = now;
        if (_available) Play(hard ? SfxLibrary.HardDrop : SfxLibrary.Place, 1f, Cues.KeyOffset);
        else FallbackPlay(hard ? SoundEffect.HardDrop : SoundEffect.Place);
    });

    /// <summary>
    /// A wave of blocks popped. Each colour group gets its own note (louder for the biggest), each step of a chain climbs one
    /// step of the region's scale, and a long chain earns one short flourish.
    /// </summary>
    public void Popped(BurstWave wave) => Post(() =>
    {
        Wake();
        int chain = Math.Max(1, wave.ChainIndex);
        if (_available) Director.OnWave(chain);

        double now = _clock();
        if (now - _lastPop < PopGapSeconds) return;
        _lastPop = now;

        if (!_available)
        {
            FallbackPlay(SoundEffect.Pop, MathF.Pow(2f, Math.Clamp(chain - 1, 0, 6) * 2f / 12f));
            return;
        }
        if (!SfxOn || !Audible) return;

        var cues = Cues;
        int order = 0;
        foreach (var group in wave.Groups.OrderByDescending(g => g.Size).Take(3))
        {
            float volume = order == 0 ? 1f : order == 1 ? 0.8f : 0.65f;
            float semitones = SfxLibrary.PopSemitones(group.Colour, cues.Scale, cues.KeyOffset, chain, order);
            Engine.PlayCue(SfxLibrary.ColourPop(group.Colour, group.Size), volume, semitones);
            order++;
        }

        // State C: a flourish for a meaningful chain, no more often than every couple of seconds.
        if (!_settings.ReducedAudio && chain >= 4 && now - _lastCombo >= ComboGapSeconds)
        {
            _lastCombo = now;
            Engine.PlayCue(chain >= 6 ? cues.ComboBig : cues.ComboSmall, 0.9f, 0f, MusicScore.TicksPerBeat / 2);
        }
    });

    /// <summary>The Orb appeared, broke, or ran out of time. Each is played at most once every few tenths of a second.</summary>
    public void OrbSound(OrbSoundKind kind) => Post(() =>
    {
        Wake();
        double now = _clock();
        if (now - _lastOrb < OrbGapSeconds) return;
        _lastOrb = now;
        var cue = kind switch { OrbSoundKind.Broke => SfxLibrary.OrbBreak, OrbSoundKind.Corrupted => SfxLibrary.OrbCorrupt, _ => SfxLibrary.OrbAppear };
        if (_available) Play(cue, kind == OrbSoundKind.Appeared ? 0.8f : 1f, Cues.KeyOffset);
        else FallbackPlay(kind == OrbSoundKind.Broke ? SoundEffect.Pop : SoundEffect.Place, kind == OrbSoundKind.Appeared ? 1.5f : 0.7f);
    });

    /// <summary>A Story level began.</summary>
    public void LevelStarted() => Post(() => Play(Cues.LevelStart, 0.8f));

    public void LevelCompleted() => Post(() => { Director.SetTension(0f); Play(Cues.LevelComplete); });
    public void LevelFailed() => Post(() => { Director.SetTension(0f); Play(Cues.LevelFailed); });

    /// <summary>A new game started: lift the pause duck without a sound (restarting from the pause menu).</summary>
    public void ClearPause() => Post(() => Director.SetPaused(false));

    public void GamePaused() => Post(() => { Director.SetPaused(true); Play(SfxLibrary.Pause, 1f, Cues.KeyOffset); });
    public void GameResumed() => Post(() => { Director.SetPaused(false); Play(SfxLibrary.Resume, 1f, Cues.KeyOffset); });

    public void Ui(UiSound sound) => Post(() =>
    {
        Wake();
        var cue = sound switch
        {
            UiSound.MenuOpen => SfxLibrary.MenuOpen,
            UiSound.MenuClose => SfxLibrary.MenuClose,
            UiSound.Locked => SfxLibrary.Locked,
            UiSound.SettingsChange => SfxLibrary.SettingsChange,
            _ => SfxLibrary.ButtonTap,
        };
        // The coin tick is the button tap an octave up and a little quieter, so a run of them reads as coins counting.
        Play(cue, sound == UiSound.Coin ? 0.7f : 1f, Cues.KeyOffset + (sound == UiSound.Coin ? 12f : 0f));
    });

    private void FallbackPlay(SoundEffect effect, float pitch = 1f)
    {
        if (_fallback is null || !SfxOn) return;
        try { _fallback.Play(effect, pitch); }
        catch (Exception ex) { Failed?.Invoke("fallback sound", ex); }
    }

    // ---------------------------------------------------------------------------------------------------- story moments

    /// <summary>
    /// Called when the story strip shows a line. <paramref name="cueJustStarted"/> is true on the first line of a picture beat,
    /// when its one-time sound should play; the quiet states (Warden hush) follow the cue for as long as it stays.
    /// </summary>
    public void StageLine(StageCue cue, bool cueJustStarted, int regionNumber, RegionState sweepTo) => Post(() =>
    {
        bool hush = (cue & (StageCue.WardenEntrance | StageCue.WardenStand | StageCue.Stillness)) != 0;
        Director.SetHush(hush);
        if (!cueJustStarted) return;

        var region = ScoreLibrary.FromNumber(regionNumber);
        var cues = RegionCues.For(region);

        if ((cue & StageCue.RestoreSweep) != 0)
        {
            Play(cues.Restoration);
            if (_scene == Scene.Region && _sceneRegion == regionNumber) _sceneState = sweepTo;
            if (_appliedTrack == 0 && Director.Region == region) Director.Restore(sweepTo);   // a song the player chose stays as it is
        }
        if ((cue & StageCue.MemoryCrystal) != 0) Play(cues.MemoryCrystal);

        // A Prism shard reveals the Spectrum motif a little more with each region; two shards at once (the caves) ring as a pair.
        if ((cue & StageCue.Shard) != 0)
            Play(cues.PrismFragments[regionNumber <= 2 ? 0 : regionNumber <= 5 ? 1 : 2]);
        if ((cue & StageCue.ShardPair) != 0)
        {
            Play(cues.ShardDiscovery);
            Later(0.28, () => Play(cues.ShardDiscovery, 0.8f, 3f));
        }
        if ((cue & StageCue.WardenEntrance) != 0) Play(SfxLibrary.WardenReveal);
    });

    /// <summary>A region was unlocked on the world map: its tune in miniature.</summary>
    public void RegionUnlocked(int regionNumber) => Post(() => Play(RegionCues.For(ScoreLibrary.FromNumber(regionNumber)).Unlocked));

    /// <summary>A Prism fragment was added to the collection (<paramref name="totalFragments"/> counts it).</summary>
    public void PrismFragmentFound(int totalFragments) => Post(() =>
    {
        var set = Cues.PrismFragments;
        int index = totalFragments <= 2 ? 0 : totalFragments <= 5 ? 1 : 2;
        Play(set[index]);
    });

    /// <summary>The Prism Heart is restored: the scripted finale.</summary>
    public void Finale() => Post(() => { Wake(); if (_available) Director.PlayFinale(); });

    // ---------------------------------------------------------------------------------------------------- shutdown

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _output.Interruption -= OnInterruption;
        _stop.Cancel();
        _jobs?.CompleteAdding();
        try { _thread?.Join(500); } catch { }
        try { _output.Dispose(); } catch { }
    }
}
