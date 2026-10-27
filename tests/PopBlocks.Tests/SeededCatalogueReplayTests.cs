using System.Collections.Concurrent;
using System.Diagnostics;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// The gate: whatever seed a player rolls, the level is one the bot can win, fair at the start, inside its difficulty band
/// (or the closest verified candidate), and never the unverified fallback. The old catalogue replay test keeps covering
/// the legacy levels; this is its seeded twin.
/// </summary>
public class SeededCatalogueReplayTests
{
    public static IEnumerable<string> Problems(int level, LevelSeed seed, LevelDefinition def)
    {
        string label = $"level {level} seed {seed.Value}";

        if (def.Seed != seed.Value) yield return label + ": level does not carry its seed";

        var board = def.CreateBoard();
        if (board.CountOccupied() == 0) yield return label + ": empty pile";
        if (!Story.IsSettled(board)) yield return label + ": pile is not settled";
        if (board.AnyInRows(0, GameConfig.HiddenRows + 1)) yield return label + ": spawn rows are not clear";
        if (ColourMatcher.FindGroups(board).Count != 0) yield return label + ": starts with a ready-made group";
        if (def.Goals.Count == 0) yield return label + ": no goals";
        if (def.MoveLimit < 6) yield return label + $": move limit {def.MoveLimit} is below 6";
        if (BonusPlanner.IsMemoryLevel(level) && def.Bonus is null) yield return label + ": memory level without an objective";

        var plan = LevelSolver.Solve(def, def.MoveLimit);
        if (!plan.Won) { yield return label + ": the bot cannot win it"; yield break; }

        var engine = new GameEngine(new ScriptedGenerator());
        engine.StartLevel(def);
        GameState? end = null;
        string? stuck = null;
        try { end = Story.Replay(engine, plan); }
        catch (InvalidOperationException e) { stuck = e.Message; }

        if (stuck is not null) yield return label + $": the bot's plan cannot be played in the real engine ({stuck})";
        else if (end != GameState.LevelComplete) yield return label + $": the bot's plan ended in {end} in the real engine";
        else if (engine.PiecesPlaced > def.MoveLimit) yield return label + ": the replay used more pieces than the limit";
    }

    private static List<string> Check(int salt, IEnumerable<(int Level, int Reroll)> cases)
    {
        var problems = new ConcurrentBag<string>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, c =>
        {
            var rolled = LevelCatalog.Roll(c.Level, salt, c.Reroll);
            foreach (var p in Problems(c.Level, rolled.Seed, rolled.Level)) problems.Add(p);

            // A level is never an unverified fallback.
            if (!LevelCatalog.Build(c.Level, rolled.Seed).Verified) problems.Add($"level {c.Level} reroll {c.Reroll}: unverified fallback");

            // In band, or nothing in the attempts did better.
            double gap = DifficultyBand.Gap(c.Level, rolled.Hardness);
            if (rolled.InBand != (gap == 0)) problems.Add($"level {c.Level} reroll {c.Reroll}: InBand flag disagrees with the band");
            if (gap > 0)
            {
                for (int attempt = 0; attempt < SeedingConfig.RollAttempts; attempt++)
                {
                    var other = LevelRoller.Evaluate(c.Level, LevelSeed.Derive(salt, c.Level, c.Reroll, attempt), attempt);
                    if (other is null) continue;
                    double otherGap = DifficultyBand.Gap(c.Level, other.Hardness);
                    // Only meaningful when the roll used every attempt; a roll that ran out of time may legitimately stop early.
                    if (otherGap + 1e-9 < gap && rolled.Attempt >= SeedingConfig.RollAttempts - 1)
                        problems.Add($"level {c.Level} reroll {c.Reroll}: attempt {attempt} was closer to the band");
                }
            }
        });
        return problems.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void Every_level_1_to_220_at_rerolls_0_to_2_passes_the_gate()
    {
        var cases = Enumerable.Range(1, 220).SelectMany(l => Enumerable.Range(0, 3).Select(r => (l, r)));
        var problems = Check(SeededLevelStructureTests.TestSalt, cases);
        Assert.True(problems.Count == 0, $"{problems.Count} problem(s):\n" + string.Join("\n", problems.Take(15)));
    }

    [Fact]
    public void A_roll_is_deterministic_for_a_salt_level_and_reroll()
    {
        foreach (int level in new[] { 5, 12, 40, 77, 126, 151, 200 })
        {
            var a = LevelCatalog.Roll(level, 99, 1);
            var b = LevelCatalog.Roll(level, 99, 1);
            Assert.Equal(a.Seed, b.Seed);
            Assert.Equal(SeededLevelStructureTests.Content(a.Level), SeededLevelStructureTests.Content(b.Level));
        }
    }

