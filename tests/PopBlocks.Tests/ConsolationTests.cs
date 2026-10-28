using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Losing a Story level pays half the pop gold plus part of the first-clear reward, once and never above it.</summary>
public class ConsolationTests
{
    private static (Consolation C, PlayerProgress P, InMemorySettings S) Make(int highestCleared = 59)
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        progress.DevSetHighestCleared(highestCleared);
        return (new Consolation(settings, progress), progress, settings);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(7, 3)]
    [InlineData(20, 10)]
    public void The_pop_half_is_half_the_whole_gold_rounded_down(int runPopGold, int expected) =>
        Assert.Equal(expected, Consolation.PopPart(runPopGold));

    [Theory]
    [InlineData(33, 0.0, 0)]
    [InlineData(33, 0.5, 8)]
    [InlineData(33, 1.0, 16)]
    [InlineData(33, 1.7, 16)]    // progress is clamped to 0..1
    [InlineData(0, 0.9, 0)]      // the tutorial levels have no first-clear reward
    public void The_progress_half_is_half_the_reward_in_proportion_to_the_goals_met(int reward, double progress, int expected) =>
        Assert.Equal(expected, Consolation.ProgressPart(reward, progress));

    [Fact]
    public void A_loss_pays_both_halves_into_the_players_gold()
    {
        var (c, progress, _) = Make();   // level 60 is current and uncleared
        int reward = GoldRewards.For(60, false);
        Assert.True(reward > 0);

        var paid = c.Pay(60, runPopGold: 9, goalProgress: 0.5, levelCleared: false, eased: false);

        Assert.Equal(4, paid.PopPart);
        Assert.Equal(reward / 4, paid.ProgressPart);   // reward x 0.5 / 2
        Assert.Equal(paid.Total, progress.Gold);
    }

    [Fact]
    public void The_progress_half_can_never_pay_more_than_the_first_clear_reward_over_all_attempts()
    {
        var (c, progress, _) = Make();
        int reward = GoldRewards.For(60, false);

        int total = 0;
        for (int attempt = 0; attempt < 20; attempt++)
            total += c.Pay(60, runPopGold: 0, goalProgress: 1.0, levelCleared: false, eased: false).ProgressPart;

        Assert.Equal(reward, total);
        Assert.Equal(reward, c.ProgressPaid(60));
        Assert.Equal(reward, progress.Gold);
        Assert.Equal(0, c.Pay(60, 0, 1.0, false, false).Total);   // the cap is lifetime: nothing more, ever
    }

    [Fact]
    public void A_cleared_level_pays_only_the_pop_half()
    {
        var (c, progress, _) = Make(highestCleared: 80);
        var paid = c.Pay(60, runPopGold: 10, goalProgress: 1.0, levelCleared: true, eased: false);
        Assert.Equal(5, paid.PopPart);
        Assert.Equal(0, paid.ProgressPart);
        Assert.Equal(5, progress.Gold);
    }

    [Fact]
    public void A_second_loss_after_a_continue_pays_only_what_is_new()
    {
        var (c, progress, _) = Make();
        int reward = GoldRewards.For(60, false);

        var first = c.Pay(60, runPopGold: 4, goalProgress: 0.5, levelCleared: false, eased: false);
        var second = c.Pay(60, runPopGold: 10, goalProgress: 0.75, levelCleared: false, eased: false, alreadyPaid: first);

        Assert.Equal(5 - first.PopPart, second.PopPart);                                   // half of 10, less what was paid
        Assert.Equal(Consolation.ProgressPart(reward, 0.75) - first.ProgressPart, second.ProgressPart);
        Assert.Equal(first.Total + second.Total, progress.Gold);
        Assert.Equal(first.ProgressPart + second.ProgressPart, c.ProgressPaid(60));
        Assert.Equal(0, c.Pay(60, 10, 0.75, false, false, new ConsolationResult(first.PopPart + second.PopPart, first.ProgressPart + second.ProgressPart)).Total);
    }

    [Fact]
    public void An_eased_level_pays_no_consolation()
    {
        var (c, progress, _) = Make();
        var paid = c.Pay(60, runPopGold: 10, goalProgress: 1.0, levelCleared: false, eased: true);
        Assert.Equal(0, paid.Total);
        Assert.Equal(0, progress.Gold);
    }

    [Fact]
    public void The_opening_levels_pay_no_progress_half()
    {
        var (c, _, _) = Make(highestCleared: 4);   // level 5 is a guided level with no reward
        Assert.Equal(0, c.Pay(5, runPopGold: 0, goalProgress: 1.0, levelCleared: false, eased: false).ProgressPart);
    }

    [Fact]
    public void Preview_pays_nothing_and_the_paid_amount_is_saved()
    {
        var (c, progress, settings) = Make();
        var preview = c.Preview(60, 4, 1.0, false, false);
        Assert.True(preview.Total > 0);
        Assert.Equal(0, progress.Gold);

        c.Pay(60, 4, 1.0, false, false);
        Assert.Equal(c.ProgressPaid(60), new Consolation(settings, progress).ProgressPaid(60));
        Assert.Contains(SaveKeys.ConsolationPaid, SaveKeys.All);
        Assert.Contains(SaveKeys.ConsolationPaid, SaveCode.Keys);
    }
}
