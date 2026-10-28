using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace PopBlocks.Graphics;

/// <summary>
/// The detailed block-face patterns of the v1.1 block sets (Starfield, Tidepool, Granite, Ember, Blossom). They have many
/// small parts, so each one is painted once per block set, colour and size into an image and then only blitted: drawing a
/// block costs one <c>DrawImage</c>. If an image cannot be made the pattern is painted straight onto the canvas instead.
/// Every pattern keeps clear of the middle of the face, where the colour's symbol sits.
/// </summary>
public static class PatternCache
{
    private const int MaxEntries = 48;
    private static readonly Dictionary<(string Set, CellColour Colour, int Pixels), IImage> Cache = new();

    /// <summary>Draws the pattern for <paramref name="colour"/> into the face square at (left, top) with side span.</summary>
    public static void Draw(ICanvas canvas, SkinPattern pattern, CellColour colour, float left, float top, float span)
    {
        var set = VisualDesign.SpecFor(colour);
        int px = Math.Max(8, (int)MathF.Round(span));
        var key = (set.Id, colour, px);

        if (!Cache.TryGetValue(key, out var image))
        {
            image = Render(pattern, colour, px);
            if (image is not null)
            {
                if (Cache.Count >= MaxEntries) Clear();
                Cache[key] = image;
            }
        }

        if (image is not null)
        {
            canvas.DrawImage(image, left, top, span, span);
            return;
        }

        canvas.SaveState();
        canvas.Translate(left, top);
        RichPatterns.Paint(canvas, pattern, colour, span);
        canvas.RestoreState();
    }

    /// <summary>Forgets every image (a block set was changed or the app is low on memory).</summary>
    public static void Clear()
    {
        foreach (var image in Cache.Values) image.Dispose();
        Cache.Clear();
    }

    private static IImage? Render(SkinPattern pattern, CellColour colour, int px)
    {
        try
        {
            float density = (float)Math.Clamp(DeviceDisplay.Current.MainDisplayInfo.Density, 1.0, 3.0);
            int pixels = (int)(px * density);
            using var context = new PlatformBitmapExportContext(pixels, pixels, density);
            context.Canvas.Scale(density, density);
            RichPatterns.Paint(context.Canvas, pattern, colour, px);
            return context.Image;
        }
        catch (Exception e)
        {
            Diagnostics.Error("PatternCache.Render", e);
            return null;
        }
    }
}

/// <summary>The patterns themselves, painted in a square of side <c>s</c> with its corner at the origin.</summary>
public static class RichPatterns
{
    public static void Paint(ICanvas canvas, SkinPattern pattern, CellColour colour, float s)
    {
        var face = VisualDesign.BaseColour(colour);
        switch (pattern)
        {
            case SkinPattern.Stars: Stars(canvas, colour, s); break;
            case SkinPattern.Waves: Waves(canvas, s); break;
            case SkinPattern.Rock: Rock(canvas, colour, s); break;
            case SkinPattern.Flame: Flame(canvas, face, s); break;
            case SkinPattern.Flower: Flower(canvas, face, s); break;
        }
    }

    private static Color Mix(Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t, 1f);

    private static float N(CellColour colour, int i, int salt) => BoardSpace.Noise((int)colour * 31 + i, salt);

    // ---- Starfield: the face darkens towards the bottom; tiny stars and one sparkle around the edge ----

    private static void Stars(ICanvas canvas, CellColour colour, float s)
    {
        // Darker towards the bottom (35% at the very bottom), in bands that stay below the symbol.
        const int bands = 7;
        for (int i = 0; i < bands; i++)
        {
            float t = (i + 1) / (float)bands;
            float y = s * (0.66f + 0.30f * i / bands);
            canvas.FillColor = Colors.Black.WithAlpha(0.35f * t / bands * 1.6f);
            canvas.FillRectangle(s * 0.06f, y, s * 0.88f, s * 0.97f - y);
        }

        // Six white dots, 1 to 1.6 units across, on a ring around the symbol.
        for (int i = 0; i < 6; i++)
        {
            float angle = N(colour, i, 1) * MathF.Tau;
            float radius = s * (0.34f + 0.12f * N(colour, i, 2));
            float d = 1f + 0.6f * N(colour, i, 3);
            canvas.FillColor = Colors.White.WithAlpha(0.5f + 0.4f * N(colour, i, 4));
            canvas.FillEllipse(s / 2f + MathF.Cos(angle) * radius - d / 2f, s / 2f + MathF.Sin(angle) * radius - d / 2f, d, d);
        }

        // One four-point sparkle, up and to the right.
        float cx = s * 0.78f, cy = s * 0.24f, arm = s * 0.10f;
        canvas.StrokeColor = Colors.White.WithAlpha(0.9f);
        canvas.StrokeSize = MathF.Max(1f, s * 0.03f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx - arm, cy, cx + arm, cy);
        canvas.DrawLine(cx, cy - arm, cx, cy + arm);
        canvas.StrokeSize = MathF.Max(0.8f, s * 0.018f);
        canvas.DrawLine(cx - arm * 0.5f, cy - arm * 0.5f, cx + arm * 0.5f, cy + arm * 0.5f);
        canvas.DrawLine(cx - arm * 0.5f, cy + arm * 0.5f, cx + arm * 0.5f, cy - arm * 0.5f);
    }

    // ---- Tidepool: two rolling wave bands in the lower half and a curved glass highlight ----

