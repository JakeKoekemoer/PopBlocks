using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Story;

/// <summary>A purely visual board accent (kept for the board-style button and older code; see <see cref="CosmeticCatalog"/>).</summary>
public sealed record BoardTheme(string Id, string Name, int RegionNumber, string UnlockId);

/// <summary>
/// What the player has equipped: block skin, burst style, board, piece frame and Pip accessory. Cosmetic only: it never
/// changes a rule, a level or a reward. An equipped id that is unknown or no longer owned quietly falls back to the
/// slot's default, so a bad save can never leave the game without a picture. The app reads this once at the start of a
/// game, so nothing changes in the middle of a level.
/// </summary>
public sealed class CosmeticLoadout
{
    public const string DefaultId = CosmeticCatalog.DefaultBoard;

    /// <summary>Every board except the default, as before (the Garden board is still the first).</summary>
    public static IReadOnlyList<BoardTheme> Themes { get; } = CosmeticCatalog.In(CosmeticSlot.Board)
        .Where(c => !CosmeticCatalog.IsDefault(c))
        .Select(c => new BoardTheme(c.Id, c.Name, c.Discover.Kind == RequirementKind.RegionRestored ? c.Discover.Region : 0, c.UnlockId))
        .ToList();

    private readonly ILocalSettings _settings;
    private readonly UnlockStore _unlocks;
    private readonly PlayerProgress? _progress;

    /// <param name="progress">Needed for rewards that depend on the campaign (a restored region); without it only stored unlocks count.</param>
    public CosmeticLoadout(ILocalSettings settings, UnlockStore unlocks, PlayerProgress? progress = null)
    {
        _settings = settings;
        _unlocks = unlocks;
        _progress = progress;
    }

    public event Action? Changed;

    private int Highest => _progress?.HighestCleared ?? 0;

    public bool IsOwned(Cosmetic c) => CosmeticCatalog.IsOwned(c, _unlocks, Highest);

    public bool IsOwned(string id) => CosmeticCatalog.Find(id) is { } c && IsOwned(c);

    /// <summary>Boards only: is this board id yours to use? (The default always is.)</summary>
    public bool IsAvailable(string id) =>
        CosmeticCatalog.Find(id) is { Slot: CosmeticSlot.Board } c && IsOwned(c);

    /// <summary>The id equipped in a slot; the slot's default if the saved one is unknown, from another slot, or not owned.</summary>
    public string Selected(CosmeticSlot slot)
    {
        var saved = _settings.GetString(CosmeticCatalog.SaveKey(slot), "");
        if (saved.Length == 0) return CosmeticCatalog.DefaultFor(slot);
        var c = CosmeticCatalog.Find(saved);
        return c is not null && c.Slot == slot && IsOwned(c) ? saved : CosmeticCatalog.DefaultFor(slot);
    }

    /// <summary>Equips something you own. False (and nothing changes) if it is unknown, from a different slot, or not yours.</summary>
    public bool Select(CosmeticSlot slot, string id)
    {
        var c = CosmeticCatalog.Find(id);
        if (c is null || c.Slot != slot || !IsOwned(c)) return false;
        if (Selected(slot) == id) return true;
        _settings.SetString(CosmeticCatalog.SaveKey(slot), id);
        Changed?.Invoke();
        return true;
    }

    // ---- The board, as the app has always asked for it ----

    public string SelectedBoard => Selected(CosmeticSlot.Board);

    public bool Select(string boardId) => Select(CosmeticSlot.Board, boardId);

    // ---- What to draw ----

    public SkinSpec Skin => CosmeticCatalog.SkinOf(Selected(CosmeticSlot.Skin));
    public BoardSpec Board => CosmeticCatalog.BoardOf(SelectedBoard);
    public BurstStyle Burst => CosmeticCatalog.BurstOf(Selected(CosmeticSlot.Burst));
    public FrameStyle Frame => CosmeticCatalog.FrameOf(Selected(CosmeticSlot.Frame));
    public AccessoryStyle Accessory => CosmeticCatalog.AccessoryOf(Selected(CosmeticSlot.Accessory));

    /// <summary>Puts every slot back to its default. For testing (see DevTools).</summary>
    public void ResetAll()
    {
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
            _settings.SetString(CosmeticCatalog.SaveKey(slot), "");
        Changed?.Invoke();
    }
}
