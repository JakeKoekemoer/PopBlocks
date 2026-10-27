using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;

namespace PopBlocks.Graphics;

public enum PipMood { Calm, Happy, Worried, Curious, Excited, Sad }

/// <summary>The colour each speaker of a story panel is drawn in. Pip keeps the default (null).</summary>
public static class PortraitTints
{
    public static Color? For(string speakerId) => speakerId switch
    {
        Cast.Pip => null,
        Cast.Mara => Color.FromArgb("#5EEAD4"),
        Cast.Bram => Color.FromArgb("#FDBA74"),
        Cast.Warden or Cast.WardenRecording or Cast.WardenMessage => Color.FromArgb("#C4B5FD"),
        Cast.Voice => Color.FromArgb("#93C5FD"),
        _ => Color.FromArgb("#A5B4FC"),
    };
}

/// <summary>
/// Pip, the Spark, as set out in the artist brief: a small round light with large eyes, two short leaf antennae and a
/// little glow tail. Drawn with shapes only, so it stays readable as a flat silhouette at portrait size.
/// <see cref="Brightness"/> (0..1) is how much of the world has been restored, so Pip glows more as the story goes on.
/// The mood changes the mouth and brows, not only the colour, so it still reads without colour.
/// </summary>
public sealed class PipDrawable : IDrawable
{
    public float Brightness { get; set; } = 0.5f;
    public PipMood Mood { get; set; } = PipMood.Calm;

    /// <summary>
    /// Colour of the glow and body for someone who is not Pip (Mara, Bram...), so each speaker in a story panel
    /// looks like a different person. Null is Pip's own pale cyan. A tinted body is the plain stand-in: no leaves, no tail.
    /// </summary>
    public Color? Tint { get; set; }

    /// <summary>Something Pip wears (a cosmetic from the workshop). Drawn over the body; it never changes Pip's mood or face.</summary>
    public AccessoryStyle Accessory { get; set; } = AccessoryStyle.None;

    /// <summary>Regions restored, 0 to 8: each adds one small light in orbit, so Pip looks more detailed as the world returns.</summary>
    public int Detail { get; set; }

    /// <summary>
    /// Seconds on an animation clock. Pip bobs, blinks and sways while this is 0 or more. Negative (the default) is a
    /// still picture, which is what every page that does not run a clock gets.
    /// </summary>
    public float Time { get; set; } = -1f;

    /// <summary>Where Pip looks: -1 left, 0 ahead, 1 right.</summary>
    public float Look { get; set; }

    /// <summary>Pip glances up.</summary>
    public bool LookUp { get; set; }

