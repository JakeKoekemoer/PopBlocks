using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

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

    /// <summary>Landing guide look: false = thin outline, true = bold (thicker line, a tint and a faint column of light).</summary>
    public bool GuideBold { get; set; }

    /// <summary>Colour Lens level from the shop: 0 off, 1 outlines groups of 3, 2 also groups of 2.</summary>
    public int LensLevel { get; set; }

    /// <summary>Chain Sense: the landing guide glows when dropping the piece where it is would start a chain.</summary>
    public bool ChainGlow { get; set; }

    /// <summary>Calmer drawing: no pulsing or wobble in the highlights.</summary>
    public bool ReducedMotion { get; set; }

    /// <summary>Outline around the piece you are moving (a cosmetic).</summary>
    public FrameStyle Frame { get; set; } = FrameStyle.None;

    private Piece? _glowPiece;
    private int _glowPlaced = -1;
    private bool _glowChain;

    /// <summary>Board colours. Cosmetic only: chosen from the board styles the player has unlocked.</summary>
    public BoardPalette Theme { get; set; } = BoardPalette.Classic;

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

            if (LensLevel > 0) DrawLens(canvas, game, originX, originY, cell);

            DrawPendingGroups(canvas, game, originX, originY, cell, now);

            if (ShowGhost || ChainGlow) DrawGhost(canvas, game, originX, originY, cell);
            for (int pass = 0; pass < 3; pass++)
                DrawActivePiece(canvas, (CellRenderer.BlockLayer)pass, game.CurrentPiece, originX, originY, cell);
            if (Frame != FrameStyle.None) DrawFrame(canvas, game.CurrentPiece, originX, originY, cell);
            Effects.Draw(canvas, originX, originY, cell, boardW, Flip, now);

            canvas.RestoreState();
        }

        canvas.RestoreState();
    }

    private void DrawBoardSurface(ICanvas canvas, float originX, float originY, float boardW, float boardH, float cell, bool flip)
    {
        var rim = Theme.Rim;
        float pad = VisualDesign.BoardPadding;
        float x = originX - pad, y = originY - pad, w = boardW + 2f * pad, h = boardH + 2f * pad;
        float corner = cell * VisualDesign.BoardCornerRadiusInCells;

        // Soft outer glow, then the glassy gradient panel.
        canvas.StrokeColor = rim.WithAlpha(0.10f);
        canvas.StrokeSize = 8f;
        canvas.DrawRoundedRectangle(x, y, w, h, corner);

        // Vertical gradient drawn as thin solid bands clipped to the rounded panel. (SetFillPaint is avoided on
        // purpose: on Android its shader stays on the paint and later solid FillColor calls come out invisible.)
        canvas.SaveState();
        var panelPath = new PathF();
        panelPath.AppendRoundedRectangle(x, y, w, h, corner);
        canvas.ClipPath(panelPath);
        const int bands = 40;
        var top = Theme.Top;
        var bottom = Theme.Bottom;
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

        // The board's decoration sits behind the grid and the blocks, faint enough that nothing is hard to read.
        if (Theme.Motif != BoardMotif.None && Theme.MotifAlpha > 0f)
            DrawMotifs(canvas, Theme, x, y, w, h, cell, flip);
        canvas.RestoreState();

        canvas.StrokeColor = rim.WithAlpha(0.35f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(x, y, w, h, corner);

        // Subtle grid lines.
        canvas.StrokeColor = Theme.Grid.WithAlpha(Theme.GridAlpha);
        canvas.StrokeSize = VisualDesign.GridLineWidth;
        for (int gx = 1; gx < GameConfig.Columns; gx++)
            canvas.DrawLine(originX + gx * cell, originY, originX + gx * cell, originY + boardH);
        for (int gy = 1; gy < GameConfig.VisibleRows; gy++)
            canvas.DrawLine(originX, originY + gy * cell, originX + boardW, originY + gy * cell);

        // Empty cells.
        float inset = cell * VisualDesign.EmptyCellInset;
        float size = cell - 2f * inset;
        canvas.FillColor = Theme.Empty.WithAlpha(0.75f);
        for (int ey = 0; ey < GameConfig.VisibleRows; ey++)
            for (int ex = 0; ex < GameConfig.Columns; ex++)
                canvas.FillRoundedRectangle(originX + ex * cell + inset, originY + ey * cell + inset, size, size, cell * 0.18f);

        // A glowing line along the floor, where blocks come to rest. Upside down on flipped levels.
        float floorY = flip ? originY : originY + boardH;
        canvas.StrokeColor = rim.WithAlpha(0.55f);
        canvas.StrokeSize = 2.5f;
        canvas.DrawLine(originX + cell * 0.2f, floorY, originX + boardW - cell * 0.2f, floorY);
    }

    private void DrawHint(ICanvas canvas, float originX, float originY, float cell, float boardH, long now)
    {
        if (HintColumn < 0) return;
        float pulse = ReducedMotion ? 0.6f : 0.5f + 0.5f * MathF.Sin(now / 260f);
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
            tint = 0.20f + 0.45f * u + (ReducedMotion ? 0f : 0.07f * MathF.Sin(u * MathF.PI * 3f));
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
    /// <summary>An outline around the piece in play. Drawn on top of the blocks; it never hides a symbol.</summary>
    private void DrawFrame(ICanvas canvas, Piece? piece, float originX, float originY, float cell)
    {
        if (piece is null) return;
        var cells = piece.Cells;
        float inset = cell * VisualDesign.CellInset * 0.5f;
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.06f);

        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y;
            if (by < GameConfig.HiddenRows) continue;
            int bx = piece.X + cells[i].X;

            canvas.StrokeColor = Frame switch
            {
                FrameStyle.Soft => Colors.White.WithAlpha(0.65f),
                FrameStyle.Gold => Color.FromArgb("#FFC83D"),
                _ => VisualDesign.NeonColour(cells[i].Colour),
            };
            canvas.DrawRoundedRectangle(
                BoardSpace.ScreenX(bx, Flip, originX, cell) + inset,
                BoardSpace.ScreenY(by, Flip, originY, cell) + inset,
                cell - 2f * inset,
                cell - 2f * inset,
                cell * VisualDesign.Corner);
        }
    }

    private static readonly PathF MotifDiamond = BuildMotifDiamond();

    private static PathF BuildMotifDiamond()
    {
        var p = new PathF();
        p.MoveTo(0f, -1f);
        p.LineTo(0.6f, 0f);
        p.LineTo(0f, 1f);
        p.LineTo(-0.6f, 0f);
        p.Close();
        return p;
    }

    /// <summary>
    /// Scatters the board's decoration over the panel at fixed, repeatable spots (nothing moves and nothing is
    /// allocated), in the rim colour at a very low alpha. The blocks, the grid and the empty cells draw over it.
    /// </summary>
    internal static void DrawMotifs(ICanvas canvas, BoardPalette theme, float x, float y, float w, float h, float cell, bool flip)
    {
        var colour = theme.Rim.WithAlpha(theme.MotifAlpha);
        canvas.StrokeColor = colour;
        canvas.FillColor = colour;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = MathF.Max(1f, cell * 0.04f);

        // Aurora is a few wide soft bands; battlements are a row along the top (the bottom when flipped).
        if (theme.Motif == BoardMotif.Aurora)
        {
            for (int i = 0; i < 4; i++)
            {
                float ay = y + h * (0.10f + 0.22f * i + 0.04f * BoardSpace.Noise(i, 3));
                float aw = w * (0.7f + 0.3f * BoardSpace.Noise(i, 5));
                canvas.FillEllipse(x + (w - aw) * BoardSpace.Noise(i, 9), ay, aw, cell * 0.55f);
            }
            return;
        }

        if (theme.Motif == BoardMotif.Battlements)
        {
            float bw = cell * 0.5f;
            float top = flip ? y + h - bw * 1.4f : y;
            for (float bx = x + cell * 0.2f; bx < x + w - bw; bx += bw * 2f)
                canvas.FillRectangle(bx, top, bw, bw * 1.4f);
            return;
        }

        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            float cx = x + w * (0.06f + 0.88f * BoardSpace.Noise(i, 11));
            float cy = y + h * (0.04f + 0.92f * BoardSpace.Noise(i, 17));
            float s = cell * (0.28f + 0.30f * BoardSpace.Noise(i, 23));
            float spin = BoardSpace.Noise(i, 29) * 360f;

            canvas.SaveState();
            canvas.Translate(cx, cy);
            switch (theme.Motif)
            {
                case BoardMotif.Leaves:
                    canvas.Rotate(spin);
                    canvas.FillEllipse(-s, -s * 0.45f, s * 2f, s * 0.9f);
                    break;

                case BoardMotif.Crystals:
                    canvas.Rotate(spin * 0.1f);
                    canvas.Scale(s, s);
                    canvas.FillPath(MotifDiamond, WindingMode.NonZero);
                    break;

                case BoardMotif.Bubbles:
                    canvas.DrawEllipse(-s * 0.6f, -s * 0.6f, s * 1.2f, s * 1.2f);
                    break;

                case BoardMotif.Gears:
                    canvas.DrawEllipse(-s * 0.6f, -s * 0.6f, s * 1.2f, s * 1.2f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * MathF.PI / 3f + spin * 0.0175f;
                        canvas.DrawLine(MathF.Cos(a) * s * 0.6f, MathF.Sin(a) * s * 0.6f, MathF.Cos(a) * s * 0.9f, MathF.Sin(a) * s * 0.9f);
                    }
                    break;

                case BoardMotif.Snowflakes:
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * MathF.PI / 3f + spin * 0.0175f;
                        canvas.DrawLine(-MathF.Cos(a) * s * 0.8f, -MathF.Sin(a) * s * 0.8f, MathF.Cos(a) * s * 0.8f, MathF.Sin(a) * s * 0.8f);
                    }
                    break;

                case BoardMotif.Lanterns:
                    canvas.FillEllipse(-s * 0.4f, -s * 0.55f, s * 0.8f, s * 1.1f);
                    canvas.DrawLine(0f, -s * 0.55f, 0f, -s * 0.95f);
                    break;

                case BoardMotif.Meadow:
                    canvas.DrawLine(0f, s * 0.5f, 0f, -s * 0.5f);
                    canvas.FillEllipse(-s * 0.18f, -s * 0.68f, s * 0.36f, s * 0.36f);
                    break;
            }
            canvas.RestoreState();
        }
    }

    /// <summary>
    /// Colour Lens: outlines groups that are a block or two short of a burst. Groups of 3 get a solid line and groups
    /// of 2 a dashed one, so the two sizes differ by pattern as well as by colour. It only shows what is on the board.
    /// </summary>
    private void DrawLens(ICanvas canvas, GameEngine game, float originX, float originY, float cell)
    {
        if (game.State is not (GameState.Playing or GameState.Paused)) return;

        var groups = ColourMatcher.FindNearGroups(game.Board, LensLevel >= 2 ? 2 : 3, 3);
        if (groups.Count == 0) return;

        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.06f);
        foreach (var group in groups)
        {
            canvas.StrokeColor = Colors.White.WithAlpha(0.85f);
            canvas.StrokeDashPattern = group.Size == 2 ? new float[] { 3f, 3f } : null;

            foreach (var c in group.Cells)
            {
                if (c.Y < GameConfig.HiddenRows) continue;
                float x0 = BoardSpace.ScreenX(c.X, Flip, originX, cell);
                float y0 = BoardSpace.ScreenY(c.Y, Flip, originY, cell);

                // Edges that face outside the group (up, right, down, left on the board).
                bool Own(int dx, int dy) => group.Cells.Any(o => o.X == c.X + dx && o.Y == c.Y + dy);
                float inset = 1.5f;
                bool top = !Own(0, -1), right = !Own(1, 0), bottom = !Own(0, 1), left = !Own(-1, 0);
                if (Flip) (top, bottom, left, right) = (bottom, top, right, left);

                if (top) canvas.DrawLine(x0 + inset, y0 + inset, x0 + cell - inset, y0 + inset);
                if (bottom) canvas.DrawLine(x0 + inset, y0 + cell - inset, x0 + cell - inset, y0 + cell - inset);
                if (left) canvas.DrawLine(x0 + inset, y0 + inset, x0 + inset, y0 + cell - inset);
                if (right) canvas.DrawLine(x0 + cell - inset, y0 + inset, x0 + cell - inset, y0 + cell - inset);
            }
        }
        canvas.StrokeDashPattern = null;
    }

    private void DrawGhost(ICanvas canvas, GameEngine game, float originX, float originY, float cell)
    {
        var piece = game.CurrentPiece;
        if (piece is null || game.State != GameState.Playing) return;

        int distance = game.LandingDistance;
        if (distance <= 0) return;

        // Chain Sense: look one drop ahead, once per piece, with the same rules the engine uses.
        bool chain = false;
        if (ChainGlow)
        {
            if (!ReferenceEquals(piece, _glowPiece) || game.PiecesPlaced != _glowPlaced)
            {
                _glowPiece = piece;
                _glowPlaced = game.PiecesPlaced;
                _glowChain = PlacementPreview.StartsChain(game.Board, piece);
            }
            chain = _glowChain;
        }

        // The plain outline shows only when the guide is on; Chain Sense shows its glow even with the guide off.
        if (!ShowGhost && !chain) return;

        float inset = cell * VisualDesign.CellInset;
        var cells = piece.Cells;
        float line = MathF.Max(1.5f, cell * 0.05f);
        if (GuideBold) line = MathF.Max(2.5f, cell * 0.09f);
        if (chain) line = MathF.Max(line, cell * 0.10f);

        // Bold guide: a faint column of light from the piece to where it lands.
        if (GuideBold && ShowGhost)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int bx = piece.X + cells[i].X;
                canvas.FillColor = Colors.White.WithAlpha(0.05f);
                float colX = BoardSpace.ScreenX(bx, Flip, originX, cell);
                float top = BoardSpace.ScreenY(piece.Y + cells[i].Y, Flip, originY, cell);
                float bottom = BoardSpace.ScreenY(piece.Y + cells[i].Y + distance, Flip, originY, cell);
                canvas.FillRectangle(colX + inset, MathF.Min(top, bottom), cell - 2f * inset, MathF.Abs(bottom - top));
            }
        }

        canvas.StrokeSize = line;
        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y + distance;
            if (by < GameConfig.HiddenRows) continue;
            int bx = piece.X + cells[i].X;

            float x = BoardSpace.ScreenX(bx, Flip, originX, cell) + inset;
            float y = BoardSpace.ScreenY(by, Flip, originY, cell) + inset;
            float size = cell - 2f * inset;

            if (GuideBold && ShowGhost)
            {
                canvas.FillColor = VisualDesign.NeonColour(cells[i].Colour).WithAlpha(0.18f);
                canvas.FillRoundedRectangle(x, y, size, size, cell * VisualDesign.CornerRadius);
            }

            canvas.StrokeColor = chain
                ? Color.FromArgb("#FFE066")
                : VisualDesign.NeonColour(cells[i].Colour).WithAlpha(VisualDesign.GhostAlpha);
            // A chain glow is also dashed, so it does not rely on colour alone.
            canvas.StrokeDashPattern = chain ? new float[] { 2.5f, 1.5f } : null;
            canvas.DrawRoundedRectangle(x, y, size, size, cell * VisualDesign.CornerRadius);
        }
        canvas.StrokeDashPattern = null;
    }
}


