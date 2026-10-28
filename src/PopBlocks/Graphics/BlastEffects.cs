using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// The moment a match actually pops. By then the blocks have already glowed and turned solid neon
/// (see <see cref="GameDrawable"/>); this class draws the burst that follows, richer as the player buys
/// Pop Flair in the shop:
/// <list type="bullet">
/// <item>0 - neon flash, ring, sparks and a rising score tag</item>
/// <item>1 - plus a shockwave across the board</item>
/// <item>2 - plus long sparks and a screen shake</item>
/// <item>3 - plus a confetti shower</item>
/// </list>
/// A small fixed set of slots is reused. Purely visual: it never touches game state.
/// </summary>
public sealed class BlastEffects
{
    private sealed class Burst
    {
        public long StartMs;
        public int DurationMs;
        public int Flair;
        public BurstStyle Style;
        public bool Reduced;
        public BurstWave Wave = null!;
        public float CentreX, CentreY;      // centroid of the cleared cells, in board cells
        public CellColour Dominant;
        /// <summary>Rows each cell fell, indexed [y * Columns + x] by its final position.</summary>
        public int[] Drop = new int[GameConfig.Columns * (GameConfig.MaxBoardRows + GameConfig.HiddenRows)];
    }

    private const int SlotCount = 4;
    private readonly Burst?[] _slots = new Burst?[SlotCount];
    private int _nextSlot;

    private long _chainStartMs = long.MinValue;
    private string _chainText = string.Empty;
    private int _chainGoldTenths;   // gold paid by the chain so far, for the banner
    private long _shakeStartMs = long.MinValue;
    private int _shakeFlair;

    public bool Enabled { get; set; } = true;

    /// <summary>Pop Flair level from the shop, 0 to 3.</summary>
    public int Flair { get; set; }

    /// <summary>
    /// Reduced motion: bursts become short fades. No shake, no confetti, no sparks, and blocks drop into place without
    /// sliding. The popping itself, and the chain banner, are unchanged.
    /// </summary>
    public bool ReducedMotion { get; set; }

    /// <summary>The shapes the pop scatters (a cosmetic). It changes how the burst looks, never how long it lasts or what it scores.</summary>
    public BurstStyle Style { get; set; } = BurstStyle.Classic;

    public void Trigger(BurstWave wave, long nowMs)
    {
        if (!Enabled) return;

        var burst = new Burst
        {
            StartMs = nowMs,
            Wave = wave,
            Flair = ReducedMotion ? 0 : Math.Clamp(Flair, 0, 3),
            Style = Style,
            Reduced = ReducedMotion,
        };
        burst.DurationMs = burst.Flair >= 3 ? 1000 : VisualDesign.BurstDurationMs;
        // Petals drift for about a second and shards are done in under half of one; they draw themselves (BurstParticles).
        if (Style is BurstStyle.Petals or BurstStyle.Shards) burst.DurationMs = BurstParticles.LifeMs(Style, ReducedMotion);
        if (ReducedMotion) burst.DurationMs = Math.Min(burst.DurationMs, 450);

        foreach (var move in wave.Moves)
            burst.Drop[move.ToY * GameConfig.Columns + move.X] = move.ToY - move.FromY;

        float sumX = 0f, sumY = 0f;
        foreach (var c in wave.ClearedCells) { sumX += c.X; sumY += c.Y; }
        int n = Math.Max(1, wave.ClearedCells.Count);
        burst.CentreX = sumX / n;
        burst.CentreY = sumY / n;
        burst.Dominant = wave.Groups.Count > 0
            ? wave.Groups.OrderByDescending(g => g.Size).First().Colour
            : CellColour.Cyan;

        _slots[_nextSlot] = burst;
        _nextSlot = (_nextSlot + 1) % SlotCount;

        _chainGoldTenths = wave.ChainIndex <= 1 ? wave.GoldTenths : _chainGoldTenths + wave.GoldTenths;
        if (wave.ChainIndex >= 2)
        {
            _chainStartMs = nowMs;
            _chainText = _chainGoldTenths > 0
                ? $"CHAIN ×{wave.Multiplier}  +{FloatingText.FormatGold(_chainGoldTenths)} gold"
                : $"CHAIN ×{wave.Multiplier}";
        }

        if (burst.Flair >= 2)
        {
            _shakeStartMs = nowMs;
            _shakeFlair = burst.Flair;
        }
    }

