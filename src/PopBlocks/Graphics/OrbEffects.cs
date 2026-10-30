using Microsoft.Maui.Graphics;
using PopBlocks.Engine;

namespace PopBlocks.Graphics;

/// <summary>
/// What happens round the Orb when it arrives, breaks or runs out of time. Three short effects in a fixed pool of three slots, all
/// drawn from the clock alone (no particles are kept, nothing is allocated while drawing):
/// it grows in with a ring of light; it breaks with a shockwave, bands of light along its two rows and two columns, and
/// glass shards; it runs out with a dark ring closing on it and the twelve blocks round it flashing. Under Reduced motion each is a
/// single short fade in place.
/// </summary>
public sealed class OrbEffects
{
    /// <summary>The same length as the hold the engine puts on the board while an Orb arrives (<c>GameEngine.OrbRevealMs</c>).</summary>
    public const int AppearMs = PopBlocks.Engine.Logic.GameEngine.OrbRevealMs;
    public const int BreakMs = 700;
    public const int CorruptMs = 600;
    public const int ReducedMs = 220;

    private enum Kind : byte { Appeared, Broke, Corrupted }

    private struct Slot
    {
        public bool Alive;
        public Kind Kind;
        public long StartMs;
        public int X, Y;   // board cell of the Orb's top-left
        public bool Reduced;
        public int PushLeft, PushRight;   // rows the blocks in the Orb's two columns were shoved up (Appeared only)
    }

    private const int SlotCount = 3;
    private readonly Slot[] _slots = new Slot[SlotCount];
    private int _next;

    private static readonly Color Glass = Color.FromArgb("#F4F7FF");
    private static readonly Color Violet = Color.FromArgb("#8C7CFF");
    private static readonly Color Shade = Color.FromArgb("#2B1E6E");
    private static readonly Color[] Shards =
    {
        Color.FromArgb("#38BDF8"), Color.FromArgb("#FB7185"), Color.FromArgb("#FBBF24"), Color.FromArgb("#A78BFA"), Color.FromArgb("#A3E635"),
    };

    public bool Enabled { get; set; } = true;
    public bool ReducedMotion { get; set; }

    public void Appeared(int x, int y, long nowMs, int pushLeft = 0, int pushRight = 0) => Start(Kind.Appeared, x, y, nowMs, pushLeft, pushRight);
    public void Broke(int x, int y, long nowMs) => Start(Kind.Broke, x, y, nowMs);
    public void Corrupted(int x, int y, long nowMs) => Start(Kind.Corrupted, x, y, nowMs);

    private void Start(Kind kind, int x, int y, long nowMs, int pushLeft = 0, int pushRight = 0)
    {
        if (!Enabled) return;
        _slots[_next] = new Slot { Alive = true, Kind = kind, StartMs = nowMs, X = x, Y = y, Reduced = ReducedMotion, PushLeft = pushLeft, PushRight = pushRight };
        _next = (_next + 1) % SlotCount;
    }

    /// <summary>The share of the arrival spent swelling and shoving; the rest is the Orb settling and its ring of light.</summary>
    private const float GrowShare = 0.72f;

    private static int LifeOf(in Slot s) =>
        s.Reduced ? ReducedMs : s.Kind switch { Kind.Appeared => AppearMs, Kind.Broke => BreakMs, _ => CorruptMs };

