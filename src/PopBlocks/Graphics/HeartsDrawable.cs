using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// Three hearts, one for each continue an attempt may buy. A heart still available is filled (<c>#FF5C7A</c>, with a small
/// highlight on its left); one that has been used is an empty outline at 60%. While one is being spent it cracks: a zigzag
/// grows across it and the fill shrinks away. Colour is not the only cue: filled and empty differ in shape and fill.
/// </summary>
public sealed class HeartsDrawable : IDrawable
{
    public const int Total = 3;
    private const float Size = 22f;
    private const float Gap = 10f;

    private static readonly Color Fill = Color.FromArgb("#FF5C7A");
    private static readonly Color Light = Color.FromArgb("#FFB3C1");

    /// <summary>Hearts still available, 0 to 3. They are the leftmost ones.</summary>
    public int Left { get; set; } = Total;

    /// <summary>The heart being spent (index from the left), or -1. It is still counted as available while <see cref="CrackT"/> runs.</summary>
    public int CrackIndex { get; set; } = -1;

    /// <summary>0 to 1: how far the crack has gone and the fill has emptied.</summary>
    public float CrackT { get; set; }

    public static float WidthFor => Total * Size + (Total - 1) * Gap;
    public static float HeightFor => Size + 2f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float x0 = dirtyRect.X + (dirtyRect.Width - WidthFor) / 2f;
        float y0 = dirtyRect.Y + (dirtyRect.Height - Size) / 2f;

        for (int i = 0; i < Total; i++)
        {
            float x = x0 + i * (Size + Gap);
            bool cracking = i == CrackIndex;
            float filled = cracking ? 1f - CrackT : i < Left ? 1f : 0f;

            var heart = HeartPath(x, y0, Size);
            if (filled > 0.01f)
            {
                canvas.FillColor = Fill;
                canvas.SaveState();
                canvas.Translate(x + Size / 2f, y0 + Size / 2f);
                canvas.Scale(filled, filled);
                canvas.Translate(-(x + Size / 2f), -(y0 + Size / 2f));
                canvas.FillPath(heart);
                canvas.FillColor = Light.WithAlpha(0.7f);
                canvas.FillEllipse(x + Size * 0.16f, y0 + Size * 0.18f, Size * 0.2f, Size * 0.14f);
                canvas.RestoreState();
            }

            canvas.StrokeColor = Fill.WithAlpha(filled > 0.99f ? 1f : 0.6f);
            canvas.StrokeSize = 2f;
            canvas.DrawPath(heart);

            if (cracking && CrackT > 0f)
            {
                // A zigzag from the top notch to the bottom point, drawn to CrackT of its length.
                float cx = x + Size / 2f;
                var pts = new (float X, float Y)[]
                {
                    (cx, y0 + Size * 0.28f), (cx - Size * 0.12f, y0 + Size * 0.48f),
                    (cx + Size * 0.1f, y0 + Size * 0.64f), (cx - Size * 0.04f, y0 + Size * 0.98f),
                };
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 2f;
                float reach = CrackT * (pts.Length - 1);
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    float part = Math.Clamp(reach - s, 0f, 1f);
                    if (part <= 0f) break;
                    canvas.DrawLine(pts[s].X, pts[s].Y, pts[s].X + (pts[s + 1].X - pts[s].X) * part, pts[s].Y + (pts[s + 1].Y - pts[s].Y) * part);
                }
            }
        }
    }

    private static PathF HeartPath(float x, float y, float s)
    {
        var p = new PathF();
        p.MoveTo(x + s * 0.5f, y + s * 0.95f);
        p.CurveTo(x - s * 0.1f, y + s * 0.55f, x + s * 0.05f, y + s * 0.0f, x + s * 0.5f, y + s * 0.28f);
        p.CurveTo(x + s * 0.95f, y + s * 0.0f, x + s * 1.1f, y + s * 0.55f, x + s * 0.5f, y + s * 0.95f);
        p.Close();
        return p;
    }
}
