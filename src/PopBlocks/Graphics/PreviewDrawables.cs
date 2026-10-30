using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Graphics;

/// <summary>
/// Draws the pieces that come after the current one, side by side, soonest first. How many there are
/// is a shop upgrade (1 to 3); the first one is drawn a little larger so it reads as "next".
/// </summary>
public sealed class UpcomingDrawable : IDrawable
{
    public GameEngine? Game { get; set; }

    /// <summary>Pieces are stacked top to bottom (the rail beside the board) instead of laid out left to right.</summary>
    public bool Vertical { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var pieces = Game?.Upcoming;
        if (pieces is null || pieces.Count == 0) return;

        if (Vertical)
        {
            float slotH = dirtyRect.Height / pieces.Count;
            for (int p = 0; p < pieces.Count; p++)
            {
                var slot = new RectF(dirtyRect.X, dirtyRect.Y + p * slotH, dirtyRect.Width, slotH);
                DrawPiece(canvas, pieces[p], slot, p == 0 ? 1f : 0.78f, p == 0 ? 1f : 0.75f);
            }
            return;
        }

        float slotW = dirtyRect.Width / pieces.Count;
        for (int p = 0; p < pieces.Count; p++)
        {
            var slot = new RectF(dirtyRect.X + p * slotW, dirtyRect.Y, slotW, dirtyRect.Height);
            DrawPiece(canvas, pieces[p], slot, p == 0 ? 1f : 0.78f, p == 0 ? 1f : 0.75f);
        }
    }

    private static void DrawPiece(ICanvas canvas, Piece piece, RectF slot, float scale, float alpha)
    {
        var cells = piece.Cells;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int i = 0; i < cells.Count; i++)
        {
            minX = Math.Min(minX, cells[i].X); maxX = Math.Max(maxX, cells[i].X);
            minY = Math.Min(minY, cells[i].Y); maxY = Math.Max(maxY, cells[i].Y);
        }

        int cols = maxX - minX + 1;
        int rows = maxY - minY + 1;
        float cell = MathF.Floor(MathF.Min(slot.Width / MathF.Max(cols, 3), slot.Height / MathF.Max(rows, 2)) * scale);
        if (cell < 4f) return;

        float offsetX = slot.X + (slot.Width - cols * cell) / 2f;
        float offsetY = slot.Y + (slot.Height - rows * cell) / 2f;

        canvas.Alpha = alpha;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                CellRenderer.DrawLayer(
                    canvas,
                    (CellRenderer.BlockLayer)pass,
                    offsetX + (cells[i].X - minX) * cell,
                    offsetY + (cells[i].Y - minY) * cell,
                    cell,
                    cells[i].Colour,
                    CellRenderer.LinksForPiece(cells, i));
            }
        }
        canvas.Alpha = 1f;
    }
}

/// <summary>Row of the five colour blocks shown on the menu and the About page.</summary>
public sealed class LogoDrawable : IDrawable
{
    private static readonly CellColour[] Colours =
    {
        CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime,
    };

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cell = MathF.Floor(MathF.Min(dirtyRect.Height, dirtyRect.Width / Colours.Length));
        float startX = dirtyRect.X + (dirtyRect.Width - cell * Colours.Length) / 2f;
        float y = dirtyRect.Y + (dirtyRect.Height - cell) / 2f;

        // A soft neon glow under each block, then the blocks themselves.
        for (int i = 0; i < Colours.Length; i++)
        {
            float cx = startX + i * cell + cell / 2f;
            float cy = y + cell / 2f;
            var glow = VisualDesign.NeonColour(Colours[i]);
            for (int ring = 3; ring >= 1; ring--)
            {
                float r = cell * (0.40f + 0.12f * ring);
                canvas.FillColor = glow.WithAlpha(0.06f);
                canvas.FillEllipse(cx - r, cy - r, r * 2f, r * 2f);
            }
        }
        for (int i = 0; i < Colours.Length; i++)
            CellRenderer.DrawBlock(canvas, startX + i * cell, y, cell, Colours[i]);
    }
}

/// <summary>A small coloured gem used as a decoration on the shop and level cards.</summary>
public sealed class GoldCoinDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float d = MathF.Min(dirtyRect.Width, dirtyRect.Height) - 2f;
        float x = dirtyRect.X + (dirtyRect.Width - d) / 2f;
        float y = dirtyRect.Y + (dirtyRect.Height - d) / 2f;

        canvas.FillColor = Color.FromArgb("#B45309");
        canvas.FillEllipse(x, y, d, d);
        canvas.FillColor = VisualDesign.Gold;
        canvas.FillEllipse(x + d * 0.06f, y + d * 0.06f, d * 0.88f, d * 0.88f);
        canvas.StrokeColor = Color.FromArgb("#FDE68A");
        canvas.StrokeSize = MathF.Max(1f, d * 0.07f);
        canvas.DrawEllipse(x + d * 0.22f, y + d * 0.22f, d * 0.56f, d * 0.56f);
        canvas.FillColor = Colors.White.WithAlpha(0.35f);
        canvas.FillEllipse(x + d * 0.18f, y + d * 0.12f, d * 0.34f, d * 0.2f);
    }
}