    /// <summary>0..1: Pip hops up off the ground (a quick jump).</summary>
    public float Hop { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float d = MathF.Min(dirtyRect.Width, dirtyRect.Height);
        if (d < 8f) return;

        bool live = Time >= 0f;
        float cx = dirtyRect.X + dirtyRect.Width / 2f;
        float cy = dirtyRect.Y + dirtyRect.Height / 2f;
        float b = Math.Clamp(Brightness, 0f, 1f);
        float body = d * 0.27f;   // leaves above, tail below and the bob all fit inside the square

        if (live) cy += MathF.Sin(Time * 2.4f) * d * 0.022f;
        cy -= Math.Clamp(Hop, 0f, 1f) * d * 0.12f;
        float pulse = live ? 1f + 0.08f * MathF.Sin(Time * 3.1f) : 1f;

        // Glow: wider and stronger as the world recovers. A separate pass, so it can be toned down on its own.
        var glow = Tint ?? StoryPalette.Cyan;
        for (int ring = 4; ring >= 1; ring--)
        {
            float r = body * (1f + 0.35f * ring);
            canvas.FillColor = glow.WithAlpha((0.05f + 0.07f * b) * (5 - ring) / 4f * pulse);
            canvas.FillEllipse(cx - r, cy - r, r * 2f, r * 2f);
        }

        if (Tint is null)
        {
            DrawTail(canvas, cx, cy, body, glow);
            DrawLeaves(canvas, cx, cy, body);
        }

        // Body: slate-blue when the world is dim, pale cyan-white when it is bright. A stand-in keeps its own tint.
        canvas.FillColor = Tint is { } tint
            ? Blend(Blend(tint, Colors.White, 0.45f), tint, b)
            : Blend(Color.FromArgb("#9FB4C6"), Color.FromArgb("#E9FBFF"), b);
        canvas.FillEllipse(cx - body, cy - body, body * 2f, body * 2f);

        // A soft cyan core low in the body, then the highlight.
        if (Tint is null)
        {
            canvas.FillColor = StoryPalette.Cyan.WithAlpha(0.10f + 0.10f * b);
            canvas.FillEllipse(cx - body * 0.62f, cy - body * 0.30f, body * 1.24f, body * 1.10f);
        }
        canvas.FillColor = Colors.White.WithAlpha(0.35f);
        canvas.FillEllipse(cx - body * 0.62f, cy - body * 0.78f, body * 0.7f, body * 0.42f);

        // Eyes: large, with a blink every few seconds and a catch-light so they read as alive.
        var ink = Color.FromArgb("#1B2547");
        float eyeDx = body * 0.38f;
        float eyeR = body * (Mood == PipMood.Excited ? 0.17f : 0.14f);
        float eyeY = cy - body * 0.12f + (Mood == PipMood.Sad ? body * 0.05f : 0f) - (LookUp ? body * 0.10f : 0f);
        float ox = Math.Clamp(Look, -1f, 1f) * body * 0.10f;

        float open = 1f;
        if (live)
        {
            float ph = Time % 4.6f;
            if (ph < 0.16f) open = 0.12f + 0.88f * MathF.Abs(ph - 0.08f) / 0.08f;
        }

        canvas.FillColor = ink;
        canvas.FillEllipse(cx - eyeDx - eyeR + ox, eyeY - eyeR * open, eyeR * 2f, eyeR * 2f * open);
        canvas.FillEllipse(cx + eyeDx - eyeR + ox, eyeY - eyeR * open, eyeR * 2f, eyeR * 2f * open);
        if (open > 0.6f)
        {
            float cr = eyeR * 0.30f;
            canvas.FillColor = Colors.White.WithAlpha(0.9f);
            canvas.FillEllipse(cx - eyeDx + eyeR * 0.30f - cr + ox, eyeY - eyeR * 0.35f - cr, cr * 2f, cr * 2f);
            canvas.FillEllipse(cx + eyeDx + eyeR * 0.30f - cr + ox, eyeY - eyeR * 0.35f - cr, cr * 2f, cr * 2f);
        }

        // Mouth and brows
        canvas.StrokeColor = ink;
        canvas.StrokeSize = MathF.Max(1.5f, body * 0.07f);
        canvas.StrokeLineCap = LineCap.Round;
        float my = cy + body * 0.34f;
        float mw = body * 0.30f;
        var mouth = new PathF();
        bool strokeMouth = true;   // Curious and Excited draw their mouth another way
        switch (Mood)
        {
            case PipMood.Happy:
                mouth.MoveTo(cx - mw, my - body * 0.04f);
                mouth.QuadTo(cx, my + body * 0.26f, cx + mw, my - body * 0.04f);
                break;
            case PipMood.Worried:
                mouth.MoveTo(cx - mw * 0.8f, my + body * 0.10f);
                mouth.QuadTo(cx, my - body * 0.10f, cx + mw * 0.8f, my + body * 0.10f);
                // Raised brows
                canvas.DrawLine(cx - eyeDx - eyeR, eyeY - eyeR * 2.4f, cx - eyeDx + eyeR, eyeY - eyeR * 3.2f);
                canvas.DrawLine(cx + eyeDx + eyeR, eyeY - eyeR * 2.4f, cx + eyeDx - eyeR, eyeY - eyeR * 3.2f);
                break;
            case PipMood.Curious:
                // A small round "o", and one brow raised higher than the other.
                strokeMouth = false;
                canvas.DrawEllipse(cx - mw * 0.28f, my - mw * 0.05f, mw * 0.56f, mw * 0.56f);
                canvas.DrawLine(cx - eyeDx - eyeR, eyeY - eyeR * 2.2f, cx - eyeDx + eyeR, eyeY - eyeR * 2.2f);
                canvas.DrawLine(cx + eyeDx - eyeR, eyeY - eyeR * 3.4f, cx + eyeDx + eyeR, eyeY - eyeR * 3.0f);
                break;
            case PipMood.Excited:
            {
                // A wide open smile.
                strokeMouth = false;
                var open2 = new PathF();
                open2.MoveTo(cx - mw * 1.1f, my - body * 0.06f);
                open2.QuadTo(cx, my + body * 0.50f, cx + mw * 1.1f, my - body * 0.06f);
                open2.Close();
                canvas.FillColor = ink;
                canvas.FillPath(open2);
                break;
            }
            case PipMood.Sad:
                mouth.MoveTo(cx - mw * 0.7f, my + body * 0.14f);
                mouth.QuadTo(cx, my - body * 0.12f, cx + mw * 0.7f, my + body * 0.14f);
                // A small tear under one eye.
                canvas.FillColor = StoryPalette.Cyan.WithAlpha(0.85f);
                canvas.FillEllipse(cx - eyeDx - eyeR * 0.45f + ox, eyeY + eyeR * 1.5f, eyeR * 0.9f, eyeR * 1.3f);
                break;
            default:
                mouth.MoveTo(cx - mw * 0.7f, my);
                mouth.LineTo(cx + mw * 0.7f, my);
                break;
        }
        if (strokeMouth) canvas.DrawPath(mouth);

        DrawDetail(canvas, cx, cy, body);
        DrawAccessory(canvas, cx, cy, body);
    }

