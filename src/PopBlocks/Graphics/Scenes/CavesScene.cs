using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 2, the Crystal Caves: crystals brighten, the path lights up, the inscription becomes readable.</summary>
internal static class CavesScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        var rock = SceneKit.ByState(state, "#2B3150", "#39406A", "#4A5388");
        var floor = SceneKit.ByState(state, "#232846", "#2E3558", "#3A4272");

        // Cave roof with hanging points.
        SceneKit.Poly(canvas, rock, (r.X, r.Y), (r.Right, r.Y), (r.Right, r.Y + r.Height * 0.2f),
            (r.X + r.Width * 0.85f, r.Y + r.Height * 0.34f), (r.X + r.Width * 0.7f, r.Y + r.Height * 0.2f),
            (r.X + r.Width * 0.52f, r.Y + r.Height * 0.38f), (r.X + r.Width * 0.35f, r.Y + r.Height * 0.18f),
            (r.X + r.Width * 0.15f, r.Y + r.Height * 0.32f), (r.X, r.Y + r.Height * 0.2f));

        float groundY = r.Y + r.Height * 0.78f;
        canvas.FillColor = floor;
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);

        // Inscription on the back wall: scratches (faded), half the words (recovering), a clear row of glyphs (restored).
        float iy = r.Y + r.Height * 0.5f;
        var ink = SceneKit.ByState(state, "#4A5170", "#9AA6D8", "#F5E6A8");
        int marks = lv == 0 ? 3 : lv == 1 ? 5 : 8;
        for (int i = 0; i < marks; i++)
        {
            float mx = r.X + r.Width * (0.3f + 0.4f * i / 8f);
            float h = r.Height * (lv == 0 ? 0.03f : 0.07f);
            SceneKit.Line(canvas, ink, MathF.Max(2f, r.Width * 0.008f), mx, iy, mx + (lv == 0 ? r.Width * 0.015f : 0), iy + h);
            if (lv == 2) SceneKit.Line(canvas, ink, MathF.Max(2f, r.Width * 0.008f), mx - r.Width * 0.01f, iy + h * 0.5f, mx + r.Width * 0.01f, iy + h * 0.5f);
        }

        // Crystals: small and dull, larger with a glow, tall and bright.
        float[] xs = { 0.12f, 0.26f, 0.42f, 0.6f, 0.76f, 0.9f };
        float[] hs = { 0.20f, 0.30f, 0.22f, 0.34f, 0.24f, 0.28f };
        float scale = lv == 0 ? 0.55f : lv == 1 ? 0.85f : 1.1f;
        for (int i = 0; i < xs.Length; i++)
        {
            float cx = r.X + r.Width * xs[i];
            float h = r.Height * hs[i] * scale;
            float w = r.Width * 0.05f * scale;
            var body = SceneKit.ByState(state, "#4B5280", i % 2 == 0 ? "#7E8CFF" : "#9B7BFF", i % 2 == 0 ? "#9FD4FF" : "#C9A8FF");
            if (lv > 0) SceneKit.Glow(canvas, cx, groundY - h * 0.5f, h * 0.6f, body, lv == 2 ? 0.28f : 0.14f);
            SceneKit.Poly(canvas, body, (cx - w, groundY), (cx - w * 0.5f, groundY - h * 0.8f), (cx, groundY - h),
                (cx + w * 0.5f, groundY - h * 0.8f), (cx + w, groundY));
            SceneKit.Line(canvas, Colors.White.WithAlpha(lv == 0 ? 0.1f : 0.4f), 1.5f, cx - w * 0.2f, groundY - h * 0.2f, cx - w * 0.1f, groundY - h * 0.8f);
        }

        // A path of floor lights appears as the caves are restored.
        int lights = lv == 0 ? 0 : lv == 1 ? 3 : 7;
        for (int i = 0; i < lights; i++)
            SceneKit.Dot(canvas, r.X + r.Width * (0.1f + 0.8f * i / 7f), groundY + r.Height * 0.1f, r.Width * 0.008f, SceneKit.C("#FEF3C7"));
    }
}
