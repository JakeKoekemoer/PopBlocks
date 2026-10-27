using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// The Warden, as set out in the artist brief: a tall, narrow, cloaked figure with angular layered shoulders and a
/// tapered cloak. The face is hidden; only a thin violet slit shows. Nothing about it is round, cute or floating, so
/// it can never be mistaken for Pip, even as a flat silhouette at portrait size.
/// Drawn with one lit edge (violet) and one dim edge, so the figure has a shape in the dark.
/// </summary>
public sealed class WardenDrawable : IDrawable
{
    /// <summary>0..1: how much of the figure has emerged (opacity and rim light). 1 is fully there.</summary>
    public float Reveal { get; set; } = 1f;

    /// <summary>Seconds on an animation clock; the cloak edge drifts while it runs. Negative (the default) is still.</summary>
    public float Time { get; set; } = -1f;

    /// <summary>Holds the figure perfectly still (the "A pause." beat): no drift, a steady eye.</summary>
    public bool Still { get; set; }

    /// <summary>Head and shoulders only, filling the frame. Used for the dialogue portrait.</summary>
    public bool Bust { get; set; }

    // The cloak outline in figure units: x from the centre line, y from the top of the hood (0) to the hem (1).
    // Clockwise from the peak: right side down to the hem, the hem across, the left side back up.
    private static readonly float[] OutX =
    {
        0.000f, 0.085f, 0.120f, 0.150f, 0.290f, 0.325f, 0.255f, 0.285f, 0.350f, 0.320f,
        0.200f, 0.100f, 0.000f, -0.100f, -0.200f,
        -0.320f, -0.350f, -0.285f, -0.255f, -0.325f, -0.290f, -0.150f, -0.120f, -0.085f,
    };

    private static readonly float[] OutY =
    {
        0.000f, 0.045f, 0.135f, 0.190f, 0.225f, 0.300f, 0.360f, 0.600f, 0.820f, 1.000f,
        0.965f, 1.000f, 0.960f, 1.000f, 0.965f,
        1.000f, 0.820f, 0.600f, 0.360f, 0.300f, 0.225f, 0.190f, 0.135f, 0.045f,
    };

    private static readonly Color Cloak = Color.FromArgb("#15102E");
    private static readonly Color Mantle = Color.FromArgb("#241A52");
    private static readonly Color Hollow = Color.FromArgb("#05040E");

