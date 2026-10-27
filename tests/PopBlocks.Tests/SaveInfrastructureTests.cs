using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class SaveInfrastructureTests
{
    [Fact]
    public void Existing_keys_have_not_been_renamed()
    {
        // These are the literal strings already on players' phones. Changing one loses their data.
        Assert.Equal("progress_highest", PlayerProgress.HighestClearedKey);
        Assert.Equal("gold", PlayerProgress.GoldKey);
        Assert.Equal("best_score", HighScoreService.BestScoreKey);
        Assert.Equal("blast_effects", GameSettings.BlastEffectsKey);
        Assert.Equal("show_ghost", GameSettings.ShowGhostKey);
        Assert.Equal("controls_collapsed", GameSettings.ControlsCollapsedKey);
        Assert.Equal("sound_effects", GameSettings.SoundEffectsKey);

        Assert.Equal(PlayerProgress.HighestClearedKey, SaveKeys.ProgressHighest);
        Assert.Equal(PlayerProgress.GoldKey, SaveKeys.Gold);
        Assert.Equal(HighScoreService.BestScoreKey, SaveKeys.BestScore);
        Assert.Equal(GameSettings.BlastEffectsKey, SaveKeys.BlastEffects);
        Assert.Equal(GameSettings.ShowGhostKey, SaveKeys.ShowGhost);
        Assert.Equal(GameSettings.ControlsCollapsedKey, SaveKeys.ControlsCollapsed);
        Assert.Equal(GameSettings.SoundEffectsKey, SaveKeys.SoundEffects);
    }

    [Fact]
    public void Shop_upgrade_keys_match_the_registry()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var shop = new ShopService(settings, progress);
        settings.SetInt(SaveKeys.UpgradePreview, 1);
        settings.SetInt(SaveKeys.UpgradePopFlair, 2);
        settings.SetInt(SaveKeys.UpgradeExtraMoves, 3);

        Assert.Equal(2, shop.PreviewCount);
        Assert.Equal(2, shop.FlairLevel);
        Assert.Equal(6, shop.ExtraMoves);
    }

    [Fact]
    public void Every_registered_key_is_unique_and_lowercase_with_underscores()
    {
        Assert.Equal(SaveKeys.All.Count, SaveKeys.All.Distinct().Count());
        foreach (var key in SaveKeys.All)
            Assert.True(key == key.ToLowerInvariant() && !key.Contains(' ') && !key.Contains('-'), key);
    }

    [Fact]
    public void Migrator_stamps_an_unversioned_save_without_touching_its_data()
    {
        var settings = new InMemorySettings();
        settings.SetInt(SaveKeys.ProgressHighest, 37);
        settings.SetInt(SaveKeys.Gold, 912);

        SaveMigrator.Run(settings);

        Assert.Equal(SaveKeys.CurrentSaveVersion, settings.GetInt(SaveKeys.SaveVersion, 0));
        Assert.Equal(37, settings.GetInt(SaveKeys.ProgressHighest, 0));
        Assert.Equal(912, settings.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(37, new PlayerProgress(settings).HighestCleared);
    }

    [Fact]
    public void Migrator_does_nothing_on_a_current_save()
    {
        var settings = new InMemorySettings();
        settings.SetInt(SaveKeys.SaveVersion, SaveKeys.CurrentSaveVersion);
        SaveMigrator.Run(settings);
        Assert.Equal(SaveKeys.CurrentSaveVersion, settings.GetInt(SaveKeys.SaveVersion, 0));
    }

    [Fact]
    public void UnlockStore_unlocks_once_and_persists()
    {
        var settings = new InMemorySettings();
        var store = new UnlockStore(settings);
        var raised = new List<string>();
        store.Unlocked += raised.Add;

        Assert.False(store.Has("fragment_r1"));
        Assert.True(store.Unlock("fragment_r1"));
        Assert.False(store.Unlock("fragment_r1"));
        Assert.True(store.Has("fragment_r1"));
        Assert.Equal(new[] { "fragment_r1" }, raised);

        var second = new UnlockStore(settings);
        Assert.True(second.Has("fragment_r1"));
        Assert.Equal(1, second.Count);
    }

    [Fact]
    public void UnlockStore_keeps_many_ids_and_counts_a_subset()
    {
        var settings = new InMemorySettings();
        var store = new UnlockStore(settings);
        foreach (var id in new[] { "a", "b", "c" }) store.Unlock(id);

        var reloaded = new UnlockStore(settings);
        Assert.Equal(3, reloaded.Count);
        Assert.Equal(2, reloaded.CountOf(new[] { "a", "c", "zzz" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has,comma")]
    public void UnlockStore_rejects_invalid_ids(string id)
    {
        var store = new UnlockStore(new InMemorySettings());
        Assert.Throws<ArgumentException>(() => store.Unlock(id));
    }

    [Fact]
    public void UnlockStore_ignores_blank_entries_in_a_hand_edited_value()
    {
        var settings = new InMemorySettings();
        settings.SetString(SaveKeys.Unlocks, " a ,, b,");
        var store = new UnlockStore(settings);
        Assert.Equal(2, store.Count);
        Assert.True(store.Has("a"));
        Assert.True(store.Has("b"));
    }
}
