using Microsoft.Maui.Graphics;

namespace PopBlocks.Graphics;

/// <summary>
/// The five Spectrum colours and supporting tones from the artist brief (docs/PopBlocks_Shattered_Spectrum_Artist_Brief.md,
/// section 2). They are for story pictures only: Pip, the Warden, crystals, flows and the stage strip.
/// The block colours in <see cref="VisualDesign"/> are a separate set on purpose. They are tuned for contrast and
/// colour-blind reading on the board and checked by the skin tests, so a story palette change never touches play.
/// </summary>
public static class StoryPalette
{
    public static readonly Color Cyan = Color.FromArgb("#00D4FF");      // flow, connection, discovery; Pip's glow
    public static readonly Color Magenta = Color.FromArgb("#FF4ED8");   // heart, emotion, memory
    public static readonly Color Amber = Color.FromArgb("#FFC837");     // energy, warmth, machinery
    public static readonly Color Lime = Color.FromArgb("#7CFF68");      // growth, life, renewal
    public static readonly Color Violet = Color.FromArgb("#986BFF");    // wisdom, mystery, the Warden

    public static readonly Color DeepBackground = Color.FromArgb("#081426");
    public static readonly Color PanelBackground = Color.FromArgb("#101F36");
    public static readonly Color Text = Color.FromArgb("#F4F8FF");
    public static readonly Color TextSoft = Color.FromArgb("#A8B8D0");
    public static readonly Color Faded = Color.FromArgb("#526174");

    /// <summary>The five colours in the brief's order, for streams, orbit lights and anything that cycles through them.</summary>
    public static readonly Color[] Spectrum = { Cyan, Magenta, Amber, Lime, Violet };

    /// <summary>The accent of each region (index 0 is region 1), for the menu's Story card and anything else that says "where".</summary>
    private static readonly Color[] RegionAccents =
    {
        Lime, Cyan, Color.FromArgb("#4FD6A8"), Amber, Color.FromArgb("#FF8A5C"), Violet, Magenta, Color.FromArgb("#FDE68A"),
    };

    /// <summary>The accent for a region number (1..8). Anything else gets the Heart's.</summary>
    public static Color RegionAccent(int regionNumber) =>
        RegionAccents[regionNumber >= 1 && regionNumber <= RegionAccents.Length ? regionNumber - 1 : RegionAccents.Length - 1];

    /// <summary>Dark text on a light background, white on a dark one.</summary>
    public static Color TextOn(Color background)
    {
        float luminance = 0.2126f * background.Red + 0.7152f * background.Green + 0.0722f * background.Blue;
        return luminance > 0.55f ? Color.FromArgb("#07101F") : Colors.White;
    }

    public static Color Mix(Color a, Color b, float t) => new(
        a.Red + (b.Red - a.Red) * t,
        a.Green + (b.Green - a.Green) * t,
        a.Blue + (b.Blue - a.Blue) * t,
        1f);
}

/// <summary>Small easing and hashing helpers for the story animations.</summary>
internal static class StageMath
{
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Ease in and out, 0..1.</summary>
    public static float Smooth(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Fast start, soft landing, 0..1.</summary>
    public static float OutCubic(float t)
    {
        float u = 1f - Clamp01(t);
        return 1f - u * u * u;
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Where t falls between from and to, clamped to 0..1.</summary>
    public static float Between(float t, float from, float to) => Clamp01((t - from) / (to - from));

    /// <summary>The fractional part, always 0..1, also for negative values.</summary>
    public static float Frac(float v) => v - MathF.Floor(v);

    /// <summary>A repeatable pseudo-random number in 0..1 for a whole-number seed, so particles need no stored state.</summary>
    public static float Hash(int seed)
    {
        unchecked
        {
            uint state = (uint)seed * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            word = (word >> 22) ^ word;
            return (word & 0xFFFFFFu) / 16777216f;
        }
    }
}
