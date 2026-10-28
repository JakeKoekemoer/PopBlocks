using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// The two bought pop styles that are more than a change of spark shape.
///
/// <b>Petals</b> are slow and soft: five teardrops from every popped block, lightened and pale, launched up and sideways, drifting
/// down on a sine sway while they tumble, and gone after about a second; each group also opens one faint bloom ring.
/// <b>Shards</b> are quick and hard: four to six thin triangles from every block in the block's own saturated colour with a white
/// edge, thrown outwards, pulled down hard, bouncing once off the floor of the board, gone in under half a second, after a
/// 60 ms white flash and a five-line crack star on the popped cell.
///
/// Both are stateless: a particle's place at any moment is worked out from its cell, its number and the age of the burst, so
/// nothing is stored, nothing is allocated while drawing (the two outlines are built once), and the wardrobe preview can
/// replay exactly the same motion. Reduced motion: petals fade in place, shards flash once.
/// </summary>
public static class BurstParticles
{
    public const int PetalLifeMs = 1100;
    public const int ShardLifeMs = 480;
    public const int ReducedLifeMs = 450;

    private const int PetalsPerCell = 5;
    private const float PetalGravity = 60f;      // dp/s^2
    private const float PetalDrag = 1.2f;        // 1/s
    private const float ShardGravity = 900f;     // dp/s^2
    private const float ShardBounce = 0.3f;      // share of the speed kept after the one bounce

    private static readonly Color PalePetal = Color.FromArgb("#FFE8F0");

    // Unit outlines, drawn through canvas transforms so no path is built per frame.
    private static readonly PathF PetalPath = BuildPetal();
    private static readonly PathF ShardPath = BuildShard();

    /// <summary>How long a burst in this style lasts.</summary>
    public static int LifeMs(BurstStyle style, bool reduced) =>
        reduced ? ReducedLifeMs : style == BurstStyle.Petals ? PetalLifeMs : ShardLifeMs;

    public static void Draw(ICanvas canvas, BurstStyle style, BurstWave wave, bool reduced,
        float originX, float originY, float cell, bool flip, long ageMs)
    {
        if (ageMs < 0) return;
        if (reduced) { DrawStill(canvas, style, wave, originX, originY, cell, flip, ageMs); return; }

        if (style == BurstStyle.Petals) DrawPetals(canvas, wave, originX, originY, cell, flip, ageMs);
        else DrawShards(canvas, wave, originX, originY, cell, flip, ageMs);
        canvas.Alpha = 1f;
    }

    // ------------------------------------------------------------------ petals

    private static void DrawPetals(ICanvas canvas, BurstWave wave, float originX, float originY, float cell, bool flip, long ageMs)
    {
        if (ageMs >= PetalLifeMs) return;
        float t = ageMs / 1000f;
        float alpha = ageMs < PetalLifeMs - 500 ? 1f : 1f - (ageMs - (PetalLifeMs - 500)) / 500f;
        float dir = flip ? -1f : 1f;
        float drag = (1f - MathF.Exp(-PetalDrag * t)) / PetalDrag;

        // One bloom ring per group, from the middle of the group.
        if (ageMs < 700)
        {
            float k = ageMs / 700f;
            for (int g = 0; g < wave.Gold.Count; g++)
            {
                var gg = wave.Gold[g];
                if (gg.CentreY - BoardSpace.ViewTop < 0) continue;
                float rx = originX + (flip ? GameConfig.Columns - gg.CentreX : gg.CentreX) * cell;
                float ry = originY + (flip ? GameConfig.VisibleRows - (gg.CentreY - BoardSpace.ViewTop) : gg.CentreY - BoardSpace.ViewTop) * cell;
                float radius = cell * 1.2f * k;
                canvas.Alpha = 0.35f * (1f - k);
                canvas.StrokeColor = Lighten(VisualDesign.BaseColour(gg.Colour), 0.35f);
                canvas.StrokeSize = MathF.Max(1.5f, cell * 0.08f);
                canvas.DrawEllipse(rx - radius, ry - radius, radius * 2f, radius * 2f);
            }
        }

        int index = 0;
        foreach (var c in wave.ClearedCells)
        {
            index++;
            if (BoardSpace.AboveView(c.Y)) continue;
            float cx = BoardSpace.ScreenX(c.X, flip, originX, cell) + cell / 2f;
            float cy = BoardSpace.ScreenY(c.Y, flip, originY, cell) + cell / 2f;
            var face = Lighten(VisualDesign.BaseColour(c.Colour), 0.35f);

            for (int p = 0; p < PetalsPerCell; p++)
            {
                int seed = index * 7 + p;
                float angle = -MathF.PI / 2f + (BoardSpace.Noise(seed, 1) - 0.5f) * MathF.PI * 1.1f;
                float speed = 40f + BoardSpace.Noise(seed, 2) * 50f;
                float sway = 10f * MathF.Sin(MathF.Tau * 1.4f * t + BoardSpace.Noise(seed, 3) * MathF.Tau);
                float x = cx + MathF.Cos(angle) * speed * drag + sway;
                float y = cy + (MathF.Sin(angle) * speed * drag + 0.5f * PetalGravity * t * t) * dir;
                float rotation = BoardSpace.Noise(seed, 4) * 360f + 90f * t;
                float length = 7f + BoardSpace.Noise(seed, 5) * 3f;

                canvas.Alpha = alpha;
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.Rotate(rotation);
                canvas.Scale(length, length);
                canvas.FillColor = seed % 5 == 0 ? PalePetal : face;
                canvas.FillPath(PetalPath);
                // Lighter towards the tip: a smaller petal in white on top, pushed out to the tip.
                canvas.Translate(0.38f, 0f);
                canvas.Scale(0.6f, 0.6f);
                canvas.FillColor = Colors.White.WithAlpha(0.28f);
                canvas.FillPath(PetalPath);
                canvas.RestoreState();
            }
        }
    }