    public bool IsActive(long nowMs)
    {
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i].Alive && nowMs - _slots[i].StartMs < LifeOf(_slots[i])) return true;
        return false;
    }

    /// <summary>
    /// How much of its full size the Orb on the board is drawn at: it starts as a single block (half the width of its 2x2 footprint),
    /// swells to full size with a small overshoot while it shoves the blocks above it away, and is whole at all other times (and
    /// always under Reduced motion).
    /// </summary>
    public float GrowFactor(long nowMs)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ref readonly var s = ref _slots[i];
            if (!s.Alive || s.Kind != Kind.Appeared || s.Reduced) continue;
            long age = nowMs - s.StartMs;
            if (age < 0 || age >= AppearMs * GrowShare) continue;
            float t = age / (AppearMs * GrowShare);
            // ease-out with a small overshoot (back-out), settling on 1; it begins at one block, not at nothing
            float k = t - 1f;
            float back = 1f + 2.2f * k * k * k + 1.2f * k * k;
            return 0.5f + 0.5f * back;
        }
        return 1f;
    }

    /// <summary>
    /// How far below its place (in cells, board-down) a locked block is drawn while an Orb swells: the blocks in the Orb's two columns
    /// above it start where they were before it arrived and slide up as it grows. Zero for every other cell and at all other times.
    /// </summary>
    public float PushOffsetCells(int x, int y, long nowMs)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ref readonly var s = ref _slots[i];
            if (!s.Alive || s.Kind != Kind.Appeared || s.Reduced) continue;
            if (y >= s.Y || (x != s.X && x != s.X + 1)) continue;
            long age = nowMs - s.StartMs;
            if (age < 0 || age >= AppearMs * GrowShare) continue;
            int rows = x == s.X ? s.PushLeft : s.PushRight;
            if (rows == 0) continue;
            float t = age / (AppearMs * GrowShare);
            float eased = 1f - (1f - t) * (1f - t);
            return rows * (1f - eased);
        }
        return 0f;
    }

    public void Draw(ICanvas canvas, float originX, float originY, float cell, bool flip, long nowMs)
    {
        if (!Enabled) return;
        for (int i = 0; i < SlotCount; i++)
        {
            ref readonly var s = ref _slots[i];
            if (!s.Alive) continue;
            long age = nowMs - s.StartMs;
            int life = LifeOf(s);
            if (age < 0 || age >= life) continue;
            if (BoardSpace.AboveView(s.Y + 1) && BoardSpace.AboveView(s.Y)) continue;

            float t = age / (float)life;
            float x0 = MathF.Min(BoardSpace.ScreenX(s.X, flip, originX, cell), BoardSpace.ScreenX(s.X + 1, flip, originX, cell));
            float y0 = MathF.Min(BoardSpace.ScreenY(s.Y, flip, originY, cell), BoardSpace.ScreenY(s.Y + 1, flip, originY, cell));
            float cx = x0 + cell, cy = y0 + cell;

            if (s.Reduced) DrawReduced(canvas, s.Kind, cx, cy, cell, t);
            else if (s.Kind == Kind.Appeared) DrawAppeared(canvas, cx, cy, cell, MathF.Max(0f, (t - GrowShare * 0.6f) / (1f - GrowShare * 0.6f)));
            else if (s.Kind == Kind.Broke) DrawBroke(canvas, s, x0, y0, cx, cy, originX, originY, cell, t, i);
            else DrawCorrupted(canvas, s, x0, y0, cx, cy, originX, originY, cell, t, flip);
        }
        canvas.Alpha = 1f;
    }

    private static void DrawReduced(ICanvas canvas, Kind kind, float cx, float cy, float cell, float t)
    {
        float r = cell * 1.1f;
        canvas.Alpha = 0.55f * (1f - t);
        canvas.FillColor = kind == Kind.Corrupted ? Shade : Glass;
        canvas.FillEllipse(cx - r, cy - r, r * 2f, r * 2f);
    }

    private static void DrawAppeared(ICanvas canvas, float cx, float cy, float cell, float t)
    {
        // A ring of light that spreads from the sphere and thins out.
        float r = cell * (0.9f + 1.7f * t);
        canvas.StrokeColor = Glass;
        canvas.Alpha = 0.65f * (1f - t) * (1f - t);
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.12f * (1f - t));
        canvas.DrawEllipse(cx - r, cy - r, r * 2f, r * 2f);
    }

    private static void DrawBroke(ICanvas canvas, in Slot s, float x0, float y0, float cx, float cy, float originX, float originY, float cell, float t, int slot)
    {
        float boardW = cell * GameConfig.Columns, boardH = cell * GameConfig.VisibleRows;

        // Bands of light along the two rows and the two columns it cleared, bright at first and gone by the middle of the effect.
        if (t < 0.5f)
        {
            float k = 1f - t / 0.5f;
            canvas.Alpha = 0.40f * k;
            canvas.FillColor = Glass;
            canvas.FillRectangle(originX, y0, boardW, cell * 2f);
            canvas.FillRectangle(x0, originY, cell * 2f, boardH);
        }

        // The shockwave.
        float wave = 1f - (1f - t) * (1f - t);
        float r = cell * (0.8f + 3.4f * wave);
        canvas.StrokeColor = Glass;
        canvas.Alpha = 0.8f * (1f - t);
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.16f * (1f - t));
        canvas.DrawEllipse(cx - r, cy - r, r * 2f, r * 2f);

        // Ten shards of glass in the five block colours, flying out and falling away.
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = MathF.Max(1.5f, cell * 0.09f);
        for (int i = 0; i < 10; i++)
        {
            float angle = (i * 36f + BoardSpace.Noise(s.X * 7 + slot, i) * 24f) * (MathF.PI / 180f);
            float dist = cell * (0.5f + (1.0f + 1.8f * BoardSpace.Noise(i, s.Y)) * wave);
            float sag = cell * 0.9f * t * t;   // they drop a little as they go
            float px = cx + MathF.Cos(angle) * dist, py = cy + MathF.Sin(angle) * dist + sag;
            float len = cell * 0.26f * (1f - t * 0.6f);
            canvas.StrokeColor = Shards[i % Shards.Length];
            canvas.Alpha = 0.95f * (1f - t);
            canvas.DrawLine(px, py, px - MathF.Cos(angle) * len, py - MathF.Sin(angle) * len);
        }
    }

    private static void DrawCorrupted(ICanvas canvas, in Slot s, float x0, float y0, float cx, float cy, float originX, float originY, float cell, float t, bool flip)
    {
        // A dark ring closes on the place where it was, and the sphere's last light goes out with it.
        float close = t * t;
        float r = cell * (2.2f - 1.7f * close);
        canvas.StrokeColor = Shade;
        canvas.Alpha = 0.85f * (1f - t);
        canvas.StrokeSize = MathF.Max(2f, cell * 0.22f * (1f - 0.5f * t));
        canvas.DrawEllipse(cx - r, cy - r, r * 2f, r * 2f);

        float core = cell * 0.9f * (1f - t);
        canvas.FillColor = Violet;
        canvas.Alpha = 0.6f * (1f - t);
        canvas.FillEllipse(cx - core, cy - core, core * 2f, core * 2f);

        // The twelve blocks round it flash dark where they took a crack.
        if (t < 0.55f)
        {
            canvas.FillColor = Shade;
            canvas.Alpha = 0.5f * (1f - t / 0.55f);
            for (int dy = -1; dy <= 2; dy++)
                for (int dx = -1; dx <= 2; dx++)
                {
                    if (dx is 0 or 1 && dy is 0 or 1) continue;
                    int bx = s.X + dx, by = s.Y + dy;
                    if (bx < 0 || bx >= GameConfig.Columns || BoardSpace.AboveView(by) || BoardSpace.BelowView(by)) continue;
                    float px = BoardSpace.ScreenX(bx, flip, originX, cell), py = BoardSpace.ScreenY(by, flip, originY, cell);
                    canvas.FillRoundedRectangle(px + 2f, py + 2f, cell - 4f, cell - 4f, cell * 0.2f);
                }
        }
    }
}
