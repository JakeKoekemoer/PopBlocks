using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class WorkshopTests
{
    private static (WorkshopService Workshop, PlayerProgress Progress) Make(int highest)
    {
        var settings = new InMemorySettings();
        if (highest > 0) settings.SetInt(PlayerProgress.HighestClearedKey, highest);
        var progress = new PlayerProgress(settings);
        return (new WorkshopService(new CampaignService(new UnlockStore(settings), progress)), progress);
    }

    [Fact]
    public void A_new_player_finds_only_the_upgrades_lamp_lit()
    {
        var (w, _) = Make(0);
        Assert.Equal(1, w.LitCount);
        Assert.True(w.IsLit(WorkshopService.AllStations[0]));
        Assert.Equal(WorkshopTab.Upgrades, WorkshopService.AllStations[0].Tab);
        Assert.Equal("looks", w.NextToLight!.Id);
    }

    [Fact]
    public void Stations_light_as_each_region_is_restored()
    {
        for (int region = 1; region <= 8; region++)
        {
            var (w, _) = Make(RegionCatalog.Get(region).LastLevel);
            foreach (var s in WorkshopService.AllStations)
                Assert.Equal(s.RegionNeeded <= region, w.IsLit(s));
        }
    }

    [Fact]
    public void Restoring_the_last_region_lights_the_whole_workshop()
    {
        var (w, _) = Make(RegionCatalog.LastCampaignLevel);
        Assert.True(w.FullyRestored);
        Assert.Null(w.NextToLight);
    }

    [Fact]
    public void Being_one_level_short_of_a_region_does_not_light_it()
    {
        var (w, _) = Make(RegionCatalog.Get(3).LastLevel - 1);
        Assert.False(w.IsLit(WorkshopService.AllStations.First(s => s.RegionNeeded == 3)));
    }

    [Fact]
    public void Station_ids_and_tabs_are_unique_and_there_is_one_station_per_tab()
    {
        Assert.Equal(WorkshopService.AllStations.Count, WorkshopService.AllStations.Select(s => s.Id).Distinct().Count());
        Assert.Equal(WorkshopService.AllStations.Count, WorkshopService.AllStations.Select(s => s.Tab).Distinct().Count());
        foreach (var tab in Enum.GetValues<WorkshopTab>())
            Assert.Single(WorkshopService.AllStations, s => s.Tab == tab);
    }

    [Fact]
    public void The_workshop_never_stands_between_the_player_and_the_story_or_settings()
    {
        // Nothing in the station list costs gold or has a price: it is derived, display-only state, and a lamp only
        // ever depends on a region that exists.
        Assert.All(WorkshopService.AllStations, s => Assert.True(s.RegionNeeded is >= 0 and <= 8));
        Assert.Contains(WorkshopService.AllStations, s => s.Tab == WorkshopTab.Trophies);
    }

    [Fact]
    public void The_archive_has_one_door_and_it_is_on_the_menu()
    {
        // The Archive is opened from the main menu; the workshop has no station that duplicates it.
        Assert.DoesNotContain(WorkshopService.AllStations, s => s.Id.Contains("archive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(WorkshopService.AllStations, s => s.Name.Contains("archive", StringComparison.OrdinalIgnoreCase));
    }
}
