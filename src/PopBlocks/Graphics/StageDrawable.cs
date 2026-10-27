using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Story;

namespace PopBlocks.Graphics;

/// <summary>
/// The picture above a dialogue panel. It shows the region's scene (the existing Faded / Recovering / Restored pictures),
/// a quiet ambience, and whatever the current line asks for (<see cref="StageCue"/>): Pip coming out from behind a stone,
/// the Warden's mark being drawn, a restoration sweep, a Prism shard, light flowing in or out, a memory crystal, the Warden.
/// It only reads <see cref="Info"/>; it never decides story. One-shot animations play from the line where a cue begins and
/// are finished on later lines. Under Reduced motion every picture is its final still frame and nothing loops.
/// </summary>
public sealed class StageDrawable : IDrawable
{
    private readonly RegionSceneDrawable _from = new();
    private readonly RegionSceneDrawable _to = new();
    private readonly PipDrawable _pip = new();
    private readonly WardenDrawable _warden = new();
    private readonly WardenMarkDrawable _mark = new();

    private StageInfo? _info;
    private long _cueStartMs;
    private long _epochMs;
    private long _lastMs;
    private float _ambient;

    /// <summary>Milliseconds. Replace it to render a specific moment deterministically.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    public bool ReducedMotion { get; set; }

    /// <summary>How bright Pip is drawn (the world's restoration), for the scene where Pip comes out from behind the stone.</summary>
    public float PipBrightness { get; set; } = 0.35f;

    /// <summary>Who is speaking now. A recording speaking makes the memory crystal pulse and send out ripples.</summary>
    public string SpeakerId { get; set; } = string.Empty;

    /// <summary>
    /// What to show. Setting it restarts the one-shot animations when the scene or the cue changes, and keeps them
    /// running when the player merely moves to the next line under the same cue.
    /// </summary>
    public StageInfo? Info
    {
        get => _info;
        set
        {
            var old = _info;
            _info = value;
            if (value is null)
            {
                _lastMs = 0;
                return;
            }

            long now = Clock();
            if (old is null || old.SceneId != value.SceneId)
            {
                _epochMs = now;
                _ambient = 0f;
                _lastMs = 0;
            }
            if (old is null || old.SceneId != value.SceneId || old.CueStartLine != value.CueStartLine || old.Cue != value.Cue)
                _cueStartMs = now;
        }
    }

    /// <summary>True while something on the stage is moving and needs repainting.</summary>
    public bool NeedsAnimation => _info is not null && !ReducedMotion;

