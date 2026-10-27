using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 3, the Verdant Wilds: still vines and a silent machine become climbing vines, running water and lit lamps.</summary>
internal static class WildsScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        float groundY = r.Y + r.Height * 0.8f;
        canvas.FillColor = SceneKit.ByState(state, "#2F3B4F", "#3F6B45", "#3C8A3E");
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);

        // The machine: a wheel on a frame. It turns only when restored (shown by spokes at an angle and motion arcs).
        float mx = r.X + r.Width * 0.5f, my = groundY - r.Height * 0.22f;
        float mr = r.Width * 0.09f;
        SceneKit.Gear(canvas, mx, my, mr, 8, SceneKit.ByState(state, "#59627E", "#8C8F9F", "#C79B52"), SceneKit.ByState(state, "#2F3B4F", "#2E4A33", "#2E5B2F"));
        canvas.FillColor = SceneKit.C("#3A3F52");
        canvas.FillRectangle(mx - mr * 0.15f, my + mr, mr * 0.3f, groundY - my - mr);
        if (lv == 2)
        {
            canvas.StrokeColor = Colors.White.WithAlpha(0.35f);
            canvas.StrokeSize = 2f;
            canvas.DrawArc(mx - mr * 1.5f, my - mr * 1.5f, mr * 3f, mr * 3f, 20, 110, false, false);
            canvas.DrawArc(mx - mr * 1.5f, my - mr * 1.5f, mr * 3f, mr * 3f, 200, 290, false, false);
        }

        // Water: dry bed, a thin trickle, a flowing stream with ripples.
        float wy = groundY + r.Height * 0.08f;
        if (lv >= 1)
        {
            canvas.StrokeColor = SceneKit.C("#7DD3FC").WithAlpha(lv == 1 ? 0.6f : 0.9f);
            canvas.StrokeSize = lv == 1 ? 3f : 7f;
            var p = new PathF();
            p.MoveTo(r.X, wy);
            p.QuadTo(r.X + r.Width * 0.25f, wy - r.Height * 0.05f, r.X + r.Width * 0.5f, wy);
            p.QuadTo(r.X + r.Width * 0.75f, wy + r.Height * 0.05f, r.Right, wy);
            canvas.DrawPath(p);
        }

        // Vines: drooping, then climbing the posts with leaves.
        float[] xs = { 0.12f, 0.3f, 0.72f, 0.88f };
        for (int i = 0; i < xs.Length; i++)
        {
            float vx = r.X + r.Width * xs[i];
            float h = r.Height * (lv == 0 ? 0.28f : 0.5f);
            var vine = SceneKit.ByState(state, "#4E5A74", "#5FA35F", "#74D26A");
            canvas.FillColor = SceneKit.C("#3A3F52");
            canvas.FillRectangle(vx - 3f, groundY - r.Height * 0.55f, 6f, r.Height * 0.55f);
            canvas.StrokeColor = vine;
            canvas.StrokeSize = 3f;
            var path = new PathF();
            if (lv == 0)
            {
                path.MoveTo(vx, groundY - r.Height * 0.55f);
                path.QuadTo(vx + r.Width * 0.04f, groundY - r.Height * 0.4f, vx + r.Width * 0.02f, groundY - r.Height * 0.25f);
            }
            else
            {
                path.MoveTo(vx, groundY);
                path.QuadTo(vx + r.Width * 0.03f, groundY - h * 0.5f, vx, groundY - h);
                for (int l = 0; l < 4; l++)
                    SceneKit.Dot(canvas, vx + (l % 2 == 0 ? 8f : -8f), groundY - h * (0.2f + 0.2f * l), r.Width * 0.012f, vine);
            }
            canvas.DrawPath(path);
        }

        // Workshop lamps.
        int lamps = lv == 0 ? 0 : lv == 1 ? 1 : 3;
        for (int i = 0; i < lamps; i++)
        {
            float lx = r.X + r.Width * (0.25f + 0.25f * i);
            float ly = groundY - r.Height * 0.5f;
            SceneKit.Glow(canvas, lx, ly, r.Width * 0.03f, SceneKit.C("#FDE68A"), 0.3f);
            SceneKit.Dot(canvas, lx, ly, r.Width * 0.012f, SceneKit.C("#FEF3C7"));
        }
    }
}
