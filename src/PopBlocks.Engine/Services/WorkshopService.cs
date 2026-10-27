using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum WorkshopTarget
{
    None,
    Shop,
    Cosmetics,
    Archive,
    Achievements,
    Journal,
}

/// <summary>
/// One corner of Bram's Workshop. A station lights up as regions are restored; that is its whole job. Every
/// destination (Archive, Journal, Achievements, the shop) opens from the start whether or not its station is lit,
/// so the workshop is something to watch grow and never a lock in front of story, accessibility or settings.
/// </summary>
public sealed record WorkshopStation(string Id, string Name, string Description, int RegionNeeded, WorkshopTarget Target);

/// <summary>Derives how the workshop looks from campaign progress. It saves nothing and costs no gold.</summary>
public sealed class WorkshopService
{
    public static IReadOnlyList<WorkshopStation> AllStations { get; } = new[]
    {
        new WorkshopStation("workbench", "The workbench", "Bram's tools: spare moves, a longer lookout, livelier pops.", 0, WorkshopTarget.Shop),
        new WorkshopStation("cosmetic_shelf", "The paint shelf", "Skins, bursts and boards found along the way.", 3, WorkshopTarget.Cosmetics),
        new WorkshopStation("archive_shelf", "The archive shelf", "Everything you have learned, kept where Bram can find it.", 4, WorkshopTarget.Archive),
        new WorkshopStation("mechanic_bench", "The mechanic's bench", "Careful helpers: they show what is on the board, or give you a second try. They never play for you.", 5, WorkshopTarget.Shop),
        new WorkshopStation("display_case", "The display case", "Small proofs of things you have done.", 6, WorkshopTarget.Achievements),
        new WorkshopStation("map_table", "The map table", "Your journey so far, in numbers.", 7, WorkshopTarget.Journal),
        new WorkshopStation("whole_workshop", "The whole workshop", "Every lamp lit. Nothing left to repair.", 8, WorkshopTarget.None),
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
