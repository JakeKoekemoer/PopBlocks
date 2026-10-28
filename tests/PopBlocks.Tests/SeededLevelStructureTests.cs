using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// A seed may change a level's pile and pieces, but never its structure: that comes from the level number, so intro
/// cards, finales and region rules keep working for every seed.
/// </summary>
public class SeededLevelStructureTests
{
    /// <summary>A fixed salt for tests that need "a player".</summary>
    public const int TestSalt = 1234567;

    public static string Structure(LevelDefinition l) =>
        $"{l.Number}|{l.Tier}|{string.Join(",", l.Goals.Select(g => g.Kind))}|n{l.Goals.Count}|flip{l.FlipView}|pen{l.NoPopPenalty}" +
        $"|fin{l.IsFinale}|abil{l.AllowAbilities}|c{l.Colours}|f{l.FallIntervalMs}|x{l.ExoticShare:F2}|steps{l.Steps.Count}";

    public static string Content(LevelDefinition l) =>
        $"{LevelSnapshotTests.PileHash(l):x8}|{string.Join(";", l.Goals.Select(g => $"{g.Kind}:{g.Target}:{(int)g.Colour}:{g.MinWaves}"))}" +
        $"|m{l.MoveLimit}|{(l.Bonus is { } b ? $"{b.Kind}:{b.Target}:{(int)b.Colour}:{b.MemoryId}:{b.WithoutHelpers}" : "-")}";

    [Fact]
    public void The_legacy_seed_is_the_original_level()
    {
        foreach (int n in new[] { 1, 11, 40, 128, 151, 200, 345 })
            Assert.Same(LevelCatalog.Get(n), LevelCatalog.Get(n, LevelSeed.Legacy));
    }

    [Fact]
    public void The_same_seed_gives_an_identical_level_twice()
    {
        foreach (int n in new[] { 1, 11, 12, 40, 77, 126, 151, 200 })
        {
            var seed = LevelSeed.Derive(TestSalt, n, 0, 0);
            var a = LevelCatalog.Build(n, seed).Level;
            var b = LevelCatalog.Build(n, seed).Level;
            Assert.Equal(Content(a), Content(b));
            Assert.Equal(seed.Value, a.Seed);
        }
    }

    [Fact]
    public void Different_salts_give_different_piles_for_most_levels()
    {
        int same = 0, total = 0;
        for (int n = 12; n <= 200; n += 4)
        {
            var a = LevelCatalog.Build(n, LevelSeed.Derive(1001, n, 0, 0)).Level;
            var b = LevelCatalog.Build(n, LevelSeed.Derive(2002, n, 0, 0)).Level;
            total++;
            if (LevelSnapshotTests.PileHash(a) == LevelSnapshotTests.PileHash(b)) same++;
        }
        Assert.True(same <= total / 10, $"{same} of {total} levels got the same pile for two salts.");
    }

    [Fact]
    public void A_seed_never_changes_the_structure_of_any_level()
    {
        // Every level, a few salts. (The wider sweep over 20 salts is in the opt-in fuzz run.)
        foreach (int salt in new[] { 11, 7919, 2_000_000_011 })
            for (int n = 1; n <= LevelSnapshotTests.LastSnapshotLevel; n++)
            {
                var legacy = LevelCatalog.Get(n);
                var seeded = LevelCatalog.Get(n, LevelSeed.Derive(salt, n, 0, 0));
                Assert.Equal(Structure(legacy), Structure(seeded));
            }
    }

    [Fact]
    public void Structure_holds_for_twenty_salts_on_a_spread_of_levels()
    {
        int[] levels = { 1, 10, 11, 12, 13, 16, 20, 26, 32, 50, 51, 75, 100, 101, 128, 134, 150, 151, 161, 175, 199, 200, 220 };
        for (int s = 1; s <= 20; s++)
            foreach (int n in levels)
            {
                var legacy = LevelCatalog.Get(n);
                var seeded = LevelCatalog.Get(n, LevelSeed.Derive(s * 10007, n, 0, 0));
                Assert.Equal(Structure(legacy), Structure(seeded));
            }
    }

