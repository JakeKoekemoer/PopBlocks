using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 6, the Frozen Expanse: opaque ice that hides the path becomes clear ice with lights and an open route.</summary>
internal static class ExpanseScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        float groundY = r.Y + r.Height * 0.8f;
        canvas.FillColor = SceneKit.ByState(state, "#9AA3B8", "#B8D2E6", "#DCEFFB");
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);

        // The route: hidden, then a faint line, then a lit path of stones.
        if (lv >= 1)
        {
            int stones = lv == 1 ? 4 : 8;
            for (int i = 0; i < stones; i++)
            {
                float t = i / (float)(stones - 1);
                SceneKit.Dot(canvas, r.X + r.Width * (0.1f + 0.8f * t), groundY + r.Height * (0.06f + 0.04f * MathF.Sin(t * 6f)),
                    r.Width * 0.01f, SceneKit.C(lv == 2 ? "#FEF3C7" : "#9BB6D0"));
            }
        }

        // Ice blocks: solid grey that hides what is behind them, frosty with outlines, then clear with only an edge.
        float[] xs = { 0.06f, 0.28f, 0.5f, 0.72f };
        float[] hs = { 0.45f, 0.58f, 0.4f, 0.52f };
        for (int i = 0; i < xs.Length; i++)
        {
            float bx = r.X + r.Width * xs[i], bw = r.Width * 0.2f;
            float h = r.Height * hs[i];
            float by = groundY - h;

            // What is inside: a frozen light, visible once the ice thins.
            if (lv >= 1)
            {
                SceneKit.Glow(canvas, bx + bw / 2f, by + h * 0.55f, bw * 0.3f, SceneKit.C("#FDE68A"), lv == 2 ? 0.35f : 0.12f);
                SceneKit.Dot(canvas, bx + bw / 2f, by + h * 0.55f, bw * 0.05f, SceneKit.C("#FEF3C7"));
            }

            float alpha = lv == 0 ? 0.95f : lv == 1 ? 0.45f : 0.18f;
            var fill = SceneKit.ByState(state, "#7C879E", "#A9CCE4", "#D8F0FF").WithAlpha(alpha);
            SceneKit.Poly(canvas, fill, (bx, groundY), (bx + bw * 0.1f, by + h * 0.1f), (bx + bw * 0.5f, by),
                (bx + bw * 0.9f, by + h * 0.12f), (bx + bw, groundY));
            canvas.StrokeColor = SceneKit.C("#EAF6FF").WithAlpha(lv == 2 ? 0.9f : 0.5f);
            canvas.StrokeSize = lv == 2 ? 2.5f : 1.5f;
            canvas.DrawLine(bx + bw * 0.1f, by + h * 0.1f, bx + bw * 0.5f, by);
            canvas.DrawLine(bx + bw * 0.5f, by, bx + bw * 0.9f, by + h * 0.12f);
        }

        // Snow specks.
        for (int i = 0; i < 10; i++)
            SceneKit.Dot(canvas, r.X + r.Width * ((i * 37 % 100) / 100f), r.Y + r.Height * ((i * 23 % 60) / 100f), 1.8f, Colors.White.WithAlpha(0.5f));
    }
}
