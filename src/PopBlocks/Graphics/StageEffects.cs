using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// The story's central picture: light drawn in toward one centre while the edges dim (the old world, everything
/// depending on the Heart), or light spreading out from a centre while the edges come on (the new plan).
/// The two are told apart by more than colour: the direction the sparks travel, a hot overloaded centre against a
/// calm one, and the edge lamps fading or brightening. Both use the same ribbons, so one effect serves every scene.
/// </summary>
internal static class LightFlow
{
    private const int Ribbons = 10;
    private const int Sparks = 3;

    /// <param name="inward">True: sparks travel toward the centre and the edge lamps dim. False: the reverse.</param>
    /// <param name="centreX">Where the centre is, as a fraction of the width (0..1).</param>
    /// <param name="centreY">Where the centre is, as a fraction of the height (0..1).</param>
    /// <param name="time">Seconds on an animation clock, or negative for a still picture.</param>
    /// <param name="progress">0..1: how far the effect has built up. The edge lamps follow this.</param>
    public static void Draw(ICanvas canvas, RectF r, bool inward, float centreX, float centreY, float time, float progress)
    {
        float sc = r.Height / 150f;
        float cx = r.X + r.Width * centreX;
        float cy = r.Y + r.Height * centreY;
        float prog = StageMath.Clamp01(progress);
        bool live = time >= 0f;

        canvas.StrokeLineCap = LineCap.Round;

        for (int i = 0; i < Ribbons; i++)
        {
            var colour = StoryPalette.Spectrum[i % StoryPalette.Spectrum.Length];

            // Where the ribbon starts: around the edge of the picture, kept inside it.
            float a = i / (float)Ribbons * MathF.PI * 2f + 0.35f;
            float ex = Math.Clamp(cx + MathF.Cos(a) * r.Width * 0.62f, r.X + 10f * sc, r.Right - 10f * sc);
            float ey = Math.Clamp(cy + MathF.Sin(a) * r.Height * 0.70f, r.Y + 10f * sc, r.Bottom - 10f * sc);

            // A gentle bend, alternating sides, so the ribbons read as flowing rather than as spokes.
            float bend = (i % 2 == 0 ? 0.20f : -0.20f);
            float qx = (ex + cx) / 2f - (ey - cy) * bend;
            float qy = (ey + cy) / 2f + (ex - cx) * bend;

            // The ribbon
            var path = new PathF();
            path.MoveTo(ex, ey);
            for (int s = 1; s <= 14; s++)
            {
                var p = Quad(ex, ey, qx, qy, cx, cy, s / 14f);
                path.LineTo(p.X, p.Y);
            }
            canvas.StrokeColor = colour.WithAlpha(0.10f + 0.16f * prog);
            canvas.StrokeSize = MathF.Max(1f, 1.6f * sc);
            canvas.DrawPath(path);

            // Sparks travelling along it
            for (int j = 0; j < Sparks; j++)
            {
                float u = live
                    ? StageMath.Frac(time * 0.22f + i * 0.137f + j / (float)Sparks)
                    : StageMath.Frac(i * 0.137f + j / (float)Sparks);
                float t = inward ? u : 1f - u;                       // 0 at the edge, 1 at the centre
                var p = Quad(ex, ey, qx, qy, cx, cy, t);
                float envelope = MathF.Sin(u * MathF.PI);
                float rad = (inward ? 3.4f - 1.4f * u : 1.8f + 1.6f * u) * sc;

                canvas.FillColor = colour.WithAlpha(0.20f * envelope * prog);
                canvas.FillEllipse(p.X - rad * 2.2f, p.Y - rad * 2.2f, rad * 4.4f, rad * 4.4f);
                canvas.FillColor = StoryPalette.Mix(colour, Colors.White, 0.4f).WithAlpha(0.95f * envelope * (0.4f + 0.6f * prog));
                canvas.FillEllipse(p.X - rad, p.Y - rad, rad * 2f, rad * 2f);
            }

            // The lamp at the edge: it goes dim as everything is drawn inward, and comes on as light is shared out.
            float lamp = inward ? 1f - 0.78f * prog : 0.22f + 0.78f * prog;
            float lr = 3.6f * sc;
            canvas.FillColor = colour.WithAlpha(0.22f * lamp);
            canvas.FillEllipse(ex - lr * 2.4f, ey - lr * 2.4f, lr * 4.8f, lr * 4.8f);
            canvas.FillColor = colour.WithAlpha(0.25f + 0.75f * lamp);
            canvas.FillEllipse(ex - lr, ey - lr, lr * 2f, lr * 2f);
        }

        DrawCentre(canvas, cx, cy, sc, inward, time);
    }

