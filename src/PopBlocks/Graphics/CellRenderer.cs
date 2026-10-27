using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>Which neighbouring cells are part of the same coloured cluster.</summary>
[Flags]
public enum CellLinks : byte
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,

    // Set only when both adjacent sides AND the diagonal cell are the same colour (a filled 2x2 corner).
    CornerUpLeft = 16,
    CornerUpRight = 32,
    CornerDownRight = 64,
    CornerDownLeft = 128,
}

/// <summary>
/// Draws one block entirely from vector operations: rounded square, border, top highlight and a
/// colour-coded symbol. Touching same-colour cells are bridged so a cluster reads as one shape
/// without thick internal borders (spec 4.4).
/// </summary>
public static class CellRenderer
{
    // Symbol outlines are built once in unit space (radius 1) and scaled when drawn.
    private static readonly PathF Diamond = BuildDiamond();
    private static readonly PathF Star = BuildStar();
    private static readonly PathF Triangle = BuildTriangle();

    /// <summary>
    /// Blocks are drawn in three passes so touching cells never paint over each other's details:
    /// all borders, then all faces, then all highlights and symbols.
    /// </summary>
    public enum BlockLayer { Border, Face, Detail }

    /// <summary>Draws a single block (all three passes). For clusters, call <see cref="DrawLayer"/> once per pass over every cell.</summary>
    public static void DrawBlock(ICanvas canvas, float x, float y, float size, CellColour colour, CellLinks links = CellLinks.None)
    {
        DrawLayer(canvas, BlockLayer.Border, x, y, size, colour, links);
        DrawLayer(canvas, BlockLayer.Face, x, y, size, colour, links);
        DrawLayer(canvas, BlockLayer.Detail, x, y, size, colour, links);
    }