    public bool IsActive(long nowMs)
    {
        if (nowMs - _chainStartMs < VisualDesign.ChainAccentMs) return true;
        if (nowMs - _shakeStartMs < VisualDesign.ShakeMs) return true;
        foreach (var slot in _slots)
            if (slot is not null && nowMs - slot.StartMs < Math.Max(slot.DurationMs, VisualDesign.FallDurationMs))
                return true;
        return false;
    }

    /// <summary>
    /// Vertical offset, in cells (zero or negative), at which the cell now at (x, y) should be drawn
    /// so it appears to slide down from where it used to be.
    /// </summary>
    public float FallOffsetCells(int x, int y, long nowMs)
    {
        if (!Enabled || ReducedMotion) return 0f;
        float offset = 0f;
        foreach (var slot in _slots)
        {
            if (slot is null) continue;
            long age = nowMs - slot.StartMs;
            if (age < 0 || age >= VisualDesign.FallDurationMs) continue;
            int rows = slot.Drop[y * GameConfig.Columns + x];
            if (rows == 0) continue;
            float remaining = 1f - age / (float)VisualDesign.FallDurationMs;
            offset = MathF.Max(offset, rows * remaining * remaining);   // ease-out
        }
        return -offset;
    }

    /// <summary>Screen shake for the board, in pixels. Zero unless a Flair 2+ pop just happened.</summary>
    public (float Dx, float Dy) ShakeOffset(float cell, long nowMs)
    {
        if (!Enabled || ReducedMotion) return (0f, 0f);
        long age = nowMs - _shakeStartMs;
        if (age < 0 || age >= VisualDesign.ShakeMs) return (0f, 0f);

        float t = age / (float)VisualDesign.ShakeMs;
        float amplitude = cell * (_shakeFlair >= 3 ? 0.22f : 0.15f) * (1f - t) * (1f - t);
        float phase = age * 0.06f;
        return (MathF.Sin(phase * 2.3f) * amplitude, MathF.Cos(phase * 3.1f) * amplitude);
    }

    public void Draw(ICanvas canvas, float originX, float originY, float cell, float boardWidth, bool flip, long nowMs)
    {
        if (!Enabled) return;

        canvas.SaveState();

        foreach (var slot in _slots)
        {
            if (slot is null) continue;
            long age = nowMs - slot.StartMs;
            if (age < 0 || age >= slot.DurationMs) continue;
            DrawBurst(canvas, slot, originX, originY, cell, flip, age, boardWidth);
        }

        long chainAge = nowMs - _chainStartMs;
        if (chainAge >= 0 && chainAge < VisualDesign.ChainAccentMs)
            DrawChainAccent(canvas, originX, originY, cell, boardWidth, flip, chainAge / (float)VisualDesign.ChainAccentMs);

        canvas.Alpha = 1f;
        canvas.RestoreState();
    }

    /// <summary>
    /// One flying piece of a pop. The style picks the shape; the Flair level still decides how many there are and how far
    /// they fly. Everything here is drawn with plain canvas calls (no allocation), so the effect pool stays cheap.
    /// </summary>
    private static void DrawSpark(ICanvas canvas, BurstStyle style, int flair, float x, float y, float angle, float cell, float fade, int seed)
    {
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        canvas.StrokeLineCap = LineCap.Round;

        switch (style)
        {
            case BurstStyle.Petals:
            {
                // A petal: an ellipse laid along its direction of travel.
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.Rotate(angle * 57.2958f);
                float w = cell * 0.26f * (0.5f + 0.5f * fade), h = cell * 0.12f * (0.5f + 0.5f * fade);
                canvas.FillEllipse(-w / 2f, -h / 2f, w, h);
                canvas.RestoreState();
                break;
            }

            case BurstStyle.Shards:
            {
                // A short, thick, square-ended sliver.
                float len = cell * 0.30f * fade;
                canvas.StrokeLineCap = LineCap.Butt;
                canvas.StrokeSize = MathF.Max(1.5f, cell * 0.11f);
                canvas.DrawLine(x, y, x - cos * len, y - sin * len);
                break;
            }

            case BurstStyle.Bubbles:
            {
                // A hollow bubble that grows a little as it rises and fades.
                float r = cell * (0.07f + 0.08f * (1f - fade));
                canvas.StrokeSize = MathF.Max(1f, cell * 0.05f);
                canvas.DrawEllipse(x - r, y - r, r * 2f, r * 2f);
                break;
            }

            case BurstStyle.Cogs:
            {
                // A small square that spins as it goes.
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.Rotate((seed * 37 + (1f - fade) * 360f) % 360f);
                float s = cell * 0.15f;
                canvas.FillRectangle(-s / 2f, -s / 2f, s, s);
                canvas.RestoreState();
                break;
            }

            case BurstStyle.Frost:
            {
                // A tiny snowflake: three crossing lines.
                float r = cell * 0.13f * (0.4f + 0.6f * fade);
                canvas.StrokeSize = MathF.Max(1f, cell * 0.04f);
                for (int k = 0; k < 3; k++)
                {
                    float a = k * MathF.PI / 3f + seed * 0.3f;
                    canvas.DrawLine(x - MathF.Cos(a) * r, y - MathF.Sin(a) * r, x + MathF.Cos(a) * r, y + MathF.Sin(a) * r);
                }
                break;
            }

            case BurstStyle.Starburst:
            {
                // A four-point star: a long cross and a short one.
                float r = cell * 0.17f * (0.4f + 0.6f * fade);
                canvas.StrokeSize = MathF.Max(1f, cell * 0.05f);
                canvas.DrawLine(x - r, y, x + r, y);
                canvas.DrawLine(x, y - r, x, y + r);
                float q = r * 0.55f;
                canvas.DrawLine(x - q, y - q, x + q, y + q);
                canvas.DrawLine(x - q, y + q, x + q, y - q);
                break;
            }

            default:
            {
                if (flair >= 2)
                {
                    float tail = cell * 0.30f * fade;
                    canvas.StrokeSize = MathF.Max(1f, cell * 0.06f);
                    canvas.DrawLine(x, y, x - cos * tail, y - sin * tail);
                }
                else
                {
                    float r = cell * 0.07f * fade;
                    canvas.FillEllipse(x - r, y - r, r * 2f, r * 2f);
                }
                break;
            }
        }
    }