    private static void DrawCentre(ICanvas canvas, float cx, float cy, float sc, bool inward, float time)
    {
        float beat = time >= 0f ? 0.5f + 0.5f * MathF.Sin(time * (inward ? 3.4f : 1.8f)) : 0.5f;

        if (inward)
        {
            // Overloaded: a hot, white-gold core that is too bright for the space around it.
            for (int i = 4; i >= 1; i--)
            {
                float rad = (7f + 5f * i) * sc * (0.95f + 0.1f * beat);
                canvas.FillColor = StoryPalette.Amber.WithAlpha(0.10f * (5 - i) / 4f);
                canvas.FillEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
            }
            float core = 6.5f * sc * (0.95f + 0.1f * beat);
            canvas.FillColor = Color.FromArgb("#FFF6D8");
            canvas.FillEllipse(cx - core, cy - core, core * 2f, core * 2f);
            canvas.StrokeColor = Colors.White.WithAlpha(0.5f);
            canvas.StrokeSize = MathF.Max(1f, 1.4f * sc);
            canvas.DrawEllipse(cx - core * 1.7f, cy - core * 1.7f, core * 3.4f, core * 3.4f);
        }
        else
        {
            // Calm: a soft cyan-white heart that is giving light away.
            for (int i = 3; i >= 1; i--)
            {
                float rad = (6f + 5f * i) * sc * (0.95f + 0.1f * beat);
                canvas.FillColor = StoryPalette.Cyan.WithAlpha(0.10f * (4 - i) / 3f);
                canvas.FillEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
            }
            float core = 4.6f * sc;
            canvas.FillColor = StoryPalette.Mix(StoryPalette.Cyan, Colors.White, 0.7f);
            canvas.FillEllipse(cx - core, cy - core, core * 2f, core * 2f);
        }
    }

    private static (float X, float Y) Quad(float x0, float y0, float x1, float y1, float x2, float y2, float t)
    {
        float u = 1f - t;
        return (u * u * x0 + 2f * u * t * x1 + t * t * x2, u * u * y0 + 2f * u * t * y1 + t * t * y2);
    }
}

/// <summary>
/// One quiet particle loop per region, so a scene with no special prop still feels like somewhere. Kept sparse and
/// faint on purpose: it must never compete with the dialogue text. The page does not call this at all under
/// Reduced motion.
/// </summary>
internal static class Ambience
{
    /// <param name="time">Seconds on the ambient clock (it slows down for a held pause).</param>
    /// <param name="strength">0..1: overall opacity.</param>
    public static void Draw(ICanvas canvas, RectF r, int region, float time, float strength)
    {
        if (strength <= 0.01f || r.Width < 20f || r.Height < 20f) return;
        float sc = r.Height / 150f;

        switch (region)
        {
            case 1: Fireflies(canvas, r, sc, time, strength); break;
            case 2: CrystalGlints(canvas, r, sc, time, strength); break;
            case 3: Spores(canvas, r, sc, time, strength); break;
            case 4: Bubbles(canvas, r, sc, time, strength); break;
            case 5: SparksAndSteam(canvas, r, sc, time, strength); break;
            case 6: Snow(canvas, r, sc, time, strength); break;
            case 7: Shards(canvas, r, sc, time, strength); break;
            case 8: Hum(canvas, r, sc, time, strength); break;
        }
    }

