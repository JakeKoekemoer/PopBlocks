using System.Diagnostics;
using System.Globalization;
using System.Text;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// The measuring tool behind the difficulty band. It does nothing in a normal test run; two environment variables switch
/// it on, so retuning the gate is "run the sweep, run the writer, commit both files". See docs/09-extending.md.
///
///   POPBLOCKS_SWEEP_CSV=path           runs the sweep and writes the CSV (POPBLOCKS_SWEEP_SEEDS = seeds per level, default 50)
///   POPBLOCKS_BAND_FROM_CSV=path       with POPBLOCKS_BAND_TO=path/to/DifficultyBand.cs, turns a CSV into the band table
/// </summary>
public class SeedSweep
{
    /// <summary>Fixed, so the sweep is repeatable. Any salt would do: the candidates are samples of "levels a player might get".</summary>
    public const int SweepSalt = 20260101;

    public const int Levels = LevelSnapshotTests.LastSnapshotLevel;

    public const string Header = "level,seed,verified,memoryOk,fullNeed,weakNeed,hardness,startBlocks,buildMs,weakMs";

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    // ------------------------------------------------------------------ the sweep

    [Fact]
    public void Run_the_sweep_when_asked()
    {
        string? path = Env("POPBLOCKS_SWEEP_CSV");
        if (path is null) return;
        int seeds = int.TryParse(Env("POPBLOCKS_SWEEP_SEEDS"), out int n) && n > 0 ? n : 50;

        var rows = new string[Levels][];
        Parallel.For(0, Levels, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            int level = i + 1;
            rows[i] = new string[seeds];
            for (int s = 0; s < seeds; s++)
                rows[i][s] = Sample(level, LevelSeed.Derive(SweepSalt, level, 0, s));
        });

