namespace PopBlocks;

/// <summary>
/// Development shortcuts (skip level, give gold, jump to a level, reset) exist only in Debug builds, or in any build
/// made with <c>-p:DevMenu=true</c>. A normal Release build compiles them out of view: the buttons never show.
/// </summary>
public static class DevMenu
{
#if DEBUG || DEVMENU
    public static bool Enabled { get; } = true;
#else
    public static bool Enabled { get; } = false;
#endif
}
