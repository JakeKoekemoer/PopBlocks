using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Audio;

/// <summary>
/// Decides what the music does. It never makes a sound itself: it tells the <see cref="AudioEngine"/> which piece to play, which
/// layers are up, and when to duck or change state. The states follow the audio spec:
/// <list type="bullet">
/// <item><b>A, normal play</b>: the region's loop, sparse enough to sit under the puzzle.</item>
/// <item><b>B, active chain</b>: a rhythmic layer fades in and out again after the chain ends. More activity, not more volume.</item>
/// <item><b>D, dialogue</b>: fewer layers, music ducked smoothly, restored afterwards.</item>
/// <item><b>E, restoration</b>: the same piece moves to its next state on a bar line, with no restart.</item>
/// <item><b>F, finale</b>: the scripted Prism Heart sequence.</item>
/// </list>
/// (State C, the combo flourish, is a sound effect; <see cref="GameAudio"/> plays it, rate-limited.)
///
/// Not thread-safe: <see cref="GameAudio"/> calls it from one worker thread only. Time is passed in, so tests can step it.
/// </summary>
public sealed class MusicDirector
{
    /// <summary>Seconds after the last pop before the chain layer fades away.</summary>
    public const double ChainHoldSeconds = 2.2;

    private const int Pad = (int)Layer.Pad, Bass = (int)Layer.Bass, Melody = (int)Layer.Melody, Motif = (int)Layer.Motif,
        Accent = (int)Layer.Accent, Counter = (int)Layer.Counter, Chain = (int)Layer.Chain, Pulse = (int)Layer.Pulse,
        Tension = (int)Layer.Tension;

    private readonly AudioEngine _engine;
    private readonly AudioSettings _settings;
    private readonly List<(double Due, Action Act)> _timed = new();

    private double _now;
    private bool _loaded;
    private RegionId _region = RegionId.Menu;
    private StateMask _state = StateMask.Faded;
    private int _chain;
    private double _lastWave;
    private bool _dialogue, _hush, _paused, _finale;
    private float _tension;

    public MusicDirector(AudioEngine engine, AudioSettings settings)
    {
        _engine = engine;
        _settings = settings;
    }

    public RegionId Region => _region;
    public StateMask State => _state;
    public bool IsLoaded => _loaded;
    public bool InFinale => _finale;
    public bool InDialogue => _dialogue;
    public int ChainLevel => _chain;

    // ---- choosing the music ------------------------------------------------------------------------------------------------

    /// <summary>Menu and world map: the Spectrum motif, quietly.</summary>
    public void ShowMenu() => Show(RegionId.Menu, StateMask.Faded);

    /// <summary>Classic mode plays the restored Garden theme.</summary>
    public void ShowClassic() => Show(RegionId.ForgottenGarden, StateMask.Restored);

    /// <summary>A Story level: the region's theme in the state the region is in.</summary>
    public void ShowRegion(RegionId region, RegionState progress) => Show(region, ToMask(progress));

    public static StateMask ToMask(RegionState state) => state switch
    {
        RegionState.Restored => StateMask.Restored,
        RegionState.Recovering => StateMask.Recovering,
        _ => StateMask.Faded,
    };

    private void Show(RegionId region, StateMask state)
    {
        if (_finale) return;                                   // the finale owns the music until it is over

        if (_loaded && region == _region)
        {
            // Same piece: just move it to the right state on the next bar. Never a restart.
            if (state != _state) { _state = state; _engine.SetState(state, Quantize.NextBar); }
            return;
        }

        var profile = RegionProfiles.Get(region);
        _region = region;
        _state = state;
        _loaded = true;
        _chain = 0;
        ResetTransient();
        _engine.ConfigureReverb(Bus.Music, profile.ReverbRoom, profile.ReverbDamping, profile.ReverbWet);
        _engine.ConfigureReverb(Bus.Ambience, MathF.Min(1f, profile.ReverbRoom + 0.1f), profile.ReverbDamping, 0.25f);
        _engine.SetAmbience(_settings.ReducedAudio ? null : profile.Ambience);
        _engine.LoadScore(new ScoreRequest(ScoreLibrary.Get(region), 0, state, Layers(), 0.9f, 1.6f));
    }

