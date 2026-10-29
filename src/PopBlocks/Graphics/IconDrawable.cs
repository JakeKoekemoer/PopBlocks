using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

public enum IconKind { None, Map, Cart, Book, Help, Share, Settings, Info, Infinity }

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
        }

        canvas.RestoreState();
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
