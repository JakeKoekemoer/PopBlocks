using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 4, the Sunken City: dark streets under water gain lit windows, glowing channels and a beacon.</summary>
internal static class CityScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        var wall = SceneKit.ByState(state, "#1E2742", "#2A3A5E", "#33507A");

        // Buildings of different heights along the sea floor.
        float groundY = r.Y + r.Height * 0.86f;
        float[] xs = { 0.04f, 0.2f, 0.36f, 0.58f, 0.74f, 0.88f };
        float[] hs = { 0.34f, 0.5f, 0.28f, 0.6f, 0.4f, 0.3f };
        float bw = r.Width * 0.11f;
        for (int i = 0; i < xs.Length; i++)
        {
            float bx = r.X + r.Width * xs[i];
            float h = r.Height * hs[i];
            canvas.FillColor = wall;
            canvas.FillRectangle(bx, groundY - h, bw, h);
            int rows = 3, cols = 2;
            for (int a = 0; a < rows; a++)
                for (int b = 0; b < cols; b++)
                {
                    // Windows: none lit, some lit, most lit.
                    bool lit = lv == 2 ? (a + b + i) % 5 != 0 : lv == 1 && (a + b + i) % 3 == 0;
                    float wx = bx + bw * (0.2f + 0.4f * b), wy = groundY - h + h * (0.15f + 0.25f * a);
                    canvas.FillColor = lit ? SceneKit.C("#FDE68A") : SceneKit.C("#141A30");
                    canvas.FillRectangle(wx, wy, bw * 0.22f, h * 0.12f);
                }
        }

        // Beacon on the tallest tower: dark, flickering (small), shining with rays.
        float tx = r.X + r.Width * 0.58f + bw / 2f;
        float ty = groundY - r.Height * 0.6f - r.Height * 0.04f;
        if (lv >= 1) SceneKit.Glow(canvas, tx, ty, r.Width * (lv == 1 ? 0.04f : 0.07f), SceneKit.C("#7DD3FC"), 0.3f);
        SceneKit.Dot(canvas, tx, ty, r.Width * 0.012f, lv == 0 ? SceneKit.C("#3A4668") : SceneKit.C("#E0F2FE"));
        if (lv == 2)
            for (int i = 0; i < 5; i++)
            {
                float a = -MathF.PI + MathF.PI * (i + 0.5f) / 5f;
                SceneKit.Line(canvas, SceneKit.C("#BAE6FD").WithAlpha(0.5f), 2f, tx, ty,
                    tx + MathF.Cos(a) * r.Width * 0.12f, ty + MathF.Sin(a) * r.Width * 0.12f);
            }

        // Channels along the sea floor: absent, faint, glowing.
        if (lv >= 1)
        {
            canvas.StrokeColor = SceneKit.C("#38BDF8").WithAlpha(lv == 1 ? 0.35f : 0.8f);
            canvas.StrokeSize = lv == 1 ? 2f : 4f;
            canvas.DrawLine(r.X, groundY + r.Height * 0.05f, r.Right, groundY + r.Height * 0.05f);
        }

        // Surface waves at the top.
        canvas.StrokeColor = Colors.White.WithAlpha(0.15f);
        canvas.StrokeSize = 2f;
        for (int i = 0; i < 4; i++)
            canvas.DrawLine(r.X + r.Width * (0.05f + 0.25f * i), r.Y + r.Height * 0.08f, r.X + r.Width * (0.15f + 0.25f * i), r.Y + r.Height * 0.08f);
    }
}
