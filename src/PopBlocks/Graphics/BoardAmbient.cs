using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// The slow movement on the v1.1 boards: twinkling stars, drifting nebula clouds, ripples, rising embers and falling petals.
/// At most twelve things move, each placed by a function of the time (nothing is stored and nothing is allocated), and the
/// time is rounded to 15 steps a second, so repainting faster never makes it busier. The page runs a repaint timer for it
/// only while a game is being played: it stops when paused, after ten seconds with no input, and under Reduced motion.
/// </summary>
public static class BoardAmbient
{
    public const int MaxParticles = 12;
    public const int StepMs = 66;   // ~15 frames a second

    /// <summary>This board has movement of its own (the others are still pictures).</summary>
    public static bool Animates(BoardMotif motif) =>
        motif is BoardMotif.Stars or BoardMotif.Nebula or BoardMotif.Waves or BoardMotif.Embers or BoardMotif.Petals;

    public static void Draw(ICanvas canvas, BoardMotif motif, Color rim, float alpha, float x, float y, float w, float h, float cell, long nowMs)
    {
        float t = (nowMs / StepMs * StepMs) / 1000f;   // seconds, in 15 steps a second
        canvas.SaveState();
        canvas.ClipRectangle(x, y, w, h);
        canvas.FillColor = rim;
        canvas.StrokeColor = rim;
        canvas.StrokeLineCap = LineCap.Round;

        switch (motif)
        {
            case BoardMotif.Stars:
                for (int i = 0; i < MaxParticles; i++)
                {
                    float px = x + w * (0.05f + 0.90f * BoardSpace.Noise(i, 41));
                    float py = y + h * (0.04f + 0.92f * BoardSpace.Noise(i, 43));
                    float twinkle = 0.5f + 0.5f * MathF.Sin(t * (0.8f + BoardSpace.Noise(i, 47)) * MathF.Tau * 0.5f + i);
                    float r = cell * (0.05f + 0.07f * twinkle);
                    canvas.Alpha = alpha * (0.5f + 0.5f * twinkle) * 2.2f;
                    canvas.FillEllipse(px - r, py - r, r * 2f, r * 2f);
                }
                break;

            case BoardMotif.Nebula:
                for (int i = 0; i < 4; i++)
                {
                    float drift = (t * (0.012f + 0.006f * i) + BoardSpace.Noise(i, 53)) % 1f;
                    float cx = x + w * drift;
                    float cy = y + h * (0.18f + 0.22f * i) + MathF.Sin(t * 0.3f + i) * cell * 0.4f;
                    float rx = cell * (2.2f + 0.6f * i), ry = cell * (1.0f + 0.3f * i);
                    canvas.Alpha = alpha * 0.8f;
                    canvas.FillEllipse(cx - rx, cy - ry, rx * 2f, ry * 2f);
                    canvas.FillEllipse(cx - w - rx, cy - ry, rx * 2f, ry * 2f);   // wraps round the edge
                }
                break;

            case BoardMotif.Waves:
                canvas.StrokeSize = MathF.Max(1f, cell * 0.05f);
                for (int i = 0; i < 5; i++)
                {
                    float phase = (t * 0.25f + i * 0.2f) % 1f;
                    float radius = cell * 1.6f * phase;
                    float cx = x + w * (0.15f + 0.7f * BoardSpace.Noise(i, 59)), cy = y + h * (0.1f + 0.8f * BoardSpace.Noise(i, 61));
                    canvas.Alpha = alpha * 2f * (1f - phase);
                    canvas.DrawEllipse(cx - radius, cy - radius * 0.45f, radius * 2f, radius * 0.9f);
                }
                break;

            case BoardMotif.Embers:
                for (int i = 0; i < MaxParticles; i++)
                {
                    float rise = (t * (0.05f + 0.04f * BoardSpace.Noise(i, 67)) + BoardSpace.Noise(i, 71)) % 1f;
                    float px = x + w * (0.06f + 0.88f * BoardSpace.Noise(i, 73)) + MathF.Sin(t * 1.3f + i) * cell * 0.15f;
                    float py = y + h * (1f - rise);
                    float r = cell * (0.04f + 0.04f * BoardSpace.Noise(i, 79));
                    canvas.Alpha = alpha * 2.4f * (1f - rise);
                    canvas.FillEllipse(px - r, py - r, r * 2f, r * 2f);
                }
                break;

            case BoardMotif.Petals:
                for (int i = 0; i < 10; i++)
                {
                    float fall = (t * (0.03f + 0.03f * BoardSpace.Noise(i, 83)) + BoardSpace.Noise(i, 89)) % 1f;
                    float px = x + w * (0.05f + 0.9f * BoardSpace.Noise(i, 97)) + MathF.Sin(t * 0.9f + i * 1.7f) * cell * 0.5f;
                    float py = y + h * fall;
                    float s = cell * (0.16f + 0.10f * BoardSpace.Noise(i, 101));
                    canvas.SaveState();
                    canvas.Translate(px, py);
                    canvas.Rotate(t * 40f + i * 36f);
                    canvas.Alpha = alpha * 2.4f;
                    canvas.FillEllipse(-s, -s * 0.45f, s * 2f, s * 0.9f);
                    canvas.RestoreState();
                }
                break;
        }

        canvas.Alpha = 1f;
        canvas.RestoreState();
    }
}
