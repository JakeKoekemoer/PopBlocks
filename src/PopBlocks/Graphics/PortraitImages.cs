using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Storage;

namespace PopBlocks.Graphics;

/// <summary>
/// The painted portraits of Mara and Bram. They are the same PNG files the website shows (Resources/Raw/portrait_mara.png
/// and portrait_bram.png, packaged as app assets), so the site and the game always agree. Until a picture has loaded, or if
/// it cannot be read, <see cref="PortraitDrawable"/> falls back to the tinted stand-in.
/// </summary>
public static class PortraitImages
{
    private static int _started;

    public static Microsoft.Maui.Graphics.IImage? Mara { get; private set; }

    public static Microsoft.Maui.Graphics.IImage? Bram { get; private set; }

    /// <summary>Loads both pictures once (later calls do nothing). <paramref name="loaded"/> runs after, to redraw.</summary>
    public static async Task LoadAsync(Action? loaded = null)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        Mara = await TryLoad("portrait_mara.png");
        Bram = await TryLoad("portrait_bram.png");
        loaded?.Invoke();
    }

    private static async Task<Microsoft.Maui.Graphics.IImage?> TryLoad(string name)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(name);
            return PlatformImage.FromStream(stream);
        }
        catch (Exception ex)
        {
            Diagnostics.Crumb($"Portrait {name} could not be loaded: {ex.Message}");
            return null;
        }
    }
}
