using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

public enum IconKind
{
    None, Map, Cart, Book, Help, Share, Settings, Info, Infinity,

    // The game controls and abilities
    ArrowLeft, ArrowRight, ArrowDown, ArrowUp, FarLeft, FarRight, Drop, DropUp, Pause, Undo, Shift, Insight, Eye, Rotate,
}

/// <summary>
/// Small line icons for the menu buttons, drawn on a 24 x 24 grid and scaled to whatever square they are given. They are drawn
/// in code like everything else in the game, so there are no image files to ship and they take the colour of the text beside them.
/// </summary>
public sealed class IconDrawable : IDrawable
{
    public IconKind Kind { get; set; } = IconKind.None;
    public Color Colour { get; set; } = Colors.White;

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Kind == IconKind.None) return;

        float s = MathF.Min(rect.Width, rect.Height) / 24f;
        canvas.SaveState();
        canvas.Translate(rect.X + (rect.Width - 24f * s) / 2f, rect.Y + (rect.Height - 24f * s) / 2f);
        canvas.Scale(s, s);
        canvas.StrokeColor = Colour;
        canvas.FillColor = Colour;
        canvas.StrokeSize = 2f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        switch (Kind)
        {
            case IconKind.Map: DrawMap(canvas); break;
            case IconKind.Cart: DrawCart(canvas); break;
            case IconKind.Book: DrawBook(canvas); break;
            case IconKind.Help: DrawHelp(canvas); break;
            case IconKind.Share: DrawShare(canvas); break;
            case IconKind.Settings: DrawSettings(canvas); break;
            case IconKind.Info: DrawInfo(canvas); break;
            case IconKind.Infinity: DrawInfinity(canvas); break;
            case IconKind.ArrowLeft: Chevron(canvas, 15f, 5f, 8f, 12f, 15f, 19f); break;
            case IconKind.ArrowRight: Chevron(canvas, 9f, 5f, 16f, 12f, 9f, 19f); break;
            case IconKind.ArrowDown: Chevron(canvas, 5f, 9f, 12f, 16f, 19f, 9f); break;
            case IconKind.ArrowUp: Chevron(canvas, 5f, 15f, 12f, 8f, 19f, 15f); break;
            case IconKind.FarLeft: DrawFar(canvas, false); break;
            case IconKind.FarRight: DrawFar(canvas, true); break;
            case IconKind.Drop: DrawDrop(canvas); break;
            case IconKind.DropUp:
                canvas.Translate(0f, 24f);
                canvas.Scale(1f, -1f);
                DrawDrop(canvas);
                break;
            case IconKind.Pause: DrawPause(canvas); break;
            case IconKind.Undo: DrawUndo(canvas); break;
            case IconKind.Shift: DrawShift(canvas); break;
            case IconKind.Insight: DrawBulb(canvas); break;
            case IconKind.Eye: DrawEye(canvas); break;
            case IconKind.Rotate: DrawRotate(canvas); break;
        }

        canvas.RestoreState();
    }

    /// <summary>A circular arrow: most of a circle, clockwise, ending in an arrowhead.</summary>
    private static void DrawRotate(ICanvas canvas)
    {
        const float cx = 12f, cy = 12f, r = 7.5f;
        float start = 25f * MathF.PI / 180f, end = 305f * MathF.PI / 180f;
        var p = new PathF();
        const int steps = 24;
        for (int i = 0; i <= steps; i++)
        {
            float a = start + (end - start) * i / steps;
            float x = cx + r * MathF.Cos(a), y = cy + r * MathF.Sin(a);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
        }
        canvas.DrawPath(p);

        // Arrowhead at the end, pointing the way the arc is going (clockwise on screen).
        float ex = cx + r * MathF.Cos(end), ey = cy + r * MathF.Sin(end);
        float tx = -MathF.Sin(end), ty = MathF.Cos(end);     // direction of travel
        float nx = MathF.Cos(end), ny = MathF.Sin(end);      // outward
        canvas.DrawLine(ex, ey, ex - tx * 4.5f + nx * 3.5f, ey - ty * 4.5f + ny * 3.5f);
        canvas.DrawLine(ex, ey, ex - tx * 4.5f - nx * 3.5f, ey - ty * 4.5f - ny * 3.5f);
    }

    private static void Dot(ICanvas canvas, float x, float y, float r) => canvas.FillEllipse(x - r, y - r, r * 2f, r * 2f);

    private static void Ring(ICanvas canvas, float x, float y, float r) => canvas.DrawEllipse(x - r, y - r, r * 2f, r * 2f);

    /// <summary>A folded paper map: three panels with the two folds showing.</summary>
    private static void DrawMap(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(3f, 6.5f);
        p.LineTo(9f, 4f);
        p.LineTo(15f, 6.5f);
        p.LineTo(21f, 4f);
        p.LineTo(21f, 17.5f);
        p.LineTo(15f, 20f);
        p.LineTo(9f, 17.5f);
        p.LineTo(3f, 20f);
        p.Close();
        canvas.DrawPath(p);
        canvas.DrawLine(9f, 4f, 9f, 17.5f);
        canvas.DrawLine(15f, 6.5f, 15f, 20f);
    }

    /// <summary>A shopping cart with a handle, a basket and two wheels.</summary>
    private static void DrawCart(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(2f, 4f);
        p.LineTo(5f, 4f);
        p.LineTo(7.8f, 14.5f);
        p.LineTo(18.5f, 14.5f);
        p.LineTo(20.5f, 7f);
        p.LineTo(5.7f, 7f);
        canvas.DrawPath(p);
        Dot(canvas, 9.5f, 19f, 1.8f);
        Dot(canvas, 17f, 19f, 1.8f);
    }

    /// <summary>An open book.</summary>
    private static void DrawBook(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(12f, 6.5f);
        p.CurveTo(9.5f, 4.5f, 5.5f, 4.3f, 3f, 5.2f);
        p.LineTo(3f, 18.7f);
        p.CurveTo(5.5f, 17.8f, 9.5f, 18f, 12f, 20f);
        p.CurveTo(14.5f, 18f, 18.5f, 17.8f, 21f, 18.7f);
        p.LineTo(21f, 5.2f);
        p.CurveTo(18.5f, 4.3f, 14.5f, 4.5f, 12f, 6.5f);
        p.Close();
        canvas.DrawPath(p);
        canvas.DrawLine(12f, 6.5f, 12f, 20f);
    }

    /// <summary>A question mark in a circle.</summary>
    private static void DrawHelp(ICanvas canvas)
    {
        Ring(canvas, 12f, 12f, 9.5f);
        var p = new PathF();
        p.MoveTo(9f, 9.4f);
        p.CurveTo(9f, 6.4f, 15f, 6.4f, 15f, 9.4f);
        p.CurveTo(15f, 11.6f, 12f, 11.8f, 12f, 14.2f);
        canvas.DrawPath(p);
        Dot(canvas, 12f, 17.3f, 1.2f);
    }

    /// <summary>The usual share mark: three dots joined by two lines.</summary>
    private static void DrawShare(ICanvas canvas)
    {
        canvas.DrawLine(8.2f, 10.8f, 15.8f, 6.7f);
        canvas.DrawLine(8.2f, 13.2f, 15.8f, 17.3f);
        Dot(canvas, 18f, 5.5f, 3f);
        Dot(canvas, 6f, 12f, 3f);
        Dot(canvas, 18f, 18.5f, 3f);
    }

    /// <summary>Three sliders.</summary>
    private static void DrawSettings(ICanvas canvas)
    {
        canvas.DrawLine(3f, 6f, 21f, 6f);
        canvas.DrawLine(3f, 12f, 21f, 12f);
        canvas.DrawLine(3f, 18f, 21f, 18f);
        Dot(canvas, 8f, 6f, 2.6f);
        Dot(canvas, 16f, 12f, 2.6f);
        Dot(canvas, 10f, 18f, 2.6f);
    }

    /// <summary>An "i" in a circle.</summary>
    private static void DrawInfo(ICanvas canvas)
    {
        Ring(canvas, 12f, 12f, 9.5f);
        canvas.DrawLine(12f, 11f, 12f, 17f);
        Dot(canvas, 12f, 7.6f, 1.2f);
    }

    /// <summary>A bold "V" or "&gt;" corner made of two strokes; the three points are given in order.</summary>
    private static void Chevron(ICanvas canvas, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        canvas.StrokeSize = 3f;
        var p = new PathF();
        p.MoveTo(x1, y1);
        p.LineTo(x2, y2);
        p.LineTo(x3, y3);
        canvas.DrawPath(p);
    }

    /// <summary>All the way to a wall: a bar and two chevrons running at it.</summary>
    private static void DrawFar(ICanvas canvas, bool right)
    {
        if (right)
        {
            canvas.Translate(24f, 0f);
            canvas.Scale(-1f, 1f);
        }
        canvas.StrokeSize = 2.6f;
        canvas.DrawLine(4f, 5f, 4f, 19f);
        Chevron(canvas, 12f, 6f, 7f, 12f, 12f, 18f);
        Chevron(canvas, 19f, 6f, 14f, 12f, 19f, 18f);
    }

    /// <summary>Straight down to the bottom: two chevrons landing on a line.</summary>
    private static void DrawDrop(ICanvas canvas)
    {
        canvas.StrokeSize = 2.8f;
        Chevron(canvas, 6f, 3.5f, 12f, 9.5f, 18f, 3.5f);
        Chevron(canvas, 6f, 9.5f, 12f, 15.5f, 18f, 9.5f);
        canvas.StrokeSize = 3f;
        canvas.DrawLine(5f, 20.5f, 19f, 20.5f);
    }

    private static void DrawPause(ICanvas canvas)
    {
        canvas.FillRoundedRectangle(6.5f, 4.5f, 4f, 15f, 1.4f);
        canvas.FillRoundedRectangle(13.5f, 4.5f, 4f, 15f, 1.4f);
    }

    /// <summary>A curved arrow turning back: Second Chance.</summary>
    private static void DrawUndo(ICanvas canvas)
    {
        var head = new PathF();
        head.MoveTo(9f, 5f);
        head.LineTo(4.5f, 10f);
        head.LineTo(9f, 15f);
        canvas.DrawPath(head);
        var tail = new PathF();
        tail.MoveTo(4.5f, 10f);
        tail.LineTo(14f, 10f);
        tail.CurveTo(20.5f, 10f, 20.5f, 19f, 13.5f, 19f);
        tail.LineTo(9f, 19f);
        canvas.DrawPath(tail);
    }

    /// <summary>Two arrows passing each other: Prism Shift rearranges the colours.</summary>
    private static void DrawShift(ICanvas canvas)
    {
        canvas.DrawLine(4f, 8f, 19f, 8f);
        Chevron(canvas, 15f, 4.5f, 19f, 8f, 15f, 11.5f);
        canvas.DrawLine(5f, 16f, 20f, 16f);
        Chevron(canvas, 9f, 12.5f, 5f, 16f, 9f, 19.5f);
    }

    /// <summary>A light bulb: Pip's Insight.</summary>
    private static void DrawBulb(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(9f, 16.5f);
        p.CurveTo(9f, 14f, 5.5f, 12.5f, 5.5f, 8.5f);
        p.CurveTo(5.5f, 5f, 8.3f, 3f, 12f, 3f);
        p.CurveTo(15.7f, 3f, 18.5f, 5f, 18.5f, 8.5f);
        p.CurveTo(18.5f, 12.5f, 15f, 14f, 15f, 16.5f);
        p.Close();
        canvas.DrawPath(p);
        canvas.DrawLine(9.5f, 19.5f, 14.5f, 19.5f);
        canvas.DrawLine(10.5f, 22f, 13.5f, 22f);
    }

    /// <summary>An open eye: Insight that stays on.</summary>
    private static void DrawEye(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(2f, 12f);
        p.CurveTo(6f, 5f, 18f, 5f, 22f, 12f);
        p.CurveTo(18f, 19f, 6f, 19f, 2f, 12f);
        canvas.DrawPath(p);
        Dot(canvas, 12f, 12f, 3f);
    }

    /// <summary>A sideways eight, for Endless.</summary>
    private static void DrawInfinity(ICanvas canvas)
    {
        var p = new PathF();
        p.MoveTo(12f, 12f);
        p.CurveTo(9.5f, 8.5f, 3f, 8.5f, 3f, 12f);
        p.CurveTo(3f, 15.5f, 9.5f, 15.5f, 12f, 12f);
        p.CurveTo(14.5f, 8.5f, 21f, 8.5f, 21f, 12f);
        p.CurveTo(21f, 15.5f, 14.5f, 15.5f, 12f, 12f);
        canvas.DrawPath(p);
    }
}