    public static void DrawLayer(ICanvas canvas, BlockLayer layer, float x, float y, float size, CellColour colour, CellLinks links)
    {
        float inset = size * VisualDesign.CellInset;
        float border = size * VisualDesign.Border;
        float radius = size * VisualDesign.Corner;
        float half = size / 2f;

        float left = x + inset;
        float top = y + inset;
        float span = size - 2f * inset;

        float innerLeft = left + border;
        float innerTop = top + border;
        float innerSpan = span - 2f * border;

        canvas.SaveState();

        switch (layer)
        {
            case BlockLayer.Border:
            {
                // Outer shape in the border colour, squared off on any side touching a same-colour cell.
                float seam = VisualDesign.SeamOverlap;
                canvas.FillColor = VisualDesign.BorderColour(colour);
                canvas.FillRoundedRectangle(left, top, span, span, radius);
                if (links.HasFlag(CellLinks.Right)) canvas.FillRectangle(x + half, top, half + seam, span);
                if (links.HasFlag(CellLinks.Left)) canvas.FillRectangle(x - seam, top, half + seam, span);
                if (links.HasFlag(CellLinks.Down)) canvas.FillRectangle(left, y + half, span, half + seam);
                if (links.HasFlag(CellLinks.Up)) canvas.FillRectangle(left, y - seam, span, half + seam);
                break;
            }

            case BlockLayer.Face:
            {
                // Inner face. Bridges run past the cell edge (further than the border bridges) so the
                // anti-aliased end of a bridge always lands on face colour, never on a border.
                float reach = VisualDesign.SeamOverlap * 3f;
                canvas.FillColor = VisualDesign.BaseColour(colour);
                canvas.FillRoundedRectangle(innerLeft, innerTop, innerSpan, innerSpan, MathF.Max(0f, radius - border));
                if (links.HasFlag(CellLinks.Right)) canvas.FillRectangle(x + half, innerTop, half + reach, innerSpan);
                if (links.HasFlag(CellLinks.Left)) canvas.FillRectangle(x - reach, innerTop, half + reach, innerSpan);
                if (links.HasFlag(CellLinks.Down)) canvas.FillRectangle(innerLeft, y + half, innerSpan, half + reach);
                if (links.HasFlag(CellLinks.Up)) canvas.FillRectangle(innerLeft, y - reach, innerSpan, half + reach);

                // Fill the pinhole where four same-colour cells meet.
                float corner = inset + border + reach;
                if (links.HasFlag(CellLinks.CornerUpLeft)) canvas.FillRectangle(x - reach, y - reach, corner + reach, corner + reach);
                if (links.HasFlag(CellLinks.CornerUpRight)) canvas.FillRectangle(x + size - corner, y - reach, corner + reach, corner + reach);
                if (links.HasFlag(CellLinks.CornerDownRight)) canvas.FillRectangle(x + size - corner, y + size - corner, corner + reach, corner + reach);
                if (links.HasFlag(CellLinks.CornerDownLeft)) canvas.FillRectangle(x - reach, y + size - corner, corner + reach, corner + reach);
                break;
            }

            default:
            {
                var style = VisualDesign.Highlight;

                // Top highlight, only where the cell has an exposed top edge. (Faceted skins draw facets instead.)
                if (!links.HasFlag(CellLinks.Up) && style != HighlightStyle.Facet)
                {
                    float stroke = size * VisualDesign.HighlightWidth;
                    float lineY = innerTop + stroke * 0.9f;
                    float lineStart = links.HasFlag(CellLinks.Left) ? x : innerLeft + radius * 0.8f;
                    float lineEnd = links.HasFlag(CellLinks.Right) ? x + size : innerLeft + innerSpan - radius * 0.8f;
                    canvas.StrokeColor = VisualDesign.HighlightColour(colour).WithAlpha(style == HighlightStyle.Soft ? 0.45f : 0.85f);
                    canvas.StrokeSize = stroke;
                    canvas.StrokeLineCap = LineCap.Round;
                    canvas.DrawLine(lineStart, lineY, lineEnd, lineY);
                }

                // Glossy sheen across an exposed top edge, and a soft shade along an exposed bottom edge.
                if (!links.HasFlag(CellLinks.Up) && style != HighlightStyle.Facet)
                {
                    float sheenLeft = links.HasFlag(CellLinks.Left) ? x : innerLeft + border;
                    float sheenRight = links.HasFlag(CellLinks.Right) ? x + size : innerLeft + innerSpan - border;
                    canvas.FillColor = Colors.White.WithAlpha(style == HighlightStyle.Soft ? 0.09f : 0.16f);
                    canvas.FillRoundedRectangle(sheenLeft, innerTop + border, sheenRight - sheenLeft, innerSpan * 0.34f, radius * 0.6f);
                }
                if (!links.HasFlag(CellLinks.Down))
                {
                    float shadeLeft = links.HasFlag(CellLinks.Left) ? x : innerLeft + border;
                    float shadeRight = links.HasFlag(CellLinks.Right) ? x + size : innerLeft + innerSpan - border;
                    canvas.FillColor = Colors.Black.WithAlpha(0.13f);
                    canvas.FillRoundedRectangle(shadeLeft, innerTop + innerSpan * 0.80f, shadeRight - shadeLeft, innerSpan * 0.17f, radius * 0.5f);
                }

                if (style == HighlightStyle.Facet) DrawFacets(canvas, innerLeft, innerTop, innerSpan);
                if (VisualDesign.Pattern != SkinPattern.None && size >= 14f) DrawPattern(canvas, VisualDesign.Pattern, innerLeft, innerTop, innerSpan);

                DrawSymbol(canvas, x + half, y + half, size, colour);
                break;
            }
        }

        canvas.RestoreState();
    }

    // Facets are built once, in a unit square, and scaled to the face when drawn (no allocation per frame).
    private static readonly PathF FacetLight = BuildCornerTriangle(0.10f, 0.10f, 0.38f);
    private static readonly PathF FacetDark = BuildCornerTriangle(0.90f, 0.90f, -0.38f);

