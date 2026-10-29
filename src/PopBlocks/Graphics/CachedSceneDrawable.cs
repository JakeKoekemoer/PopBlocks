using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using PopBlocks.Engine.Story;
using IImage = Microsoft.Maui.Graphics.IImage;
using Region = PopBlocks.Engine.Story.Region;

namespace PopBlocks.Graphics;

/// <summary>
/// A region picture that is drawn once into an image and then only blitted. The main menu uses it behind the Story card:
/// the picture is static, so it is rendered again only when the size, the region or its state changes.
/// </summary>
public sealed class CachedSceneDrawable : IDrawable
{
    private readonly RegionSceneDrawable _scene = new();
    private IImage? _image;
    private int _width, _height;
    private int _region = -1;
    private RegionState _state = RegionState.Faded;
    private bool _cacheBroken;

    /// <summary>Which picture to show. Does nothing (and returns false) when it is the one already cached.</summary>
    public bool Show(Region region, RegionState state)
    {
        if (region.Number == _region && state == _state) return false;
        _region = region.Number;
        _state = state;
        _scene.Region = region;
        _scene.State = state;
        Invalidate();
        return true;
    }

    public void Invalidate()
    {
        _image?.Dispose();
        _image = null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_region < 0 || dirtyRect.Width < 20f || dirtyRect.Height < 20f) return;

        int w = (int)MathF.Ceiling(dirtyRect.Width), h = (int)MathF.Ceiling(dirtyRect.Height);
        if (!_cacheBroken && (_image is null || w != _width || h != _height))
        {
            _width = w;
            _height = h;
            _image?.Dispose();
            _image = Render(w, h);
        }

        if (_image is not null)
        {
            try
            {
                canvas.DrawImage(_image, dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
                return;
            }
            catch (Exception e)
            {
                // Never take the menu down over a background picture: forget the image and draw live from now on.
                Diagnostics.Error("CachedSceneDrawable.Draw", e);
                _image = null;
                _cacheBroken = true;
            }
        }
        _scene.Draw(canvas, dirtyRect);   // if the offscreen image could not be made or used, draw it live
    }

    private IImage? Render(int width, int height)
    {
        try
        {
            float density = (float)Math.Clamp(DeviceDisplay.Current.MainDisplayInfo.Density, 1.0, 3.0);
            // disposeBitmap: false. The returned image keeps using this bitmap after the context is gone; with the default
            // (true) the bitmap is released here and drawing the image later crashes the app.
            using var context = new PlatformBitmapExportContext((int)(width * density), (int)(height * density), density, disposeBitmap: false);
            context.Canvas.Scale(density, density);
            _scene.Draw(context.Canvas, new RectF(0, 0, width, height));
            return context.Image;
        }
        catch (Exception e)
        {
            Diagnostics.Error("CachedSceneDrawable.Render", e);
            return null;
        }
    }
}
