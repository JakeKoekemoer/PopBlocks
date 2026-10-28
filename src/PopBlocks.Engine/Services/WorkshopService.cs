using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>
/// The tabs of Bram's Workshop. The Archive is not here on purpose: it opens from the main menu, and the workshop
/// does not keep a second door to it.
/// </summary>
public enum WorkshopTab
{
    /// <summary>Permanent upgrades: Lookout, Pop Flair, Spare Moves, Colour Lens, Chain Sense, Prism Reserve.</summary>
    Upgrades,

    /// <summary>Looks: block skins, burst styles, boards, piece frames and Pip's accessories.</summary>
    Looks,

    /// <summary>Helpers that are used up: Second Chance and Prism Shift.</summary>
    Supplies,

    /// <summary>Achievements and the journal: small proofs of things you have done, and your bests.</summary>
    Trophies,
}

/// <summary>
/// One tab of Bram's Workshop and the lamp that burns over it. A lamp lights as regions are restored; that is its whole
/// job. Every tab opens from the start whether or not its lamp is lit, so the workshop is something to watch grow and
/// never a lock in front of story, accessibility or settings.
/// </summary>
public sealed record WorkshopStation(string Id, string Name, string Description, int RegionNeeded, WorkshopTab Tab);

/// <summary>Derives how the workshop looks from campaign progress. It saves nothing and costs no gold.</summary>
public sealed class WorkshopService
{
    public static IReadOnlyList<WorkshopStation> AllStations { get; } = new[]
    {
        new WorkshopStation("upgrades", "Upgrades", "Kept for good. They show you more; they never play for you.", 0, WorkshopTab.Upgrades),
        new WorkshopStation("looks", "Looks", "Skins, bursts, boards, frames and Pip's accessories, found along the way.", 3, WorkshopTab.Looks),
        new WorkshopStation("supplies", "Supplies", "Careful helpers: they give you a second try or a better colour. They never play for you.", 5, WorkshopTab.Supplies),
        new WorkshopStation("trophies", "Trophies", "Small proofs of things you have done, and your journey so far in numbers.", 6, WorkshopTab.Trophies),
    };

    private readonly CampaignService _campaign;

    public WorkshopService(CampaignService campaign) => _campaign = campaign;

    public bool IsLit(WorkshopStation station) =>
        station.RegionNeeded == 0 || RegionCatalog.Progress(RegionCatalog.Get(station.RegionNeeded), HighestCleared).IsRestored;

    private int HighestCleared => _campaign.HighestCleared;

    public int LitCount => AllStations.Count(IsLit);

    public bool FullyRestored => AllStations.All(IsLit);

    /// <summary>The next station still to light, with the region that lights it. Null once everything is lit.</summary>
    public WorkshopStation? NextToLight => AllStations.FirstOrDefault(s => !IsLit(s));
}