    /// <summary>The little glow tail under the body. It sways a little while the clock runs.</summary>
    private void DrawTail(ICanvas canvas, float cx, float cy, float body, Color glow)
    {
        float sway = Time >= 0f ? MathF.Sin(Time * 2.4f - 0.8f) * body * 0.12f : 0f;
        var tail = new PathF();
        tail.MoveTo(cx - body * 0.26f, cy + body * 0.80f);
        tail.QuadTo(cx + sway, cy + body * 1.85f, cx + body * 0.26f, cy + body * 0.80f);
        tail.Close();
        canvas.FillColor = glow.WithAlpha(0.55f);
        canvas.FillPath(tail);
    }

    /// <summary>Two short leaf antennae, one each side of the top of the head.</summary>
    private void DrawLeaves(ICanvas canvas, float cx, float cy, float body)
    {
        float wave = Time >= 0f ? MathF.Sin(Time * 2.0f) * body * 0.06f : 0f;
        DrawLeaf(canvas, cx - body * 0.30f, cy - body * 0.86f, -1f, body, wave);
        DrawLeaf(canvas, cx + body * 0.30f, cy - body * 0.86f, 1f, body, -wave);
    }

    private static void DrawLeaf(ICanvas canvas, float bx, float by, float side, float body, float wave)
    {
        float tx = bx + side * body * 0.40f + wave;
        float ty = by - body * 0.62f;
        var leaf = new PathF();
        leaf.MoveTo(bx, by);
        leaf.QuadTo(bx + side * body * 0.04f, by - body * 0.46f, tx, ty);
        leaf.QuadTo(bx + side * body * 0.44f, by - body * 0.28f, bx, by);
        leaf.Close();
        canvas.FillColor = StoryPalette.Lime;
        canvas.FillPath(leaf);
        canvas.StrokeColor = Colors.White.WithAlpha(0.35f);
        canvas.StrokeSize = MathF.Max(1f, body * 0.03f);
        canvas.DrawLine(bx, by, tx - side * body * 0.04f, ty + body * 0.04f);
    }

    /// <summary>One small light per restored region, evenly spaced around Pip.</summary>
    private void DrawDetail(ICanvas canvas, float cx, float cy, float body)
    {
        int n = Math.Clamp(Detail, 0, 8);
        if (n == 0) return;

        float orbit = body * 1.62f;
        float r = MathF.Max(1.5f, body * 0.07f);
        canvas.FillColor = Color.FromArgb("#FFE9A8").WithAlpha(0.9f);
        for (int i = 0; i < n; i++)
        {
            float a = -MathF.PI / 2f + i * MathF.PI * 2f / 8f;
            canvas.FillEllipse(cx + MathF.Cos(a) * orbit - r, cy + MathF.Sin(a) * orbit - r, r * 2f, r * 2f);
        }
    }