    private static void Fireflies(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        for (int i = 0; i < 9; i++)
        {
            float x = StageMath.Hash(i * 3 + 1) + 0.04f * MathF.Sin(t * 0.35f + i * 1.7f);
            float y = 0.22f + 0.50f * StageMath.Hash(i * 3 + 2) + 0.05f * MathF.Cos(t * 0.28f + i * 2.3f);
            float a = (0.30f + 0.70f * (0.5f + 0.5f * MathF.Sin(t * 0.9f + i * 2.1f))) * k;
            var c = i % 3 == 0 ? StoryPalette.Lime : StoryPalette.Amber;
            Dot(canvas, r.X + r.Width * x, r.Y + r.Height * y, 2.2f * sc, c, a, 3.2f);
        }
    }

    private static void CrystalGlints(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        canvas.StrokeLineCap = LineCap.Round;
        for (int i = 0; i < 8; i++)
        {
            float tw = MathF.Max(0f, MathF.Sin(t * 0.8f + i * 1.9f));
            tw = tw * tw * tw;
            if (tw < 0.03f) continue;
            float x = r.X + r.Width * (0.06f + 0.88f * StageMath.Hash(i * 7 + 3));
            float y = r.Y + r.Height * (0.08f + 0.60f * StageMath.Hash(i * 7 + 4));
            float len = 6f * sc * tw;
            var c = i % 2 == 0 ? Colors.White : StoryPalette.Cyan;
            canvas.StrokeColor = c.WithAlpha(0.9f * tw * k);
            canvas.StrokeSize = MathF.Max(1f, 1.2f * sc);
            canvas.DrawLine(x - len, y, x + len, y);
            canvas.DrawLine(x, y - len, x, y + len);
        }
    }

    private static void Spores(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        for (int i = 0; i < 14; i++)
        {
            float cycle = StageMath.Frac(t * (0.020f + 0.020f * StageMath.Hash(i * 5 + 2)) + StageMath.Hash(i * 5 + 1));
            float x = StageMath.Hash(i * 5 + 3) + 0.03f * MathF.Sin(t * 0.6f + i);
            float y = 1f - cycle;
            float env = MathF.Sin(cycle * MathF.PI);
            Dot(canvas, r.X + r.Width * x, r.Y + r.Height * y, 1.7f * sc, StoryPalette.Lime, 0.65f * env * k, 2.6f);
        }
    }

    private static void Bubbles(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        canvas.StrokeSize = MathF.Max(1f, 1.1f * sc);
        for (int i = 0; i < 10; i++)
        {
            float cycle = StageMath.Frac(t * (0.045f + 0.045f * StageMath.Hash(i * 5 + 2)) + StageMath.Hash(i * 5 + 1));
            float x = StageMath.Hash(i * 5 + 3) + 0.02f * MathF.Sin(t * 0.9f + i * 1.3f);
            float y = 1.05f - 1.1f * cycle;
            float env = MathF.Sin(cycle * MathF.PI);
            float rad = (2.4f + 3.6f * StageMath.Hash(i * 5 + 4)) * sc;
            float px = r.X + r.Width * x;
            float py = r.Y + r.Height * y;
            canvas.StrokeColor = StoryPalette.Cyan.WithAlpha(0.55f * env * k);
            canvas.DrawEllipse(px - rad, py - rad, rad * 2f, rad * 2f);
            canvas.FillColor = Colors.White.WithAlpha(0.5f * env * k);
            canvas.FillEllipse(px - rad * 0.45f, py - rad * 0.55f, rad * 0.4f, rad * 0.4f);
        }
    }

    private static void SparksAndSteam(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        // Steam first, soft and low, then sparks over it.
        for (int i = 0; i < 3; i++)
        {
            float cycle = StageMath.Frac(t * 0.07f + i / 3f);
            float x = 0.18f + 0.34f * i + 0.02f * MathF.Sin(t * 0.5f + i);
            float y = 0.92f - 0.45f * cycle;
            float rad = (6f + 12f * cycle) * sc;
            canvas.FillColor = Color.FromArgb("#C9D6E8").WithAlpha(0.10f * (1f - cycle) * k);
            canvas.FillEllipse(r.X + r.Width * x - rad, r.Y + r.Height * y - rad, rad * 2f, rad * 2f);
        }

        for (int i = 0; i < 8; i++)
        {
            float cycle = StageMath.Frac(t * (0.16f + 0.10f * StageMath.Hash(i * 5 + 2)) + StageMath.Hash(i * 5 + 1));
            float x = 0.10f + 0.80f * StageMath.Hash(i * 5 + 3) + 0.03f * MathF.Sin(t * 1.4f + i);
            float y = 0.95f - 0.75f * cycle;
            Dot(canvas, r.X + r.Width * x, r.Y + r.Height * y, 1.6f * sc, StoryPalette.Amber, 0.9f * (1f - cycle) * k, 2.6f);
        }
    }

