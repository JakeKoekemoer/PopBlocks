using System.Globalization;
using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;

namespace PopBlocks.Graphics;

/// <summary>
/// The gold a popped group paid, floating up from where it popped: "+0.6" with a small coin, and under it "×3 CHAIN" when
/// the pop was part of a chain. A fixed pool of twelve slots, each holding its text already formatted, so nothing is
/// allocated while drawing. Reduced motion has the number fade in place instead of rising.
/// </summary>
public sealed class FloatingText
{
    public const int Slots = 12;
    public const int LifeMs = 900;
    public const int ReducedLifeMs = 600;

    private const float RiseDp = 36f;
    private const float PopInMs = 120f;
    private const float HoldMs = 500f;

    private static readonly Color Fill = Color.FromArgb("#FFD54A");
    private static readonly Color Outline = Color.FromArgb("#3A2A00");
    private static readonly Color ChainFill = Color.FromArgb("#FF7A45");

    private struct Slot
    {
        public bool Alive;
        public long StartMs;
        public float CellX, CellY;     // board cells, hidden rows included
        public string Text;
        public string? Second;         // "×3 CHAIN"
    }

    private readonly Slot[] _slots = new Slot[Slots];
    private int _next;

    public bool ReducedMotion { get; set; }

    /// <summary>Gold is shown with one decimal under 10 ("+0.6") and as a whole number from 10.</summary>
    public static string FormatGold(int tenths) =>
        tenths >= 100 ? (tenths / 10).ToString(CultureInfo.InvariantCulture)
        : (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>The label for a pop: "+0.6".</summary>
    public static string Label(int tenths) => "+" + FormatGold(tenths);

    /// <summary>Floats a number for every group in the wave that paid something. The oldest slot is reused when all twelve are alive.</summary>
    public void Spawn(BurstWave wave, long nowMs)
    {
        var gold = wave.Gold;
        for (int i = 0; i < gold.Count; i++)
        {
            if (gold[i].GoldTenths <= 0) continue;
            string? second = gold[i].Multiplier > 1 ? "×" + gold[i].Multiplier.ToString(CultureInfo.InvariantCulture) + " CHAIN" : null;
            Spawn(gold[i].CentreX, gold[i].CentreY, Label(gold[i].GoldTenths), second, nowMs);
        }
    }

    /// <summary>Floats any short text (used for the orb bonus).</summary>
    public void Spawn(float cellX, float cellY, string text, string? second, long nowMs)
    {
        int slot = _next;
        for (int i = 0; i < Slots; i++)
        {
            int candidate = (_next + i) % Slots;
            if (!_slots[candidate].Alive) { slot = candidate; break; }
        }
        _next = (slot + 1) % Slots;
        _slots[slot] = new Slot { Alive = true, StartMs = nowMs, CellX = cellX, CellY = cellY, Text = text, Second = second };
    }

    public bool IsActive(long nowMs)
    {
        int life = ReducedMotion ? ReducedLifeMs : LifeMs;
        for (int i = 0; i < Slots; i++)
            if (_slots[i].Alive && nowMs - _slots[i].StartMs < life) return true;
        return false;
    }

    public void Clear()
    {
        for (int i = 0; i < Slots; i++) _slots[i].Alive = false;
    }

    public void Draw(ICanvas canvas, float originX, float originY, float cell, bool flip, long nowMs)
    {
        int life = ReducedMotion ? ReducedLifeMs : LifeMs;
        for (int i = 0; i < Slots; i++)
        {
            ref var s = ref _slots[i];
            if (!s.Alive) continue;
            long age = nowMs - s.StartMs;
            if (age < 0) continue;
            if (age >= life) { s.Alive = false; continue; }

            float alpha, rise, scale;
            if (ReducedMotion)
            {
                alpha = age < 100 ? age / 100f : age < 400 ? 1f : 1f - (age - 400) / 200f;
                rise = 0f;
                scale = 1f;
            }
            else
            {
                float t = age / (float)LifeMs;
                float eased = 1f - (1f - t) * (1f - t);
                rise = RiseDp * eased;
                scale = age < PopInMs ? 0.8f + 0.2f * (age / PopInMs) : 1f;
                alpha = age <= HoldMs ? 1f : 1f - (age - HoldMs) / (LifeMs - HoldMs);
            }

            // The group's centre in screen space (the board may be drawn upside down), then up by the rise.
            float row = s.CellY - BoardSpace.ViewTop;
            float x = originX + (flip ? GameConfig.Columns - s.CellX : s.CellX) * cell;
            float y = originY + (flip ? GameConfig.VisibleRows - row : row) * cell;
            y -= flip ? -rise : rise;

            canvas.Alpha = MathF.Max(0f, MathF.Min(1f, alpha));
            DrawLine(canvas, s.Text, x, y, 15f * scale, Fill, withCoin: true);
            if (s.Second is not null)
                DrawLine(canvas, s.Second, x, y + 17f * scale, 12f * scale, ChainFill, withCoin: false);
        }
        canvas.Alpha = 1f;
    }

    private static void DrawLine(ICanvas canvas, string text, float cx, float cy, float size, Color fill, bool withCoin)
    {
        const float w = 120f;
        float h = size + 6f;
        float shift = withCoin ? 7f : 0f;   // the coin sits to the left of the number, so the pair stays centred
        float x = cx - w / 2f + shift;
        float y = cy - h / 2f;

        canvas.FontSize = size;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;

        // A 2 px outline: the text drawn eight times around, then the fill on top.
        canvas.FontColor = Outline;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    canvas.DrawString(text, x + dx * 2f, y + dy * 2f, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = fill;
        canvas.DrawString(text, x, y, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);

        if (withCoin)
        {
            // A 12 px coin just left of the number. Its distance from the centre grows with the text length.
            float half = text.Length * size * 0.30f + 8f;
            float coinX = cx + shift - half - 12f;
            canvas.FillColor = Outline;
            canvas.FillEllipse(coinX - 1f, cy - 7f, 14f, 14f);
            canvas.FillColor = Fill;
            canvas.FillEllipse(coinX, cy - 6f, 12f, 12f);
            canvas.FillColor = Color.FromArgb("#B45309");
            canvas.FillEllipse(coinX + 3f, cy - 3f, 6f, 6f);
        }
    }
}

/// <summary>
/// A handful of coins flying from where a result card's gold is to the gold chip in the HUD. Positions are in the
/// drawing view's own pixels. It draws nothing once <see cref="Progress"/> passes 1, and the page hides its view then.
/// </summary>
public sealed class CoinFlyDrawable : IDrawable
{
    public const int MaxCoins = 10;

    private static readonly Color Rim = Color.FromArgb("#B45309");
    private static readonly Color Face = Color.FromArgb("#FFD54A");

    public int Coins { get; set; } = 8;
    public PointF From { get; set; }
    public PointF To { get; set; }

    /// <summary>0 to 1 over the whole flight. Coins leave one after another, so the last one is still in the air near the end.</summary>
    public float Progress { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        int n = Math.Clamp(Coins, 0, MaxCoins);
        for (int i = 0; i < n; i++)
        {
            float delay = n <= 1 ? 0f : 0.45f * i / (n - 1);
            float t = (Progress - delay) / (1f - 0.45f);
            if (t <= 0f || t >= 1f) continue;

            float eased = t * t * (3f - 2f * t);
            // A small sideways swing that differs per coin, so they fan out instead of travelling in a line.
            float swing = (i % 2 == 0 ? 1f : -1f) * (14f + 5f * (i % 3)) * MathF.Sin(MathF.PI * eased);
            float x = From.X + (To.X - From.X) * eased + swing;
            float y = From.Y + (To.Y - From.Y) * eased - 24f * MathF.Sin(MathF.PI * eased);

            canvas.Alpha = t > 0.85f ? (1f - t) / 0.15f : 1f;
            canvas.FillColor = Rim;
            canvas.FillEllipse(x - 7f, y - 7f, 14f, 14f);
            canvas.FillColor = Face;
            canvas.FillEllipse(x - 5.5f, y - 5.5f, 11f, 11f);
        }
        canvas.Alpha = 1f;
    }
}
