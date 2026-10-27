using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Graphics;

/// <summary>
/// Renders the current <see cref="GameEngine"/> state. It only reads state; all rules live in the engine.
/// The board scales to the available area, keeping square cells and the 8 x 16 proportions.
///
/// A match that is about to pop (the engine's pending groups) lights up in its own colour, turns solid neon,
/// and then <see cref="BlastEffects"/> takes over with the burst. On flipped (Extreme) levels the whole picture
/// is drawn upside down while the engine runs unchanged.
/// </summary>
public sealed class GameDrawable : IDrawable
{
    private IReadOnlyList<ColourGroup>? _lastPending;
    private long _pendingStartMs;
    private readonly bool[] _pendingMask = new bool[GameConfig.Columns * GameConfig.TotalRows];

    public GameEngine? Game { get; set; }
    public BlastEffects Effects { get; } = new();
    public bool ShowGhost { get; set; } = true;

    /// <summary>Draw the board upside down (Extreme levels).</summary>
    public bool Flip { get; set; }

    /// <summary>Board column to pulse as a tutorial hint, or -1.</summary>
    public int HintColumn { get; set; } = -1;
    public int HintWidth { get; set; } = 1;

    /// <summary>Time source in milliseconds. Replaceable so pictures of the animation phases can be tested.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>Cell edge length from the most recent draw, in the same units as touch coordinates.</summary>
    public float CellSize { get; private set; } = 24f;

    /// <summary>True while something on the board is animating, so the page keeps repainting.</summary>
    public bool NeedsAnimation(long nowMs) =>
        (Game is { PendingGroups.Count: > 0 }) || Effects.IsActive(nowMs) || HintColumn >= 0;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        long now = Clock();

        canvas.SaveState();

        // ---- Layout: biggest square-cell board that fits ----
        float pad = VisualDesign.BoardPadding;
        float availW = dirtyRect.Width - 2f * (VisualDesign.BoardMargin + pad);
        float availH = dirtyRect.Height - 2f * (VisualDesign.BoardMargin + pad);
        float cell = MathF.Floor(MathF.Min(availW / GameConfig.Columns, availH / GameConfig.VisibleRows));
        if (cell < 4f) { canvas.RestoreState(); return; }
        CellSize = cell;

        float boardW = cell * GameConfig.Columns;
        float boardH = cell * GameConfig.VisibleRows;
        float originX = dirtyRect.X + (dirtyRect.Width - boardW) / 2f;
        float originY = dirtyRect.Y + (dirtyRect.Height - boardH) / 2f;

        var (shakeX, shakeY) = Effects.ShakeOffset(cell, now);
        canvas.Translate(shakeX, shakeY);

        DrawBoardSurface(canvas, originX, originY, boardW, boardH, cell, Flip);

        var game = Game;
        if (game is not null)
        {
            // Everything inside the board is clipped, so falling cells never poke out of the edge.
            canvas.SaveState();
            canvas.ClipRectangle(originX - pad, originY - pad, boardW + 2f * pad, boardH + 2f * pad);

            DrawHint(canvas, originX, originY, cell, boardH, now);

            for (int pass = 0; pass < 3; pass++)
                DrawLockedCells(canvas, (CellRenderer.BlockLayer)pass, game.Board, originX, originY, cell, now);

            DrawPendingGroups(canvas, game, originX, originY, cell, now);

            if (ShowGhost) DrawGhost(canvas, game, originX, originY, cell);
            for (int pass = 0; pass < 3; pass++)
                DrawActivePiece(canvas, (CellRenderer.BlockLayer)pass, game.CurrentPiece, originX, originY, cell);
            Effects.Draw(canvas, originX, originY, cell, boardW, Flip, now);

            canvas.RestoreState();
        }