/// <summary>A board's colours and decoration. The rules and the block colours never change with it.</summary>
public sealed record BoardPalette(
    Color Top, Color Bottom, Color Rim, Color Grid, float GridAlpha, Color Empty, BoardMotif Motif, float MotifAlpha)
{
    public static BoardPalette Classic { get; } = new(
        VisualDesign.BoardTop, VisualDesign.BoardBottom, VisualDesign.BoardRim,
        VisualDesign.GridLine, VisualDesign.GridLineAlpha, VisualDesign.EmptyCell, BoardMotif.None, 0f);

    /// <summary>The palette for a board id from the catalogue; anything unknown is the classic board.</summary>
    public static BoardPalette For(string id)
    {
        if (id == CosmeticCatalog.DefaultBoard) return Classic;

        var spec = CosmeticCatalog.BoardOf(id);
        var bottom = Color.FromArgb(spec.Bottom);
        var rim = Color.FromArgb(spec.Rim);
        return new BoardPalette(
            Color.FromArgb(spec.Top), bottom, rim,
            Color.FromArgb(spec.Grid), (float)spec.GridAlpha,
            Mix(bottom, rim, 0.10f), spec.Motif, (float)spec.MotifAlpha);
    }

    private static Color Mix(Color a, Color b, float t) => new(
        a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t, 1f);
}