    private static readonly IReadOnlyList<BlockCell> NoCells = Array.Empty<BlockCell>();

    private static void DrawBurst(ICanvas canvas, Burst burst, float originX, float originY, float cell, bool flip, long age, float boardWidth)
    {
        float t = MathF.Min(1f, age / (float)VisualDesign.BurstDurationMs);   // the core burst
        float fade = 1f - t;
        float inset = cell * VisualDesign.CellInset;
        int flair = burst.Flair;
        var wave = burst.Wave;

        // Petals and shards are whole particle systems of their own and replace the neon swell and the sparks.
        bool particles = burst.Style is BurstStyle.Petals or BurstStyle.Shards;
        if (particles) BurstParticles.Draw(canvas, burst.Style, wave, burst.Reduced, originX, originY, cell, flip, age);

        int index = 0;
        foreach (var c in particles ? NoCells : wave.ClearedCells)
        {
            index++;
            if (BoardSpace.AboveView(c.Y)) continue;

            float px = BoardSpace.ScreenX(c.X, flip, originX, cell);
            float py = BoardSpace.ScreenY(c.Y, flip, originY, cell);
            float cx = px + cell / 2f;
            float cy = py + cell / 2f;
            var neon = VisualDesign.NeonColour(c.Colour);

            if (t < 1f)
            {
                // Solid neon block that swells for a moment, then shrinks away.
                float swell = t < 0.18f ? 1f + 0.35f * (t / 0.18f) : 1.35f - 1.1f * ((t - 0.18f) / 0.82f);
                float size = MathF.Max(0f, (cell - 2f * inset) * swell);
                canvas.Alpha = MathF.Min(1f, fade * 1.6f);
                canvas.FillColor = neon;
                canvas.FillRoundedRectangle(cx - size / 2f, cy - size / 2f, size, size, size * VisualDesign.CornerRadius);

                // Hot white core.
                if (t < 0.55f)
                {
                    float core = size * 0.55f * (1f - t / 0.55f);
                    canvas.Alpha = 1f - t / 0.55f;
                    canvas.FillColor = Colors.White;
                    canvas.FillEllipse(cx - core / 2f, cy - core / 2f, core, core);
                }

                // Expanding ring.
                float ring = cell * (0.35f + 0.95f * t);
                canvas.Alpha = fade;
                canvas.StrokeColor = neon;
                canvas.StrokeSize = MathF.Max(1f, cell * 0.10f * fade);
                canvas.DrawEllipse(cx - ring, cy - ring, ring * 2f, ring * 2f);
            }

            // Sparks flying outwards: 4 plain dots, or 8 streaks from Flair 2. Reduced motion keeps the swell and
            // the fade of the block itself but sends nothing flying.
            int sparks = burst.Reduced ? 0 : flair >= 2 ? 8 : 4;
            float spread = cell * (flair >= 2 ? 1.5f : 1.0f);
            for (int i = 0; i < sparks; i++)
            {
                float angle = (i / (float)sparks) * MathF.PI * 2f + BoardSpace.Noise(index, i) * 0.7f;
                float speed = 0.65f + BoardSpace.Noise(i, index) * 0.55f;
                float travel = spread * speed * (1f - fade * fade);
                float sx = cx + MathF.Cos(angle) * travel;
                float sy = cy + MathF.Sin(angle) * travel;
                var sparkColour = i % 2 == 0 ? neon : Colors.White;
                canvas.Alpha = fade;
                canvas.FillColor = sparkColour;
                canvas.StrokeColor = sparkColour;
                DrawSpark(canvas, burst.Style, flair, sx, sy, angle, cell, fade, index + i);
            }
        }

        // Shockwave across the board from the middle of the pop.
        if (flair >= 1 && t < 1f)
        {
            float cx = BoardSpace.ScreenX((int)MathF.Round(burst.CentreX), flip, originX, cell) + cell / 2f;
            float cy = BoardSpace.ScreenY((int)MathF.Round(burst.CentreY), flip, originY, cell) + cell / 2f;
            float radius = boardWidth * 0.85f * (1f - fade * fade);
            canvas.Alpha = fade * 0.55f;
            canvas.StrokeColor = VisualDesign.NeonColour(burst.Dominant);
            canvas.StrokeSize = MathF.Max(2f, cell * 0.16f * fade);
            canvas.DrawEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
        }

        // Confetti shower (Flair 3).
        if (flair >= 3)
        {
            float life = age / (float)burst.DurationMs;
            int pieces = Math.Min(40, wave.ClearedCells.Count * 3);
            for (int i = 0; i < pieces; i++)
            {
                var source = wave.ClearedCells[i % wave.ClearedCells.Count];
                if (BoardSpace.AboveView(source.Y)) continue;

                float ox = BoardSpace.ScreenX(source.X, flip, originX, cell) + cell / 2f;
                float oy = BoardSpace.ScreenY(source.Y, flip, originY, cell) + cell / 2f;
                float angle = -MathF.PI / 2f + (BoardSpace.Noise(i, 3) - 0.5f) * 2.4f;
                float speed = cell * (2.2f + BoardSpace.Noise(i, 7) * 2.6f);
                float tt = life * 1.1f;
                float x = ox + MathF.Cos(angle) * speed * tt;
                float gravity = cell * 7f * tt * tt * (flip ? -1f : 1f);
                float y = oy + MathF.Sin(angle) * speed * tt * (flip ? -1f : 1f) + gravity;

                var colour = VisualDesign.NeonColour(i % 3 == 0 ? burst.Dominant : source.Colour);
                float w = cell * 0.18f, h = cell * 0.10f;
                canvas.SaveState();
                canvas.Alpha = MathF.Max(0f, 1f - life * life);
                canvas.FillColor = colour;
                canvas.Translate(x, y);
                canvas.Rotate((i * 47 + age * 0.5f * (1 + i % 3)) % 360f);
                canvas.FillRectangle(-w / 2f, -h / 2f, w, h);
                canvas.RestoreState();
            }
        }

        // Score tag rising from the pop.
        if (wave.Points > 0 && burst.CentreY >= BoardSpace.ViewTop - 1)
        {
            float life = age / (float)burst.DurationMs;
            float alpha = life < 0.12f ? life / 0.12f : MathF.Max(0f, 1f - (life - 0.12f) / 0.88f);
            float rise = cell * 1.1f * life * (flip ? -1f : 1f);
            float cx = BoardSpace.ScreenX((int)MathF.Round(burst.CentreX), flip, originX, cell) + cell / 2f;
            float cy = BoardSpace.ScreenY((int)MathF.Round(burst.CentreY), flip, originY, cell) + cell / 2f;

            canvas.Alpha = alpha;
            canvas.FontColor = Colors.White;
            canvas.FontSize = cell * (0.55f + 0.1f * flair);
            canvas.DrawString($"+{wave.Points:N0}", cx - cell * 2f, cy - cell * 0.4f - rise, cell * 4f, cell * 0.8f,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        canvas.Alpha = 1f;
    }

    private void DrawChainAccent(ICanvas canvas, float originX, float originY, float cell, float boardWidth, bool flip, float t)
    {
        float alpha = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
        float rise = cell * 1.2f * t;
        float baseY = originY + cell * 5f;
        float y = flip ? baseY + rise : baseY - rise;

        canvas.Alpha = MathF.Max(0f, alpha);
        canvas.FontColor = VisualDesign.Yellow;
        canvas.FontSize = cell * 0.95f;
        canvas.DrawString(_chainText, originX, y, boardWidth, cell * 1.3f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.Alpha = 1f;
    }
}
