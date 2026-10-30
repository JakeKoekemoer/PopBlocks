using PopBlocks.Engine.Models;
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
        if (slot == CosmeticSlot.Board && _settings.GetInt(SaveKeys.CosmeticBoardPinned, -1) != 1)
        {
            _settings.SetInt(SaveKeys.CosmeticBoardPinned, 1);   // a board chosen by hand stays, whatever the area
            if (Selected(slot) == id) { Changed?.Invoke(); return true; }
        }
        if (Selected(slot) == id) return true;
        _settings.SetString(CosmeticCatalog.SaveKey(slot), id);
        Changed?.Invoke();
        return true;
    }

    // ---- The board, as the app has always asked for it ----

    public string SelectedBoard => Selected(CosmeticSlot.Board);

    /// <summary>
    /// The board takes the look of the area being played (the region's own board, if the player has it; otherwise the classic one).
    /// True until the player chooses a board by hand; <see cref="FollowAreaBoard"/> gives it back. A save from before this existed
    /// that already holds a chosen board keeps it.
    /// </summary>
    public bool BoardFollowsArea
    {
        get
        {
            int pinned = _settings.GetInt(SaveKeys.CosmeticBoardPinned, -1);
            return pinned >= 0 ? pinned == 0 : _settings.GetString(CosmeticCatalog.SaveKey(CosmeticSlot.Board), "").Length == 0;
        }
    }

    /// <summary>Back to the area's own board (the player's hand-picked board is forgotten).</summary>
    public void FollowAreaBoard()
    {
        _settings.SetInt(SaveKeys.CosmeticBoardPinned, 0);
        _settings.SetString(CosmeticCatalog.SaveKey(CosmeticSlot.Board), "");
        Changed?.Invoke();
    }

    /// <summary>
    /// The board to draw on a level (0 for Endless): the player's pick if they made one, otherwise the area's themed board if they
    /// have it, otherwise the classic board.
    /// </summary>
    public string BoardFor(int level)
    {
        if (!BoardFollowsArea) return SelectedBoard;
        if (RegionCatalog.For(level) is { } region)
        {
            var themed = Themes.FirstOrDefault(t => t.RegionNumber == region.Number && IsAvailable(t.Id));
            if (themed is not null) return themed.Id;
        }
        return DefaultId;
    }

    public bool Select(string boardId) => Select(CosmeticSlot.Board, boardId);

    // ---- What to draw ----

    public SkinSpec Skin => CosmeticCatalog.SkinOf(Selected(CosmeticSlot.Skin));
    public BoardSpec Board => CosmeticCatalog.BoardOf(SelectedBoard);
    public BurstStyle Burst => CosmeticCatalog.BurstOf(Selected(CosmeticSlot.Burst));
    public FrameStyle Frame => CosmeticCatalog.FrameOf(Selected(CosmeticSlot.Frame));
    public AccessoryStyle Accessory => CosmeticCatalog.AccessoryOf(Selected(CosmeticSlot.Accessory));

    // ---- Patchwork: the block set used for each colour ----

    private static readonly CellColour[] Five =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    public static string MixKey(CellColour colour) => colour switch
    {
        CellColour.Cyan => SaveKeys.CosmeticMixCyan,
        CellColour.Coral => SaveKeys.CosmeticMixCoral,
        CellColour.Yellow => SaveKeys.CosmeticMixYellow,
        CellColour.Purple => SaveKeys.CosmeticMixPurple,
        CellColour.Lime => SaveKeys.CosmeticMixLime,
        _ => throw new ArgumentOutOfRangeException(nameof(colour), "Patchwork has only the five block colours."),
    };

    /// <summary>The block set Patchwork uses for a colour: the saved one if it is a set you own, otherwise Classic.</summary>
    public string Mix(CellColour colour)
    {
        string saved = _settings.GetString(MixKey(colour), "");
        if (saved.Length == 0 || saved == CosmeticCatalog.PatchworkSkin) return CosmeticCatalog.DefaultSkin;
        return CosmeticCatalog.Find(saved) is { Slot: CosmeticSlot.Skin } c && IsOwned(c) ? saved : CosmeticCatalog.DefaultSkin;
    }

    /// <summary>Chooses the set for one colour. False (nothing changes) unless it is a block set you own other than Patchwork itself.</summary>
    public bool SetMix(CellColour colour, string skinId)
    {
        if (skinId == CosmeticCatalog.PatchworkSkin) return false;
        if (CosmeticCatalog.Find(skinId) is not { Slot: CosmeticSlot.Skin } c || !IsOwned(c)) return false;
        if (Mix(colour) == skinId) return true;
        _settings.SetString(MixKey(colour), skinId);
        Changed?.Invoke();
        return true;
    }

    /// <summary>The block sets Patchwork draws each colour with, resolved once (a lookup table for the renderer).</summary>
    public IReadOnlyDictionary<CellColour, SkinSpec> PatchworkSources() =>
        Five.ToDictionary(c => c, c => CosmeticCatalog.SkinOf(Mix(c)));

    // ---- The NEW ribbon: looks the player has not looked at yet ----

    private HashSet<string> SeenIds() =>
        _settings.GetString(SaveKeys.CosmeticSeen, "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    /// <summary>A look added in v1.1 that the player has not seen yet (it wears a NEW ribbon).</summary>
    public bool IsUnseen(Cosmetic c) => CosmeticCatalog.IsNew(c) && !SeenIds().Contains(c.Id);

    /// <summary>Marks looks as seen (the ribbon goes away). Does nothing for looks that are not new.</summary>
    public void MarkSeen(IEnumerable<Cosmetic> looks)
    {
        var seen = SeenIds();
        bool changed = false;
        foreach (var c in looks)
            if (CosmeticCatalog.IsNew(c)) changed |= seen.Add(c.Id);
        if (!changed) return;
        _settings.SetString(SaveKeys.CosmeticSeen, string.Join(',', seen.OrderBy(s => s, StringComparer.Ordinal)));
        Changed?.Invoke();
    }

    /// <summary>Puts every slot back to its default. For testing (see DevTools).</summary>
    public void ResetAll()
    {
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
            _settings.SetString(CosmeticCatalog.SaveKey(slot), "");
        foreach (var colour in Five) _settings.SetString(MixKey(colour), "");
        Changed?.Invoke();
    }
}