    /// <summary>Fades the music out (leaving the screen for a place with no music, or shutting down).</summary>
    public void Stop(float fadeSeconds = 0.8f)
    {
        _loaded = false;
        _finale = false;
        _timed.Clear();
        _engine.LoadScore(new ScoreRequest(null, 0, StateMask.Faded, Layers(), fadeSeconds, 0f));
    }

    private void ResetTransient()
    {
        _dialogue = false; _hush = false; _paused = false; _tension = 0f;
        _engine.SetMusicDuck(1f, 0.4f);
        _engine.SetAmbienceDuck(1f, 0.4f);
    }

    // ---- adaptive states ------------------------------------------------------------------------------------------------------

    /// <summary>A wave of blocks popped. <paramref name="chainIndex"/> is 1 for the first wave of a chain, 2 for the next…</summary>
    public void OnWave(int chainIndex)
    {
        if (!_loaded || _finale) return;
        bool stepUp = chainIndex >= 2 && chainIndex > _chain;
        _chain = Math.Max(_chain, chainIndex);
        _lastWave = _now;
        if (stepUp) Apply(Quantize.NextBeat);               // state B: arrives on the beat
    }

    /// <summary>The player is reading the story: fewer layers, quieter music.</summary>
    public void SetDialogue(bool on)
    {
        if (_dialogue == on) return;
        _dialogue = on;
        if (!on) _hush = false;
        _engine.SetMusicDuck(on ? 0.62f : (_paused ? 0.55f : 1f), on ? 0.45f : 0.9f);
        _engine.SetAmbienceDuck(on ? 0.3f : 1f, on ? 0.45f : 1.2f);
        Apply(Quantize.NextBeat);
    }

    /// <summary>The Warden's reveal and confession: only the low pad and a few isolated motif notes are left.</summary>
    public void SetHush(bool on)
    {
        if (_hush == on) return;
        _hush = on;
        Apply(Quantize.Immediate);
    }

    /// <summary>The game is paused: the music dips but plays on.</summary>
    public void SetPaused(bool on)
    {
        if (_paused == on) return;
        _paused = on;
        if (!_dialogue) _engine.SetMusicDuck(on ? 0.55f : 1f, on ? 0.25f : 0.6f);
    }

    /// <summary>
    /// Optional, subtle: 0 (comfortable) to 1 (nearly out of moves). It only raises a quiet low pulse in the accompaniment; it
    /// is never an alarm, and reduced-audio mode turns it off.
    /// </summary>
    public void SetTension(float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        if (MathF.Abs(level - _tension) < 0.05f) return;
        _tension = level;
        Apply(Quantize.NextBeat);
    }

    /// <summary>
    /// A region has been restored (or has started to recover): the same piece moves to its new state on the next bar while the
    /// bright layers return slowly, so the change is heard as the world filling with colour and not as a new song.
    /// </summary>
    public void Restore(RegionState to)
    {
        if (_finale) return;
        var mask = ToMask(to);
        if (!_loaded || mask == _state) return;
        _state = mask;
        _engine.SetState(mask, Quantize.NextBar);
        // The extra layers come up a little after the harmony changes.
        Schedule(1.5, () => Apply(Quantize.NextBeat));
    }

