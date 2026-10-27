using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// The five blocks in a skin, side by side, drawn with the real block renderer so what you see is what the game draws:
/// the same shapes, borders and symbols.
/// </summary>
public sealed class SkinPreviewDrawable : IDrawable
{
    private static readonly CellColour[] Five =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    public SkinSpec Skin { get; set; } = CosmeticCatalog.Classic;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = MathF.Min(dirtyRect.Width / 5f, dirtyRect.Height);
        if (size < 8f) return;

        float left = dirtyRect.X + (dirtyRect.Width - size * 5f) / 2f;
        float top = dirtyRect.Y + (dirtyRect.Height - size) / 2f;

        // The skin is switched for this picture only and put back straight after.
        using (VisualDesign.Preview(Skin))
        {
            for (int i = 0; i < Five.Length; i++)
                CellRenderer.DrawBlock(canvas, left + i * size, top, size, Five[i]);
        }
    }
}

/// <summary>A small board panel in a board's own colours, with its grid and decoration.</summary>
public sealed class BoardPreviewDrawable : IDrawable
{
    public string BoardId { get; set; } = CosmeticCatalog.DefaultBoard;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 20f || dirtyRect.Height < 20f) return;

        var palette = BoardPalette.For(BoardId);
        float x = dirtyRect.X + 2f, y = dirtyRect.Y + 2f, w = dirtyRect.Width - 4f, h = dirtyRect.Height - 4f;
        float cell = MathF.Min(w, h) / 4f;

        canvas.SaveState();
        var clip = new PathF();
        clip.AppendRoundedRectangle(x, y, w, h, 8f);
        canvas.ClipPath(clip);

        const int bands = 12;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            canvas.FillColor = new Color(
                palette.Top.Red + (palette.Bottom.Red - palette.Top.Red) * t,
                palette.Top.Green + (palette.Bottom.Green - palette.Top.Green) * t,
                palette.Top.Blue + (palette.Bottom.Blue - palette.Top.Blue) * t,
                1f);
            canvas.FillRectangle(x, y + h * i / bands, w, h / bands + 1f);
        }

        if (palette.Motif != BoardMotif.None)
            GameDrawable.DrawMotifs(canvas, palette, x, y, w, h, cell, false);

        canvas.StrokeColor = palette.Grid.WithAlpha(MathF.Max(0.35f, palette.GridAlpha));
        canvas.StrokeSize = 1f;
        for (int gx = 1; gx < 6; gx++) canvas.DrawLine(x + w * gx / 6f, y, x + w * gx / 6f, y + h);
        for (int gy = 1; gy < 3; gy++) canvas.DrawLine(x, y + h * gy / 3f, x + w, y + h * gy / 3f);
        canvas.RestoreState();

        canvas.StrokeColor = palette.Rim.WithAlpha(0.8f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(x, y, w, h, 8f);
    }
}
