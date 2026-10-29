using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Models;

namespace PopBlocks.Graphics;

/// <summary>
/// A block colour as a chip on the settings page: the block's face in its colour with its symbol on it. When the colour is
/// switched off the chip is dimmed and crossed out, so the state never depends on colour alone.
/// </summary>
public sealed class ColourChipDrawable : IDrawable
{
    public CellColour Colour { get; set; } = CellColour.Cyan;
    public bool On { get; set; } = true;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float s = MathF.Min(dirtyRect.Width, dirtyRect.Height) - 6f;
        float x = dirtyRect.X + (dirtyRect.Width - s) / 2f, y = dirtyRect.Y + (dirtyRect.Height - s) / 2f;

        canvas.Alpha = On ? 1f : 0.35f;
        canvas.FillColor = VisualDesign.BaseColour(Colour);
        canvas.FillRoundedRectangle(x, y, s, s, s * 0.22f);
        canvas.StrokeColor = VisualDesign.BorderColour(Colour);
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(x, y, s, s, s * 0.22f);
        CellRenderer.DrawSymbol(canvas, x + s / 2f, y + s / 2f, s, Colour);
        canvas.Alpha = 1f;

        if (!On)
        {
            canvas.StrokeColor = Colors.White.WithAlpha(0.85f);
            canvas.StrokeSize = 2.5f;
            canvas.DrawLine(x + s * 0.12f, y + s * 0.88f, x + s * 0.88f, y + s * 0.12f);
        }
    }
}

/// <summary>One piece shape as a chip: its cells drawn small in a neutral colour, crossed out and dimmed when switched off.</summary>
public sealed class ShapeChipDrawable : IDrawable
{
    private static readonly Color Face = Color.FromArgb("#8FA3D6");
    private static readonly Color Edge = Color.FromArgb("#C9D4F5");

    public ShapeDef Shape { get; set; } = ShapeCatalog.Classic[0];
    public bool On { get; set; } = true;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var offsets = Shape.Offsets;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var o in offsets)
        {
            if (o.Dx < minX) minX = o.Dx;
            if (o.Dx > maxX) maxX = o.Dx;
            if (o.Dy < minY) minY = o.Dy;
            if (o.Dy > maxY) maxY = o.Dy;
        }
        int w = maxX - minX + 1, h = maxY - minY + 1;
        float cell = MathF.Min((dirtyRect.Width - 12f) / MathF.Max(w, 3), (dirtyRect.Height - 12f) / MathF.Max(h, 3));
        float ox = dirtyRect.X + (dirtyRect.Width - w * cell) / 2f, oy = dirtyRect.Y + (dirtyRect.Height - h * cell) / 2f;

        canvas.Alpha = On ? 1f : 0.3f;
        foreach (var o in offsets)
        {
            float x = ox + (o.Dx - minX) * cell, y = oy + (o.Dy - minY) * cell;
            canvas.FillColor = Face;
            canvas.FillRoundedRectangle(x + 1f, y + 1f, cell - 2f, cell - 2f, cell * 0.2f);
            canvas.StrokeColor = Edge;
            canvas.StrokeSize = 1.2f;
            canvas.DrawRoundedRectangle(x + 1f, y + 1f, cell - 2f, cell - 2f, cell * 0.2f);
        }
        canvas.Alpha = 1f;

        if (!On)
        {
            canvas.StrokeColor = Colors.White.WithAlpha(0.85f);
            canvas.StrokeSize = 2.5f;
            canvas.DrawLine(dirtyRect.X + 8f, dirtyRect.Bottom - 8f, dirtyRect.Right - 8f, dirtyRect.Y + 8f);
        }
    }
}