    private static PathF BuildCornerTriangle(float cx, float cy, float arm)
    {
        var p = new PathF();
        p.MoveTo(cx, cy);
        p.LineTo(cx + arm, cy);
        p.LineTo(cx, cy + arm);
        p.Close();
        return p;
    }

    /// <summary>Two flat facets, light at the top left and dark at the bottom right, kept clear of the centre where the symbol sits.</summary>
    private static void DrawFacets(ICanvas canvas, float left, float top, float span)
    {
        canvas.SaveState();
        canvas.Translate(left, top);
        canvas.Scale(span, span);
        canvas.FillColor = Colors.White.WithAlpha(0.20f);
        canvas.FillPath(FacetLight, WindingMode.NonZero);
        canvas.FillColor = Colors.Black.WithAlpha(0.12f);
        canvas.FillPath(FacetDark, WindingMode.NonZero);
        canvas.RestoreState();
    }

    /// <summary>
    /// A faint texture in the corners of the face. It never reaches the middle, so the symbol (which is what tells the
    /// colours apart) is untouched by every skin.
    /// </summary>
    private static void DrawPattern(ICanvas canvas, SkinPattern pattern, float left, float top, float span)
    {
        switch (pattern)
        {
            case SkinPattern.Lines:
                canvas.StrokeColor = Colors.White.WithAlpha(0.20f);
                canvas.StrokeSize = MathF.Max(1f, span * 0.045f);
                canvas.StrokeLineCap = LineCap.Round;
                canvas.DrawLine(left + span * 0.62f, top + span * 0.90f, left + span * 0.90f, top + span * 0.62f);
                canvas.DrawLine(left + span * 0.74f, top + span * 0.90f, left + span * 0.90f, top + span * 0.74f);
                canvas.DrawLine(left + span * 0.10f, top + span * 0.38f, left + span * 0.38f, top + span * 0.10f);
                break;

            case SkinPattern.Dots:
                canvas.FillColor = Colors.White.WithAlpha(0.28f);
                canvas.FillEllipse(left + span * 0.70f, top + span * 0.72f, span * 0.12f, span * 0.12f);
                canvas.FillEllipse(left + span * 0.80f, top + span * 0.58f, span * 0.07f, span * 0.07f);
                canvas.FillEllipse(left + span * 0.14f, top + span * 0.20f, span * 0.08f, span * 0.08f);
                break;

            case SkinPattern.Grain:
                canvas.FillColor = Colors.Black.WithAlpha(0.14f);
                canvas.FillEllipse(left + span * 0.16f, top + span * 0.70f, span * 0.06f, span * 0.06f);
                canvas.FillEllipse(left + span * 0.30f, top + span * 0.84f, span * 0.05f, span * 0.05f);
                canvas.FillEllipse(left + span * 0.78f, top + span * 0.22f, span * 0.06f, span * 0.06f);
                canvas.FillEllipse(left + span * 0.66f, top + span * 0.12f, span * 0.05f, span * 0.05f);
                break;
        }
    }

