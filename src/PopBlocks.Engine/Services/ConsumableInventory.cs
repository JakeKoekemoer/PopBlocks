using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum Consumable
{
    /// <summary>Takes back the last placement.</summary>
    SecondChance,

    /// <summary>Rearranges the colours of the next piece.</summary>
    PrismShift,
}

/// <summary>What one helper is, what it costs and when it opens up.</summary>
public sealed record ConsumableInfo(
    Consumable Kind, string Id, string Title, string Description, string Rules, int Price, string SaveKey, Requirement Requires);

/// <summary>
/// The two helpers that are used up. Prices live here and nowhere else. They are bought in small stacks (never more
/// than <see cref="Cap"/> in stock), used at most once each per level, and switched off on finales, tutorials and in
/// Classic. No level needs one: the bot that proves every level winnable plays without them.
/// </summary>
public static class ConsumableCatalog
{
    /// <summary>The most of each the player can hold.</summary>
    public const int Cap = 3;

    public static IReadOnlyList<ConsumableInfo> All { get; } = new[]
    {
        new ConsumableInfo(Consumable.SecondChance, "second_chance", "Second Chance",
            "Take back the last block you placed.",
            "Once per level. Not on tutorials or the last level of a region.",
            150, SaveKeys.ConsumableSecondChance, Requirement.RegionRestored(2)),

        new ConsumableInfo(Consumable.PrismShift, "prism_shift", "Prism Shift",
            "Rearrange the colours of the NEXT piece. Its shape stays the same.",
            "Once per level (more with Prism Reserve). Not on tutorials or the last level of a region.",
            100, SaveKeys.ConsumablePrismShift, Requirement.RegionRestored(4)),
    };

    public static ConsumableInfo Get(Consumable kind) => All.First(c => c.Kind == kind);
}

/// <summary>A helper's row in the Workshop, ready to show.</summary>
public sealed record ConsumableEntry(ConsumableInfo Info, int Count, bool Locked, string LockHint, bool CanAfford)
{
    public bool IsFull => Count >= ConsumableCatalog.Cap;
    public bool CanBuy => !Locked && !IsFull && CanAfford;
}

/// <summary>
/// How many of each helper the player is holding, and the buying and using of them. Stock is saved under its own keys.
/// A level copies the stock into the engine when it starts (<see cref="ApplyTo"/>), and one is taken off when the
/// engine reports that it was used (<see cref="Attach"/>), so buying mid-level changes nothing and a level that is
/// quit before using a helper costs nothing.
/// </summary>
public sealed class ConsumableInventory
{
    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;
    private readonly UnlockStore? _unlocks;
    private GameEngine? _engine;

    public ConsumableInventory(ILocalSettings settings, PlayerProgress progress, UnlockStore? unlocks = null)
    {
        _settings = settings;
        _progress = progress;
        _unlocks = unlocks;
    }

    public event Action? Changed;

    public int Count(Consumable kind) =>
        Math.Clamp(_settings.GetInt(ConsumableCatalog.Get(kind).SaveKey, 0), 0, ConsumableCatalog.Cap);

    public bool IsLocked(Consumable kind) =>
        !ConsumableCatalog.Get(kind).Requires.IsMetBy(_progress.HighestCleared, _unlocks);

    public bool CanBuy(Consumable kind) =>
        !IsLocked(kind) && Count(kind) < ConsumableCatalog.Cap && _progress.Gold >= ConsumableCatalog.Get(kind).Price;

    public ConsumableEntry Entry(Consumable kind)
    {
        var info = ConsumableCatalog.Get(kind);
        bool locked = IsLocked(kind);
        return new ConsumableEntry(info, Count(kind), locked, locked ? info.Requires.Describe() : "", _progress.Gold >= info.Price);
    }

    public IReadOnlyList<ConsumableEntry> Entries() => ConsumableCatalog.All.Select(c => Entry(c.Kind)).ToList();

    /// <summary>Buys one. Refused when locked, when already at the cap, or when gold is short (nothing is spent then).</summary>
    public bool TryBuy(Consumable kind)
    {
        if (IsLocked(kind) || Count(kind) >= ConsumableCatalog.Cap) return false;
        var info = ConsumableCatalog.Get(kind);
        if (!_progress.TrySpend(info.Price)) return false;
        _settings.SetInt(info.SaveKey, Count(kind) + 1);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Takes one out of stock. False when there is none.</summary>
    public bool Use(Consumable kind)
    {
        int count = Count(kind);
        if (count <= 0) return false;
        _settings.SetInt(ConsumableCatalog.Get(kind).SaveKey, count - 1);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sets the stock directly, without paying. For testing (see DevTools).</summary>
    public void DevSetCount(Consumable kind, int count)
    {
        _settings.SetInt(ConsumableCatalog.Get(kind).SaveKey, Math.Clamp(count, 0, ConsumableCatalog.Cap));
        Changed?.Invoke();
    }

    /// <summary>Copies the stock and the free allowance into an engine; they apply from its next game.</summary>
    public void ApplyTo(GameEngine engine, ShopService shop)
    {
        engine.SecondChanceStock = Count(Consumable.SecondChance);
        engine.PrismShiftStock = Count(Consumable.PrismShift);
        engine.PrismFreeCharges = shop.PrismFreeCharges;
    }

    /// <summary>Starts taking a unit out of stock whenever the engine says a helper was used. Replaces any earlier attachment.</summary>
    public void Attach(GameEngine engine)
    {
        Detach();
        _engine = engine;
        engine.EventRaised += OnEvent;
    }

    public void Detach()
    {
        if (_engine is null) return;
        _engine.EventRaised -= OnEvent;
        _engine = null;
    }

    private void OnEvent(GameEvent e)
    {
        switch (e)
        {
            case GameEvent.SecondChanceUsed:
                Use(Consumable.SecondChance);
                break;
            case GameEvent.PrismShifted when _engine is { LastShiftWasPaid: true }:
                Use(Consumable.PrismShift);
                break;
        }
    }
}
