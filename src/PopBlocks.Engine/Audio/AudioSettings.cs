using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Audio;

/// <summary>
/// The player's audio choices, saved on the phone (nothing else is stored and nothing leaves it): master, music, effects and
/// ambience volumes, a mute for each, and a reduced-audio mode. "Effects on" is the same switch the game has always had
/// (<see cref="GameSettings.SoundEffects"/>), so older saves keep their choice.
/// </summary>
public sealed class AudioSettings
{
    public const int DefaultMaster = 100;
    public const int DefaultMusic = 70;
    public const int DefaultSfx = 80;
    public const int DefaultAmbience = 60;

    /// <summary>How much quieter effects are in reduced-audio mode.</summary>
    public const float ReducedSfxFactor = 0.7f;

    private readonly ILocalSettings _store;
    private readonly GameSettings _game;

    private int _master, _music, _sfx, _ambience;
    private bool _muteMaster, _muteMusic, _muteAmbience, _reduced;

    public AudioSettings(ILocalSettings store, GameSettings game)
    {
        _store = store;
        _game = game;
        _master = Clamp(store.GetInt(SaveKeys.AudioMaster, DefaultMaster));
        _music = Clamp(store.GetInt(SaveKeys.AudioMusic, DefaultMusic));
        _sfx = Clamp(store.GetInt(SaveKeys.AudioSfx, DefaultSfx));
        _ambience = Clamp(store.GetInt(SaveKeys.AudioAmbience, DefaultAmbience));
        _muteMaster = store.GetBool(SaveKeys.AudioMuteMaster, false);
        _muteMusic = store.GetBool(SaveKeys.AudioMuteMusic, false);
        _muteAmbience = store.GetBool(SaveKeys.AudioMuteAmbience, false);
        _reduced = store.GetBool(SaveKeys.AudioReduced, false);
    }

    /// <summary>Raised after any setting changes (from any thread that changed it).</summary>
    public event Action? Changed;

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);

    private void Set(ref int field, string key, int value)
    {
        value = Clamp(value);
        if (field == value) return;
        field = value;
        _store.SetInt(key, value);
        Changed?.Invoke();
    }

    private void Set(ref bool field, string key, bool value)
    {
        if (field == value) return;
        field = value;
        _store.SetBool(key, value);
        Changed?.Invoke();
    }

    public int MasterPercent { get => _master; set => Set(ref _master, SaveKeys.AudioMaster, value); }
    public int MusicPercent { get => _music; set => Set(ref _music, SaveKeys.AudioMusic, value); }
    public int SfxPercent { get => _sfx; set => Set(ref _sfx, SaveKeys.AudioSfx, value); }
    public int AmbiencePercent { get => _ambience; set => Set(ref _ambience, SaveKeys.AudioAmbience, value); }

    public bool MasterMuted { get => _muteMaster; set => Set(ref _muteMaster, SaveKeys.AudioMuteMaster, value); }
    public bool MusicMuted { get => _muteMusic; set => Set(ref _muteMusic, SaveKeys.AudioMuteMusic, value); }
    public bool AmbienceMuted { get => _muteAmbience; set => Set(ref _muteAmbience, SaveKeys.AudioMuteAmbience, value); }
    public bool ReducedAudio { get => _reduced; set => Set(ref _reduced, SaveKeys.AudioReduced, value); }

    /// <summary>Effects mute. The same switch as "Sound effects" in Settings.</summary>
    public bool SfxMuted
    {
        get => !_game.SoundEffects;
        set
        {
            if (_game.SoundEffects == !value) return;
            _game.SoundEffects = !value;
            Changed?.Invoke();
        }
    }

    /// <summary>Whether anything at all can be heard.</summary>
    public bool AnythingAudible => !_muteMaster && _master > 0 &&
        ((!_muteMusic && _music > 0) || (!SfxMuted && _sfx > 0) || (!_muteAmbience && _ambience > 0 && !_reduced));

    /// <summary>
    /// The gain for one bus: master × the bus's own volume, or 0 when either is muted. Volumes are squared
    /// (<see cref="Dsp.VolumeToGain"/>) so the middle of a slider is a natural-sounding half.
    /// </summary>
    public float GainFor(Bus bus)
    {
        if (_muteMaster) return 0f;
        float master = Dsp.VolumeToGain(_master / 100f);
        return bus switch
        {
            Bus.Music => _muteMusic ? 0f : master * Dsp.VolumeToGain(_music / 100f),
            Bus.Sfx => SfxMuted ? 0f : master * Dsp.VolumeToGain(_sfx / 100f) * (_reduced ? ReducedSfxFactor : 1f),
            _ => _muteAmbience || _reduced ? 0f : master * Dsp.VolumeToGain(_ambience / 100f),
        };
    }

    /// <summary>Back to the defaults (a settings "reset" button).</summary>
    public void ResetToDefaults()
    {
        MasterPercent = DefaultMaster; MusicPercent = DefaultMusic; SfxPercent = DefaultSfx; AmbiencePercent = DefaultAmbience;
        MasterMuted = false; MusicMuted = false; AmbienceMuted = false; ReducedAudio = false; SfxMuted = false;
    }
}
