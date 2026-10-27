using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum ItemCategory
{
    /// <summary>Changes how the game helps you play. Never required to finish a level.</summary>
    Gameplay,

    /// <summary>Changes how things look. Never changes a rule.</summary>
    Cosmetic,

    /// <summary>Used up: bought in small stacks.</summary>
    Consumable,
}

public enum RequirementKind
{
    None,
    RegionRestored,
    Achievement,
    CampaignComplete,
}

/// <summary>What has to have happened before something can be bought. Gold never unlocks story or accessibility.</summary>
public sealed record Requirement(RequirementKind Kind, int Region = 0, string AchievementId = "")
{
    public static Requirement None { get; } = new(RequirementKind.None);
    public static Requirement RegionRestored(int region) => new(RequirementKind.RegionRestored, region);
    public static Requirement Achievement(string id) => new(RequirementKind.Achievement, 0, id);
    public static Requirement CampaignComplete { get; } = new(RequirementKind.CampaignComplete);

    /// <summary>
    /// Is this requirement met? <paramref name="highestCleared"/> is the furthest level cleared. An achievement
    /// requirement needs <paramref name="unlocks"/> and is unmet without it.
    /// </summary>
    public bool IsMetBy(int highestCleared, UnlockStore? unlocks) => Kind switch
    {
        RequirementKind.RegionRestored => RegionCatalog.Progress(RegionCatalog.Get(Region), highestCleared).IsRestored,
        RequirementKind.Achievement => unlocks?.Has(ShopCatalog.AchievementUnlockId(AchievementId)) == true,
        RequirementKind.CampaignComplete => highestCleared >= RegionCatalog.LastCampaignLevel,
        _ => true,
    };

    /// <summary>The hint shown on a locked item: how to discover it.</summary>
    public string Describe()
    {
        switch (Kind)
        {
            case RequirementKind.RegionRestored:
                return $"Restore {RegionCatalog.Get(Region).TitleInSentence}";
            case RequirementKind.Achievement:
                var a = AchievementCatalog.Find(AchievementId);
                return a is null ? "Earn an achievement" : $"Earn the achievement \"{a.Name}\"";
            case RequirementKind.CampaignComplete:
                return "Finish the campaign";
            default:
                return string.Empty;
        }
    }
}

/// <summary>One purchasable step of an item: what it costs, what it gives, and what must come first.</summary>
public sealed record ShopTier(int Price, string Effect, Requirement Requires);

/// <summary>An item in the Workshop. <see cref="SaveKey"/> holds the level owned (0 = none); existing keys never change.</summary>
public sealed record ShopItem(
    string Id, string Title, string Description, ItemCategory Category, string SaveKey,
    string BaseEffect, IReadOnlyList<ShopTier> Tiers)
{
    public int MaxLevel => Tiers.Count;
}

/// <summary>
/// Every item for sale, as data. Prices and requirements live here and nowhere else; the page only shows what
/// <see cref="ShopService"/> reports. See docs/story/economy.md for why the numbers are what they are.
/// </summary>
public static class ShopCatalog
{
    public const string Preview = "preview";
    public const string PopFlair = "popflair";
    public const string ExtraMoves = "extramoves";
    public const string ColourLens = "colourlens";
    public const string ChainSense = "chainsense";
    public const string PrismReserve = "prismreserve";

    private static ShopTier T(int price, string effect, Requirement? requires = null) =>
        new(price, effect, requires ?? Requirement.None);

    public static IReadOnlyList<ShopItem> Items { get; } = new[]
    {
        // ---- The three original upgrades. Keys, prices and meaning are exactly as shipped; Lookout gains two later levels. ----
        new ShopItem(Preview, "Lookout", "See more of the pieces that are coming.", ItemCategory.Gameplay,
            SaveKeys.UpgradePreview, "Next piece",
            new[]
            {
                T(120, "Next 2 pieces"),
                T(400, "Next 3 pieces"),
                T(900, "Next 4 pieces", Requirement.RegionRestored(3)),
                T(1500, "Next 5 pieces", Requirement.RegionRestored(6)),
            }),

        new ShopItem(PopFlair, "Pop Flair", "Fancier effects when blocks burst.", ItemCategory.Gameplay,
            SaveKeys.UpgradePopFlair, "Neon pop",
            new[] { T(80, "Shockwaves"), T(250, "Sparks and screen shake"), T(600, "Confetti finale") }),

        new ShopItem(ExtraMoves, "Spare Moves", "Extra moves on every Story level.", ItemCategory.Gameplay,
            SaveKeys.UpgradeExtraMoves, "No bonus",
            new[] { T(150, "+2 moves per level"), T(450, "+4 moves per level"), T(1000, "+6 moves per level") }),

        // ---- New helpers. They show what is already on the board; none of them plays the level for you. ----
        new ShopItem(ColourLens, "Colour Lens", "Outlines groups that are a block or two short of a burst.", ItemCategory.Gameplay,
            SaveKeys.UpgradeColourLens, "Off",
            new[]
            {
                T(500, "Outlines groups of 3", Requirement.RegionRestored(2)),
                T(1100, "Outlines groups of 2 and 3", Requirement.RegionRestored(4)),
            }),

        new ShopItem(ChainSense, "Chain Sense", "The landing outline glows when dropping where the piece is now would start a chain.", ItemCategory.Gameplay,
            SaveKeys.UpgradeChainSense, "Off",
            new[] { T(700, "Landing glow on chains", Requirement.RegionRestored(3)) }),

        // ---- Prism Shift's upgrade. The shift itself is a consumable (see ConsumableCatalog); this adds free ones each level. ----
        new ShopItem(PrismReserve, "Prism Reserve", "Free Prism Shifts on every level that allows helpers, with no stock needed.", ItemCategory.Gameplay,
            SaveKeys.UpgradePrismReserve, "None",
            new[]
            {
                T(600, "1 free Prism Shift per level", Requirement.RegionRestored(5)),
                T(1200, "2 free Prism Shifts per level", Requirement.RegionRestored(7)),
            }),
    };

    public static ShopItem? Find(string id) => Items.FirstOrDefault(i => i.Id == id);

    public static ShopItem Get(string id) => Find(id) ?? throw new ArgumentException($"No shop item '{id}'.", nameof(id));

    /// <summary>The unlock id an achievement is stored under (see <see cref="UnlockStore"/>).</summary>
    public static string AchievementUnlockId(string achievementId) => "ach_" + achievementId;

    /// <summary>The gold paid when a region is restored for the first time (live play only).</summary>
    public static int RegionClearGold(int regionNumber) => 25 + 25 * regionNumber;
}