    /// <summary>
    /// Draws a block that is about to pop, on top of the normal block. <paramref name="glow"/> (0 to 1) is the strength
    /// of the halo; <paramref name="tint"/> (0 to 1) is how far the face has turned to solid neon. Once the tint is
    /// complete, touching cells are bridged so the match reads as one glowing shape. Three passes, like
    /// <see cref="DrawLayer"/>: halos, faces, then detail (sheen and the colour symbol, which stays visible).
    /// </summary>
    public static void DrawPopLayer(ICanvas canvas, BlockLayer layer, float x, float y, float size, CellColour colour,
        CellLinks links, float glow, float tint)
    {
        float inset = size * VisualDesign.CellInset;
        float span = size - 2f * inset;
        float left = x + inset;
        float top = y + inset;
        float radius = size * VisualDesign.Corner;
        float half = size / 2f;
        var neon = VisualDesign.NeonColour(colour);

        canvas.SaveState();
        switch (layer)
        {
            case BlockLayer.Border:
            {
                // Soft halo: a few stroked outlines that widen and fade.
                for (int i = 3; i >= 1; i--)
                {
                    float grow = size * 0.07f * i * (0.6f + 0.6f * glow);
                    canvas.StrokeColor = neon.WithAlpha(0.16f + 0.26f * glow / i);
                    canvas.StrokeSize = size * 0.07f;
                    canvas.DrawRoundedRectangle(left - grow, top - grow, span + 2f * grow, span + 2f * grow, radius + grow);
                }
                break;
            }

            case BlockLayer.Face:
            {
                float alpha = Math.Clamp(tint, 0f, 1f);
                canvas.FillColor = neon.WithAlpha(alpha);
                canvas.FillRoundedRectangle(left, top, span, span, radius);
                if (alpha >= 0.999f)
                {
                    float reach = VisualDesign.SeamOverlap * 3f;
                    if (links.HasFlag(CellLinks.Right)) canvas.FillRectangle(x + half, top, half + reach, span);
                    if (links.HasFlag(CellLinks.Left)) canvas.FillRectangle(x - reach, top, half + reach, span);
                    if (links.HasFlag(CellLinks.Down)) canvas.FillRectangle(left, y + half, span, half + reach);
                    if (links.HasFlag(CellLinks.Up)) canvas.FillRectangle(left, y - reach, span, half + reach);

                    float corner = inset + reach;
                    if (links.HasFlag(CellLinks.CornerUpLeft)) canvas.FillRectangle(x - reach, y - reach, corner + reach, corner + reach);
                    if (links.HasFlag(CellLinks.CornerUpRight)) canvas.FillRectangle(x + size - corner, y - reach, corner + reach, corner + reach);
                    if (links.HasFlag(CellLinks.CornerDownRight)) canvas.FillRectangle(x + size - corner, y + size - corner, corner + reach, corner + reach);
                    if (links.HasFlag(CellLinks.CornerDownLeft)) canvas.FillRectangle(x - reach, y + size - corner, corner + reach, corner + reach);
                }
                break;
            }

            default:
            {
                if (tint >= 0.999f && !links.HasFlag(CellLinks.Up))
                {
                    float sheenLeft = links.HasFlag(CellLinks.Left) ? x : left + size * 0.06f;
                    float sheenRight = links.HasFlag(CellLinks.Right) ? x + size : left + span - size * 0.06f;
                    canvas.FillColor = Colors.White.WithAlpha(0.45f);
                    canvas.FillRoundedRectangle(sheenLeft, top + size * 0.05f, sheenRight - sheenLeft, span * 0.30f, radius * 0.6f);
                }
                DrawSymbol(canvas, x + half, y + half, size, colour);
                break;
            }
        }
        canvas.RestoreState();
    }

    /// <summary>Builds the link flags from which of the eight neighbours share this cell's colour.</summary>
    public static CellLinks Compose(bool up, bool right, bool down, bool left, bool upRight, bool downRight, bool downLeft, bool upLeft)
    {
        var links = CellLinks.None;
        if (up) links |= CellLinks.Up;
        if (right) links |= CellLinks.Right;
        if (down) links |= CellLinks.Down;
        if (left) links |= CellLinks.Left;
        if (up && right && upRight) links |= CellLinks.CornerUpRight;
        if (down && right && downRight) links |= CellLinks.CornerDownRight;
        if (down && left && downLeft) links |= CellLinks.CornerDownLeft;
        if (up && left && upLeft) links |= CellLinks.CornerUpLeft;
        return links;
    }

