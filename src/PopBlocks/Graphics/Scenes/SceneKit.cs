using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics.Scenes;

/// <summary>
/// Small drawing helpers shared by the region pictures. Every picture has three looks (Faded, Recovering, Restored)
/// that differ in shape as well as colour: sizes, gaps, open or closed forms, how many lights are on.
/// </summary>
internal static class SceneKit
{
    public static Color C(string hex) => Color.FromArgb(hex);

    public static Color Mix(Color a, Color b, float t) => new(
        a.Red + (b.Red - a.Red) * t,
        a.Green + (b.Green - a.Green) * t,
        a.Blue + (b.Blue - a.Blue) * t,
        1f);

    public static Color ByState(RegionState s, string faded, string recovering, string restored) =>
        C(s == RegionState.Faded ? faded : s == RegionState.Recovering ? recovering : restored);

    /// <summary>0 faded, 1 recovering, 2 restored.</summary>
    public static int Level(RegionState s) => s == RegionState.Faded ? 0 : s == RegionState.Recovering ? 1 : 2;

    public static void Glow(ICanvas canvas, float x, float y, float radius, Color colour, float strength = 0.22f)
    {
        for (int i = 3; i >= 1; i--)
        {
            float r = radius * (0.5f + 0.35f * i);
            canvas.FillColor = colour.WithAlpha(strength * (4 - i) / 3f);
            canvas.FillEllipse(x - r, y - r, r * 2f, r * 2f);
        }
    }

    public static void Dot(ICanvas canvas, float x, float y, float radius, Color colour)
    {
        canvas.FillColor = colour;
        canvas.FillEllipse(x - radius, y - radius, radius * 2f, radius * 2f);
    }

    public static void Poly(ICanvas canvas, Color fill, params (float X, float Y)[] points)
    {
        var p = new PathF();
        p.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++) p.LineTo(points[i].X, points[i].Y);
        p.Close();
        canvas.FillColor = fill;
        canvas.FillPath(p);
    }

    public static void Line(ICanvas canvas, Color colour, float width, float x1, float y1, float x2, float y2)
    {
        canvas.StrokeColor = colour;
        canvas.StrokeSize = width;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(x1, y1, x2, y2);
    }

    /// <summary>A toothed wheel: a disc with square teeth around the rim and a hole in the middle.</summary>
    public static void Gear(ICanvas canvas, float cx, float cy, float radius, int teeth, Color colour, Color hole)
    {
        canvas.FillColor = colour;
        canvas.FillEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
        float tw = radius * 0.34f, th = radius * 0.3f;
        for (int i = 0; i < teeth; i++)
        {
            float a = i * MathF.PI * 2f / teeth;
            float tx = cx + MathF.Cos(a) * radius * 1.08f;
            float ty = cy + MathF.Sin(a) * radius * 1.08f;
            canvas.FillRectangle(tx - tw / 2f, ty - th / 2f, tw, th);
        }
        canvas.FillColor = hole;
        canvas.FillEllipse(cx - radius * 0.35f, cy - radius * 0.35f, radius * 0.7f, radius * 0.7f);
    }

    public static (float X, float Y) P(RectF r, float fx, float fy) => (r.X + r.Width * fx, r.Y + r.Height * fy);
}