    private void DrawAccessory(ICanvas canvas, float cx, float cy, float body)
    {
        var ink = Color.FromArgb("#1B2547");
        canvas.StrokeLineCap = LineCap.Round;

        switch (Accessory)
        {
            case AccessoryStyle.Bow:
            {
                // A bow at the upper right: two wings and a knot.
                float bx = cx + body * 0.62f, by = cy - body * 0.82f, w = body * 0.34f, h = body * 0.22f;
                canvas.FillColor = Color.FromArgb("#FB7185");
                canvas.FillEllipse(bx - w * 1.15f, by - h / 2f, w, h);
                canvas.FillEllipse(bx + w * 0.15f, by - h / 2f, w, h);
                canvas.FillColor = Color.FromArgb("#E11D48");
                canvas.FillEllipse(bx - w * 0.2f, by - h * 0.35f, w * 0.4f, h * 0.7f);
                break;
            }

            case AccessoryStyle.Cap:
            {
                // A little cap with a brim, set on top.
                float topY = cy - body * 1.0f;
                canvas.FillColor = Color.FromArgb("#A78BFA");
                canvas.FillRoundedRectangle(cx - body * 0.55f, topY - body * 0.26f, body * 1.1f, body * 0.42f, body * 0.2f);
                canvas.StrokeColor = Color.FromArgb("#7C3AED");
                canvas.StrokeSize = MathF.Max(1.5f, body * 0.08f);
                canvas.DrawLine(cx - body * 0.72f, topY + body * 0.16f, cx + body * 0.72f, topY + body * 0.16f);
                break;
            }

            case AccessoryStyle.Antenna:
            {
                // A thin antenna with a bright tip.
                float baseY = cy - body * 0.96f, tipY = cy - body * 1.55f;
                canvas.StrokeColor = ink;
                canvas.StrokeSize = MathF.Max(1.5f, body * 0.07f);
                canvas.DrawLine(cx, baseY, cx + body * 0.12f, tipY);
                canvas.FillColor = Color.FromArgb("#FFC83D");
                float r = body * 0.12f;
                canvas.FillEllipse(cx + body * 0.12f - r, tipY - r, r * 2f, r * 2f);
                break;
            }

            case AccessoryStyle.Lantern:
            {
                // A tiny lantern on a string, hanging at the lower right.
                float lx = cx + body * 1.05f, ly = cy + body * 0.15f;
                canvas.StrokeColor = ink;
                canvas.StrokeSize = MathF.Max(1.5f, body * 0.06f);
                canvas.DrawLine(cx + body * 0.78f, cy - body * 0.1f, lx, ly);
                canvas.FillColor = Color.FromArgb("#FDE68A");
                canvas.FillRoundedRectangle(lx - body * 0.14f, ly, body * 0.28f, body * 0.38f, body * 0.08f);
                canvas.StrokeColor = Color.FromArgb("#D97706");
                canvas.DrawRoundedRectangle(lx - body * 0.14f, ly, body * 0.28f, body * 0.38f, body * 0.08f);
                break;
            }
        }
    }

    private static Color Blend(Color a, Color b, float t) => new(
        a.Red + (b.Red - a.Red) * t,
        a.Green + (b.Green - a.Green) * t,
        a.Blue + (b.Blue - a.Blue) * t,
        1f);
}

/// <summary>
/// A region picture for the map and the region page. Three looks, told apart by shape and by colour:
/// Faded (drooping stems, closed buds, no lights), Recovering (upright stems, half-open flowers, a few lights),
/// Restored (open flowers with centres, many lights). Each region has its own picture in Graphics/Scenes with the
/// same three states; the plain silhouette is only a fallback for an unknown region.
/// </summary>
public sealed class RegionSceneDrawable : IDrawable
{
    public Region? Region { get; set; }
    public RegionState State { get; set; } = RegionState.Faded;
    public bool Locked { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 20f || dirtyRect.Height < 20f) return;

        var sky = State switch
        {
            RegionState.Faded => (Top: "#1B2340", Bottom: "#2A3354"),
            RegionState.Recovering => (Top: "#1E2D4E", Bottom: "#33527A"),
            _ => (Top: "#233B63", Bottom: "#5C7FA8"),
        };
        DrawBands(canvas, dirtyRect, Color.FromArgb(sky.Top), Color.FromArgb(sky.Bottom));

        if (Locked)
        {
            DrawLockedMark(canvas, dirtyRect);
            return;
        }