    [Fact]
    public void Different_rerolls_give_different_levels()
    {
        foreach (int level in new[] { 12, 60, 130, 180 })
        {
            var seeds = Enumerable.Range(0, 4).Select(r => LevelCatalog.Roll(level, 99, r).Seed).ToList();
            Assert.Equal(seeds.Count, seeds.Distinct().Count());
        }
    }

    [Fact]
    public void A_rolled_level_has_the_structure_of_its_level_number()
    {
        foreach (int level in new[] { 13, 20, 32, 128, 134, 151, 161, 175, 200 })
        {
            var rolled = LevelCatalog.Roll(level, 4321, 0).Level;
            Assert.Equal(SeededLevelStructureTests.Structure(LevelCatalog.Get(level)), SeededLevelStructureTests.Structure(rolled));
        }
    }

    [Fact]
    public void Memory_levels_always_carry_an_objective_when_rolled()
    {
        foreach (var region in RegionCatalog.All)
            foreach (int offset in BonusPlanner.Offsets)
            {
                int level = region.FirstLevel + offset;
                if (RegionRules.IsFinale(level)) continue;
                Assert.NotNull(LevelCatalog.Roll(level, 777, 0).Level.Bonus);
            }
    }

    [Fact]
    public void A_roll_never_returns_when_nothing_is_verified()
    {
        // The search refuses to hand out an unverified level: with no verified candidate it fails loudly instead.
        Assert.False(LevelRoller.Evaluate(12, LevelSeed.Derive(1, 12, 0, 0)) is { Level: { } l } && !LevelCatalog.Build(12, new LevelSeed(l.Seed)).Verified);
    }

    [Fact]
    public void Cancelling_a_roll_stops_it()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => LevelRoller.Roll(40, 1, 0, cts.Token));
    }

    [Fact]
    public void The_band_table_covers_every_level_and_lets_the_gate_succeed()
    {
        for (int level = 1; level <= DifficultyBand.LastLevel + 5; level++)
        {
            var (low, high) = DifficultyBand.For(level);
            Assert.InRange(low, 0.5, SeedingConfig.HardnessCap);
            Assert.True(low <= high, $"level {level}");
            Assert.Equal(0, DifficultyBand.Gap(level, (low + high) / 2));
            Assert.True(DifficultyBand.Gap(level, high + 0.25) > 0 || high >= SeedingConfig.HardnessCap);
        }
    }
}

/// <summary>The same assertions over thousands of seeds, switched on by POPBLOCKS_FUZZ because it takes minutes.</summary>
public class SeedFuzzTests
{
    [Fact]
    public void Five_thousand_seeds_per_tier_pass_the_gate_when_asked()
    {
        if (Environment.GetEnvironmentVariable("POPBLOCKS_FUZZ") is not { Length: > 0 }) return;
        int perTier = int.TryParse(Environment.GetEnvironmentVariable("POPBLOCKS_FUZZ_SEEDS"), out int n) && n > 0 ? n : 5000;

        var problems = new ConcurrentBag<string>();
        var times = new ConcurrentBag<(Tier Tier, long Ms)>();
        foreach (Tier tier in Enum.GetValues<Tier>())
        {
            int first = StoryConfig.FirstLevelOf(tier);
            int length = tier == Tier.Extreme ? 70 : StoryConfig.LengthOf(tier);   // levels 151..220
            Parallel.For(0, perTier, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
            {
                int level = first + (int)((uint)SeedMix.Mix32(i, (int)tier) % (uint)length);
                int salt = SeedMix.Mix32(i, 9001);
                var clock = Stopwatch.StartNew();
                var rolled = LevelCatalog.Roll(level, salt, i % 3);
                times.Add((tier, clock.ElapsedMilliseconds));
                foreach (var p in SeededCatalogueReplayTests.Problems(level, rolled.Seed, rolled.Level)) problems.Add(p);
            });
        }

        string stats = string.Join("; ", times.GroupBy(t => t.Tier).Select(g =>
        {
            var ms = g.Select(t => t.Ms).OrderBy(x => x).ToList();
            return $"{g.Key}: p50 {ms[ms.Count / 2]} ms, p95 {ms[(int)(ms.Count * 0.95)]} ms, max {ms[^1]} ms";
        }));
        string path = Environment.GetEnvironmentVariable("POPBLOCKS_FUZZ_STATS") ?? "";
        if (path.Length > 0) File.WriteAllText(path, stats + "\n");

        Assert.True(problems.IsEmpty, $"{problems.Count} problem(s); first: {problems.OrderBy(p => p).FirstOrDefault()}\n{stats}");
    }
}