        var sb = new StringBuilder(Header).Append('\n');
        foreach (var level in rows) foreach (var row in level) sb.Append(row).Append('\n');
        File.WriteAllText(path, sb.ToString());
    }

    private static string Sample(int level, LevelSeed seed)
    {
        var clock = Stopwatch.StartNew();
        var built = LevelCatalog.Build(level, seed);
        long buildMs = clock.ElapsedMilliseconds;

        bool memoryOk = !BonusPlanner.IsMemoryLevel(level) || built.Level.Bonus is not null;
        int weak = 0;
        double hardness = 0;
        long weakMs = 0;
        // "Verified" here is what the roll accepts: the bot wins, and a person could play its plan in the real engine.
        bool verified = built.Verified && built.FullNeed > 0 && LevelSolver.PlanIsPlayable(built.Level, built.Plan);
        if (verified)
        {
            clock.Restart();
            weak = LevelRoller.WeakNeed(built.Level);
            weakMs = clock.ElapsedMilliseconds;
            hardness = Math.Min(SeedingConfig.HardnessCap, weak / (double)built.FullNeed);
        }

        return string.Join(',',
            level, seed.Value, verified ? 1 : 0, memoryOk ? 1 : 0, built.FullNeed, weak,
            hardness.ToString("F4", CultureInfo.InvariantCulture), LevelRoller.StartBlocks(built.Level), buildMs, weakMs);
    }

    // ------------------------------------------------------------------ the band table

    public sealed record Row(int Level, bool Verified, bool MemoryOk, int FullNeed, int WeakNeed, double Hardness, int StartBlocks, long BuildMs, long WeakMs);

    public static List<Row> ReadCsv(string path)
    {
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (line.Length == 0) continue;
            var p = line.Split(',');
            rows.Add(new Row(int.Parse(p[0]), p[2] == "1", p[3] == "1", int.Parse(p[4]), int.Parse(p[5]),
                double.Parse(p[6], CultureInfo.InvariantCulture), int.Parse(p[7]), long.Parse(p[8]), long.Parse(p[9])));
        }
        return rows;
    }

    /// <summary>Nearest-rank percentile of a sorted list: a real value from the sample, never an in-between one.</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        int rank = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    /// <summary>Every level where something new starts, read from the rule constants rather than a list kept here.</summary>
    public static IReadOnlyCollection<int> IntroductionLevels() => new SortedSet<int>
    {
        RegionRules.ExoticShapesIntro, RegionRules.PopColourIntro, RegionRules.ClearDownIntro, RegionRules.ChainIntro,
        RegionRules.BigGroupIntro, RegionRules.DualGoalIntro, RegionRules.TripleGoalIntro,
        RegionRules.FlipLevelsInRegion6[0], RegionRules.PenaltyLevelsInRegion6[0], StoryConfig.HardLastLevel + 1,
    };

    /// <summary>The first levels of each tier after the first (the first tier has no earlier levels to compare with).</summary>
    public const int SoftStartLength = 5;
    public const int SoftStartAfterIntro = 2;
    public const int SmoothingWindow = 5;
    public const double BandLow = 0.20, BandHigh = 0.80, RailLow = 0.10, RailHigh = 0.90;

    public sealed record Band(double[] Lo, double[] Hi, bool[] Soft, (int FullLo, int FullHi, int StartLo, int StartHi)[] Rails);

    public static Band ComputeBand(IReadOnlyList<Row> rows)
    {
        var good = rows.Where(r => r.Verified && r.MemoryOk && r.FullNeed > 0).ToList();
        var byLevel = good.GroupBy(r => r.Level).ToDictionary(g => g.Key, g => g.Select(r => r.Hardness).OrderBy(h => h).ToList());

        List<double> Pool(int from, int to)
        {
            var pooled = new List<double>();
            for (int l = from; l <= to; l++) if (byLevel.TryGetValue(l, out var h)) pooled.AddRange(h);
            pooled.Sort();
            return pooled;
        }

        var lo = new double[Levels];
        var hi = new double[Levels];
        var soft = new bool[Levels];
        var intros = IntroductionLevels();

        for (int n = 1; n <= Levels; n++)
        {
            var tier = StoryConfig.TierFor(n);
            int first = Math.Max(StoryConfig.FirstLevelOf(tier), n - SmoothingWindow);
            int last = Math.Min(StoryConfig.FirstLevelOf(tier) + StoryConfig.LengthOf(tier) - 1, n + SmoothingWindow);
            if (tier == Tier.Extreme) last = Math.Min(Levels, n + SmoothingWindow);
            var window = Pool(first, Math.Min(last, Levels));
            lo[n - 1] = Percentile(window, BandLow);
            hi[n - 1] = Percentile(window, BandHigh);
        }

        // Softer starts: the top of the band is held to the median of the five levels before it.
        for (int n = 2; n <= Levels; n++)
        {
            bool tierStart = n > StoryConfig.EasyLastLevel && n - StoryConfig.FirstLevelOf(StoryConfig.TierFor(n)) < SoftStartLength;
            bool afterIntro = intros.Any(i => n >= i && n <= i + SoftStartAfterIntro);
            if (!tierStart && !afterIntro) continue;

            var previous = Pool(Math.Max(1, n - 5), n - 1);
            if (previous.Count == 0) continue;
            double median = Percentile(previous, 0.5);
            double clamped = Math.Min(hi[n - 1], median);
            hi[n - 1] = Math.Max(clamped, lo[n - 1]);   // if that would empty the band, accept up to its lower edge
            soft[n - 1] = true;
        }

        // Guard rails: the 10th to 90th percentile of the pile size and of the pieces the bot needs. A tier ramps up from
        // its first level to its last, so the rails follow the same window as the band (a whole-tier rail would shut out the
        // end of the tier). Everything outside them is an outlier for that stretch of levels.
        var byLevelRows = good.GroupBy(r => r.Level).ToDictionary(g => g.Key, g => g.ToList());
        var rails = new (int, int, int, int)[Levels];
        for (int n = 1; n <= Levels; n++)
        {
            var tier = StoryConfig.TierFor(n);
            int first = Math.Max(StoryConfig.FirstLevelOf(tier), n - SmoothingWindow);
            int last = Math.Min(Levels, n + SmoothingWindow);
            if (tier != Tier.Extreme) last = Math.Min(last, StoryConfig.FirstLevelOf(tier) + StoryConfig.LengthOf(tier) - 1);
            var near = Enumerable.Range(first, last - first + 1).Where(byLevelRows.ContainsKey).SelectMany(l => byLevelRows[l]).ToList();
            var full = near.Select(r => (double)r.FullNeed).OrderBy(x => x).ToList();
            var start = near.Select(r => (double)r.StartBlocks).OrderBy(x => x).ToList();
            rails[n - 1] = ((int)Percentile(full, RailLow), (int)Percentile(full, RailHigh),
                            (int)Percentile(start, RailLow), (int)Percentile(start, RailHigh));
        }

        return new Band(lo, hi, soft, rails);
    }

    [Fact]
    public void Write_the_band_table_when_asked()
    {
        string? csv = Env("POPBLOCKS_BAND_FROM_CSV");
        string? output = Env("POPBLOCKS_BAND_TO");
        if (csv is null || output is null) return;

        var band = ComputeBand(ReadCsv(csv));
        File.WriteAllText(output, RenderBandFile(band));
    }

    public static string RenderBandFile(Band band)
    {
        string Num(double d) => d.ToString("0.000", CultureInfo.InvariantCulture);
        string Lo(double d) => Num(Math.Floor(d * 1000) / 1000);
        string Hi(double d) => Num(Math.Ceiling(d * 1000) / 1000);

        var sb = new StringBuilder();
        sb.Append("namespace PopBlocks.Engine.Levels;\n\n");
        sb.Append("// <auto-generated>\n");
        sb.Append("// Written by SeedSweep.Write_the_band_table_when_asked (tests/PopBlocks.Tests/SeedSweep.cs) from docs/story/seed-sweep.csv.\n");
        sb.Append("// Do not edit by hand. To retune the gate, rerun the sweep and the writer: see docs/09-extending.md.\n");
        sb.Append("// </auto-generated>\n\n");
        sb.Append("/// <summary>\n");
        sb.Append("/// How hard a rolled level may be. <c>hardness = min(2, weakNeed / fullNeed)</c>: how many more pieces a bot that cannot\n");
        sb.Append("/// look ahead needs than the one that sets move limits. Each level has a band (the 20th to 80th percentile of the sweep,\n");
        sb.Append("/// smoothed over neighbouring levels of its tier, and held lower where a tier or a mechanic starts), and each level has\n");
        sb.Append("/// guard rails (a preference, not a rule) on the pile size and the solve length.\n");
        sb.Append("/// </summary>\n");
        sb.Append("public static class DifficultyBand\n{\n");
        sb.Append($"    /// <summary>The last level with its own band. Levels beyond it use this one's.</summary>\n    public const int LastLevel = {Levels};\n\n");
        sb.Append("    /// <summary>Allowance for rounding when a hardness lands exactly on an edge.</summary>\n    private const double Epsilon = 1e-9;\n\n");

        sb.Append("    // Level n is at index n - 1.\n");
        AppendArray(sb, "Low", band.Lo.Select(Lo));
        AppendArray(sb, "High", band.Hi.Select(Hi));

        sb.Append("    // Guard rails per level: pieces the bot needs (low, high) and blocks in the start pile (low, high).\n");
        sb.Append("    private static readonly (int FullLow, int FullHigh, int StartLow, int StartHigh)[] Rails =\n    {\n");
        foreach (var r in band.Rails) sb.Append($"        ({r.FullLo}, {r.FullHi}, {r.StartLo}, {r.StartHi}),\n");
        sb.Append("    };\n\n");

        sb.Append("    /// <summary>The band for a level: lowest and highest hardness accepted.</summary>\n");
        sb.Append("    public static (double Low, double High) For(int level)\n    {\n");
        sb.Append("        int i = Math.Clamp(level, 1, LastLevel) - 1;\n        return (Low[i], High[i]);\n    }\n\n");

        sb.Append("    /// <summary>How far a hardness score is outside the band for this level: 0 when inside it.</summary>\n");
        sb.Append("    public static double Gap(int level, double hardness)\n    {\n");
        sb.Append("        var (low, high) = For(level);\n");
        sb.Append("        if (hardness < low - Epsilon) return low - hardness;\n");
        sb.Append("        if (hardness > high + Epsilon) return hardness - high;\n");
        sb.Append("        return 0;\n    }\n\n");

        sb.Append("    /// <summary>False if the solve length or the pile size is an outlier for this stretch of levels.</summary>\n");
        sb.Append("    public static bool WithinGuardRails(int level, int fullNeed, int startBlocks)\n    {\n");
        sb.Append("        var r = Rails[Math.Clamp(level, 1, LastLevel) - 1];\n");
        sb.Append("        return fullNeed >= r.FullLow && fullNeed <= r.FullHigh && startBlocks >= r.StartLow && startBlocks <= r.StartHigh;\n    }\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendArray(StringBuilder sb, string name, IEnumerable<string> values)
    {
        sb.Append($"    private static readonly double[] {name} =\n    {{\n");
        var list = values.ToList();
        for (int i = 0; i < list.Count; i += 10)
            sb.Append("        ").Append(string.Join(", ", list.Skip(i).Take(10))).Append(",\n");
        sb.Append("    };\n\n");
    }

    // ------------------------------------------------------------------ checks that run on every test run

    [Fact]
    public void The_percentile_picks_real_values_from_the_sample()
    {
        var sample = new List<double> { 1.0, 1.0, 1.2, 1.5, 2.0 };
        Assert.Equal(1.0, Percentile(sample, 0.20));
        Assert.Equal(1.5, Percentile(sample, 0.80));
        Assert.Equal(1.2, Percentile(sample, 0.50));
        Assert.Equal(2.0, Percentile(sample, 1.0));
        Assert.Equal(0, Percentile(new List<double>(), 0.5));
    }

    [Fact]
    public void The_introduction_levels_are_read_from_the_rule_constants()
    {
        var intros = IntroductionLevels();
        foreach (int expected in new[] { 11, 13, 16, 20, 26, 32, 128, 134, 151, 161 })
            Assert.Contains(expected, intros);
    }

    [Fact]
    public void The_band_computation_holds_the_top_down_at_a_soft_start_and_never_empties_a_band()
    {
        // A fabricated sweep: hardness rises with the level, so the "median of the previous five" is below the band's top.
        var rows = new List<Row>();
        for (int level = 1; level <= Levels; level++)
            for (int s = 0; s < 20; s++)
                rows.Add(new Row(level, true, true, 10, 10, 1.0 + (level / 220.0) + (s % 5) * 0.1, 30, 1, 1));

        var band = ComputeBand(rows);

        for (int n = 1; n <= Levels; n++) Assert.True(band.Lo[n - 1] <= band.Hi[n - 1], $"level {n}");

        // Tier starts and the levels where something new begins are soft; an ordinary level in the middle is not.
        foreach (int soft in new[] { 51, 55, 101, 105, 151, 155, 12, 13, 15, 16, 18, 20, 22, 128, 130, 134, 136, 161, 163 })
            Assert.True(band.Soft[soft - 1], $"level {soft} should be a soft start");
        foreach (int ordinary in new[] { 45, 70, 120, 180 })
            Assert.False(band.Soft[ordinary - 1], $"level {ordinary} should not be a soft start");

        // A soft start is held down to what the levels before it looked like, so it is not harder than an ordinary level.
        Assert.True(band.Hi[51 - 1] <= band.Hi[60 - 1]);
        Assert.True(band.Hi[101 - 1] <= band.Hi[110 - 1]);
    }

    [Fact]
    public void The_guard_rails_follow_the_stretch_of_levels_they_belong_to()
    {
        var rows = new List<Row>();
        for (int level = 1; level <= Levels; level++)
            for (int s = 0; s < 20; s++)
                rows.Add(new Row(level, true, true, 4 + level / 4 + s % 3, 4, 1.0, 20 + level / 5 + s % 4, 1, 1));

        var band = ComputeBand(rows);

        Assert.Equal(Levels, band.Rails.Length);
        Assert.True(band.Rails[200 - 1].FullLo > band.Rails[10 - 1].FullHi, "a late level's rail must not be the early levels' rail");
        for (int n = 1; n <= Levels; n++)
        {
            var r = band.Rails[n - 1];
            Assert.True(r.FullLo <= r.FullHi && r.StartLo <= r.StartHi, $"level {n}");
        }
    }
}
