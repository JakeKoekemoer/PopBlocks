using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class AccessibilitySettingsTests
{
    [Fact]
    public void An_old_save_with_the_ghost_on_gets_the_outline_guide()
    {
        var s = new InMemorySettings();
        s.SetBool(SaveKeys.ShowGhost, true);
        var settings = new GameSettings(s);
        Assert.Equal(1, settings.LandingGuideStyle);
        Assert.True(settings.ShowGhost);
    }

    [Fact]
    public void An_old_save_with_the_ghost_off_keeps_the_guide_off()
    {
        var s = new InMemorySettings();
        s.SetBool(SaveKeys.ShowGhost, false);
        var settings = new GameSettings(s);
        Assert.Equal(0, settings.LandingGuideStyle);
        Assert.False(settings.ShowGhost);
    }

    [Fact]
    public void A_fresh_install_defaults_to_the_outline_and_full_motion()
    {
        var settings = new GameSettings(new InMemorySettings());
        Assert.Equal(1, settings.LandingGuideStyle);
        Assert.False(settings.ReducedMotion);
    }

    [Fact]
    public void Choosing_a_style_is_remembered_and_keeps_the_old_key_meaningful()
    {
        var s = new InMemorySettings();
        var settings = new GameSettings(s) { LandingGuideStyle = 2 };
        Assert.True(s.GetBool(SaveKeys.ShowGhost, false));

        var reloaded = new GameSettings(s);
        Assert.Equal(2, reloaded.LandingGuideStyle);

        reloaded.LandingGuideStyle = 0;
        Assert.False(s.GetBool(SaveKeys.ShowGhost, true));
        Assert.Equal(0, new GameSettings(s).LandingGuideStyle);
    }

    [Fact]
    public void The_old_on_off_switch_still_works_and_does_not_downgrade_a_bold_guide()
    {
        var settings = new GameSettings(new InMemorySettings()) { LandingGuideStyle = 2 };
        settings.ShowGhost = true;
        Assert.Equal(2, settings.LandingGuideStyle);
        settings.ShowGhost = false;
        Assert.Equal(0, settings.LandingGuideStyle);
        settings.ShowGhost = true;
        Assert.Equal(1, settings.LandingGuideStyle);
    }

    [Fact]
    public void A_nonsense_saved_style_falls_back()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.LandingGuideStyle, 9);
        Assert.Equal(1, new GameSettings(s).LandingGuideStyle);
        Assert.Equal(2, new GameSettings(s) { LandingGuideStyle = 7 }.LandingGuideStyle);
    }

    [Fact]
    public void Reduced_motion_is_remembered()
    {
        var s = new InMemorySettings();
        new GameSettings(s) { ReducedMotion = true };
        Assert.True(new GameSettings(s).ReducedMotion);
    }

    [Fact]
    public void New_keys_are_registered_and_unique()
    {
        Assert.Contains(SaveKeys.LandingGuideStyle, SaveKeys.All);
        Assert.Contains(SaveKeys.ReducedMotion, SaveKeys.All);
        Assert.Equal(SaveKeys.All.Count, SaveKeys.All.Distinct().Count());
    }
}

public class JournalStatsTests
{
    [Fact]
    public void Journal_keys_are_registered()
    {
        Assert.Contains(SaveKeys.StatMostGoals, SaveKeys.All);
        for (int r = 1; r <= 8; r++) Assert.Contains(SaveKeys.StatRegionPlays(r), SaveKeys.All);
        Assert.Contains(SaveKeys.StatMostGoals, SaveKeys.StatKeys);
    }

    [Fact]
    public void Clearing_the_journal_clears_the_new_stats_too()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.StatMostGoals, 3);
        s.SetInt(SaveKeys.StatRegionPlays(2), 9);
        var stats = new LifetimeStats(s);
        Assert.Equal(3, stats.MostGoalsOnClearedLevel);
        Assert.Equal(9, stats.PlaysInRegion(2));
        stats.DevClear();
        Assert.Equal(0, stats.MostGoalsOnClearedLevel);
        Assert.Equal(0, stats.PlaysInRegion(2));
    }
}