    public void Draw(ICanvas canvas, RectF r)
    {
        if (r.Width < 8f || r.Height < 8f) return;
        float rev = StageMath.Clamp01(Reveal);
        if (rev <= 0.001f) return;

        bool drift = Time >= 0f && !Still;
        float scale = Bust ? r.Height * 1.75f : MathF.Min(r.Height * 0.94f, r.Width * 1.35f);
        float cx = r.X + r.Width / 2f;
        float top = Bust ? r.Y + r.Height * 0.08f : r.Bottom - scale - r.Height * 0.03f;
        float eyeGlow = Still ? 0.7f : (Time >= 0f ? 0.8f + 0.2f * MathF.Sin(Time * 1.7f) : 0.9f);

        canvas.SaveState();
        canvas.ClipRectangle(r.X, r.Y, r.Width, r.Height);

        // A faint violet aura behind the figure.
        for (int i = 3; i >= 1; i--)
        {
            float rad = scale * (0.20f + 0.15f * i);
            canvas.FillColor = StoryPalette.Violet.WithAlpha(0.05f * rev * (4 - i) / 3f);
            canvas.FillEllipse(cx - rad, top + scale * 0.38f - rad, rad * 2f, rad * 2f);
        }

        // Outline points, with the hem drifting a little while the clock runs.
        int n = OutX.Length;
        var px = new float[n];
        var py = new float[n];
        for (int i = 0; i < n; i++)
        {
            float nx = OutX[i];
            float ny = OutY[i];
            if (drift && ny > 0.5f) nx += MathF.Sin(Time * 0.9f + i * 1.3f) * 0.008f * (ny - 0.5f) * 2f;
            px[i] = cx + nx * scale;
            py[i] = top + ny * scale;
        }

        // Cloak
        var body = new PathF();
        body.MoveTo(px[0], py[0]);
        for (int i = 1; i < n; i++) body.LineTo(px[i], py[i]);
        body.Close();
        canvas.FillColor = Cloak.WithAlpha(rev);
        canvas.FillPath(body);

        // Shoulder mantle: a second, lighter layer
        var mantle = new PathF();
        mantle.MoveTo(cx + 0.150f * scale, top + 0.190f * scale);
        mantle.LineTo(cx + 0.290f * scale, top + 0.225f * scale);
        mantle.LineTo(cx + 0.325f * scale, top + 0.300f * scale);
        mantle.LineTo(cx + 0.255f * scale, top + 0.360f * scale);
        mantle.LineTo(cx, top + 0.335f * scale);
        mantle.LineTo(cx - 0.255f * scale, top + 0.360f * scale);
        mantle.LineTo(cx - 0.325f * scale, top + 0.300f * scale);
        mantle.LineTo(cx - 0.290f * scale, top + 0.225f * scale);
        mantle.LineTo(cx - 0.150f * scale, top + 0.190f * scale);
        mantle.LineTo(cx, top + 0.215f * scale);
        mantle.Close();
        canvas.FillColor = Mantle.WithAlpha(rev);
        canvas.FillPath(mantle);

        // Folds in the cloak
        canvas.StrokeColor = Hollow.WithAlpha(0.55f * rev);
        canvas.StrokeSize = MathF.Max(1f, scale * 0.012f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx - 0.07f * scale, top + 0.38f * scale, cx - 0.14f * scale, top + 0.95f * scale);
        canvas.DrawLine(cx + 0.07f * scale, top + 0.38f * scale, cx + 0.14f * scale, top + 0.95f * scale);

        // The hollow of the hood
        var hood = new PathF();
        hood.MoveTo(cx, top + 0.040f * scale);
        hood.LineTo(cx + 0.070f * scale, top + 0.100f * scale);
        hood.LineTo(cx + 0.062f * scale, top + 0.175f * scale);
        hood.LineTo(cx, top + 0.205f * scale);
        hood.LineTo(cx - 0.062f * scale, top + 0.175f * scale);
        hood.LineTo(cx - 0.070f * scale, top + 0.100f * scale);
        hood.Close();
        canvas.FillColor = Hollow.WithAlpha(rev);
        canvas.FillPath(hood);

        // Rim light: bright on the right edge, dim on the left. This is what gives the figure its outline in the dark.
        var lit = new PathF();
        lit.MoveTo(px[0], py[0]);
        for (int i = 1; i <= 9; i++) lit.LineTo(px[i], py[i]);
        canvas.StrokeColor = StoryPalette.Violet.WithAlpha(0.85f * rev);
        canvas.StrokeSize = MathF.Max(1.2f, scale * 0.013f);
        canvas.DrawPath(lit);

        var dim = new PathF();
        dim.MoveTo(px[0], py[0]);
        for (int i = n - 1; i >= 15; i--) dim.LineTo(px[i], py[i]);
        canvas.StrokeColor = StoryPalette.Violet.WithAlpha(0.30f * rev);
        canvas.DrawPath(dim);

        // The eye: a thin violet slit, with a wide soft glow under it.
        float ey = top + 0.125f * scale;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeColor = StoryPalette.Violet.WithAlpha(0.28f * rev * eyeGlow);
        canvas.StrokeSize = MathF.Max(2f, scale * 0.032f);
        canvas.DrawLine(cx - 0.030f * scale, ey, cx + 0.030f * scale, ey);
        canvas.StrokeColor = Color.FromArgb("#E4D8FF").WithAlpha(rev * eyeGlow);
        canvas.StrokeSize = MathF.Max(1.2f, scale * 0.009f);
        canvas.DrawLine(cx - 0.030f * scale, ey, cx + 0.030f * scale, ey);

        canvas.RestoreState();
    }
}

/// <summary>
/// The Warden's mark: one simple, symmetric sign with a split at its centre. An outer ring open at top and bottom, a
/// diamond whose halves do not quite meet, and four chevrons: the top and bottom pair point in (toward the centre),
/// the left and right pair point out. That is the inward and outward reading the story is about.
/// It is made of strokes, so it can be drawn on a piece at a time, then glow. It is legible as a small icon and as a
/// large wall symbol.
/// </summary>
public sealed class WardenMarkDrawable : IDrawable
{
    /// <summary>0..1: how much of the mark has been drawn on.</summary>
    public float Progress { get; set; } = 1f;

    /// <summary>0..1: strength of the glow after it is drawn.</summary>
    public float Glow { get; set; }

    /// <summary>Seconds on an animation clock; the glow breathes while it runs. Negative (the default) is still.</summary>
    public float Time { get; set; } = -1f;

    public Color Colour { get; set; } = StoryPalette.Violet;

    // Each stroke is a run of x,y pairs in mark units, -1..1 from the centre, y down.
    private static readonly float[][] Strokes;
    private static readonly float Total;

