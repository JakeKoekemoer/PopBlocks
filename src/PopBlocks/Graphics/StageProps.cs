using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// Props for the story stage: the memory crystal, the Prism shard, the cracked stone and the ice slab.
/// Each is drawn from a centre point and a size, so the same code serves the stage strip and the portrait.
/// Glow is its own pass where the brief asks for it (shards), so it can be turned down without losing the prop.
/// </summary>
internal static class StageProps
{
    // ---------------------------------------------------------------- memory crystal

    /// <summary>
    /// A memory crystal: a faceted cyan gem with a glowing core and soft ripple rings. <paramref name="size"/> is half its
    /// height. <paramref name="time"/> is seconds on an animation clock, or negative for a still picture. While
    /// <paramref name="playing"/> (a recording is speaking) the core pulses faster and the ripples are stronger.
    /// </summary>
    public static void DrawMemoryCrystal(ICanvas canvas, float cx, float cy, float size, float time, bool playing, Color core)
    {
        if (size < 3f) return;
        bool live = time >= 0f;

        float beat = live ? 0.5f + 0.5f * MathF.Sin(time * MathF.PI * 2f / (playing ? 1.2f : 2.0f)) : 0.6f;
        float strength = playing ? 1f : 0.35f;

        // Ripple rings: three, spread around one cycle. A still picture shows one at a fixed point.
        canvas.StrokeSize = MathF.Max(1f, size * 0.035f);
        for (int k = 0; k < 3; k++)
        {
            float phase = live ? StageMath.Frac(time / 2.4f + k / 3f) : 0.3f;
            if (!live && k > 0) break;
            float rad = size * (0.75f + 1.15f * phase);
            float fade = (1f - phase) * (1f - phase);
            canvas.StrokeColor = core.WithAlpha(0.45f * fade * strength);
            canvas.DrawEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
        }

        // Soft halo
        for (int i = 3; i >= 1; i--)
        {
            float rad = size * (0.7f + 0.4f * i) * (0.95f + 0.1f * beat);
            canvas.FillColor = StoryPalette.Cyan.WithAlpha(0.06f * (4 - i) / 3f * (0.6f + 0.4f * strength));
            canvas.FillEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
        }

        float hw = size * 0.55f;
        var deep = StoryPalette.DeepBackground;

        // Body, then the two halves and the top facets
        FillPoly(canvas, StoryPalette.Mix(StoryPalette.Cyan, deep, 0.35f),
            cx, cy - size, cx + hw, cy - size * 0.42f, cx + hw, cy + size * 0.38f, cx, cy + size, cx - hw, cy + size * 0.38f, cx - hw, cy - size * 0.42f);
        FillPoly(canvas, StoryPalette.Mix(StoryPalette.Cyan, deep, 0.62f),
            cx, cy - size, cx - hw, cy - size * 0.42f, cx - hw, cy + size * 0.38f, cx, cy + size);
        FillPoly(canvas, StoryPalette.Mix(StoryPalette.Cyan, Colors.White, 0.15f),
            cx, cy - size, cx + hw, cy - size * 0.42f, cx + hw, cy + size * 0.38f, cx, cy + size);
        FillPoly(canvas, StoryPalette.Mix(StoryPalette.Cyan, Colors.White, 0.45f).WithAlpha(0.85f),
            cx, cy - size, cx + hw, cy - size * 0.42f, cx, cy - size * 0.20f);
        FillPoly(canvas, StoryPalette.Mix(StoryPalette.Cyan, Colors.White, 0.30f).WithAlpha(0.6f),
            cx, cy - size, cx - hw, cy - size * 0.42f, cx, cy - size * 0.20f);

        // Core
        float coreR = size * (0.20f + 0.07f * beat * strength);
        for (int i = 3; i >= 1; i--)
        {
            float rad = coreR * (1f + 0.6f * i);
            canvas.FillColor = core.WithAlpha(0.14f * (4 - i) / 3f * (0.7f + 0.3f * beat));
            canvas.FillEllipse(cx - rad, cy + size * 0.02f - rad, rad * 2f, rad * 2f);
        }
        canvas.FillColor = StoryPalette.Mix(core, Colors.White, 0.55f);
        canvas.FillEllipse(cx - coreR, cy + size * 0.02f - coreR, coreR * 2f, coreR * 2f);

        // Edge and a small specular highlight
        var outline = new PathF();
        outline.MoveTo(cx, cy - size);
        outline.LineTo(cx + hw, cy - size * 0.42f);
        outline.LineTo(cx + hw, cy + size * 0.38f);
        outline.LineTo(cx, cy + size);
        outline.LineTo(cx - hw, cy + size * 0.38f);
        outline.LineTo(cx - hw, cy - size * 0.42f);
        outline.Close();
        canvas.StrokeColor = Colors.White.WithAlpha(0.5f);
        canvas.StrokeSize = MathF.Max(1f, size * 0.04f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(outline);

        canvas.FillColor = Colors.White.WithAlpha(0.8f);
        canvas.FillEllipse(cx + hw * 0.42f - size * 0.05f, cy - size * 0.55f - size * 0.05f, size * 0.10f, size * 0.10f);
    }

    /// <summary>A pale slab of ice for a crystal to sit in, with a few cracks. Used where the memory is frozen into the wall.</summary>
    public static void DrawIceSlab(ICanvas canvas, float cx, float cy, float w, float h)
    {
        float x = cx - w / 2f;
        float y = cy - h / 2f;
        float corner = MathF.Min(w, h) * 0.16f;

        canvas.FillColor = Color.FromArgb("#BFE9FF").WithAlpha(0.22f);
        canvas.FillRoundedRectangle(x, y, w, h, corner);
        canvas.StrokeColor = Colors.White.WithAlpha(0.35f);
        canvas.StrokeSize = MathF.Max(1f, h * 0.012f);
        canvas.DrawRoundedRectangle(x, y, w, h, corner);

        canvas.StrokeColor = Colors.White.WithAlpha(0.22f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(x + w * 0.08f, y + h * 0.22f, x + w * 0.24f, y + h * 0.36f);
        canvas.DrawLine(x + w * 0.24f, y + h * 0.36f, x + w * 0.20f, y + h * 0.52f);
        canvas.DrawLine(x + w * 0.92f, y + h * 0.78f, x + w * 0.76f, y + h * 0.64f);
        canvas.DrawLine(x + w * 0.76f, y + h * 0.64f, x + w * 0.82f, y + h * 0.50f);
    }

    // ---------------------------------------------------------------- Prism shard

    /// <summary>
    /// A Prism shard: one shared construction for all five colours (a leaning kite with a darker left face, a lighter
    /// right face, a bevel line and a small highlight). <paramref name="size"/> is half its height, <paramref name="tilt"/>
    /// is degrees. Call with (glow: true, body: false) and (glow: false, body: true) to draw the two layers
    /// separately; <paramref name="pulse"/> 0..1 drives the glow. Shards given the same pulse beat in step.
    /// </summary>
    public static void DrawShard(ICanvas canvas, float cx, float cy, float size, Color colour, float pulse, bool glow, bool body, float tilt = 0f)
    {
        if (size < 3f) return;

        if (glow)
        {
            for (int i = 4; i >= 1; i--)
            {
                float rad = size * (0.55f + 0.42f * i) * (0.92f + 0.14f * pulse);
                canvas.FillColor = colour.WithAlpha(0.07f * (5 - i) / 4f * (0.7f + 0.5f * pulse));
                canvas.FillEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
            }
        }

        if (!body) return;

        canvas.SaveState();
        canvas.Translate(cx, cy);
        canvas.Rotate(tilt);

        var dark = StoryPalette.Mix(colour, StoryPalette.DeepBackground, 0.45f);
        var light = StoryPalette.Mix(colour, Colors.White, 0.45f);

        // Corners: top, right, bottom, left
        float tx = 0f, ty = -size;
        float rx = size * 0.50f, ry = -size * 0.20f;
        float bx = size * 0.10f, by = size;
        float lx = -size * 0.46f, ly = size * 0.15f;

        FillPoly(canvas, colour, tx, ty, rx, ry, bx, by, lx, ly);
        FillPoly(canvas, dark, tx, ty, lx, ly, bx, by);
        FillPoly(canvas, light.WithAlpha(0.85f), tx, ty, rx, ry, bx, by);
        FillPoly(canvas, Colors.White.WithAlpha(0.55f), tx, ty, size * 0.20f, -size * 0.58f, -size * 0.04f, -size * 0.45f);

        var edge = new PathF();
        edge.MoveTo(tx, ty);
        edge.LineTo(rx, ry);
        edge.LineTo(bx, by);
        edge.LineTo(lx, ly);
        edge.Close();
        canvas.StrokeColor = Colors.White.WithAlpha(0.6f);
        canvas.StrokeSize = MathF.Max(1f, size * 0.04f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(edge);

        canvas.RestoreState();
    }

    // ---------------------------------------------------------------- the cracked stone

    /// <summary>The cracked stone Pip waits behind in the first scene. <paramref name="baseY"/> is where it sits on the ground.</summary>
    public static void DrawCrackedStone(ICanvas canvas, float cx, float baseY, float w, float h)
    {
        float hw = w / 2f;
        var rock = new PathF();
        rock.MoveTo(cx - hw, baseY);
        rock.LineTo(cx - hw * 1.08f, baseY - h * 0.38f);
        rock.LineTo(cx - hw * 0.60f, baseY - h * 0.80f);
        rock.LineTo(cx + hw * 0.10f, baseY - h);
        rock.LineTo(cx + hw * 0.76f, baseY - h * 0.72f);
        rock.LineTo(cx + hw * 1.06f, baseY - h * 0.30f);
        rock.LineTo(cx + hw, baseY);
        rock.Close();
        canvas.FillColor = Color.FromArgb("#4A5575");
        canvas.FillPath(rock);

        // A lighter face on the upper left
        var face = new PathF();
        face.MoveTo(cx - hw * 1.08f, baseY - h * 0.38f);
        face.LineTo(cx - hw * 0.60f, baseY - h * 0.80f);
        face.LineTo(cx + hw * 0.10f, baseY - h);
        face.LineTo(cx - hw * 0.05f, baseY - h * 0.50f);
        face.Close();
        canvas.FillColor = Color.FromArgb("#62709A");
        canvas.FillPath(face);

        // The crack, top to bottom
        canvas.StrokeColor = Color.FromArgb("#141B38");
        canvas.StrokeSize = MathF.Max(1.5f, h * 0.035f);
        canvas.StrokeLineCap = LineCap.Round;
        var crack = new PathF();
        crack.MoveTo(cx + hw * 0.10f, baseY - h);
        crack.LineTo(cx - hw * 0.05f, baseY - h * 0.66f);
        crack.LineTo(cx + hw * 0.14f, baseY - h * 0.44f);
        crack.LineTo(cx - hw * 0.02f, baseY - h * 0.18f);
        crack.LineTo(cx + hw * 0.04f, baseY);
        canvas.DrawPath(crack);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Fills a polygon given as x,y pairs.</summary>
    private static void FillPoly(ICanvas canvas, Color colour, params float[] xy)
    {
        var path = new PathF();
        path.MoveTo(xy[0], xy[1]);
        for (int i = 2; i < xy.Length; i += 2) path.LineTo(xy[i], xy[i + 1]);
        path.Close();
        canvas.FillColor = colour;
        canvas.FillPath(path);
    }
}
