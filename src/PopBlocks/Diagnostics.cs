namespace PopBlocks;

/// <summary>
/// Startup breadcrumbs and last-chance exception logging. Everything goes to logcat under the tag
/// "PopBlocks" and nowhere else; nothing leaves the device.
/// View with:  adb logcat -s PopBlocks
/// </summary>
public static class Diagnostics
{
    private const string Tag = "PopBlocks";

    // ---- Repaint counter (debug builds only) ----
    // Counts how often the board is drawn in the last minute. A board that is idle should read about zero; if it does not,
    // something is keeping a repaint timer alive. The calls are removed from release builds.
    private static readonly int[] FrameSeconds = new int[60];
    private static long _frameSecond = -1;

    [System.Diagnostics.Conditional("DEBUG")]
    public static void CountFrame()
    {
        long second = Environment.TickCount64 / 1000;
        if (second != _frameSecond)
        {
            // Clear the seconds that passed without a frame.
            for (long s = _frameSecond + 1; s <= second && s - _frameSecond <= 60; s++) FrameSeconds[(int)(s % 60)] = 0;
            _frameSecond = second;
        }
        FrameSeconds[(int)(second % 60)]++;
    }

    /// <summary>Board frames drawn in the last 60 seconds (always 0 in a release build).</summary>
    public static int FramesPerMinute()
    {
        long now = Environment.TickCount64 / 1000;
        int total = 0;
        for (long s = Math.Max(0, now - 59); s <= now; s++)
            if (_frameSecond >= 0 && s <= _frameSecond) total += FrameSeconds[(int)(s % 60)];
        return total;
    }

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
