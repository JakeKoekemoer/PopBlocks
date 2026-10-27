using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum CosmeticState
{
    /// <summary>Yours. Equip it any time.</summary>
    Owned,

    /// <summary>Discovered and affordable.</summary>
    Buyable,

    /// <summary>Discovered, but you need more gold.</summary>
    Unaffordable,

    /// <summary>Not discovered yet. Shows a hint, never a price.</summary>
    Locked,
}

/// <summary>A cosmetic as the wardrobe shows it.</summary>
public sealed record CosmeticEntry(Cosmetic Item, CosmeticState State, bool Equipped, string Hint);

/// <summary>
/// Buying cosmetics with gold. A cosmetic is first discovered through the story or an achievement and only then can be
/// bought; rewards are not for sale at all. Buying stores the item's unlock id, so it is paid for once.
/// </summary>
public sealed class CosmeticShop
{
    private readonly UnlockStore _unlocks;
    private readonly PlayerProgress _progress;
    private readonly CosmeticLoadout _loadout;

    public CosmeticShop(UnlockStore unlocks, PlayerProgress progress, CosmeticLoadout loadout)
    {
        _unlocks = unlocks;
        _progress = progress;
        _loadout = loadout;
    }

    public event Action? Changed;

    public bool IsDiscovered(Cosmetic c) => c.Discover.IsMetBy(_progress.HighestCleared, _unlocks);

    public CosmeticState StateOf(Cosmetic c)
    {
        if (_loadout.IsOwned(c)) return CosmeticState.Owned;
        if (!IsDiscovered(c)) return CosmeticState.Locked;
        return _progress.Gold >= c.Price ? CosmeticState.Buyable : CosmeticState.Unaffordable;
    }

    public CosmeticEntry Entry(Cosmetic c)
    {
        var state = StateOf(c);
        return new CosmeticEntry(c, state, _loadout.Selected(c.Slot) == c.Id,
            state == CosmeticState.Locked ? c.Discover.Describe() : "");
    }

    /// <summary>The wardrobe's rows for a slot: what you own first, then what you can buy, then what is still hidden.</summary>
    public IReadOnlyList<CosmeticEntry> Entries(CosmeticSlot slot) =>
        CosmeticCatalog.In(slot).Select(Entry).OrderBy(e => (int)e.State).ThenBy(e => e.Item.Price).ToList();

    /// <summary>Buys a discovered, priced item you do not own yet. False (and no gold taken) otherwise.</summary>
    public bool TryBuy(string id)
    {
        if (CosmeticCatalog.Find(id) is not { } c || c.IsReward) return false;
        var state = StateOf(c);
        if (state is CosmeticState.Owned or CosmeticState.Locked) return false;
        if (!_progress.TrySpend(c.Price)) return false;

        _unlocks.Unlock(c.UnlockId);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Buys it if needed and affordable, then equips it. True if it ends up equipped.</summary>
    public bool Equip(string id)
    {
        if (CosmeticCatalog.Find(id) is not { } c) return false;
        return _loadout.Select(c.Slot, id);
    }
}
