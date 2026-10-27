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

    private readonly ILocalSettings _settings;

    public GameSettings(ILocalSettings settings)
    {
        _settings = settings;
        _blastEffects = settings.GetBool(BlastEffectsKey, true);
        _showGhost = settings.GetBool(ShowGhostKey, true);
        _controlsCollapsed = settings.GetBool(ControlsCollapsedKey, false);
    }

    private bool _blastEffects;
    public bool BlastEffects
    {
        get => _blastEffects;
        set { _blastEffects = value; _settings.SetBool(BlastEffectsKey, value); }
    }

    private bool _showGhost;
    /// <summary>Outline showing where the active piece will land.</summary>
    public bool ShowGhost
    {
        get => _showGhost;
        set { _showGhost = value; _settings.SetBool(ShowGhostKey, value); }
    }

    private bool _controlsCollapsed;
    /// <summary>The on-screen button drawer is folded away (gestures still work).</summary>
    public bool ControlsCollapsed
    {
        get => _controlsCollapsed;
        set { _controlsCollapsed = value; _settings.SetBool(ControlsCollapsedKey, value); }
    }
}
