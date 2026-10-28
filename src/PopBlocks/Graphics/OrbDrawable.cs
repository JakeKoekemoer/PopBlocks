using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// The Orb: a glass sphere filling its 2x2 cells (minus 3 units), a swirl of the five block colours turning slowly inside it, a
/// white highlight, and a ring round it that empties as its timer runs down, with the number of pieces left in the middle.
/// White while there is time, amber at three, red at one; a wobble and dark veins from two. Under Reduced motion the swirl does
/// not turn and nothing wobbles. The number is written out as well as drawn in a colour, so colour is never the only cue.
/// </summary>
public static class OrbDrawable
{
    private static readonly Color Light = Color.FromArgb("#F4F7FF");
    private static readonly Color Mid = Color.FromArgb("#8C7CFF");
    private static readonly Color Dark = Color.FromArgb("#2B1E6E");
    private static readonly Color Amber = Color.FromArgb("#FFB020");
    private static readonly Color Red = Color.FromArgb("#FF4D4D");
    private static readonly Color[] Swirl =
    {
        Color.FromArgb("#38BDF8"), Color.FromArgb("#FB7185"), Color.FromArgb("#FBBF24"), Color.FromArgb("#A78BFA"), Color.FromArgb("#A3E635"),
    };

    private static readonly string[] Numbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

    /// <param name="x">Left edge of the Orb''s 2x2 area on screen.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="size">Width and height of the 2x2 area (two cells).</param>
    /// <param name="timerLeft">Pieces left before it cracks the blocks round it.</param>
    /// <param name="timerMax">What the timer started at (the ring''s full length).</param>
    public static void Draw(ICanvas canvas, float x, float y, float size, int timerLeft, int timerMax, long nowMs, bool reducedMotion, bool animate)
    {
        float inset = 3f;
        float d = size - 2f * inset;
        float cx = x + size / 2f, cy = y + size / 2f, r = d / 2f;
        bool urgent = timerLeft is > 0 and <= 2;

        canvas.SaveState();
        if (urgent && !reducedMotion && animate)
        {
            float wobble = MathF.Sin(nowMs / 1000f * MathF.Tau * 3.2f) * 2.2f;   // a small shiver
            canvas.Translate(cx, cy);
            canvas.Rotate(wobble);
            canvas.Translate(-cx, -cy);
        }

        // The sphere: three round steps from dark at the edge to light near the top-left.
        canvas.FillColor = Dark;
        canvas.FillEllipse(cx - r, cy - r, d, d);
        canvas.FillColor = Mid;
        canvas.FillEllipse(cx - r * 0.88f, cy - r * 0.88f, d * 0.88f, d * 0.88f);
        canvas.FillColor = Light.WithAlpha(0.85f);
        canvas.FillEllipse(cx - r * 0.78f, cy - r * 0.78f, d * 0.52f, d * 0.52f);

        // The swirl: five arcs, one in each block colour, turning once in six seconds (still when time stands still).
        float turn = reducedMotion || !animate ? 0f : (nowMs % 6000) / 6000f * 360f;
        canvas.StrokeSize = MathF.Max(2f, size * 0.05f);
        canvas.StrokeLineCap = LineCap.Round;
        float sr = r * 0.58f;
        for (int i = 0; i < Swirl.Length; i++)
        {
            canvas.StrokeColor = Swirl[i].WithAlpha(0.35f);
            float start = turn + i * 72f;
            canvas.DrawArc(cx - sr, cy - sr, sr * 2f, sr * 2f, start, start + 52f, false, false);
        }

        // A white highlight.
        canvas.FillColor = Colors.White.WithAlpha(0.7f);
        canvas.FillEllipse(cx - r * 0.62f, cy - r * 0.74f, r * 0.62f, r * 0.34f);

        // Dark veins once it is nearly out of time.
        if (urgent)
        {
            canvas.StrokeColor = Colors.Black.WithAlpha(0.45f);
            canvas.StrokeSize = MathF.Max(1.2f, size * 0.025f);
            canvas.DrawLine(cx - r * 0.1f, cy - r * 0.95f, cx - r * 0.35f, cy - r * 0.4f);
            canvas.DrawLine(cx - r * 0.35f, cy - r * 0.4f, cx - r * 0.2f, cy + r * 0.1f);
            canvas.DrawLine(cx + r * 0.55f, cy + r * 0.85f, cx + r * 0.3f, cy + r * 0.35f);
        }

        // The timer ring, 4 units, emptying clockwise from the top as the pieces are used.
        float fraction = timerMax <= 0 ? 0f : Math.Clamp(timerLeft / (float)timerMax, 0f, 1f);
        var ring = timerLeft <= 1 ? Red : timerLeft <= 3 ? Amber : Colors.White;
        float rr = r + 1f;
        canvas.StrokeSize = 4f;
        canvas.StrokeColor = Colors.White.WithAlpha(0.18f);
        canvas.DrawEllipse(cx - rr, cy - rr, rr * 2f, rr * 2f);
        if (fraction > 0f)
        {
            canvas.StrokeColor = ring;
            canvas.DrawArc(cx - rr, cy - rr, rr * 2f, rr * 2f, 90f, 90f - 360f * fraction, true, false);
        }

        // The number, 20 bold with a dark outline.
        string text = Numbers[Math.Clamp(timerLeft, 0, Numbers.Length - 1)];
        canvas.FontSize = 20f;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontColor = Colors.Black.WithAlpha(0.8f);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    canvas.DrawString(text, cx - 30f + dx, cy - 14f + dy, 60f, 28f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = ring;
        canvas.DrawString(text, cx - 30f, cy - 14f, 60f, 28f, HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.RestoreState();
    }
}
