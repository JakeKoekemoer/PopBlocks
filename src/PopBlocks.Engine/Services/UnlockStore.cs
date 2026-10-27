namespace PopBlocks.Engine.Services;

/// <summary>
/// A set of string ids that the player has unlocked or seen: story scenes, Prism fragments, lore entries,
/// cosmetics, achievements. Saved as one comma-separated string so the whole profile is a single value.
/// Ids are short, lowercase, and contain no commas or spaces (for example <c>scene_seen_r1_open</c>).
/// </summary>
public sealed class UnlockStore
{
    private readonly ILocalSettings _settings;
    private readonly HashSet<string> _ids;

    public UnlockStore(ILocalSettings settings)
    {
        _settings = settings;
        _ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in settings.GetString(SaveKeys.Unlocks, "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _ids.Add(part);
    }

    /// <summary>Raised with the id when something is unlocked for the first time.</summary>
    public event Action<string>? Unlocked;

    public int Count => _ids.Count;

    public IReadOnlyCollection<string> All => _ids;

    public bool Has(string id) => _ids.Contains(id);

    /// <summary>Records an id. Returns true only the first time, so a reward can be paid exactly once.</summary>
    public bool Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Contains(',') || id.Any(char.IsWhiteSpace))
            throw new ArgumentException($"'{id}' is not a valid unlock id.", nameof(id));

        if (!_ids.Add(id)) return false;

        _settings.SetString(SaveKeys.Unlocks, string.Join(',', _ids.OrderBy(i => i, StringComparer.Ordinal)));
        Unlocked?.Invoke(id);
        return true;
    }

    /// <summary>How many of the given ids are unlocked.</summary>
    public int CountOf(IEnumerable<string> ids) => ids.Count(Has);

    /// <summary>Forgets every unlock and seen scene. For testing (see DevTools); normal play never removes an unlock.</summary>
    public void DevClear()
    {
        _ids.Clear();
        _settings.SetString(SaveKeys.Unlocks, "");
    }
}
