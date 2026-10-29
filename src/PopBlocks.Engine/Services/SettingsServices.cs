namespace PopBlocks.Engine.Services;

/// <summary>
/// Tiny key/value store abstraction. The app implements it with the MAUI Preferences API;
/// tests use an in-memory version. Nothing leaves the device.
/// </summary>
public interface ILocalSettings
{
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    bool GetBool(string key, bool defaultValue);
    void SetBool(string key, bool value);
    string GetString(string key, string defaultValue);
    void SetString(string key, string value);
}

/// <summary>
/// Classic-mode personal best. Saved the moment it is beaten so an unexpected exit cannot lose it.
/// (Story mode tracks progress instead: see <see cref="PlayerProgress"/>.)
/// </summary>
public sealed class HighScoreService
{
    public const string BestScoreKey = "best_score";

    private readonly ILocalSettings _settings;
    private readonly Dictionary<string, int> _best = new();

    public HighScoreService(ILocalSettings settings) => _settings = settings;

    public int Best => GetBest(BestScoreKey);

    public int GetBest(string key)
    {
        if (!_best.TryGetValue(key, out int value))
        {
            value = Math.Max(0, _settings.GetInt(key, 0));
            _best[key] = value;
        }
        return value;
    }

    /// <summary>Records a score if it beats the personal best. Returns true when it did.</summary>
    public bool Submit(int score) => Submit(BestScoreKey, score);

    public bool Submit(string key, int score)
    {
        if (score <= GetBest(key)) return false;
        _best[key] = score;
        _settings.SetInt(key, score);
        return true;
    }
}

/// <summary>Optional gameplay/visual preferences, persisted locally.</summary>
public sealed class GameSettings
{
    public const string BlastEffectsKey = "blast_effects";
    public const string ShowGhostKey = "show_ghost";
    public const string ControlsCollapsedKey = "controls_collapsed";
    public const string SoundEffectsKey = "sound_effects";
    public const string LandingGuideStyleKey = SaveKeys.LandingGuideStyle;
    public const string ReducedMotionKey = SaveKeys.ReducedMotion;

    private readonly ILocalSettings _settings;

    public GameSettings(ILocalSettings settings)
    {
        _settings = settings;
        _blastEffects = settings.GetBool(BlastEffectsKey, true);
        _showGhost = settings.GetBool(ShowGhostKey, true);

        // Older saves have only the on/off ghost setting; it decides the guide until the player picks a style.
        _guideStyle = settings.GetInt(LandingGuideStyleKey, _showGhost ? 1 : 0);
        if (_guideStyle is < 0 or > 2) _guideStyle = _showGhost ? 1 : 0;
        _reducedMotion = settings.GetBool(ReducedMotionKey, false);
        _controlsCollapsed = settings.GetBool(ControlsCollapsedKey, false);
        _soundEffects = settings.GetBool(SoundEffectsKey, true);
        _showHanging = settings.GetBool(SaveKeys.ShowHanging, true);
        _colourAssist = settings.GetBool(SaveKeys.ColourAssist, false);
    }

    /// <summary>The first Orb has been explained (once, ever).</summary>
    public bool SeenOrbIntro
    {
        get => _settings.GetBool(SaveKeys.SeenOrbIntro, false);
        set => _settings.SetBool(SaveKeys.SeenOrbIntro, value);
    }

    private bool _showHanging;
    /// <summary>The faint column and arrow under blocks that hang over a gap. On by default.</summary>
    public bool ShowHanging
    {
        get => _showHanging;
        set { _showHanging = value; _settings.SetBool(SaveKeys.ShowHanging, value); }
    }

    private bool _colourAssist;
    /// <summary>A symbol on every block, whatever the block set. Some sets hide them for a cleaner look; this puts them back.</summary>
    public bool ColourAssist
    {
        get => _colourAssist;
        set { _colourAssist = value; _settings.SetBool(SaveKeys.ColourAssist, value); }
    }

    private bool _blastEffects;
    public bool BlastEffects
    {
        get => _blastEffects;
        set { _blastEffects = value; _settings.SetBool(BlastEffectsKey, value); }
    }

    private bool _showGhost;
    /// <summary>Outline showing where the active piece will land. Kept in step with <see cref="LandingGuideStyle"/>.</summary>
    public bool ShowGhost
    {
        get => _showGhost;
        set
        {
            _showGhost = value;
            _settings.SetBool(ShowGhostKey, value);
            // Turning it on from here gives the usual outline; turning it off switches the guide off.
            int style = value ? Math.Max(1, _guideStyle) : 0;
            _guideStyle = style;
            _settings.SetInt(LandingGuideStyleKey, style);
        }
    }

    private int _guideStyle;
    /// <summary>The landing guide: 0 off, 1 outline, 2 bold (a thick outline with a faint column of light).</summary>
    public int LandingGuideStyle
    {
        get => _guideStyle;
        set
        {
            _guideStyle = Math.Clamp(value, 0, 2);
            _settings.SetInt(LandingGuideStyleKey, _guideStyle);
            // The old key keeps meaning "any guide at all", so an older build reading this save still behaves.
            _showGhost = _guideStyle > 0;
            _settings.SetBool(ShowGhostKey, _showGhost);
        }
    }

    private bool _reducedMotion;
    /// <summary>No screen shake, flashes or confetti; bursts become quick fades. Gameplay is unchanged.</summary>
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set { _reducedMotion = value; _settings.SetBool(ReducedMotionKey, value); }
    }

    private bool _soundEffects;
    /// <summary>Short game sounds (rotate, place, drop, pop). On by default.</summary>
    public bool SoundEffects
    {
        get => _soundEffects;
        set { _soundEffects = value; _settings.SetBool(SoundEffectsKey, value); }
    }

    private bool _controlsCollapsed;
    /// <summary>The on-screen button drawer is folded away (gestures still work).</summary>
    public bool ControlsCollapsed
    {
        get => _controlsCollapsed;
        set { _controlsCollapsed = value; _settings.SetBool(ControlsCollapsedKey, value); }
    }
}
