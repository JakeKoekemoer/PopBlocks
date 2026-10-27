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

/// <summary>Gold-for-upgrades shop. Each upgrade has a few levels, each costing more. Purchases are saved.</summary>
public sealed class ShopService
{
    private static readonly int[][] Costs =
    {
        new[] { 120, 400 },          // Preview: 1 -> 2 -> 3 upcoming pieces
        new[] { 80, 250, 600 },      // PopFlair: 0 -> 3
        new[] { 150, 450, 1000 },    // ExtraMoves: +0 -> +6
    };

    private static readonly string[] PreviewEffects = { "Next piece", "Next 2 pieces", "Next 3 pieces" };
    private static readonly string[] FlairEffects = { "Neon pop", "Shockwaves", "Sparks and screen shake", "Confetti finale" };
    private static readonly string[] MoveEffects = { "No bonus", "+2 moves per level", "+4 moves per level", "+6 moves per level" };

    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;

    public ShopService(ILocalSettings settings, PlayerProgress progress)
    {
        _settings = settings;
        _progress = progress;
    }

    public event Action? Changed;

    private static string Key(Upgrade u) => $"upgrade_{u.ToString().ToLowerInvariant()}";

    public int MaxLevel(Upgrade u) => Costs[(int)u].Length;

    public int Level(Upgrade u) => Math.Clamp(_settings.GetInt(Key(u), 0), 0, MaxLevel(u));

    public int? NextCost(Upgrade u) => Level(u) >= MaxLevel(u) ? null : Costs[(int)u][Level(u)];

    public bool CanAfford(Upgrade u) => NextCost(u) is { } cost && _progress.Gold >= cost;

    /// <summary>Buys the next level of an upgrade if there is one and the player has the gold.</summary>
    public bool TryBuy(Upgrade u)
    {
        if (NextCost(u) is not { } cost) return false;
        if (!_progress.TrySpend(cost)) return false;
        _settings.SetInt(Key(u), Level(u) + 1);
        Changed?.Invoke();
        return true;
    }

    // ---- What the upgrades do ----

    /// <summary>Upcoming pieces shown (1 to 3).</summary>
    public int PreviewCount => 1 + Level(Upgrade.Preview);

    /// <summary>Pop effect richness (0 to 3).</summary>
    public int FlairLevel => Level(Upgrade.PopFlair);

    /// <summary>Bonus moves on non-tutorial Story levels.</summary>
    public int ExtraMoves => 2 * Level(Upgrade.ExtraMoves);

    public IReadOnlyList<UpgradeInfo> Items() => new[]
    {
        Describe(Upgrade.Preview, "Lookout", "See more of the pieces that are coming.", PreviewEffects),
        Describe(Upgrade.PopFlair, "Pop Flair", "Fancier effects when blocks burst.", FlairEffects),
        Describe(Upgrade.ExtraMoves, "Spare Moves", "Extra moves on every Story level.", MoveEffects),
    };

    private UpgradeInfo Describe(Upgrade u, string title, string description, string[] effects)
    {
        int level = Level(u);
        int max = MaxLevel(u);
        // Preview has two purchasable levels but its effects start at "Next piece" (level 0).
        string current = effects[Math.Min(level, effects.Length - 1)];
        string next = level >= max ? "" : effects[Math.Min(level + 1, effects.Length - 1)];
        return new UpgradeInfo(u, title, description, level, max, NextCost(u), current, next);
    }
}
