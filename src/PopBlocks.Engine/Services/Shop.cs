using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum Upgrade
{
    /// <summary>See more of the upcoming pieces.</summary>
    Preview,

    /// <summary>Fancier pop effects.</summary>
    PopFlair,

    /// <summary>Extra moves on every Story level.</summary>
    ExtraMoves,
}

/// <summary>A shop row, ready to show.</summary>
public sealed record UpgradeInfo(
    Upgrade Kind, string Title, string Description, int Level, int MaxLevel, int? NextCost,
    string CurrentEffect, string NextEffect)
{
    public bool IsMaxed => Level >= MaxLevel;
}

/// <summary>
/// One row of the Workshop, ready to show. <see cref="Locked"/> items cannot be bought yet and say why in
/// <see cref="LockHint"/> (a hint to discover, never a price).
/// </summary>
public sealed record ShopEntry(
    ShopItem Item, int Level, int? NextPrice, string CurrentEffect, string NextEffect,
    bool Locked, string LockHint, bool CanAfford)
{
    public bool IsMaxed => Level >= Item.MaxLevel;
    public bool CanBuy => !IsMaxed && !Locked && CanAfford;
}

/// <summary>
/// Gold-for-upgrades shop, driven by <see cref="ShopCatalog"/>. Each item has levels; each level has a price and may
/// need a region restored or an achievement first. Purchases are saved under the item's own key.
/// </summary>
public sealed class ShopService
{
    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;
    private readonly UnlockStore? _unlocks;

    /// <param name="unlocks">Needed only for items that ask for an achievement; without it those stay locked.</param>
    public ShopService(ILocalSettings settings, PlayerProgress progress, UnlockStore? unlocks = null)
    {
        _settings = settings;
        _progress = progress;
        _unlocks = unlocks;
    }

    public event Action? Changed;

    private static string IdOf(Upgrade u) => u switch
    {
        Upgrade.Preview => ShopCatalog.Preview,
        Upgrade.PopFlair => ShopCatalog.PopFlair,
        _ => ShopCatalog.ExtraMoves,
    };

    // ---- Generic API (by item id) ----

    public int MaxLevel(string id) => ShopCatalog.Get(id).MaxLevel;

    public int Level(string id)
    {
        var item = ShopCatalog.Get(id);
        return Math.Clamp(_settings.GetInt(item.SaveKey, 0), 0, item.MaxLevel);
    }

    public int? NextCost(string id)
    {
        var item = ShopCatalog.Get(id);
        int level = Level(id);
        return level >= item.MaxLevel ? null : item.Tiers[level].Price;
    }

    /// <summary>The thing the next level asks for, or <see cref="Requirement.None"/>.</summary>
    public Requirement NextRequirement(string id)
    {
        var item = ShopCatalog.Get(id);
        int level = Level(id);
        return level >= item.MaxLevel ? Requirement.None : item.Tiers[level].Requires;
    }

    public bool IsMet(Requirement r) => r.IsMetBy(_progress.HighestCleared, _unlocks);

    /// <summary>The next level of this item is not open yet.</summary>
    public bool IsLocked(string id) => NextCost(id) != null && !IsMet(NextRequirement(id));

    public bool CanAfford(string id) => NextCost(id) is { } cost && !IsLocked(id) && _progress.Gold >= cost;

    /// <summary>Buys the next level if there is one, it is unlocked and the player has the gold.</summary>
    public bool TryBuy(string id)
    {
        if (NextCost(id) is not { } cost || IsLocked(id)) return false;
        if (!_progress.TrySpend(cost)) return false;
        _settings.SetInt(ShopCatalog.Get(id).SaveKey, Level(id) + 1);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sets an item level directly, without paying or checking. For testing (see DevTools).</summary>
    public void DevSetLevel(string id, int level)
    {
        var item = ShopCatalog.Get(id);
        _settings.SetInt(item.SaveKey, Math.Clamp(level, 0, item.MaxLevel));
        Changed?.Invoke();
    }

    public ShopEntry Entry(string id)
    {
        var item = ShopCatalog.Get(id);
        int level = Level(id);
        bool maxed = level >= item.MaxLevel;
        string current = level == 0 ? item.BaseEffect : item.Tiers[level - 1].Effect;
        string next = maxed ? "" : item.Tiers[level].Effect;
        bool locked = IsLocked(id);
        return new ShopEntry(item, level, NextCost(id), current, next, locked,
            locked ? NextRequirement(id).Describe() : "", CanAfford(id));
    }

    public IReadOnlyList<ShopEntry> Entries(ItemCategory? category = null) =>
        ShopCatalog.Items.Where(i => category == null || i.Category == category).Select(i => Entry(i.Id)).ToList();

    // ---- Legacy API (the three original upgrades) ----

    public int MaxLevel(Upgrade u) => MaxLevel(IdOf(u));

    public int Level(Upgrade u) => Level(IdOf(u));

    public int? NextCost(Upgrade u) => NextCost(IdOf(u));

    public bool CanAfford(Upgrade u) => CanAfford(IdOf(u));

    public bool TryBuy(Upgrade u) => TryBuy(IdOf(u));

    public void DevSetLevel(Upgrade u, int level) => DevSetLevel(IdOf(u), level);

    // ---- What the upgrades do ----

    /// <summary>Upcoming pieces shown (1 to 5).</summary>
    public int PreviewCount => 1 + Level(ShopCatalog.Preview);

    /// <summary>Pop effect richness (0 to 3).</summary>
    public int FlairLevel => Level(ShopCatalog.PopFlair);

    /// <summary>Bonus moves on non-tutorial Story levels.</summary>
    public int ExtraMoves => 2 * Level(ShopCatalog.ExtraMoves);

    /// <summary>Colour Lens: 0 off, 1 outlines groups of 3, 2 also groups of 2.</summary>
    public int LensLevel => Level(ShopCatalog.ColourLens);

    /// <summary>Chain Sense: the landing outline glows when the drop would start a chain.</summary>
    public bool ChainSenseOwned => Level(ShopCatalog.ChainSense) > 0;

    /// <summary>Prism Reserve: Prism Shifts per level that cost nothing (0 to 2).</summary>
    public int PrismFreeCharges => Level(ShopCatalog.PrismReserve);

    /// <summary>Pip's Insight: 0 not bought, 1 three uses a level, 2 six uses, 3 unlimited with an Always on switch.</summary>
    public int InsightLevel => Level(ShopCatalog.Insight);

    /// <summary>Uses of Insight per level at the level owned (<see cref="int.MaxValue"/> when unlimited, 0 when not bought).</summary>
    public int InsightUsesPerLevel => InsightLevel switch { 0 => 0, 1 => 3, 2 => 6, _ => int.MaxValue };

    /// <summary>Insight level 3 only: show the suggestion for every piece without pressing anything. Off until the player turns it on.</summary>
    public bool InsightAlwaysOn
    {
        get => InsightLevel >= 3 && _settings.GetBool(SaveKeys.InsightAlwaysOn, false);
        set => _settings.SetBool(SaveKeys.InsightAlwaysOn, value && InsightLevel >= 3);
    }

    public IReadOnlyList<UpgradeInfo> Items() => new[]
    {
        Describe(Upgrade.Preview),
        Describe(Upgrade.PopFlair),
        Describe(Upgrade.ExtraMoves),
    };

    private UpgradeInfo Describe(Upgrade u)
    {
        var e = Entry(IdOf(u));
        return new UpgradeInfo(u, e.Item.Title, e.Item.Description, e.Level, e.Item.MaxLevel, e.NextPrice, e.CurrentEffect, e.NextEffect);
    }
}
