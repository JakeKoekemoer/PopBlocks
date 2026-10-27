using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Extensions.DependencyInjection;

namespace PopBlocks;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Diagnostics.Crumb("MainActivity.OnCreate: before base");
        base.OnCreate(savedInstanceState);
        Diagnostics.Crumb("MainActivity.OnCreate: after base");

        // Keep the screen awake while playing.
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        Diagnostics.Crumb("MainActivity.OnCreate: done");
    }

    /// <summary>
    /// Writes the save file before the app goes to the background, so a change made a moment ago is not lost if Android
    /// then closes the app (or the player uninstalls it straight away).
    /// </summary>
    protected override void OnPause()
    {
        base.OnPause();
        try
        {
            Microsoft.Maui.IPlatformApplication.Current?.Services.GetService<PopBlocks.Engine.Services.PersistentSettings>()?.Flush();
        }
        catch (Exception e)
        {
            Diagnostics.Error("MainActivity.OnPause: save file flush", e);
        }
    }
}