    static WardenMarkDrawable()
    {
        var list = new List<float[]>
        {
            Arc(-70f, 70f, 0.92f, 14),                                   // outer ring, right half (open at top and bottom)
            Arc(110f, 250f, 0.92f, 14),                                  // outer ring, left half
            new[] { 0.04f, -0.55f, 0.46f, 0f, 0.04f, 0.55f },            // diamond, right half
            new[] { -0.04f, -0.55f, -0.46f, 0f, -0.04f, 0.55f },         // diamond, left half
            new[] { 0f, -0.28f, 0f, -0.07f },                            // the split in the centre, upper
            new[] { 0f, 0.07f, 0f, 0.28f },                              // ...and lower
            new[] { -0.14f, -0.80f, 0f, -0.68f, 0.14f, -0.80f },         // chevron pointing in, top
            new[] { -0.14f, 0.80f, 0f, 0.68f, 0.14f, 0.80f },            // chevron pointing in, bottom
            new[] { 0.60f, -0.12f, 0.72f, 0f, 0.60f, 0.12f },            // chevron pointing out, right
            new[] { -0.60f, -0.12f, -0.72f, 0f, -0.60f, 0.12f },         // chevron pointing out, left
        };
        Strokes = list.ToArray();

        float total = 0f;
        foreach (var s in Strokes)
            for (int i = 2; i < s.Length; i += 2)
                total += MathF.Sqrt((s[i] - s[i - 2]) * (s[i] - s[i - 2]) + (s[i + 1] - s[i - 1]) * (s[i + 1] - s[i - 1]));
        Total = total;
    }

    /// <summary>An arc of the ring as short straight pieces. Angles in degrees, 0 is to the right, counter-clockwise.</summary>
    private static float[] Arc(float fromDeg, float toDeg, float radius, int steps)
    {
        var pts = new float[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++)
        {
            float a = (fromDeg + (toDeg - fromDeg) * i / steps) * MathF.PI / 180f;
            pts[i * 2] = MathF.Cos(a) * radius;
            pts[i * 2 + 1] = -MathF.Sin(a) * radius;
        }
        return pts;
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        float s = MathF.Min(r.Width, r.Height) * 0.46f;
        if (s < 6f) return;
        float p = StageMath.Clamp01(Progress);
        if (p <= 0f) return;

        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height / 2f;
        float glow = StageMath.Clamp01(Glow);
        float breathe = Time >= 0f ? 0.85f + 0.15f * MathF.Sin(Time * 2.6f) : 1f;
        float remaining = p * Total;

        canvas.StrokeLineCap = LineCap.Round;

        // Glow first (wide and faint), so the crisp line sits on top of it.
        if (glow > 0.01f)
        {
            DrawStrokes(canvas, cx, cy, s, Total, Colour.WithAlpha(0.14f * glow * breathe), s * 0.22f);
            DrawStrokes(canvas, cx, cy, s, Total, Colour.WithAlpha(0.26f * glow * breathe), s * 0.11f);
        }

        var tip = DrawStrokes(canvas, cx, cy, s, remaining, StoryPalette.Mix(Colour, Colors.White, 0.35f), MathF.Max(1.6f, s * 0.055f));

        // While it is still being drawn, a bright point shows where the line has got to.
        if (p < 1f)
        {
            float tr = MathF.Max(2f, s * 0.06f);
            canvas.FillColor = Colour.WithAlpha(0.35f);
            canvas.FillEllipse(tip.X - tr * 2f, tip.Y - tr * 2f, tr * 4f, tr * 4f);
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(tip.X - tr, tip.Y - tr, tr * 2f, tr * 2f);
        }
    }

    /// <summary>Draws strokes in order until <paramref name="length"/> has been used. Returns where it stopped.</summary>
    private static (float X, float Y) DrawStrokes(ICanvas canvas, float cx, float cy, float s, float length, Color colour, float width)
    {
        canvas.StrokeColor = colour;
        canvas.StrokeSize = width;
        float left = length;
        float lastX = cx;
        float lastY = cy;

        foreach (var stroke in Strokes)
        {
            if (left <= 0f) break;

            var path = new PathF();
            float x = cx + stroke[0] * s;
            float y = cy + stroke[1] * s;
            path.MoveTo(x, y);
            lastX = x;
            lastY = y;

            for (int i = 2; i < stroke.Length; i += 2)
            {
                float nx = cx + stroke[i] * s;
                float ny = cy + stroke[i + 1] * s;
                float dx = nx - x;
                float dy = ny - y;
                float seg = MathF.Sqrt(dx * dx + dy * dy) / s;   // back in mark units, to match Total

                if (seg <= left)
                {
                    path.LineTo(nx, ny);
                    left -= seg;
                    x = nx;
                    y = ny;
                    lastX = nx;
                    lastY = ny;
                }
                else
                {
                    float f = seg <= 0f ? 0f : left / seg;
                    lastX = x + dx * f;
                    lastY = y + dy * f;
                    path.LineTo(lastX, lastY);
                    left = 0f;
                    break;
                }
            }

            canvas.DrawPath(path);
        }

        return (lastX, lastY);
    }
}