    // ---- the finale ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The Prism Heart restoration. The Heart theme is already playing (or is started in its recovering state). The backing thins
    /// to a low pad, the scripted sequence plays, and the Heart theme then moves to its restored state under the last note.
    /// </summary>
    public void PlayFinale()
    {
        if (_finale) return;
        if (!_loaded || _region != RegionId.HeartOfSpectrum) Show(RegionId.HeartOfSpectrum, StateMask.Recovering);

        _finale = true;
        _dialogue = false; _hush = false; _chain = 0; _tension = 0f;
        _engine.SetMusicDuck(1f, 0.3f);
        _engine.SetAmbienceDuck(0.25f, 1.5f);

        // 1. Thin the backing to the pad alone.
        var thin = new float[MusicScore.LayerCount];
        thin[Pad] = 1f;
        _engine.SetLayerTargets(thin, Quantize.Immediate);

        _engine.PlayCue(FinaleScript.Cue);

        Schedule(FinaleScript.RestoreAtMs / 1000.0, () =>
        {
            _state = StateMask.Restored;
            _engine.SetState(StateMask.Restored, Quantize.NextBar);
        });
        Schedule(FinaleScript.RestoreAtMs / 1000.0 + 2.0, () =>
        {
            _finale = false;
            _engine.SetAmbienceDuck(1f, 4f);
            Apply(Quantize.NextBar);
        });
    }

    // ---- time -----------------------------------------------------------------------------------------------------------------

    /// <summary>Call about ten times a second with a steadily increasing time in seconds.</summary>
    public void Update(double nowSeconds)
    {
        _now = nowSeconds;

        if (_chain > 0 && _now - _lastWave > ChainHoldSeconds)
        {
            _chain = 0;
            Apply(Quantize.NextBeat);                              // fade back gracefully
        }

        for (int i = _timed.Count - 1; i >= 0; i--)
        {
            if (_timed[i].Due > _now) continue;
            var act = _timed[i].Act;
            _timed.RemoveAt(i);
            act();
        }
    }

    private void Schedule(double inSeconds, Action act) => _timed.Add((_now + inSeconds, act));

    /// <summary>Re-reads the settings (reduced audio changes which layers play and whether ambience runs).</summary>
    public void SettingsChanged()
    {
        if (!_loaded) return;
        _engine.SetAmbience(_settings.ReducedAudio ? null : RegionProfiles.Get(_region).Ambience);
        if (!_finale) Apply(Quantize.Immediate);
    }

    // ---- the layer mix ----------------------------------------------------------------------------------------------------------

    private void Apply(Quantize q)
    {
        if (!_loaded || _finale) return;        // the finale sets its own mix
        _engine.SetLayerTargets(Layers(), q);
    }

    /// <summary>The level of each layer for the current situation (see the class remarks for what each state does).</summary>
    public float[] LayerTargets() => Layers();

    private float[] Layers()
    {
        var t = new float[MusicScore.LayerCount];
        bool menu = _region == RegionId.Menu;
        bool reduced = _settings.ReducedAudio;

        t[Pad] = 1f;
        t[Motif] = 1f;
        t[Melody] = 0.85f;
        t[Bass] = menu ? 0f : reduced ? 0.7f : 0.85f;
        t[Accent] = reduced ? 0.3f : 0.7f;
        t[Counter] = reduced ? 0f : 0.6f;
        t[Pulse] = reduced ? 0f : 0.5f;

        if (_chain >= 2 && !reduced)
        {
            // State B: more activity, not more loudness.
            t[Chain] = Math.Min(1f, 0.55f + 0.1f * (_chain - 2));
            t[Counter] = 0.9f;
            t[Pulse] = Math.Max(t[Pulse], 0.6f);
        }

        if (_tension > 0f && !reduced && !_dialogue)
            t[Tension] = _tension * 0.7f;

        if (_dialogue)
        {
            // State D: leave room for reading. Less rhythm, fewer high notes, a quieter tune.
            t[Pulse] = 0f; t[Chain] = 0f; t[Tension] = 0f;
            t[Counter] = Math.Min(t[Counter], 0.2f);
            t[Accent] = Math.Min(t[Accent], 0.2f);
            t[Melody] = Math.Min(t[Melody], 0.45f);
        }

        if (_hush)
        {
            // The Warden scenes: a low tone and a few isolated motif notes.
            t[Melody] = 0f; t[Accent] = 0f; t[Counter] = 0f; t[Pulse] = 0f; t[Chain] = 0f; t[Tension] = 0f;
            t[Bass] = Math.Min(t[Bass], 0.4f);
            t[Motif] = 0.8f;
        }

        return t;
    }
}
