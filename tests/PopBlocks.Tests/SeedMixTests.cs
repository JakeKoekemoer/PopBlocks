using PopBlocks.Engine.Levels;
using Xunit;

namespace PopBlocks.Tests;

public class SeedMixTests
{
    [Fact]
    public void Mix32_has_golden_values()
    {
        // These must never change: every stored player seed was derived with this function.
        Assert.Equal(GoldenMixA, SeedMix.Mix32(1, 2, 3, 4));
        Assert.Equal(GoldenMixB, SeedMix.Mix32(0, 0, 0, 0));
        Assert.Equal(GoldenMixC, SeedMix.Mix32(123456789, 220, 2, 23));
        Assert.Equal(GoldenMixD, SeedMix.Mix32(-5, 1));
    }

    [Fact]
    public void Mix32_depends_on_the_order_and_every_part()
    {
        Assert.NotEqual(SeedMix.Mix32(1, 2, 3, 4), SeedMix.Mix32(4, 3, 2, 1));
        Assert.NotEqual(SeedMix.Mix32(1, 2, 3, 4), SeedMix.Mix32(1, 2, 3, 5));
        Assert.NotEqual(SeedMix.Mix32(7, 1), SeedMix.Mix32(7, 1, 0));
    }

    [Fact]
    public void Hash32_has_golden_values()
    {
        Assert.Equal(GoldenHashA, SeedMix.Hash32("Traveller", 0));
        Assert.Equal(GoldenHashB, SeedMix.Hash32("Traveller", 1));
        Assert.Equal(GoldenHashC, SeedMix.Hash32("", 0));
        Assert.Equal(GoldenHashD, SeedMix.Hash32("Zoë ✨", 305419896));
    }

    [Fact]
    public void Hash32_is_positive_and_changes_with_the_name_and_the_install_number()
    {
        Assert.NotEqual(SeedMix.Hash32("Ann", 5), SeedMix.Hash32("Anna", 5));
        Assert.NotEqual(SeedMix.Hash32("Ann", 5), SeedMix.Hash32("Ann", 6));
        for (int i = 0; i < 5000; i++) Assert.InRange(SeedMix.Hash32("n" + i, i * 7919), 1, int.MaxValue);
    }

    [Fact]
    public void A_derived_seed_is_positive_thirty_bits_and_never_zero()
    {
        for (int salt = 1; salt <= 40; salt++)
            for (int level = 1; level <= 220; level += 3)
                for (int reroll = 0; reroll < 3; reroll++)
                    for (int attempt = 0; attempt < 24; attempt += 5)
                    {
                        var seed = LevelSeed.Derive(salt * 104729, level, reroll, attempt);
                        Assert.InRange(seed.Value, 1, 0x3FFFFFFF);
                        Assert.False(seed.IsLegacy);
                    }
    }

    [Fact]
    public void A_derived_seed_is_stable()
    {
        Assert.Equal(LevelSeed.Derive(99, 40, 0, 0), LevelSeed.Derive(99, 40, 0, 0));
        Assert.NotEqual(LevelSeed.Derive(99, 40, 0, 0), LevelSeed.Derive(99, 40, 1, 0));
        Assert.NotEqual(LevelSeed.Derive(99, 40, 0, 0), LevelSeed.Derive(99, 41, 0, 0));
        Assert.NotEqual(LevelSeed.Derive(99, 40, 0, 0), LevelSeed.Derive(100, 40, 0, 0));
        Assert.Equal(GoldenDerive, LevelSeed.Derive(99, 40, 0, 0).Value);
    }

    [Fact]
    public void The_legacy_seed_is_zero_and_prints_as_Original()
    {
        Assert.True(LevelSeed.Legacy.IsLegacy);
        Assert.Equal(0, LevelSeed.Legacy.Value);
        Assert.Equal("Original", LevelSeed.Legacy.ToString());
        Assert.Equal("483920117", new LevelSeed(483920117).ToString());
    }

    private const int GoldenMixA = 480273864;
    private const int GoldenMixB = -638199634;
    private const int GoldenMixC = -165118352;
    private const int GoldenMixD = 2125562355;
    private const int GoldenHashA = 869989412;
    private const int GoldenHashB = 67582773;
    private const int GoldenHashC = 1268118805;
    private const int GoldenHashD = 1259764278;
    private const int GoldenDerive = 473574153;
}
