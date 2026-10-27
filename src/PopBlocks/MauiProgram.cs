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
        builder.Services.AddSingleton<ILocalSettings, LocalSettingsService>();
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
#if ANDROID
        builder.Services.AddSingleton<ISoundPlayer, AndroidSoundPlayer>();
#else
        builder.Services.AddSingleton<ISoundPlayer, NullSoundPlayer>();
#endif

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<GameViewModel>();
        builder.Services.AddTransient<LevelSelectPage>();
        builder.Services.AddTransient<ShopPage>();
        builder.Services.AddTransient<WorkshopPage>();
        builder.Services.AddTransient<AchievementsPage>();
        builder.Services.AddTransient<JournalPage>();
        builder.Services.AddTransient<WardrobePage>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<WorldMapPage>();
        builder.Services.AddTransient<RegionPage>();
        builder.Services.AddTransient<ArchivePage>();
        builder.Services.AddTransient<DevMenuPage>();

        var app = builder.Build();

        // Stamp the save format version (a no-op once current) before any page reads saved data.
        SaveMigrator.Run(app.Services.GetRequiredService<ILocalSettings>());

        // Players who finished a region before the story existed still get its fragment, journal entry and board style.
        app.Services.GetRequiredService<CampaignService>().Reconcile();
        return app;
    }
}