    public void Draw(ICanvas canvas, RectF r)
    {
        var info = _info;
        if (info is null || r.Width < 40f || r.Height < 40f) return;

        long now = Clock();
        bool still = info.Has(StageCue.Stillness);
        float t = ReducedMotion ? -1f : (now - _epochMs) / 1000f;

        // One-shot animations: finished on any line after the one the cue began on, and under Reduced motion.
        bool settled = ReducedMotion || info.Line > info.CueStartLine;
        float cueT = settled ? 99f : MathF.Max(0f, (now - _cueStartMs) / 1000f);

        // The ambient clock slows right down for a held pause, so the world seems to hold its breath.
        if (!ReducedMotion)
        {
            float dt = _lastMs == 0 ? 0f : MathF.Min(0.1f, (now - _lastMs) / 1000f);
            _ambient += dt * (still ? 0.08f : 1f);
        }
        _lastMs = now;

        canvas.SaveState();
        var clip = new PathF();
        clip.AppendRoundedRectangle(r.X, r.Y, r.Width, r.Height, 14f);
        canvas.ClipPath(clip);

        DrawBackdrop(canvas, r, info, cueT);

        if (!ReducedMotion) Ambience.Draw(canvas, r, info.RegionNumber, _ambient, still ? 0.4f : 1f);

        if (still)
        {
            canvas.FillColor = Colors.Black.WithAlpha(0.28f * StageMath.Smooth(cueT / 1.2f));
            canvas.FillRectangle(r.X, r.Y, r.Width, r.Height);
        }

        DrawProps(canvas, r, info, cueT, t);

        canvas.RestoreState();

        canvas.StrokeColor = Colors.White.WithAlpha(0.14f);
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f, 14f);
    }

    // ---------------------------------------------------------------- backdrop and the restoration sweep

    private void DrawBackdrop(ICanvas canvas, RectF r, StageInfo info, float cueT)
    {
        var region = RegionCatalog.Get(Math.Clamp(info.RegionNumber, 1, RegionCatalog.All.Count));
        _from.Region = region;
        _to.Region = region;
        _from.Locked = false;
        _to.Locked = false;

        _from.State = info.World;
        _from.Draw(canvas, r);

        if (!info.Has(StageCue.RestoreSweep)) return;

        // The restored picture is revealed from the left by a soft wave of colour.
        float p = StageMath.Smooth(cueT / 2.4f);
        if (p <= 0f) return;

        float soft = r.Width * 0.12f;
        float front = r.X + (r.Width + soft) * p;

        canvas.SaveState();
        canvas.ClipRectangle(r.X, r.Y, MathF.Min(r.Width, front - r.X), r.Height);
        _to.State = info.SweepTo;
        _to.Draw(canvas, r);
        canvas.RestoreState();

        if (p < 1f) DrawWaveFront(canvas, r, front, soft);
    }

    private static void DrawWaveFront(ICanvas canvas, RectF r, float front, float soft)
    {
        const int strips = 6;
        float w = soft / strips;
        for (int k = 0; k < strips; k++)
        {
            canvas.FillColor = StoryPalette.Cyan.WithAlpha(0.20f * (1f - k / (float)strips));
            canvas.FillRectangle(front - (k + 1) * w, r.Y, w + 1f, r.Height);
        }

        canvas.StrokeColor = Colors.White.WithAlpha(0.55f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(front, r.Y, front, r.Bottom);

        // A few lights catching as the wave goes past.
        float sc = r.Height / 150f;
        for (int k = 0; k < 6; k++)
        {
            float x = front - soft * StageMath.Hash(k * 3 + 2);
            float y = r.Y + r.Height * (0.15f + 0.7f * StageMath.Hash(k * 3 + 1));
            float rad = 2.2f * sc;
            canvas.FillColor = Colors.White.WithAlpha(0.75f);
            canvas.FillEllipse(x - rad, y - rad, rad * 2f, rad * 2f);
        }
    }

    // ---------------------------------------------------------------- the cues

    private void DrawProps(ICanvas canvas, RectF r, StageInfo info, float cueT, float t)
    {
        float h = r.Height;
        float cx = r.X + r.Width / 2f;
        float cy = r.Y + h / 2f;
        bool warden = info.Has(StageCue.WardenEntrance) || info.Has(StageCue.WardenStand);

        // Light flow sits behind everything else. With the Warden on stage the centre moves left, to leave room.
        if (info.Has(StageCue.FlowInward) || info.Has(StageCue.FlowOutward))
            LightFlow.Draw(canvas, r, info.Has(StageCue.FlowInward), warden ? 0.30f : 0.5f, 0.5f, t, StageMath.Smooth(cueT / 2.5f));

        if (info.Has(StageCue.PipHidden) || info.Has(StageCue.PipReveal))
            DrawPipAndStone(canvas, r, info, cueT, t);

        if (info.Has(StageCue.Mark))
        {
            float side = h * 0.80f;
            canvas.FillColor = Color.FromArgb("#060A18").WithAlpha(0.42f);
            canvas.FillRoundedRectangle(cx - side / 2f, cy - side / 2f, side, side, side * 0.14f);

            _mark.Colour = StoryPalette.Violet;
            _mark.Time = t;
            _mark.Progress = StageMath.Smooth(cueT / 1.1f);
            _mark.Glow = StageMath.Smooth((cueT - 1.0f) / 0.8f);
            _mark.Draw(canvas, new RectF(cx - side / 2f, cy - side / 2f, side, side));
        }

        if (info.Has(StageCue.MemoryCrystal))
        {
            float appear = StageMath.OutCubic(cueT / 0.9f);
            float size = h * 0.34f * (0.6f + 0.4f * appear);
            bool playing = StageDirections.IsRecording(SpeakerId);
            var core = SpeakerId == Cast.WardenRecording ? StoryPalette.Violet : StoryPalette.Magenta;

            // In the Frozen Expanse the memory is set into the ice.
            if (info.RegionNumber == 6) StageProps.DrawIceSlab(canvas, cx, cy, size * 1.9f, h * 0.90f);
            StageProps.DrawMemoryCrystal(canvas, cx, cy, size, t, playing, core);
        }

        if (warden)
        {
            float p = info.Has(StageCue.WardenEntrance) ? StageMath.Smooth(cueT / 2.0f) : 1f;
            float figW = h * 0.62f;
            float x = r.X + r.Width * 0.74f + (1f - p) * r.Width * 0.05f;

            _warden.Reveal = p;
            _warden.Time = t;
            _warden.Still = info.Has(StageCue.Stillness);
            _warden.Bust = false;
            _warden.Draw(canvas, new RectF(x - figW / 2f, r.Y + h * 0.06f, figW, h * 0.92f));
        }

        if (info.Has(StageCue.Shard) || info.Has(StageCue.ShardPair))
            DrawShards(canvas, r, info, cueT, t);
    }

    /// <summary>
    /// Pip waits behind the cracked stone (only a glow shows), then drifts out and up to float beside it.
    /// Pip is drawn first and the stone over it, so Pip really does come out from behind.
    /// </summary>
    private void DrawPipAndStone(ICanvas canvas, RectF r, StageInfo info, float cueT, float t)
    {
        float h = r.Height;
        float stoneX = r.X + r.Width * 0.34f;
        float ground = r.Y + h * 0.88f;
        float p = info.Has(StageCue.PipReveal) ? StageMath.Smooth(cueT / 2.2f) : 0f;

        float size = h * 0.52f;
        float px = stoneX + p * r.Width * 0.24f;
        float py = ground - h * 0.22f - p * h * 0.16f;

        _pip.Brightness = PipBrightness;
        _pip.Time = t;
        _pip.Mood = p > 0.75f ? PipMood.Happy : PipMood.Curious;
        _pip.Look = p < 0.75f ? 0.6f : 0f;
        _pip.Draw(canvas, new RectF(px - size / 2f, py - size / 2f, size, size));

        StageProps.DrawCrackedStone(canvas, stoneX, ground, h * 0.52f, h * 0.50f);
    }

    /// <summary>
    /// One shard, or two. Each rises after the sweep has passed and then pulses. Two shards are given the same pulse,
    /// so the second answers the first exactly, as the story says ("the fragment responds to the first").
    /// </summary>
    private static void DrawShards(ICanvas canvas, RectF r, StageInfo info, float cueT, float t)
    {
        float rise = StageMath.OutCubic((cueT - 1.4f) / 1.2f);
        if (rise <= 0.001f) return;

        float h = r.Height;
        float cx = r.X + r.Width / 2f;
        float pulse = t >= 0f ? 0.5f + 0.5f * MathF.Sin(t * MathF.PI * 2f / 2.0f) : 0.6f;
        float size = h * 0.19f * (0.7f + 0.3f * rise);
        float bob = t >= 0f ? MathF.Sin(t * 1.6f) * h * 0.015f : 0f;
        float y = StageMath.Lerp(r.Bottom - h * 0.12f, r.Y + h * 0.42f, rise) + bob;

        if (info.Has(StageCue.ShardPair))
        {
            float dx = r.Width * 0.12f;
            DrawShardLayers(canvas, cx - dx, y, size, StoryPalette.Lime, pulse, -8f);
            DrawShardLayers(canvas, cx + dx, y, size, StoryPalette.Magenta, pulse, 8f);
        }
        else
        {
            DrawShardLayers(canvas, cx, y, size, ShardColour(info.RegionNumber), pulse, 0f);
        }
    }

    /// <summary>The glow and the body are separate passes, as the brief asks, so the glow can be dropped on its own.</summary>
    private static void DrawShardLayers(ICanvas canvas, float x, float y, float size, Color colour, float pulse, float tilt)
    {
        StageProps.DrawShard(canvas, x, y, size, colour, pulse, glow: true, body: false, tilt: tilt);
        StageProps.DrawShard(canvas, x, y, size, colour, pulse, glow: false, body: true, tilt: tilt);
    }

    private static Color ShardColour(int region) => region switch
    {
        1 => StoryPalette.Lime,
        2 => StoryPalette.Magenta,
        3 => StoryPalette.Lime,
        4 => StoryPalette.Cyan,
        5 => StoryPalette.Amber,
        6 => StoryPalette.Cyan,
        7 => StoryPalette.Violet,
        _ => StoryPalette.Magenta,
    };
}

