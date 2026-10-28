using System.Diagnostics;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

/// <summary>How long rolling a level took in one tier.</summary>
public sealed record TierTiming(Tier Tier, int Rolls, long P50Ms, long P95Ms, long MaxMs, int OutsideBand);

/// <summary>
/// Times the difficulty gate: how long a roll takes at each tier. The dev menu runs this on the phone itself, which is how
/// the attempt count and time budget are checked against real hardware (a roll should usually be well under a second).
/// </summary>
public static class GateTiming
{
    public static IReadOnlyList<TierTiming> Measure(int rollsPerTier, int salt = 20260101, Action<string>? progress = null, CancellationToken cancel = default)
    {
        var result = new List<TierTiming>();
        foreach (Tier tier in Enum.GetValues<Tier>())
        {
            int first = StoryConfig.FirstLevelOf(tier);
            int length = tier == Tier.Extreme ? 70 : StoryConfig.LengthOf(tier);
            var times = new List<long>();
            int outside = 0;

            for (int i = 0; i < rollsPerTier; i++)
            {
                cancel.ThrowIfCancellationRequested();
                int level = first + (int)((long)i * length / Math.Max(1, rollsPerTier));
                var clock = Stopwatch.StartNew();
                var rolled = LevelRoller.Roll(level, salt, reroll: i % 3, cancel);
                times.Add(clock.ElapsedMilliseconds);
                if (!rolled.InBand) outside++;
                progress?.Invoke($"{tier}: {i + 1} / {rollsPerTier}");
            }

            times.Sort();
            result.Add(new TierTiming(tier, times.Count, times[times.Count / 2], times[Math.Min(times.Count - 1, (int)(times.Count * 0.95))], times[^1], outside));
        }
        return result;
    }

    public static string Describe(IEnumerable<TierTiming> timings) =>
        string.Join("\n", timings.Select(t =>
            $"{StoryConfig.Name(t.Tier)}: {t.Rolls} rolls, median {t.P50Ms} ms, 95th {t.P95Ms} ms, slowest {t.MaxMs} ms, {t.OutsideBand} outside the band"));
}