        canvas.RestoreState();
    }

    private static void DrawBoardSurface(ICanvas canvas, float originX, float originY, float boardW, float boardH, float cell, bool flip)
    {
        float pad = VisualDesign.BoardPadding;
        float x = originX - pad, y = originY - pad, w = boardW + 2f * pad, h = boardH + 2f * pad;
        float corner = cell * VisualDesign.BoardCornerRadiusInCells;

        // Soft outer glow, then the glassy gradient panel.
        canvas.StrokeColor = VisualDesign.BoardRim.WithAlpha(0.10f);
        canvas.StrokeSize = 8f;
        canvas.DrawRoundedRectangle(x, y, w, h, corner);

        // Vertical gradient drawn as thin solid bands clipped to the rounded panel. (SetFillPaint is avoided on
        // purpose: on Android its shader stays on the paint and later solid FillColor calls come out invisible.)
        canvas.SaveState();
        var panelPath = new PathF();
        panelPath.AppendRoundedRectangle(x, y, w, h, corner);
        canvas.ClipPath(panelPath);
        const int bands = 40;
        var top = VisualDesign.BoardTop;
        var bottom = VisualDesign.BoardBottom;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            canvas.FillColor = new Color(
                top.Red + (bottom.Red - top.Red) * t,
                top.Green + (bottom.Green - top.Green) * t,
                top.Blue + (bottom.Blue - top.Blue) * t,
                top.Alpha + (bottom.Alpha - top.Alpha) * t);
            float by = y + h * i / bands;
            canvas.FillRectangle(x, by, w, h / bands + 1f);
        }
        canvas.RestoreState();

        canvas.StrokeColor = VisualDesign.BoardRim.WithAlpha(0.35f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(x, y, w, h, corner);

        // Subtle grid lines.
        canvas.StrokeColor = VisualDesign.GridLine.WithAlpha(VisualDesign.GridLineAlpha);
        canvas.StrokeSize = VisualDesign.GridLineWidth;
        for (int gx = 1; gx < GameConfig.Columns; gx++)
            canvas.DrawLine(originX + gx * cell, originY, originX + gx * cell, originY + boardH);
        for (int gy = 1; gy < GameConfig.VisibleRows; gy++)
            canvas.DrawLine(originX, originY + gy * cell, originX + boardW, originY + gy * cell);

        // Empty cells.
        float inset = cell * VisualDesign.EmptyCellInset;
        float size = cell - 2f * inset;
        canvas.FillColor = VisualDesign.EmptyCell.WithAlpha(0.75f);
        for (int ey = 0; ey < GameConfig.VisibleRows; ey++)
            for (int ex = 0; ex < GameConfig.Columns; ex++)
                canvas.FillRoundedRectangle(originX + ex * cell + inset, originY + ey * cell + inset, size, size, cell * 0.18f);

        // A glowing line along the floor, where blocks come to rest. Upside down on flipped levels.
        float floorY = flip ? originY : originY + boardH;
        canvas.StrokeColor = VisualDesign.BoardRim.WithAlpha(0.55f);
        canvas.StrokeSize = 2.5f;
        canvas.DrawLine(originX + cell * 0.2f, floorY, originX + boardW - cell * 0.2f, floorY);
    }

    private void DrawHint(ICanvas canvas, float originX, float originY, float cell, float boardH, long now)
    {
        if (HintColumn < 0) return;
        float pulse = 0.5f + 0.5f * MathF.Sin(now / 260f);
        int screenCol = BoardSpace.ScreenColumn(HintColumn, Flip);
        if (Flip) screenCol -= HintWidth - 1;
        canvas.FillColor = Color.FromArgb("#38BDF8").WithAlpha(0.07f + 0.10f * pulse);
        canvas.FillRoundedRectangle(originX + screenCol * cell, originY, cell * HintWidth, boardH, cell * 0.2f);
        canvas.StrokeColor = Color.FromArgb("#38BDF8").WithAlpha(0.25f + 0.35f * pulse);
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(originX + screenCol * cell, originY, cell * HintWidth, boardH, cell * 0.2f);
    }

    private void DrawLockedCells(ICanvas canvas, CellRenderer.BlockLayer layer, Board board, float originX, float originY, float cell, long now)
    {
        for (int y = GameConfig.HiddenRows; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var colour = board[x, y];
                if (colour == CellColour.Empty) continue;

                var links = LinksAt(board, x, y, colour);
                float fall = Effects.FallOffsetCells(x, y, now);
                CellRenderer.DrawLayer(
                    canvas,
                    layer,
                    BoardSpace.ScreenX(x, Flip, originX, cell),
                    BoardSpace.ScreenY(y, Flip, originY, cell, fall),
                    cell,
                    colour,
                    Flip ? CellRenderer.Flip(links) : links);
            }
        }
    }

    private static CellLinks LinksAt(Board board, int x, int y, CellColour colour)
    {
        bool Same(int dx, int dy) => board.IsInside(x + dx, y + dy) && board[x + dx, y + dy] == colour;
        return CellRenderer.Compose(
            Same(0, -1), Same(1, 0), Same(0, 1), Same(-1, 0),
            Same(1, -1), Same(1, 1), Same(-1, 1), Same(-1, -1));
    }

    /// <summary>
    /// The match that is about to pop: first it glows in its own colour, then it turns into solid neon.
    /// The elapsed time is measured from the moment these groups first appeared.
    /// </summary>
    private void DrawPendingGroups(ICanvas canvas, GameEngine game, float originX, float originY, float cell, long now)
    {
        var pending = game.PendingGroups;
        if (pending.Count == 0)
        {
            _lastPending = null;
            return;
        }

        if (!ReferenceEquals(pending, _lastPending))
        {
            _lastPending = pending;
            _pendingStartMs = now;
        }

        float duration = MathF.Max(1f, game.Timing.HighlightMs);
        float t = Math.Clamp((now - _pendingStartMs) / duration, 0f, 1f);

        float glow, tint;
        if (t < VisualDesign.GlowShare)
        {
            float u = t / VisualDesign.GlowShare;
            glow = 0.3f + 0.7f * u;
            tint = 0.20f + 0.45f * u + 0.07f * MathF.Sin(u * MathF.PI * 3f);
        }
        else
        {
            float s = (t - VisualDesign.GlowShare) / (1f - VisualDesign.GlowShare);
            glow = 1f;
            tint = MathF.Min(1f, 0.62f + s * 1.6f);
        }

        // Mark the pending cells so each can find its same-group neighbours.
        Array.Clear(_pendingMask);
        foreach (var group in pending)
            foreach (var c in group.Cells)
                _pendingMask[c.Y * GameConfig.Columns + c.X] = true;

        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var group in pending)
            {
                foreach (var c in group.Cells)
                {
                    if (c.Y < GameConfig.HiddenRows) continue;

                    bool Same(int dx, int dy)
                    {
                        int nx = c.X + dx, ny = c.Y + dy;
                        return nx >= 0 && nx < GameConfig.Columns && ny >= 0 && ny < GameConfig.TotalRows
                               && _pendingMask[ny * GameConfig.Columns + nx]
                               && game.Board[nx, ny] == group.Colour;
                    }

                    var links = CellRenderer.Compose(
                        Same(0, -1), Same(1, 0), Same(0, 1), Same(-1, 0),
                        Same(1, -1), Same(1, 1), Same(-1, 1), Same(-1, -1));

                    CellRenderer.DrawPopLayer(
                        canvas,
                        (CellRenderer.BlockLayer)pass,
                        BoardSpace.ScreenX(c.X, Flip, originX, cell),
                        BoardSpace.ScreenY(c.Y, Flip, originY, cell),
                        cell,
                        group.Colour,
                        Flip ? CellRenderer.Flip(links) : links,
                        glow,
                        tint);
                }
            }
        }
    }

    private void DrawActivePiece(ICanvas canvas, CellRenderer.BlockLayer layer, Piece? piece, float originX, float originY, float cell)
    {
        if (piece is null) return;
        var cells = piece.Cells;

        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y;
            if (by < GameConfig.HiddenRows) continue;   // still in the hidden spawn rows

            int bx = piece.X + cells[i].X;
            var links = CellRenderer.LinksForPiece(cells, i);

            CellRenderer.DrawLayer(
                canvas,
                layer,
                BoardSpace.ScreenX(bx, Flip, originX, cell),
                BoardSpace.ScreenY(by, Flip, originY, cell),
                cell,
                cells[i].Colour,
                Flip ? CellRenderer.Flip(links) : links);
        }
    }

    /// <summary>Faint outline showing where the active piece will land.</summary>
    private void DrawGhost(ICanvas canvas, GameEngine game, float originX, float originY, float cell)
    {
        var piece = game.CurrentPiece;
        if (piece is null || game.State != GameState.Playing) return;

        int distance = game.LandingDistance;
        if (distance <= 0) return;

        float inset = cell * VisualDesign.CellInset;
        var cells = piece.Cells;
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.05f);
        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y + distance;
            if (by < GameConfig.HiddenRows) continue;
            int bx = piece.X + cells[i].X;
            canvas.StrokeColor = VisualDesign.NeonColour(cells[i].Colour).WithAlpha(VisualDesign.GhostAlpha);
            canvas.DrawRoundedRectangle(
                BoardSpace.ScreenX(bx, Flip, originX, cell) + inset,
                BoardSpace.ScreenY(by, Flip, originY, cell) + inset,
                cell - 2f * inset,
                cell - 2f * inset,
                cell * VisualDesign.CornerRadius);
        }
    }
}
