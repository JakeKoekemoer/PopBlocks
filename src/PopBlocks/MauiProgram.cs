using PopBlocks.Engine.Services;
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

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<GameViewModel>();
        builder.Services.AddTransient<LevelSelectPage>();
        builder.Services.AddTransient<ShopPage>();
        builder.Services.AddTransient<AboutPage>();

        return builder.Build();
    }
}
