using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

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
}