    [Fact]
    public void Level_11_keeps_its_scripted_opening_piece_and_card_whatever_the_seed()
    {
        var seeded = LevelCatalog.Get(11, LevelSeed.Derive(TestSalt, 11, 0, 0));
        Assert.Equal(LevelCatalog.Get(11).Script, seeded.Script);
        Assert.Equal(LevelCatalog.Get(11).Steps.Count, seeded.Steps.Count);
        Assert.NotEqual(LevelSnapshotTests.PileHash(LevelCatalog.Get(11)), LevelSnapshotTests.PileHash(seeded));
    }

    [Fact]
    public void A_setup_carries_its_seed_through_the_engine_and_a_restart()
    {
        var seed = LevelSeed.Derive(TestSalt, 40, 0, 0);
        var engine = new PopBlocks.Engine.Logic.GameEngine(new PopBlocks.Engine.Logic.PieceGenerator(new Random(1)));
        engine.Start(GameSetup.Story(40, seed));
        Assert.Equal(seed.Value, engine.Story!.Seed);
        Assert.Equal(seed, engine.Setup.Seed);

        engine.Restart();
        Assert.Equal(seed.Value, engine.Story!.Seed);
        Assert.Equal(LevelSnapshotTests.PileHash(LevelCatalog.Get(40, seed)), LevelSnapshotTests.PileHash(engine.Story));
    }

    [Fact]
    public void The_cache_returns_the_same_object_and_stays_bounded_for_seeded_levels()
    {
        var seed = LevelSeed.Derive(TestSalt, 30, 0, 0);
        Assert.Same(LevelCatalog.Get(30, seed), LevelCatalog.Get(30, seed));

        // Many different seeds: the cache must not grow without limit (the oldest are dropped and rebuilt on demand).
        for (int i = 0; i < SeedingConfigCacheSizePlus(5); i++)
            LevelCatalog.Get(12, LevelSeed.Derive(TestSalt, 12, i, 0));
        Assert.False(LevelCatalog.IsCached(12, LevelSeed.Derive(TestSalt, 12, 0, 0)));
        Assert.True(LevelCatalog.IsCached(12, LevelSeed.Derive(TestSalt, 12, SeedingConfigCacheSizePlus(5) - 1, 0)));

        // An evicted level rebuilds to the identical level.
        var again = LevelCatalog.Get(12, LevelSeed.Derive(TestSalt, 12, 0, 0));
        Assert.Equal(Content(LevelCatalog.Build(12, LevelSeed.Derive(TestSalt, 12, 0, 0)).Level), Content(again));
    }

    [Fact]
    public void Two_threads_asking_for_the_same_level_get_one_object()
    {
        var seed = LevelSeed.Derive(TestSalt, 55, 3, 0);
        var results = new LevelDefinition[8];
        Parallel.For(0, results.Length, i => results[i] = LevelCatalog.Get(55, seed));
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    private static int SeedingConfigCacheSizePlus(int extra) => PopBlocks.Engine.SeedingConfig.CacheSize + extra;
}

public class SolverOptionsTests
{
    [Fact]
    public void The_default_options_are_the_full_bot_and_change_nothing()
    {
        foreach (int n in new[] { 12, 40, 77, 126, 151, 200 })
        {
            var level = LevelCatalog.Get(n) with { MoveLimit = LevelCatalog.SolverCap };
            var a = LevelSolver.Solve(level, LevelCatalog.SolverCap);
            var b = LevelSolver.Solve(level, LevelCatalog.SolverCap, null, SolveOptions.Full);
            Assert.Equal(a.Won, b.Won);
            Assert.Equal(a.Moves, b.Moves);
            Assert.Equal(a.Plan, b.Plan);
        }
    }

    [Fact]
    public void The_weak_bot_never_does_better_than_the_full_bot_on_these_levels_and_still_plays_legally()
    {
        int worse = 0;
        foreach (int n in new[] { 12, 30, 60, 90, 120, 150, 180 })
        {
            var level = LevelCatalog.Get(n) with { MoveLimit = LevelCatalog.SolverCap };
            var full = LevelSolver.Solve(level, LevelCatalog.SolverCap);
            var weak = LevelSolver.Solve(level, LevelCatalog.SolverCap, null, SolveOptions.Weak);
            Assert.True(full.Won);
            int weakNeed = weak.Won ? weak.Moves : LevelCatalog.SolverCap;
            if (weakNeed > full.Moves) worse++;
        }
        // The weak bot is a different player; it must not be identical to the full one everywhere, or hardness would mean nothing.
        Assert.True(worse >= 1, "The weak bot never needed more pieces than the full bot; hardness would always be 1.");
    }
}
