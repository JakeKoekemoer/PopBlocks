using PopBlocks.Engine.Models;

namespace PopBlocks.Engine;

/// <summary>
/// Story-mode level bands. These are recommendations, not hard caps: every rule that depends on
/// a level number reads them from here, so the bands can be moved in one place.
/// </summary>
public static class StoryConfig
{
    /// <summary>Levels 1..this are guided tutorial levels (no gold).</summary>
    public const int TutorialLastLevel = 10;

    public const int EasyLastLevel = 50;
    public const int MediumLastLevel = 100;
    public const int HardLastLevel = 150;

    public static Tier TierFor(int level) =>
        level <= EasyLastLevel ? Tier.Easy
        : level <= MediumLastLevel ? Tier.Medium
        : level <= HardLastLevel ? Tier.Hard
        : Tier.Extreme;

    public static int FirstLevelOf(Tier tier) => tier switch
    {
        Tier.Easy => 1,
        Tier.Medium => EasyLastLevel + 1,
        Tier.Hard => MediumLastLevel + 1,
        _ => HardLastLevel + 1,
    };

    /// <summary>Number of levels in a band, or 100 for the open-ended Extreme band (used to pace its ramp).</summary>
    public static int LengthOf(Tier tier) => tier switch
    {
        Tier.Easy => EasyLastLevel,
        Tier.Medium => MediumLastLevel - EasyLastLevel,
        Tier.Hard => HardLastLevel - MediumLastLevel,
        _ => 100,
    };

    /// <summary>0 at the start of a band, 1 at its end (Extreme keeps climbing to 1 over 100 levels, then holds).</summary>
    public static double ProgressInTier(int level)
    {
        var tier = TierFor(level);
        double p = (level - FirstLevelOf(tier)) / (double)Math.Max(1, LengthOf(tier) - 1);
        return Math.Clamp(p, 0.0, 1.0);
    }

    public static string Name(Tier tier) => tier switch
    {
        Tier.Easy => "Easy",
        Tier.Medium => "Medium",
        Tier.Hard => "Hard",
        _ => "Extreme",
    };
}
