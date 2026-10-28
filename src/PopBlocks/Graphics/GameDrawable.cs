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
    private bool[] _pendingMask = new bool[GameConfig.Columns * (GameConfig.MaxBoardRows + GameConfig.HiddenRows)];

    // ---- The camera (tall boards). The engine decides where it is; the picture eases there over 300 ms. ----
    private const int CameraEaseMs = 300;
    private float _shownTop = GameConfig.HiddenRows, _fromTop = GameConfig.HiddenRows, _toTop = GameConfig.HiddenRows;
    private long _tweenStartMs;

    /// <summary>Puts the camera straight where the engine says it is (a new game), with no easing.</summary>
    public void ResetCamera()
    {
        _shownTop = _fromTop = _toTop = Game?.ViewTopRow ?? GameConfig.HiddenRows;
        BoardSpace.ViewTop = _shownTop;
    }

    /// <summary>The camera is still easing to a new place, so the page keeps repainting.</summary>
    public bool CameraMoving(long nowMs) => nowMs - _tweenStartMs < CameraEaseMs && MathF.Abs(_shownTop - _toTop) > 0.001f;

    private void StepCamera(long now)
    {
        float target = Game?.ViewTopRow ?? GameConfig.HiddenRows;
        if (target != _toTop)
        {
            _fromTop = _shownTop;
            _toTop = target;
            _tweenStartMs = now;
            if (ReducedMotion) _shownTop = _fromTop = target;   // Reduced motion: the camera just jumps
        }
        if (_shownTop != _toTop)
        {
            float t = Math.Clamp((now - _tweenStartMs) / (float)CameraEaseMs, 0f, 1f);
            _shownTop = _fromTop + (_toTop - _fromTop) * (t * t * (3f - 2f * t));   // ease in and out
            if (t >= 1f) _shownTop = _toTop;
        }
        BoardSpace.ViewTop = _shownTop;
    }

    public GameEngine? Game { get; set; }
    public BlastEffects Effects { get; } = new();

    /// <summary>The gold each popped group paid, floating up from where it popped.</summary>
    public FloatingText Floaters { get; } = new();
    public bool ShowGhost { get; set; } = true;

    /// <summary>Landing guide look: false = thin outline, true = bold (thicker line, a tint and a faint column of light).</summary>
    public bool GuideBold { get; set; }

    /// <summary>Colour Lens level from the shop: 0 off, 1 outlines groups of 3, 2 also groups of 2.</summary>
    public int LensLevel { get; set; }

    /// <summary>Chain Sense: the landing guide glows when dropping the piece where it is would start a chain.</summary>
    public bool ChainGlow { get; set; }

    /// <summary>0..1: a white flash over the top row of the board, used once when the stack reaches the top.</summary>
    public float TopRowFlash { get; set; }

    /// <summary>Calmer drawing: no pulsing or wobble in the highlights.</summary>
    public bool ReducedMotion { get; set; }

    /// <summary>
    /// Shows the empty cells under blocks that hang over a gap (they stay put until a pop lets everything fall) as a faint
    /// column in the stack's colour with a small arrow where it would come to rest. Nothing moves, so it is the same
    /// under Reduced motion.
    /// </summary>
    public bool ShowHanging { get; set; } = true;

    private readonly List<HangingRun> _hanging = new();

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

    /// <summary>Pip's Insight: where the piece in hand should go. Drawn as a dashed outline until it is reached or four seconds pass.</summary>
    public PlacementSuggestion? Insight { get; set; }
    public long InsightSinceMs { get; set; }
    private const int InsightShowMs = 4000;
    private static readonly float[] InsightDash = { 4f, 3f };
    private string _turnText = string.Empty;
    private static readonly PathF InsightArrow = BuildInsightArrow();

    private static PathF BuildInsightArrow()
    {
        var p = new PathF();
        p.MoveTo(-1f, -0.7f);
        p.LineTo(1f, -0.7f);
        p.LineTo(0f, 0.9f);
        p.Close();
        return p;
    }

    private bool InsightVisible(long nowMs) =>
        Insight is not null && nowMs - InsightSinceMs < InsightShowMs
        && Game is { State: GameState.Playing, CurrentPiece: { } piece }
        && PlacementSuggestion.Describe(piece) != Insight.Signature;

    /// <summary>The page lets the board's own movement run (a game in play, not paused, not idle, not Reduced motion).</summary>
    public bool AmbientOn { get; set; }

    /// <summary>The board's movement (stars, clouds, ripples, embers, petals) wants repainting.</summary>
    public bool NeedsAmbient => AmbientOn && !ReducedMotion && BoardAmbient.Animates(Theme.Motif);

    /// <summary>Any slow movement (board ambience, an animated frame, an Orb) wants a gentle repaint.</summary>
    public bool WantsSlowRepaint => NeedsAmbient || FrameAnimates || OrbAnimates;

    /// <summary>The frame in use moves while a piece falls, so it wants repainting too.</summary>
    public bool FrameAnimates =>
        !ReducedMotion && Frame is FrameStyle.Starlight or FrameStyle.Ripple or FrameStyle.Flame or FrameStyle.Neon
        && Game is { State: GameState.Playing, CurrentPiece: not null };

    /// <summary>True while something on the board is animating, so the page keeps repainting.</summary>
    public bool NeedsAnimation(long nowMs) =>
        (Game is { PendingGroups.Count: > 0 }) || Effects.IsActive(nowMs) || Floaters.IsActive(nowMs) || HintColumn >= 0 || CameraMoving(nowMs) || (!ReducedMotion && InsightVisible(nowMs));

    /// <summary>The slow movement (board ambience, frame animation) is all that is running: a gentle repaint is enough.</summary>
    public bool OnlyAmbientRunning(long nowMs) =>
        WantsSlowRepaint
        && !(Game is { PendingGroups.Count: > 0 }) && !Effects.IsActive(nowMs) && !Floaters.IsActive(nowMs) && HintColumn < 0 && !CameraMoving(nowMs);

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        long now = Clock();
        Diagnostics.CountFrame();   // (debug builds only) so a board that repaints while idle shows up in the numbers

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

        StepCamera(now);   // where the screen is on a tall board (always the floor on the standard one)
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
            if (ShowHanging) DrawHanging(canvas, game.Board, originX, originY, cell);

            for (int pass = 0; pass < 3; pass++)
                DrawLockedCells(canvas, (CellRenderer.BlockLayer)pass, game.Board, originX, originY, cell, now);
            DrawOrb(canvas, game, originX, originY, cell, now);

            if (LensLevel > 0) DrawLens(canvas, game, originX, originY, cell);

            DrawPendingGroups(canvas, game, originX, originY, cell, now);

            if (ShowGhost || ChainGlow) DrawGhost(canvas, game, originX, originY, cell);
            for (int pass = 0; pass < 3; pass++)
                DrawActivePiece(canvas, (CellRenderer.BlockLayer)pass, game.CurrentPiece, originX, originY, cell);
            if (Frame != FrameStyle.None) DrawFrame(canvas, game.CurrentPiece, originX, originY, cell, now);
            Effects.Draw(canvas, originX, originY, cell, boardW, Flip, now);
            Floaters.Draw(canvas, originX, originY, cell, Flip, now);
            DrawCameraHints(canvas, game, originX, originY, boardW, boardH, cell);
            if (InsightVisible(now)) DrawInsight(canvas, originX, originY, cell, now);

            if (TopRowFlash > 0f)
            {
                canvas.FillColor = Colors.White.WithAlpha(0.6f * TopRowFlash);
                canvas.FillRectangle(originX, Flip ? originY + boardH - cell : originY, boardW, cell);
            }

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
        if (_panelPath is null || _panelX != x || _panelY != y || _panelW != w || _panelH != h || _panelCorner != corner)
        {
            // The clip outline only changes with the size of the board, so it is built when that changes, not every frame.
            _panelPath = new PathF();
            _panelPath.AppendRoundedRectangle(x, y, w, h, corner);
            (_panelX, _panelY, _panelW, _panelH, _panelCorner) = (x, y, w, h, corner);
        }
        canvas.ClipPath(_panelPath);
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
        if (NeedsAmbient) BoardAmbient.Draw(canvas, Theme.Motif, Theme.Rim, Theme.MotifAlpha, x, y, w, h, cell, Clock());
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

    /// <summary>
    /// Blocks hanging over a gap stay where they are until a pop lets everything fall, which can look like a mistake. Tint
    /// the empty cells under each hanging stack in its colour and put a small arrow in the lowest one, where it would come
    /// to rest. The arrow means the cue does not rely on colour alone. Drawn under the blocks, so it never hides one.
    /// </summary>
    private void DrawHanging(ICanvas canvas, Board board, float originX, float originY, float cell)
    {
        GravityResolver.FindHanging(board, _hanging);
        if (_hanging.Count == 0) return;

        float inset = cell * VisualDesign.EmptyCellInset;
        float size = cell - 2f * inset;
        float down = Flip ? -1f : 1f;   // which way "down the board" points on screen
        float s = cell * 0.15f;

        foreach (var run in _hanging)
        {
            var neon = VisualDesign.NeonColour(run.Colour);
            float x = BoardSpace.ScreenX(run.X, Flip, originX, cell) + inset;

            canvas.FillColor = neon.WithAlpha(0.10f);
            for (int g = 1; g <= run.Gap; g++)
            {
                float y = BoardSpace.ScreenY(run.Y + g, Flip, originY, cell) + inset;
                canvas.FillRoundedRectangle(x, y, size, size, cell * VisualDesign.CornerRadius);
            }

            float cx = BoardSpace.ScreenX(run.X, Flip, originX, cell) + cell / 2f;
            float cy = BoardSpace.ScreenY(run.Y + run.Gap, Flip, originY, cell) + cell / 2f;
            var arrow = new PathF();
            arrow.MoveTo(cx - s, cy - down * s * 0.6f);
            arrow.LineTo(cx + s, cy - down * s * 0.6f);
            arrow.LineTo(cx, cy + down * s * 0.8f);
            arrow.Close();
            canvas.FillColor = neon.WithAlpha(0.45f);
            canvas.FillPath(arrow);
        }
    }

    private void DrawLockedCells(ICanvas canvas, CellRenderer.BlockLayer layer, Board board, float originX, float originY, float cell, long now)
    {
        // Only the rows on (or one row either side of) the screen are drawn.
        int firstRow = Math.Max(GameConfig.HiddenRows, (int)MathF.Floor(BoardSpace.ViewTop));
        for (int y = firstRow; y < board.Height; y++)
        {
            if (BoardSpace.BelowView(y)) break;
            for (int x = 0; x < board.Width; x++)
            {
                var colour = board[x, y];
                if (colour is CellColour.Empty or CellColour.Orb) continue;   // an Orb is drawn as a whole, below

                var links = LinksAt(board, x, y, colour);
                float fall = Effects.FallOffsetCells(x, y, now);
                CellRenderer.DrawLayer(
                    canvas,
                    layer,
                    BoardSpace.ScreenX(x, Flip, originX, cell),
                    BoardSpace.ScreenY(y, Flip, originY, cell, fall),
                    cell,
                    colour,
                    Flip ? CellRenderer.Flip(links) : links,
                    board.FractureAt(x, y));
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
                    if (BoardSpace.AboveView(c.Y)) continue;

                    bool Same(int dx, int dy)
                    {
                        int nx = c.X + dx, ny = c.Y + dy;
                        return nx >= 0 && nx < GameConfig.Columns && ny >= 0 && ny < game.Board.Height
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
            if (BoardSpace.AboveView(by)) continue;   // still in the hidden spawn rows

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
    private void DrawFrame(ICanvas canvas, Piece? piece, float originX, float originY, float cell, long now)
    {
        if (piece is null) return;
        if (Frame >= FrameStyle.Starlight)
        {
            DrawSoldFrame(canvas, piece, originX, originY, cell, ReducedMotion ? 0f : (now / 33 * 33) / 1000f);
            return;
        }
        var cells = piece.Cells;
        float inset = cell * VisualDesign.CellInset * 0.5f;
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.06f);

        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y;
            if (BoardSpace.AboveView(by)) continue;
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

    // ---- the six frames sold in v1.1. Time is 0 under Reduced motion, so they are still pictures there. ----

    private void DrawSoldFrame(ICanvas canvas, Piece piece, float originX, float originY, float cell, float t)
    {
        var cells = piece.Cells;
        float inset = cell * VisualDesign.CellInset * 0.5f;
        float line = Math.Clamp(cell * 0.07f, 2f, 3f);
        float corner = cell * VisualDesign.Corner;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        canvas.StrokeSize = line;
        canvas.StrokeLineCap = LineCap.Round;

        for (int i = 0; i < cells.Count; i++)
        {
            int by = piece.Y + cells[i].Y;
            if (BoardSpace.AboveView(by)) continue;
            int bx = piece.X + cells[i].X;
            float cx = BoardSpace.ScreenX(bx, Flip, originX, cell) + inset;
            float cy = BoardSpace.ScreenY(by, Flip, originY, cell) + inset;
            float side = cell - 2f * inset;
            minX = MathF.Min(minX, cx); minY = MathF.Min(minY, cy);
            maxX = MathF.Max(maxX, cx + side); maxY = MathF.Max(maxY, cy + side);

            switch (Frame)
            {
                case FrameStyle.Starlight:
                    canvas.StrokeColor = Color.FromArgb("#BFD3FF");
                    canvas.DrawRoundedRectangle(cx, cy, side, side, corner);
                    break;

                case FrameStyle.Ripple:
                {
                    float wobble = MathF.Sin(t * 5f + i * 1.3f) * 1f;   // plus or minus one unit
                    canvas.StrokeColor = Color.FromArgb("#7EE7F5");
                    canvas.DrawRoundedRectangle(cx - wobble, cy - wobble, side + 2f * wobble, side + 2f * wobble, corner);
                    break;
                }

                case FrameStyle.Stone:
                {
                    // A 3 unit bevel: light along the top and left, dark along the bottom and right.
                    canvas.StrokeSize = 3f;
                    canvas.StrokeColor = Color.FromArgb("#B8B2A8");
                    canvas.DrawLine(cx + 1.5f, cy + 1.5f, cx + side - 1.5f, cy + 1.5f);
                    canvas.DrawLine(cx + 1.5f, cy + 1.5f, cx + 1.5f, cy + side - 1.5f);
                    canvas.StrokeColor = Color.FromArgb("#6B655C");
                    canvas.DrawLine(cx + 1.5f, cy + side - 1.5f, cx + side - 1.5f, cy + side - 1.5f);
                    canvas.DrawLine(cx + side - 1.5f, cy + 1.5f, cx + side - 1.5f, cy + side - 1.5f);
                    break;
                }

                case FrameStyle.Flame:
                {
                    float mix = cells.Count <= 1 ? 0f : i / (float)(cells.Count - 1);
                    canvas.StrokeColor = new Color(1f, 0.416f + 0.17f * mix, 0.169f + 0.31f * mix);   // #FF6A2B towards #FFD23F
                    canvas.DrawRoundedRectangle(cx, cy, side, side, corner);
                    // Two flickering tips on the top edge.
                    for (int k = 0; k < 2; k++)
                    {
                        float flick = 0.5f + 0.5f * MathF.Sin(t * 14f + i * 2.1f + k * 1.7f);
                        float tx = cx + side * (0.3f + 0.4f * k);
                        canvas.DrawLine(tx, cy, tx + (k == 0 ? -1f : 1f), cy - cell * (0.08f + 0.12f * flick));
                    }
                    break;
                }

                case FrameStyle.Vine:
                    canvas.StrokeColor = Color.FromArgb("#5FBF4A");
                    canvas.DrawRoundedRectangle(cx, cy, side, side, corner);
                    break;

                case FrameStyle.Neon:
                {
                    float pulse = 0.8f + 0.2f * MathF.Sin(t * MathF.Tau * 0.8f);   // 0.6 to 1.0 at 0.8 Hz
                    var neon = Color.FromArgb("#FF4FD8");
                    canvas.StrokeSize = line + 4f;
                    canvas.StrokeColor = neon.WithAlpha(0.22f * pulse);   // the glow: a wide soft line under the sharp one
                    canvas.DrawRoundedRectangle(cx, cy, side, side, corner);
                    canvas.StrokeSize = line;
                    canvas.StrokeColor = neon.WithAlpha(pulse);
                    canvas.DrawRoundedRectangle(cx, cy, side, side, corner);
                    break;
                }
            }
        }

        if (minX > maxX) return;

        if (Frame == FrameStyle.Starlight)
        {
            // Four sparkles circling slowly round the piece.
            float cxm = (minX + maxX) / 2f, cym = (minY + maxY) / 2f;
            float rx = (maxX - minX) / 2f + cell * 0.25f, ry = (maxY - minY) / 2f + cell * 0.25f;
            canvas.StrokeSize = 1.5f;
            canvas.StrokeColor = Colors.White.WithAlpha(0.9f);
            for (int k = 0; k < 4; k++)
            {
                float a = t * 0.7f + k * MathF.PI / 2f;
                float sx = cxm + MathF.Cos(a) * rx, sy = cym + MathF.Sin(a) * ry, arm = cell * 0.09f;
                canvas.DrawLine(sx - arm, sy, sx + arm, sy);
                canvas.DrawLine(sx, sy - arm, sx, sy + arm);
            }
        }
        else if (Frame == FrameStyle.Vine)
        {
            // A small leaf on three of the four corners.
            canvas.FillColor = Color.FromArgb("#5FBF4A");
            float leaf = cell * 0.16f;
            canvas.FillEllipse(minX - leaf * 0.6f, minY - leaf * 0.4f, leaf * 1.2f, leaf * 0.8f);
            canvas.FillEllipse(maxX - leaf * 0.6f, minY - leaf * 0.4f, leaf * 1.2f, leaf * 0.8f);
            canvas.FillEllipse(minX - leaf * 0.6f, maxY - leaf * 0.4f, leaf * 1.2f, leaf * 0.8f);
        }
    }

    /// <summary>
    /// A dashed 2-unit outline where the piece should land, in the piece's own colours at 70% (pulsing unless Reduced motion),
    /// an arrow above its column and a turn glyph with the number of quarter turns. It disappears once the piece is in place.
    /// </summary>
    private void DrawInsight(ICanvas canvas, float originX, float originY, float cell, long now)
    {
        var s = Insight!;
        float pulse = ReducedMotion ? 1f : 0.75f + 0.25f * MathF.Sin(now / 1000f * MathF.Tau * 1.2f);
        float inset = cell * VisualDesign.CellInset * 0.5f;
        canvas.StrokeSize = 2f;
        canvas.StrokeDashPattern = InsightDash;
        float minSx = float.MaxValue, maxSx = float.MinValue, topSy = float.MaxValue;
        for (int li = 0; li < s.Landed.Count; li++)
        {
            var c = s.Landed[li];
            if (BoardSpace.AboveView(c.Y) || BoardSpace.BelowView(c.Y)) continue;
            float sx = BoardSpace.ScreenX(c.X, Flip, originX, cell), sy = BoardSpace.ScreenY(c.Y, Flip, originY, cell);
            minSx = MathF.Min(minSx, sx);
            maxSx = MathF.Max(maxSx, sx + cell);
            topSy = MathF.Min(topSy, sy);
            canvas.StrokeColor = VisualDesign.NeonColour(c.Colour).WithAlpha(0.7f * pulse);
            canvas.DrawRoundedRectangle(sx + inset, sy + inset, cell - 2f * inset, cell - 2f * inset, cell * VisualDesign.Corner);
        }
        canvas.StrokeDashPattern = null;
        if (minSx > maxSx) return;

        // An arrow above the column(s), pointing down at the spot.
        float ax = (minSx + maxSx) / 2f, ay = originY + cell * 0.55f, r = cell * 0.28f;
        canvas.FillColor = Colors.White.WithAlpha(0.85f * pulse);
        canvas.SaveState();
        canvas.Translate(ax, ay);
        canvas.Scale(r, r);
        canvas.FillPath(InsightArrow, WindingMode.NonZero);
        canvas.RestoreState();
        if (s.Rotations > 0)
        {
            if (_turnText.Length == 0 || _turnText[^1] != (char)('0' + s.Rotations)) _turnText = "\u21BB" + s.Rotations;
            canvas.FontColor = Colors.White.WithAlpha(0.9f);
            canvas.FontSize = cell * 0.42f;
            canvas.DrawString(_turnText, ax - cell, ay + r + 2f, cell * 2f, cell * 0.6f, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    /// <summary>The Orb, if there is one, drawn over its four cells.</summary>
    private void DrawOrb(ICanvas canvas, GameEngine game, float originX, float originY, float cell, long now)
    {
        if (game.OrbAt is not { } orb) return;
        if (BoardSpace.AboveView(orb.Y) && BoardSpace.AboveView(orb.Y + 1)) return;
        float x0 = MathF.Min(BoardSpace.ScreenX(orb.X, Flip, originX, cell), BoardSpace.ScreenX(orb.X + 1, Flip, originX, cell));
        float y0 = MathF.Min(BoardSpace.ScreenY(orb.Y, Flip, originY, cell), BoardSpace.ScreenY(orb.Y + 1, Flip, originY, cell));
        OrbDrawable.Draw(canvas, x0, y0, cell * 2f, game.OrbTimerLeft, game.Rules.OrbTimer, now, ReducedMotion, AmbientOn);
    }

    /// <summary>The Orb's swirl and shiver move while it is on the board.</summary>
    public bool OrbAnimates => AmbientOn && !ReducedMotion && Game is { OrbAt: not null };

    private PathF? _panelPath;
    private float _panelX, _panelY, _panelW, _panelH, _panelCorner;

    private int _belowShown = -1;
    private string _belowText = string.Empty;

    /// <summary>
    /// On a tall board: a fade and "â–¼ n rows below" when part of the board is under the screen, and a thin gauge down the
    /// right-hand edge: the whole board's height, a bracket for what the screen shows, filled up to the top of the stack.
    /// </summary>
    private void DrawCameraHints(ICanvas canvas, GameEngine game, float originX, float originY, float boardW, float boardH, float cell)
    {
        int rows = game.BoardRows;
        if (rows <= GameConfig.ViewportRows) return;

        int below = game.CameraBottom;
        if (below > 0)
        {
            float fade = cell * 1.3f, bottom = originY + boardH;
            for (int i = 0; i < 5; i++)
            {
                canvas.FillColor = Theme.Bottom.WithAlpha(0.18f * (i + 1));
                canvas.FillRectangle(originX, bottom - fade + fade * i / 5f, boardW, fade / 5f + 1f);
            }
            if (below != _belowShown)
            {
                _belowShown = below;
                _belowText = below == 1 ? "â–¼ 1 row below" : $"â–¼ {below} rows below";
            }
            canvas.FontColor = Colors.White.WithAlpha(0.9f);
            canvas.FontSize = cell * 0.40f;
            canvas.DrawString(_belowText, originX, bottom - cell * 0.95f, boardW, cell * 0.8f, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        float gx = originX + boardW + 0.5f, gw = 4f;
        float perRow = boardH / rows;
        canvas.FillColor = Theme.Grid.WithAlpha(0.55f);
        canvas.FillRectangle(gx, originY, gw, boardH);
        float filled = MathF.Min(rows, game.StackTop) * perRow;
        canvas.FillColor = Theme.Rim.WithAlpha(0.85f);
        canvas.FillRectangle(gx, originY + boardH - filled, gw, filled);
        float barBottom = originY + boardH - below * perRow, barTop = barBottom - GameConfig.ViewportRows * perRow;
        canvas.StrokeColor = Colors.White.WithAlpha(0.9f);
        canvas.StrokeSize = 1.2f;
        canvas.DrawRectangle(gx - 0.5f, barTop, gw + 1f, barBottom - barTop);
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

        // Wide, still pictures: clouds, ripple lines, rock layers and quilt squares.
        switch (theme.Motif)
        {
            case BoardMotif.Nebula:
                for (int i = 0; i < 4; i++)
                {
                    float rx = cell * (2.4f + 0.7f * i), ry = cell * (1.1f + 0.3f * i);
                    canvas.FillEllipse(x + w * (0.1f + 0.8f * BoardSpace.Noise(i, 31)) - rx, y + h * (0.12f + 0.24f * i) - ry, rx * 2f, ry * 2f);
                }
                return;

            case BoardMotif.Waves:
                for (int i = 0; i < 6; i++)
                {
                    float wy = y + h * (0.08f + 0.16f * i);
                    float px = x, py = wy;
                    for (int k = 1; k <= 16; k++)
                    {
                        float nx = x + w * k / 16f, ny = wy + MathF.Sin(k * 0.9f + i) * cell * 0.12f;
                        canvas.DrawLine(px, py, nx, ny);
                        px = nx;
                        py = ny;
                    }
                }
                return;

            case BoardMotif.Strata:
                for (int i = 0; i < 6; i++)
                {
                    float sy = y + h * (0.05f + 0.16f * i + 0.03f * BoardSpace.Noise(i, 37));
                    canvas.FillRectangle(x, sy, w, cell * (0.18f + 0.22f * BoardSpace.Noise(i, 39)));
                }
                return;

            case BoardMotif.Patchwork:
            {
                float sq = cell * 2f;
                for (int gy = 0; gy * sq < h; gy++)
                    for (int gx = 0; gx * sq < w; gx++)
                    {
                        if ((gx + gy) % 2 == 0) canvas.FillRectangle(x + gx * sq + 2f, y + gy * sq + 2f, sq - 4f, sq - 4f);
                        else canvas.DrawRectangle(x + gx * sq + 3f, y + gy * sq + 3f, sq - 6f, sq - 6f);
                    }
                return;
            }
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

                case BoardMotif.Stars:
                    canvas.DrawLine(-s * 0.35f, 0f, s * 0.35f, 0f);
                    canvas.DrawLine(0f, -s * 0.35f, 0f, s * 0.35f);
                    break;

                case BoardMotif.Embers:
                    canvas.FillEllipse(-s * 0.18f, -s * 0.18f, s * 0.36f, s * 0.36f);
                    break;

                case BoardMotif.Petals:
                    canvas.Rotate(spin);
                    canvas.FillEllipse(-s * 0.55f, -s * 0.25f, s * 1.1f, s * 0.5f);
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

        var groups = ColourMatcher.FindNearGroups(game.Board, LensLevel >= 2 ? 2 : 3, 3, game.Rules.MinGroupSize);
        if (groups.Count == 0) return;

        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.06f);
        foreach (var group in groups)
        {
            canvas.StrokeColor = Colors.White.WithAlpha(0.85f);
            canvas.StrokeDashPattern = group.Size == 2 ? new float[] { 3f, 3f } : null;

            foreach (var c in group.Cells)
            {
                if (BoardSpace.AboveView(c.Y)) continue;
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
                _glowChain = PlacementPreview.StartsChain(game.Board, piece, game.Rules.MinGroupSize, game.FractureActive, game.OrbTimerLeft == 0 ? int.MaxValue : game.OrbTimerLeft);
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
            if (BoardSpace.AboveView(by)) continue;
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