        switch (Region?.Number)
        {
            case 1: DrawGarden(canvas, dirtyRect); break;
            case 2: Scenes.CavesScene.Draw(canvas, dirtyRect, State); break;
            case 3: Scenes.WildsScene.Draw(canvas, dirtyRect, State); break;
            case 4: Scenes.CityScene.Draw(canvas, dirtyRect, State); break;
            case 5: Scenes.WastesScene.Draw(canvas, dirtyRect, State); break;
            case 6: Scenes.ExpanseScene.Draw(canvas, dirtyRect, State); break;
            case 7: Scenes.CitadelScene.Draw(canvas, dirtyRect, State); break;
            case 8: Scenes.HeartScene.Draw(canvas, dirtyRect, State); break;
            default: DrawSilhouette(canvas, dirtyRect); break;
        }
    }

    // Vertical gradient as solid bands (SetFillPaint is avoided: see GameDrawable).
    private static void DrawBands(ICanvas canvas, RectF r, Color top, Color bottom)
    {
        const int bands = 20;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            canvas.FillColor = new Color(
                top.Red + (bottom.Red - top.Red) * t,
                top.Green + (bottom.Green - top.Green) * t,
                top.Blue + (bottom.Blue - top.Blue) * t,
                1f);
            canvas.FillRectangle(r.X, r.Y + r.Height * i / bands, r.Width, r.Height / bands + 1f);
        }
    }

    private void DrawGarden(ICanvas canvas, RectF r)
    {
        bool faded = State == RegionState.Faded;
        bool restored = State == RegionState.Restored;

        var ground = faded ? Color.FromArgb("#3B4660") : restored ? Color.FromArgb("#3F7D3A") : Color.FromArgb("#4C6B52");
        var stem = faded ? Color.FromArgb("#5C6B8A") : restored ? Color.FromArgb("#5BBF4A") : Color.FromArgb("#6E9A66");
        var petal = faded ? Color.FromArgb("#7B86A6") : restored ? Color.FromArgb("#F472B6") : Color.FromArgb("#C4A0C0");
        var heart = faded ? Color.FromArgb("#586383") : Color.FromArgb("#FDE047");

        float groundY = r.Y + r.Height * 0.78f;
        canvas.FillColor = ground;
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);
        canvas.FillEllipse(r.X - r.Width * 0.1f, groundY - r.Height * 0.06f, r.Width * 0.7f, r.Height * 0.2f);
        canvas.FillEllipse(r.X + r.Width * 0.45f, groundY - r.Height * 0.05f, r.Width * 0.7f, r.Height * 0.18f);

        // A tree trunk with a root mark: the mid-region discovery lives here.
        float treeX = r.X + r.Width * 0.5f;
        canvas.FillColor = faded ? Color.FromArgb("#4A5575") : Color.FromArgb("#6B4F3A");
        canvas.FillRectangle(treeX - r.Width * 0.025f, r.Y + r.Height * 0.30f, r.Width * 0.05f, groundY - (r.Y + r.Height * 0.30f));
        float crown = r.Width * 0.14f;
        canvas.FillColor = faded ? Color.FromArgb("#4F5C7E") : restored ? Color.FromArgb("#4FAE45") : Color.FromArgb("#5F8A5C");
        canvas.FillEllipse(treeX - crown, r.Y + r.Height * 0.12f, crown * 2f, crown * 1.5f);

        // Flowers
        float[] xs = { 0.12f, 0.27f, 0.74f, 0.88f, 0.38f, 0.63f };
        float[] hs = { 0.30f, 0.38f, 0.34f, 0.28f, 0.22f, 0.24f };
        for (int i = 0; i < xs.Length; i++)
        {
            float fx = r.X + r.Width * xs[i];
            float h = r.Height * hs[i];
            float top = groundY - h;
            canvas.StrokeColor = stem;
            canvas.StrokeSize = MathF.Max(2f, r.Width * 0.012f);
            canvas.StrokeLineCap = LineCap.Round;

            var path = new PathF();
            path.MoveTo(fx, groundY);
            if (faded)
            {
                // Drooping: the stem bows over to one side and the bud hangs down.
                float side = i % 2 == 0 ? 1f : -1f;
                float hx = fx + side * h * 0.55f;
                float hy = top + h * 0.35f;
                path.QuadTo(fx, top, hx, hy);
                canvas.DrawPath(path);
                DrawBud(canvas, hx, hy + 4f, r.Width * 0.022f, petal);
            }
            else
            {
                path.LineTo(fx, top);
                canvas.DrawPath(path);
                if (restored) DrawOpenFlower(canvas, fx, top, r.Width * 0.045f, petal, heart);
                else DrawHalfFlower(canvas, fx, top, r.Width * 0.035f, petal);
            }
        }

        // Lights: none when faded, a few when recovering, many when restored.
        int lights = faded ? 0 : restored ? 9 : 3;
        for (int i = 0; i < lights; i++)
        {
            float lx = r.X + r.Width * (0.08f + 0.84f * ((i * 37 % 100) / 100f));
            float ly = r.Y + r.Height * (0.18f + 0.45f * ((i * 53 % 100) / 100f));
            float lr = r.Width * (restored ? 0.014f : 0.011f);
            canvas.FillColor = Color.FromArgb("#FDE68A").WithAlpha(0.25f);
            canvas.FillEllipse(lx - lr * 2.4f, ly - lr * 2.4f, lr * 4.8f, lr * 4.8f);
            canvas.FillColor = Color.FromArgb("#FEF3C7");
            canvas.FillEllipse(lx - lr, ly - lr, lr * 2f, lr * 2f);
        }
    }

    private static void DrawBud(ICanvas canvas, float x, float y, float size, Color c)
    {
        canvas.FillColor = c;
        canvas.FillEllipse(x - size, y - size * 1.3f, size * 2f, size * 2.6f);
    }

    private static void DrawHalfFlower(ICanvas canvas, float x, float y, float size, Color c)
    {
        canvas.FillColor = c;
        canvas.FillEllipse(x - size, y - size * 1.5f, size * 2f, size * 3f);
        canvas.FillEllipse(x - size * 1.7f, y - size * 1.2f, size * 1.5f, size * 2.4f);
        canvas.FillEllipse(x + size * 0.2f, y - size * 1.2f, size * 1.5f, size * 2.4f);
    }

    private static void DrawOpenFlower(ICanvas canvas, float x, float y, float size, Color petal, Color heart)
    {
        canvas.FillColor = petal;
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.PI / 3f;
            float px = x + MathF.Cos(a) * size * 0.9f;
            float py = y + MathF.Sin(a) * size * 0.9f;
            canvas.FillEllipse(px - size * 0.55f, py - size * 0.55f, size * 1.1f, size * 1.1f);
        }
        canvas.FillColor = heart;
        canvas.FillEllipse(x - size * 0.5f, y - size * 0.5f, size, size);
    }

    /// <summary>Plain stand-in for regions without a picture yet: a ridge line whose brightness follows the state.</summary>
    private void DrawSilhouette(ICanvas canvas, RectF r)
    {
        var ridge = State switch
        {
            RegionState.Faded => Color.FromArgb("#2E3858"),
            RegionState.Recovering => Color.FromArgb("#47628A"),
            _ => Color.FromArgb("#6F95C4"),
        };
        canvas.FillColor = ridge;
        var p = new PathF();
        p.MoveTo(r.X, r.Bottom);
        p.LineTo(r.X, r.Y + r.Height * 0.65f);
        p.LineTo(r.X + r.Width * 0.25f, r.Y + r.Height * 0.42f);
        p.LineTo(r.X + r.Width * 0.45f, r.Y + r.Height * 0.60f);
        p.LineTo(r.X + r.Width * 0.7f, r.Y + r.Height * 0.35f);
        p.LineTo(r.Right, r.Y + r.Height * 0.62f);
        p.LineTo(r.Right, r.Bottom);
        p.Close();
        canvas.FillPath(p);

        if (State != RegionState.Faded)
        {
            canvas.FillColor = Color.FromArgb("#FEF3C7");
            float lr = r.Width * 0.012f;
            canvas.FillEllipse(r.X + r.Width * 0.7f - lr, r.Y + r.Height * 0.33f - lr, lr * 2f, lr * 2f);
        }
    }

    private static void DrawLockedMark(ICanvas canvas, RectF r)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        float s = MathF.Min(r.Width, r.Height) * 0.16f;
        canvas.StrokeColor = Color.FromArgb("#66FFFFFF");
        canvas.StrokeSize = MathF.Max(2f, s * 0.22f);
        canvas.DrawArc(cx - s * 0.6f, cy - s * 1.4f, s * 1.2f, s * 1.4f, 0, 180, false, false);
        canvas.FillColor = Color.FromArgb("#55FFFFFF");
        canvas.FillRoundedRectangle(cx - s, cy - s * 0.4f, s * 2f, s * 1.5f, s * 0.25f);
    }
}
