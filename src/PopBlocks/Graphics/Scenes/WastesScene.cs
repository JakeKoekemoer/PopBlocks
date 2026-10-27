using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 5, the Clockwork Wastes: stopped gears and a broken bridge become moving mechanisms and a whole bridge.</summary>
internal static class WastesScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        float groundY = r.Y + r.Height * 0.82f;
        canvas.FillColor = SceneKit.ByState(state, "#3A3A48", "#4F4A45", "#6B5A44");
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);

        var metal = SceneKit.ByState(state, "#5A5E70", "#9C8A64", "#E0B050");
        var hole = SceneKit.ByState(state, "#2A2D3A", "#3A342C", "#4A3C28");

        // Big gears; they sit at an angle in the recovering state and have a lit hub when restored.
        SceneKit.Gear(canvas, r.X + r.Width * 0.22f, r.Y + r.Height * 0.5f, r.Width * 0.12f, 10, metal, hole);
        SceneKit.Gear(canvas, r.X + r.Width * 0.38f, r.Y + r.Height * 0.62f, r.Width * 0.07f, 8, metal, hole);
        SceneKit.Gear(canvas, r.X + r.Width * 0.78f, r.Y + r.Height * 0.45f, r.Width * 0.1f, 9, metal, hole);
        if (lv == 2)
            foreach (var (fx, fy) in new[] { (0.22f, 0.5f), (0.38f, 0.62f), (0.78f, 0.45f) })
                SceneKit.Glow(canvas, r.X + r.Width * fx, r.Y + r.Height * fy, r.Width * 0.03f, SceneKit.C("#FDE68A"), 0.35f);

        // The bridge across a gap: two stubs, a half bridge, then a whole bridge.
        float by = groundY - r.Height * 0.12f;
        float left = r.X + r.Width * 0.45f, right = r.X + r.Width * 0.7f;
        canvas.StrokeColor = SceneKit.C("#2B2F40");
        canvas.StrokeSize = 6f;
        canvas.DrawLine(left, by, left + (right - left) * (lv == 0 ? 0.2f : lv == 1 ? 0.6f : 1f), by);
        if (lv == 0) canvas.DrawLine(right - (right - left) * 0.2f, by, right, by);
        if (lv == 2)
            for (int i = 0; i <= 6; i++)
                SceneKit.Line(canvas, SceneKit.C("#FDE68A").WithAlpha(0.8f), 2f, left + (right - left) * i / 6f, by - 3f, left + (right - left) * i / 6f, by - 8f);

        // A clock face on the right: dark, hands stopped, then lit.
        float cx = r.X + r.Width * 0.9f, cy = r.Y + r.Height * 0.25f, cr = r.Width * 0.05f;
        SceneKit.Dot(canvas, cx, cy, cr, lv == 0 ? SceneKit.C("#2A2D3A") : SceneKit.C("#FEF3C7"));
        SceneKit.Line(canvas, SceneKit.C("#1B2547"), 2f, cx, cy, cx, cy - cr * 0.7f);
        SceneKit.Line(canvas, SceneKit.C("#1B2547"), 2f, cx, cy, cx + cr * (lv == 2 ? 0.5f : 0f), cy + cr * (lv == 2 ? 0.2f : -0.4f));
    }
}
