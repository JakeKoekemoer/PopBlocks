namespace PopBlocks;

/// <summary>
/// Startup breadcrumbs and last-chance exception logging. Everything goes to logcat under the tag
/// "PopBlocks" and nowhere else; nothing leaves the device.
/// View with:  adb logcat -s PopBlocks
/// </summary>
public static class Diagnostics
{
    private const string Tag = "PopBlocks";

    public static void Crumb(string message) => Android.Util.Log.Info(Tag, message);

    public static void Error(string where, Exception? ex) =>
        Android.Util.Log.Error(Tag, $"{where}: {ex}");

    /// <summary>Hooks managed exception events so a crash names its real exception in logcat.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Error("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
            Error("TaskScheduler.UnobservedTaskException", e.Exception);

        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            Error("AndroidEnvironment.UnhandledExceptionRaiser", e.Exception);
    }
}
