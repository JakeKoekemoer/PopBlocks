using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>Region 7, the Shattered Citadel: broken bridges reconnect and the central hall lights up.</summary>
internal static class CitadelScene
{
    public static void Draw(ICanvas canvas, RectF r, RegionState state)
    {
        int lv = SceneKit.Level(state);
        var stone = SceneKit.ByState(state, "#3A3F5A", "#4C5378", "#5F6A9A");
        float groundY = r.Y + r.Height * 0.85f;
        canvas.FillColor = SceneKit.ByState(state, "#2A2E45", "#353B5C", "#444C78");
        canvas.FillRectangle(r.X, groundY, r.Width, r.Bottom - groundY);

        // Two outer towers and a central hall with battlements.
        Tower(canvas, r.X + r.Width * 0.06f, groundY, r.Width * 0.16f, r.Height * 0.55f, stone);
        Tower(canvas, r.X + r.Width * 0.78f, groundY, r.Width * 0.16f, r.Height * 0.5f, stone);
        float hx = r.X + r.Width * 0.36f, hw = r.Width * 0.28f, hh = r.Height * 0.45f;
        canvas.FillColor = stone;
        canvas.FillRectangle(hx, groundY - hh, hw, hh);
        for (int i = 0; i < 5; i++) canvas.FillRectangle(hx + hw * i / 5f, groundY - hh - r.Height * 0.04f, hw / 8f, r.Height * 0.04f);

        // The hall's window and door: sealed and dark, a glimmer, then a lit hall with the seal on the door.
        float wx = hx + hw / 2f, wy = groundY - hh * 0.6f;
        if (lv >= 1) SceneKit.Glow(canvas, wx, wy, hw * 0.3f, SceneKit.C("#FDE68A"), lv == 2 ? 0.35f : 0.15f);
        canvas.FillColor = lv == 0 ? SceneKit.C("#1A1D30") : lv == 1 ? SceneKit.C("#B49A5A") : SceneKit.C("#FEF3C7");
        canvas.FillRoundedRectangle(wx - hw * 0.09f, wy - hh * 0.12f, hw * 0.18f, hh * 0.3f, hw * 0.08f);
        if (lv == 2)
        {
            SceneKit.Dot(canvas, wx, groundY - hh * 0.2f, hw * 0.07f, SceneKit.C("#1B2547").WithAlpha(0.6f));
            SceneKit.Dot(canvas, wx, groundY - hh * 0.2f, hw * 0.035f, SceneKit.C("#FDE68A"));
        }
        else
        {
            canvas.StrokeColor = SceneKit.C("#14172A");
            canvas.StrokeSize = 4f;
            canvas.DrawLine(wx - hw * 0.1f, groundY, wx + hw * 0.1f, groundY - hh * 0.3f);   // sealed door, crossed
            canvas.DrawLine(wx + hw * 0.1f, groundY, wx - hw * 0.1f, groundY - hh * 0.3f);
        }

        // Bridges between towers and hall: broken stubs, half a span, whole.
        float by = groundY - r.Height * 0.2f;
        Bridge(canvas, r.X + r.Width * 0.22f, hx, by, lv);
        Bridge(canvas, r.X + r.Width * 0.78f, hx + hw, by, lv, reverse: true);
    }

    private static void Tower(ICanvas canvas, float x, float groundY, float w, float h, Color stone)
    {
        canvas.FillColor = stone;
        canvas.FillRectangle(x, groundY - h, w, h);
        for (int i = 0; i < 3; i++) canvas.FillRectangle(x + w * i / 3f, groundY - h - h * 0.07f, w / 5f, h * 0.07f);
    }

    private static void Bridge(ICanvas canvas, float fromX, float toX, float y, int lv, bool reverse = false)
    {
        float span = toX - fromX;
        float cover = lv == 0 ? 0.25f : lv == 1 ? 0.6f : 1f;
        canvas.StrokeColor = SceneKit.C("#23273F");
        canvas.StrokeSize = 6f;
        canvas.DrawLine(fromX, y, fromX + span * cover, y);
        if (lv == 0) canvas.DrawLine(toX - span * 0.15f, y, toX, y);   // a broken stub on the far side
        if (lv == 2) SceneKit.Line(canvas, SceneKit.C("#FDE68A").WithAlpha(0.7f), 2f, fromX, y - 6f, toX, y - 6f);
    }
}