    // ------------------------------------------------------------------ shards

    private static void DrawShards(ICanvas canvas, BurstWave wave, float originX, float originY, float cell, bool flip, long ageMs)
    {
        if (ageMs >= ShardLifeMs) return;
        float t = ageMs / 1000f;
        float alpha = ageMs < ShardLifeMs - 150 ? 1f : 1f - (ageMs - (ShardLifeMs - 150)) / 150f;
        float dir = flip ? -1f : 1f;
        float floor = originY + (flip ? 0f : GameConfig.VisibleRows * cell);

        int index = 0;
        foreach (var c in wave.ClearedCells)
        {
            index++;
            if (BoardSpace.AboveView(c.Y)) continue;
            float px = BoardSpace.ScreenX(c.X, flip, originX, cell);
            float py = BoardSpace.ScreenY(c.Y, flip, originY, cell);
            float cx = px + cell / 2f;
            float cy = py + cell / 2f;
            var face = VisualDesign.NeonColour(c.Colour);

            // A 60 ms white flash on the cell, and a five-line crack star for 120 ms.
            if (ageMs < 60)
            {
                canvas.Alpha = 0.9f * (1f - ageMs / 60f);
                canvas.FillColor = Colors.White;
                canvas.FillRectangle(px, py, cell, cell);
            }
            if (ageMs < 120)
            {
                canvas.Alpha = 1f - ageMs / 120f;
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 1.5f;
                for (int k = 0; k < 5; k++)
                {
                    float a = k * MathF.Tau / 5f + BoardSpace.Noise(index, 9) * 0.8f;
                    float r = cell * (0.28f + 0.2f * BoardSpace.Noise(index, k));
                    canvas.DrawLine(cx, cy, cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
                }
            }

            int count = 4 + (int)(BoardSpace.Noise(index, 11) * 3f);   // 4 to 6
            if (count > 6) count = 6;
            for (int s = 0; s < count; s++)
            {
                int seed = index * 11 + s;
                float angle = BoardSpace.Noise(seed, 1) * MathF.Tau;
                float speed = 220f + BoardSpace.Noise(seed, 2) * 160f;
                float length = 4f + BoardSpace.Noise(seed, 3) * 10f;
                float spin = (540f + BoardSpace.Noise(seed, 4) * 360f) * (s % 2 == 0 ? 1f : -1f);

                float x = cx + MathF.Cos(angle) * speed * t;
                float y = cy + (MathF.Sin(angle) * speed * t + 0.5f * ShardGravity * t * t) * dir;

                // One bounce off the floor of the board, keeping 30% of the way back up.
                if (!flip && y > floor) y = floor - (y - floor) * ShardBounce;
                else if (flip && y < floor) y = floor + (floor - y) * ShardBounce;

                float rotation = BoardSpace.Noise(seed, 5) * 360f + spin * t;
                canvas.Alpha = alpha;
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.Rotate(rotation);
                canvas.Scale(length, length * 0.38f);
                canvas.FillColor = face;
                canvas.FillPath(ShardPath);
                canvas.RestoreState();

                // The white specular edge along the long side.
                float rad = rotation * (MathF.PI / 180f);
                canvas.StrokeColor = Colors.White.WithAlpha(0.85f * alpha);
                canvas.StrokeSize = 1f;
                canvas.DrawLine(x, y, x + MathF.Cos(rad) * length, y + MathF.Sin(rad) * length);
            }
        }
    }

    // ------------------------------------------------------------------ reduced motion

    private static void DrawStill(ICanvas canvas, BurstStyle style, BurstWave wave, float originX, float originY, float cell, bool flip, long ageMs)
    {
        // Petals fade in place; shards are one flash. Nothing moves.
        int life = style == BurstStyle.Petals ? ReducedLifeMs : 60;
        if (ageMs >= life) return;
        float alpha = 1f - ageMs / (float)life;

        foreach (var c in wave.ClearedCells)
        {
            if (BoardSpace.AboveView(c.Y)) continue;
            float px = BoardSpace.ScreenX(c.X, flip, originX, cell);
            float py = BoardSpace.ScreenY(c.Y, flip, originY, cell);
            canvas.Alpha = (style == BurstStyle.Petals ? 0.7f : 0.9f) * alpha;
            canvas.FillColor = style == BurstStyle.Petals ? Lighten(VisualDesign.BaseColour(c.Colour), 0.35f) : Colors.White;
            canvas.FillRoundedRectangle(px + 2f, py + 2f, cell - 4f, cell - 4f, cell * 0.2f);
        }
        canvas.Alpha = 1f;
    }

    // ------------------------------------------------------------------ helpers

    private static Color Lighten(Color c, float t) => new(c.Red + (1f - c.Red) * t, c.Green + (1f - c.Green) * t, c.Blue + (1f - c.Blue) * t, 1f);

    /// <summary>A teardrop from its point at the origin out to its round end at x = 1: two quadratic curves.</summary>
    private static PathF BuildPetal()
    {
        var p = new PathF();
        p.MoveTo(0f, 0f);
        p.QuadTo(0.35f, -0.62f, 1f, 0f);
        p.QuadTo(0.35f, 0.62f, 0f, 0f);
        p.Close();
        return p;
    }

    /// <summary>A thin triangle: wide at the origin, a point at x = 1.</summary>
    private static PathF BuildShard()
    {
        var p = new PathF();
        p.MoveTo(0f, -0.5f);
        p.LineTo(1f, 0f);
        p.LineTo(0f, 0.5f);
        p.Close();
        return p;
    }
}
