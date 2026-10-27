using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>
/// Shortcuts for testing: give gold, jump to a level, max the shop, wipe progress. Everything here changes saved
/// data directly and ignores the normal rules, so the app only shows it in development builds.
/// </summary>
public sealed class DevTools
{
    private readonly PlayerProgress _progress;
    private readonly ShopService _shop;
    private readonly UnlockStore _unlocks;
    private readonly CampaignService _campaign;
    private readonly CosmeticLoadout _loadout;
    private readonly LifetimeStats? _lifetime;
    private readonly EndingService? _ending;
    private readonly ConsumableInventory? _consumables;

    public DevTools(PlayerProgress progress, ShopService shop, UnlockStore unlocks, CampaignService campaign, CosmeticLoadout loadout,
        LifetimeStats? lifetime = null, EndingService? ending = null, ConsumableInventory? consumables = null)
    {
        _consumables = consumables;
        _lifetime = lifetime;
        _ending = ending;
        _progress = progress;
        _shop = shop;
        _unlocks = unlocks;
        _campaign = campaign;
        _loadout = loadout;
    }

    public void AddGold(int amount) => _progress.DevSetGold((int)Math.Clamp((long)_progress.Gold + amount, 0, int.MaxValue));

    public void SetGold(int amount) => _progress.DevSetGold(amount);

    /// <summary>
    /// Makes <paramref name="level"/> the next level to play (levels below it count as cleared). Story flags are
    /// reset first and then rebuilt for that point: regions already passed have their fragment, journal entry and
    /// board style, and scenes not yet reached are unseen, so each story moment can be tested again.
    /// </summary>
    public void SetNextLevel(int level)
    {
        level = Math.Max(1, level);
        ResetStoryFlags();
        _progress.DevSetHighestCleared(level - 1);
        _campaign.Reconcile();
    }

    public void MaxShop()
    {
        foreach (var item in ShopCatalog.Items) _shop.DevSetLevel(item.Id, item.MaxLevel);
        FillConsumables();
    }

    /// <summary>Fills the stock of both helpers to the cap.</summary>
    public void FillConsumables()
    {
        if (_consumables is null) return;
        foreach (var c in ConsumableCatalog.All) _consumables.DevSetCount(c.Kind, ConsumableCatalog.Cap);
    }

    /// <summary>Gives every look on the paint shelf (for testing how each one draws). Costs nothing and pays nothing.</summary>
    public void UnlockAllLooks()
    {
        foreach (var c in CosmeticCatalog.All) _unlocks.Unlock(c.UnlockId);
    }

    /// <summary>Forgets story progress (seen scenes, fragments, journal, board style) but keeps levels and gold.</summary>
    public void ResetStoryFlags()
    {
        _unlocks.DevClear();
        _ending?.DevClear();
        _loadout.ResetAll();
    }

    /// <summary>Back to a fresh install: level 1, no gold, no upgrades, no story.</summary>
    public void ResetEverything()
    {
        ResetStoryFlags();
        _lifetime?.DevClear();
        _progress.DevSetHighestCleared(0);
        _progress.DevSetGold(0);
        foreach (var item in ShopCatalog.Items) _shop.DevSetLevel(item.Id, 0);
        if (_consumables is not null)
            foreach (var c in ConsumableCatalog.All) _consumables.DevSetCount(c.Kind, 0);
    }

    /// <summary>A few lines describing where the save is, for the dev menu.</summary>
    public string Describe()
    {
        var region = RegionCatalog.For(_progress.NextLevel);
        return $"Next level {_progress.NextLevel} ({region?.Title ?? RegionCatalog.BeyondTitle})\n" +
               $"Cleared {_progress.HighestCleared}   Gold {_progress.Gold:N0}\n" +
               $"Regions restored {_campaign.RestoredRegionCount} / {RegionCatalog.All.Count}   Fragments {_campaign.FragmentCount}\n" +
               $"Unlock ids {_unlocks.Count}   Board {_loadout.SelectedBoard}";
    }
}
