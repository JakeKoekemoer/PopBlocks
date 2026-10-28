using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Services;
using PopBlocks.ViewModels;
using PopBlocks.Views;

namespace PopBlocks;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Local-only services. No network, analytics or ad SDKs anywhere.
        // The real store is Android Preferences. PersistentSettings wraps it and keeps a durable copy of the save in a file that
        // outlives an uninstall; every service gets that wrapper as its ILocalSettings.
        builder.Services.AddSingleton<LocalSettingsService>();
#if ANDROID
        builder.Services.AddSingleton<ISaveFileStore, AndroidSaveFileStore>();
#else
        builder.Services.AddSingleton<ISaveFileStore, NullSaveFileStore>();
#endif
        builder.Services.AddSingleton<PersistentSettings>(sp => new PersistentSettings(
            sp.GetRequiredService<LocalSettingsService>(), sp.GetRequiredService<ISaveFileStore>()));
        builder.Services.AddSingleton<ILocalSettings>(sp => sp.GetRequiredService<PersistentSettings>());
        builder.Services.AddSingleton<HighScoreService>();
        builder.Services.AddSingleton<GameSettings>();
        builder.Services.AddSingleton<PlayerProgress>();
        builder.Services.AddSingleton<ShopService>();
        builder.Services.AddSingleton<ConsumableInventory>();
        builder.Services.AddSingleton<UnlockStore>();
        builder.Services.AddSingleton<TutorialProgress>();
        builder.Services.AddSingleton<CampaignService>();
        builder.Services.AddSingleton<CosmeticLoadout>();
        builder.Services.AddSingleton<CosmeticShop>();
        builder.Services.AddSingleton<DevTools>();
        builder.Services.AddSingleton<LifetimeStats>();
        builder.Services.AddSingleton<EndingService>();
        builder.Services.AddSingleton<AchievementService>();
        builder.Services.AddSingleton<WorkshopService>();
        builder.Services.AddSingleton<PlayerIdentity>(sp => new PlayerIdentity(sp.GetRequiredService<ILocalSettings>()));
        builder.Services.AddSingleton<StoryLevels>(sp => new StoryLevels(
            sp.GetRequiredService<ILocalSettings>(), sp.GetRequiredService<PlayerProgress>(), sp.GetRequiredService<PlayerIdentity>()));
        builder.Services.AddSingleton<Consolation>(sp => new Consolation(sp.GetRequiredService<ILocalSettings>(), sp.GetRequiredService<PlayerProgress>()));
        builder.Services.AddSingleton<ClassicGold>(sp => new ClassicGold(sp.GetRequiredService<ILocalSettings>(), sp.GetRequiredService<PlayerProgress>()));
#if ANDROID
        builder.Services.AddSingleton<ISoundPlayer, AndroidSoundPlayer>();
#else
        builder.Services.AddSingleton<ISoundPlayer, NullSoundPlayer>();
#endif

        // Music and sound. AudioSettings is the player's volumes and mutes; GameAudio is the one object the rest of the app talks to.
        // The old SoundPool player above is only a fallback now, used if the new audio output cannot start.
        builder.Services.AddSingleton<AudioSettings>(sp => new AudioSettings(
            sp.GetRequiredService<ILocalSettings>(), sp.GetRequiredService<GameSettings>()));
#if ANDROID
        builder.Services.AddSingleton<IAudioOutput, AndroidAudioOutput>();
#else
        builder.Services.AddSingleton<IAudioOutput, NullAudioOutput>();
#endif
        builder.Services.AddSingleton<GameAudio>(sp => new GameAudio(
            sp.GetRequiredService<AudioSettings>(), sp.GetRequiredService<IAudioOutput>(),
            new LazySoundPlayer(() => sp.GetRequiredService<ISoundPlayer>())));

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<GameViewModel>();
        builder.Services.AddTransient<WorkshopPage>();
        builder.Services.AddTransient<ShopPanel>();
        builder.Services.AddTransient<WardrobePanel>();
        builder.Services.AddTransient<AchievementsPanel>();
        builder.Services.AddTransient<JournalPanel>();
        builder.Services.AddTransient<TrophiesPanel>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<WorldMapPage>();
        builder.Services.AddTransient<ArchivePage>();
        builder.Services.AddTransient<DevMenuPage>();

        var app = builder.Build();

        // A fresh install picks its progress back up from the save file kept outside the app (see PersistentSettings). This has to
        // happen before anything reads saved data, and before the version is stamped, which is how a fresh install is recognised.
        var persistent = app.Services.GetRequiredService<PersistentSettings>();
        persistent.RestoreIfFresh();

        // Stamp the save format version (a no-op once current) before any page reads saved data.
        SaveMigrator.Run(persistent);

        // Players who finished a region before the story existed still get its fragment, journal entry and board style.
        app.Services.GetRequiredService<CampaignService>().Reconcile();

        // Bring the save file up to date shortly after start-up (this also creates it for players who had none).
        persistent.RequestSave();

        // Start the music engine. If the device cannot give us an audio output the game simply stays quiet (or falls back to the
        // old effects player); it never stops the app from starting.
        try
        {
            var audio = app.Services.GetRequiredService<GameAudio>();
            audio.Failed += (where, e) => Diagnostics.Error("Audio: " + where, e);
            audio.Start();
        }
        catch (Exception e)
        {
            Diagnostics.Error("MauiProgram: audio start", e);
        }
        return app;
    }
}