    /// <summary>The links as they look when the picture is turned upside down (up/down and left/right swap).</summary>
    public static CellLinks Flip(CellLinks links)
    {
        var flipped = CellLinks.None;
        if (links.HasFlag(CellLinks.Up)) flipped |= CellLinks.Down;
        if (links.HasFlag(CellLinks.Down)) flipped |= CellLinks.Up;
        if (links.HasFlag(CellLinks.Left)) flipped |= CellLinks.Right;
        if (links.HasFlag(CellLinks.Right)) flipped |= CellLinks.Left;
        if (links.HasFlag(CellLinks.CornerUpLeft)) flipped |= CellLinks.CornerDownRight;
        if (links.HasFlag(CellLinks.CornerDownRight)) flipped |= CellLinks.CornerUpLeft;
        if (links.HasFlag(CellLinks.CornerUpRight)) flipped |= CellLinks.CornerDownLeft;
        if (links.HasFlag(CellLinks.CornerDownLeft)) flipped |= CellLinks.CornerUpRight;
        return flipped;
    }

    /// <summary>Link flags for cell <paramref name="index"/> of a piece (neighbours are the piece's own cells).</summary>
    public static CellLinks LinksForPiece(IReadOnlyList<BlockCell> cells, int index)
    {
        var c = cells[index];
        bool Has(int dx, int dy)
        {
            for (int j = 0; j < cells.Count; j++)
                if (j != index && cells[j].Colour == c.Colour && cells[j].X == c.X + dx && cells[j].Y == c.Y + dy)
                    return true;
            return false;
        }
        return Compose(Has(0, -1), Has(1, 0), Has(0, 1), Has(-1, 0), Has(1, -1), Has(1, 1), Has(-1, 1), Has(-1, -1));
    }

    public static void DrawSymbol(ICanvas canvas, float cx, float cy, float size, CellColour colour)
    {
        float r = size * VisualDesign.SymbolRadius;
        canvas.FillColor = VisualDesign.SymbolNow;

        switch (colour)
        {
            case CellColour.Cyan:
                FillUnitPath(canvas, Diamond, cx, cy, r);
                break;

            case CellColour.Coral:
                float d = r * VisualDesign.CircleScale;
                canvas.FillEllipse(cx - d, cy - d, d * 2f, d * 2f);
                break;

            case CellColour.Yellow:
                FillUnitPath(canvas, Star, cx, cy, r);
                break;

            case CellColour.Purple:
                float t = size * VisualDesign.PlusThickness;
                canvas.FillRoundedRectangle(cx - r, cy - t / 2f, r * 2f, t, t * 0.3f);
                canvas.FillRoundedRectangle(cx - t / 2f, cy - r, t, r * 2f, t * 0.3f);
                break;

            case CellColour.Lime:
                FillUnitPath(canvas, Triangle, cx, cy + r * 0.12f, r);
                break;
        }
    }

    private static void FillUnitPath(ICanvas canvas, PathF path, float cx, float cy, float scale)
    {
        canvas.SaveState();
        canvas.Translate(cx, cy);
        canvas.Scale(scale, scale);
        canvas.FillPath(path, WindingMode.NonZero);
        canvas.RestoreState();
    }

    private static PathF BuildDiamond()
    {
        var p = new PathF();
        p.MoveTo(0f, -1.15f);
        p.LineTo(0.95f, 0f);
        p.LineTo(0f, 1.15f);
        p.LineTo(-0.95f, 0f);
        p.Close();
        return p;
    }

    private static PathF BuildStar()
    {
        var p = new PathF();
        const float outer = 1.2f;
        const float inner = 0.5f;
        for (int i = 0; i < 10; i++)
        {
            float radius = i % 2 == 0 ? outer : inner;
            float angle = -MathF.PI / 2f + i * MathF.PI / 5f;
            float px = MathF.Cos(angle) * radius;
            float py = MathF.Sin(angle) * radius;
            if (i == 0) p.MoveTo(px, py); else p.LineTo(px, py);
        }
        p.Close();
        return p;
    }

    private static PathF BuildTriangle()
    {
        var p = new PathF();
        p.MoveTo(0f, -1.05f);
        p.LineTo(1.0f, 0.8f);
        p.LineTo(-1.0f, 0.8f);
        p.Close();
        return p;
    }
}
