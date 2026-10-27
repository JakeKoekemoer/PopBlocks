using Android.App;
using Android.Runtime;

namespace PopBlocks;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        Diagnostics.Install();
        Diagnostics.Crumb("MainApplication ctor");
    }

    protected override MauiApp CreateMauiApp()
    {
        Diagnostics.Crumb("CreateMauiApp: start");
        var app = MauiProgram.CreateMauiApp();
        Diagnostics.Crumb("CreateMauiApp: built");
        return app;
    }
}
