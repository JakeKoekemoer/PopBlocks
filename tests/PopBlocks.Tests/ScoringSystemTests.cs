using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using Xunit;

namespace PopBlocks.Tests;

public class ScoringSystemTests
{
    [Fact]
    public void Group_of_four_scores_base_points_only()
    {
        Assert.Equal(40, ScoringSystem.GroupPoints(4));
    }

    [Fact]
    public void Larger_groups_earn_a_size_bonus_per_extra_cell()
    {
        // 5 cells: 50 base + 1 extra * 10 bonus. 7 cells: 70 base + 3 * 10.
        Assert.Equal(60, ScoringSystem.GroupPoints(5));
        Assert.Equal(100, ScoringSystem.GroupPoints(7));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    public void Chain_multiplier_rises_with_each_wave(int chainIndex, int expected)
    {
        Assert.Equal(expected, ScoringSystem.Multiplier(chainIndex));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(1000)]
    public void Chain_multiplier_never_exceeds_the_maximum(int chainIndex)
    {
        Assert.Equal(GameConfig.MaxChainMultiplier, ScoringSystem.Multiplier(chainIndex));
    }

    [Fact]
    public void Chain_multiplier_is_at_least_one()
    {
        Assert.Equal(1, ScoringSystem.Multiplier(0));
        Assert.Equal(1, ScoringSystem.Multiplier(-3));
    }

    [Fact]
    public void Wave_points_sum_every_group_then_apply_the_multiplier()
    {
        // Groups of 4 and 5 -> 40 + 60 = 100 base.
        Assert.Equal(100, ScoringSystem.WavePoints(new[] { 4, 5 }, 1));
        Assert.Equal(300, ScoringSystem.WavePoints(new[] { 4, 5 }, 3));
        Assert.Equal(500, ScoringSystem.WavePoints(new[] { 4, 5 }, 9));   // capped at x5
    }

    [Fact]
    public void Level_rises_with_score()
    {
        Assert.Equal(1, ScoringSystem.LevelForScore(0));
        Assert.Equal(1, ScoringSystem.LevelForScore(GameConfig.LevelScoreStep - 1));
        Assert.Equal(2, ScoringSystem.LevelForScore(GameConfig.LevelScoreStep));
    }

    [Fact]
    public void Fall_interval_starts_at_the_initial_pace_and_speeds_up()
    {
        Assert.Equal(GameConfig.InitialFallIntervalMs, ScoringSystem.FallIntervalMs(1));
        Assert.True(ScoringSystem.FallIntervalMs(2) < ScoringSystem.FallIntervalMs(1));
        Assert.True(ScoringSystem.FallIntervalMs(5) < ScoringSystem.FallIntervalMs(4));
    }

    [Fact]
    public void Fall_interval_never_drops_below_the_cap()
    {
        Assert.Equal(GameConfig.MinFallIntervalMs, ScoringSystem.FallIntervalMs(500));
    }

    [Fact]
    public void Fall_interval_never_increases_as_levels_rise()
    {
        int previous = int.MaxValue;
        for (int level = 1; level <= 60; level++)
        {
            int interval = ScoringSystem.FallIntervalMs(level);
            Assert.True(interval <= previous);
            previous = interval;
        }
    }

    [Fact]
    public void Colour_count_grows_from_four_to_five_and_stops()
    {
        Assert.Equal(GameConfig.StartingColours, ScoringSystem.ColourCountForLevel(1));
        Assert.Equal(GameConfig.ColourFamilies, ScoringSystem.ColourCountForLevel(50));
    }
}