    private static void Waves(ICanvas canvas, float s)
    {
        canvas.StrokeLineCap = LineCap.Round;
        for (int band = 0; band < 2; band++)
        {
            float baseY = s * (band == 0 ? 0.66f : 0.82f);
            canvas.StrokeColor = Colors.White.WithAlpha(band == 0 ? 0.20f : 0.35f);
            canvas.StrokeSize = MathF.Max(1.5f, s * 0.07f);
            float px = s * 0.10f, py = baseY;
            const int steps = 14;
            for (int i = 1; i <= steps; i++)
            {
                float x = s * (0.10f + 0.80f * i / steps);
                float y = baseY + MathF.Sin(i / (float)steps * MathF.Tau * 1.5f + band * 1.2f) * s * 0.035f;
                canvas.DrawLine(px, py, x, y);
                px = x;
                py = y;
            }
        }

        // A curved glass highlight across the top left.
        canvas.StrokeColor = Colors.White.WithAlpha(0.40f);
        canvas.StrokeSize = MathF.Max(1.5f, s * 0.05f);
        canvas.DrawArc(s * 0.12f, s * 0.12f, s * 0.62f, s * 0.62f, 150, 245, true, false);
    }

    // ---- Granite: speckles, one hairline crack, a chiselled two-tone edge ----

    private static void Rock(ICanvas canvas, CellColour colour, float s)
    {
        for (int i = 0; i < 26; i++)
        {
            float angle = N(colour, i, 1) * MathF.Tau;
            float radius = s * (0.30f + 0.18f * N(colour, i, 2));
            float d = s * (0.03f + 0.03f * N(colour, i, 3));
            canvas.FillColor = i % 2 == 0 ? Colors.Black.WithAlpha(0.25f) : Colors.White.WithAlpha(0.15f);
            canvas.FillEllipse(s / 2f + MathF.Cos(angle) * radius - d / 2f, s / 2f + MathF.Sin(angle) * radius - d / 2f, d, d);
        }

        canvas.StrokeColor = Colors.Black.WithAlpha(0.35f);
        canvas.StrokeSize = MathF.Max(0.8f, s * 0.018f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(s * 0.20f, s * 0.04f, s * 0.14f, s * 0.26f);
        canvas.DrawLine(s * 0.14f, s * 0.26f, s * 0.22f, s * 0.42f);
        canvas.DrawLine(s * 0.22f, s * 0.42f, s * 0.12f, s * 0.60f);

        canvas.StrokeSize = MathF.Max(1.2f, s * 0.05f);
        canvas.StrokeColor = Colors.White.WithAlpha(0.32f);
        canvas.DrawLine(s * 0.04f, s * 0.04f, s * 0.96f, s * 0.04f);
        canvas.DrawLine(s * 0.04f, s * 0.04f, s * 0.04f, s * 0.96f);
        canvas.StrokeColor = Colors.Black.WithAlpha(0.32f);
        canvas.DrawLine(s * 0.04f, s * 0.96f, s * 0.96f, s * 0.96f);
        canvas.DrawLine(s * 0.96f, s * 0.04f, s * 0.96f, s * 0.96f);
    }

    // ---- Ember: a char ring, a hot core low in the middle, three tongues of flame from the bottom edge ----

    private static void Flame(ICanvas canvas, Color face, float s)
    {
        canvas.StrokeColor = Colors.Black.WithAlpha(0.32f);
        canvas.StrokeSize = MathF.Max(1.5f, s * 0.06f);
        canvas.DrawRoundedRectangle(s * 0.04f, s * 0.04f, s * 0.92f, s * 0.92f, s * 0.18f);

        var tongue = Mix(face, Colors.White, 0.30f);
        canvas.FillColor = tongue;
        for (int i = 0; i < 3; i++)
        {
            float cx = s * (0.24f + 0.26f * i);
            float height = s * (i == 1 ? 0.30f : 0.22f);
            var path = new PathF();
            path.MoveTo(cx - s * 0.09f, s * 0.95f);
            path.QuadTo(cx - s * 0.07f, s * 0.95f - height * 0.55f, cx, s * 0.95f - height);
            path.QuadTo(cx + s * 0.07f, s * 0.95f - height * 0.55f, cx + s * 0.09f, s * 0.95f);
            path.Close();
            canvas.FillPath(path);
        }

        // The hot core, 40% lighter.
        canvas.FillColor = Mix(face, Colors.White, 0.40f).WithAlpha(0.8f);
        canvas.FillEllipse(s * 0.30f, s * 0.74f, s * 0.40f, s * 0.20f);
    }

    // ---- Blossom: a five-petal flower with a pale centre, up in the right-hand corner ----

    private static void Flower(ICanvas canvas, Color face, float s)
    {
        float cx = s * 0.74f, cy = s * 0.26f, r = s * 0.14f;
        canvas.FillColor = Mix(face, Colors.White, 0.45f);
        for (int i = 0; i < 5; i++)
        {
            float a = -MathF.PI / 2f + i * MathF.Tau / 5f;
            float px = cx + MathF.Cos(a) * r * 0.62f, py = cy + MathF.Sin(a) * r * 0.62f;
            canvas.FillEllipse(px - r * 0.42f, py - r * 0.42f, r * 0.84f, r * 0.84f);
        }
        canvas.FillColor = Color.FromArgb("#FFF2B3");
        canvas.FillEllipse(cx - r * 0.30f, cy - r * 0.30f, r * 0.60f, r * 0.60f);
    }
}
