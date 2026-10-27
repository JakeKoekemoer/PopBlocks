using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>
/// Region 8, the Heart of Spectrum: a fractured Heart becomes a whole one, and light travels OUTWARD along visible
/// paths to eight lit regions. Power is shown moving away from the Heart, never gathering in it.
/// </summary>
internal static class HeartScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        float ringX = r.Width * 0.4f, ringY = r.Height * 0.36f;

        // The eight region nodes on a ring, and the paths from the Heart to them.
        for (int i = 0; i < 8; i++)
        {
            float a = -MathF.PI / 2f + i * MathF.PI * 2f / 8f;
            float nx = cx + MathF.Cos(a) * ringX, ny = cy + MathF.Sin(a) * ringY;
            bool lit = lv == 2 || (lv == 1 && i % 2 == 0);

            if (lit)
            {
                var path = SceneKit.C("#FDE68A").WithAlpha(lv == 2 ? 0.8f : 0.35f);
                SceneKit.Line(canvas, path, lv == 2 ? 3f : 2f, cx, cy, nx, ny);
                if (lv == 2)
                {
                    // Chevrons pointing away from the centre: the light is moving outward.
                    for (int k = 1; k <= 2; k++)
                    {
                        float t = 0.3f + 0.25f * k;
                        float px = cx + (nx - cx) * t, py = cy + (ny - cy) * t;
                        float ux = (nx - cx), uy = (ny - cy);
                        float len = MathF.Sqrt(ux * ux + uy * uy);
                        ux /= len; uy /= len;
                        float s = r.Width * 0.015f;
                        SceneKit.Line(canvas, path, 2.5f, px - ux * s - uy * s, py - uy * s + ux * s, px, py);
                        SceneKit.Line(canvas, path, 2.5f, px - ux * s + uy * s, py - uy * s - ux * s, px, py);
                    }
                }
            }

            SceneKit.Glow(canvas, nx, ny, r.Width * 0.025f, SceneKit.C(lit ? "#FDE68A" : "#5A6285"), lit ? 0.3f : 0.1f);
            SceneKit.Dot(canvas, nx, ny, r.Width * 0.018f, SceneKit.C(lit ? "#FEF3C7" : "#3E4568"));
        }

        // The Heart: scattered pieces, pieces drawing together, a whole diamond.
        float hr = MathF.Min(r.Width, r.Height) * 0.13f;
        var body = SceneKit.ByState(state, "#6A7298", "#9AA8F0", "#E0F2FF");
        if (lv >= 1) SceneKit.Glow(canvas, cx, cy, hr, SceneKit.C("#BAE6FD"), lv == 2 ? 0.35f : 0.15f);

        float gap = lv == 0 ? hr * 0.35f : lv == 1 ? hr * 0.12f : 0f;
        // Four quarters of the diamond, pushed apart by `gap`.
        SceneKit.Poly(canvas, body, (cx, cy - gap), (cx, cy - hr - gap), (cx - hr * 0.7f - gap, cy - gap));
        SceneKit.Poly(canvas, body.WithAlpha(0.92f), (cx + gap, cy), (cx + hr * 0.7f + gap, cy), (cx + gap, cy - hr * 0.9f));
        SceneKit.Poly(canvas, body.WithAlpha(0.85f), (cx - gap, cy + gap), (cx - hr * 0.7f - gap, cy + gap), (cx - gap, cy + hr + gap));
        SceneKit.Poly(canvas, body.WithAlpha(0.95f), (cx + gap, cy + gap), (cx + gap, cy + hr * 0.9f + gap), (cx + hr * 0.7f + gap, cy + gap));
        if (lv == 2) SceneKit.Dot(canvas, cx, cy, hr * 0.18f, Colors.White);
    }
}