    private static void Snow(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        for (int i = 0; i < 16; i++)
        {
            float cycle = StageMath.Frac(t * (0.035f + 0.030f * StageMath.Hash(i * 5 + 2)) + StageMath.Hash(i * 5 + 1));
            float x = StageMath.Hash(i * 5 + 3) + 0.025f * MathF.Sin(t * 0.7f + i * 1.1f);
            float rad = (1.2f + 1.0f * StageMath.Hash(i * 5 + 4)) * sc;
            canvas.FillColor = Colors.White.WithAlpha(0.65f * k);
            canvas.FillEllipse(r.X + r.Width * x - rad, r.Y + r.Height * cycle - rad, rad * 2f, rad * 2f);
        }
    }

    private static void Shards(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        for (int i = 0; i < 6; i++)
        {
            float x = 0.10f + 0.80f * StageMath.Hash(i * 5 + 1);
            float y = 0.18f + 0.55f * StageMath.Hash(i * 5 + 2) + 0.04f * MathF.Sin(t * 0.5f + i * 1.7f);
            float s = (3.5f + 3.0f * StageMath.Hash(i * 5 + 3)) * sc;

            canvas.SaveState();
            canvas.Translate(r.X + r.Width * x, r.Y + r.Height * y);
            canvas.Rotate(t * 14f + i * 40f);
            var shard = new PathF();
            shard.MoveTo(0f, -s * 1.3f);
            shard.LineTo(s * 0.7f, 0f);
            shard.LineTo(0f, s * 1.3f);
            shard.LineTo(-s * 0.7f, 0f);
            shard.Close();
            canvas.FillColor = StoryPalette.Violet.WithAlpha(0.50f * k);
            canvas.FillPath(shard);
            canvas.RestoreState();
        }
    }

    private static void Hum(ICanvas canvas, RectF r, float sc, float t, float k)
    {
        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height * 0.55f;

        canvas.StrokeSize = MathF.Max(1f, 1.2f * sc);
        for (int i = 0; i < 3; i++)
        {
            float phase = StageMath.Frac(t / 4f + i / 3f);
            float rad = r.Height * (0.15f + 0.55f * phase);
            canvas.StrokeColor = (i % 2 == 0 ? StoryPalette.Magenta : StoryPalette.Cyan).WithAlpha(0.22f * (1f - phase) * k);
            canvas.DrawEllipse(cx - rad, cy - rad, rad * 2f, rad * 2f);
        }

        for (int i = 0; i < StoryPalette.Spectrum.Length; i++)
        {
            float a = t * 0.2f + i * MathF.PI * 2f / StoryPalette.Spectrum.Length;
            Dot(canvas, cx + MathF.Cos(a) * r.Height * 0.34f, cy + MathF.Sin(a) * r.Height * 0.20f, 2.0f * sc, StoryPalette.Spectrum[i], 0.7f * k, 3.0f);
        }
    }

    /// <summary>A small light: a soft halo and a bright centre.</summary>
    private static void Dot(ICanvas canvas, float x, float y, float radius, Color colour, float alpha, float haloScale)
    {
        if (alpha <= 0.01f) return;
        float halo = radius * haloScale;
        canvas.FillColor = colour.WithAlpha(0.18f * alpha);
        canvas.FillEllipse(x - halo, y - halo, halo * 2f, halo * 2f);
        canvas.FillColor = colour.WithAlpha(alpha);
        canvas.FillEllipse(x - radius, y - radius, radius * 2f, radius * 2f);
    }
}