/// <summary>
/// The small portrait beside a dialogue line. Picks the picture by who is speaking, following the brief: Pip is Pip,
/// the Warden is the Warden's silhouette, and recordings are never a character, a memory crystal for a recorded voice
/// and the Warden's mark for the Warden's message. Mara and Bram stay a tinted stand-in until their designs are made.
/// </summary>
public sealed class PortraitDrawable : IDrawable
{
    private readonly WardenDrawable _warden = new() { Bust = true };
    private readonly WardenMarkDrawable _mark = new() { Progress = 1f, Glow = 0.7f };
    private long _epochMs;

    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    public string SpeakerId { get; set; } = string.Empty;

    /// <summary>False draws a still picture (Reduced motion, or no scene open).</summary>
    public bool Animated { get; set; }

    /// <summary>Pip's drawing, which the page configures (brightness, accessory, lights, mood, tint for stand-ins).</summary>
    public PipDrawable Pip { get; } = new();

    public void Draw(ICanvas canvas, RectF r)
    {
        if (_epochMs == 0) _epochMs = Clock();
        float t = Animated ? ((Clock() - _epochMs) % 3_600_000L) / 1000f : -1f;

        switch (SpeakerId)
        {
            case Cast.Warden:
                _warden.Time = t;
                _warden.Draw(canvas, r);
                break;

            case Cast.Voice:
            case Cast.WardenRecording:
            {
                float size = MathF.Min(r.Width, r.Height) * 0.34f;
                var core = SpeakerId == Cast.WardenRecording ? StoryPalette.Violet : StoryPalette.Magenta;
                StageProps.DrawMemoryCrystal(canvas, r.X + r.Width / 2f, r.Y + r.Height / 2f, size, t, true, core);
                break;
            }

            case Cast.WardenMessage:
                _mark.Time = t;
                _mark.Draw(canvas, r);
                break;

            default:
                Pip.Time = t;
                Pip.Draw(canvas, r);
                break;
        }
    }
}
