using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class ShareTextTests
{
    public static IEnumerable<object[]> Contexts() => new[]
    {
        new object[] { new ShareContext(ShareKind.Menu) },
        new object[] { new ShareContext(ShareKind.StoryWin, Level: 37, Region: "The Crystal Caves", MovesLeft: 4) },
        new object[] { new ShareContext(ShareKind.StoryWin, Level: 250, MovesLeft: 0) },
        new object[] { new ShareContext(ShareKind.Endless, Score: 1234567, BestChain: 6) },
    };

    [Theory, MemberData(nameof(Contexts))]
    public void Every_text_has_the_link_and_fits(ShareContext context)
    {
        string text = ShareText.Build(context);
        Assert.Contains(ShareText.Link, text);
        Assert.EndsWith(ShareText.Link, text);
        Assert.True(text.Length <= ShareText.MaxLength, $"{text.Length} characters");
    }

    [Fact]
    public void Link_is_the_store_page() =>
        Assert.Equal("https://play.google.com/store/apps/details?id=com.popblocks.game", ShareText.Link);

    [Fact]
    public void Endless_text_uses_thousands_separators()
    {
        string text = ShareText.Build(new ShareContext(ShareKind.Endless, Score: 1234567, BestChain: 6));
        Assert.StartsWith("1,234,567 points in PopBlocks Endless mode, with a best chain of 6.", text);
    }

    [Fact]
    public void Story_text_names_level_region_and_moves()
    {
        string text = ShareText.Build(new ShareContext(ShareKind.StoryWin, Level: 37, Region: "The Crystal Caves", MovesLeft: 4));
        Assert.StartsWith("Just cleared level 37 of PopBlocks in The Crystal Caves with 4 moves to spare.", text);
    }

    [Fact]
    public void Past_the_campaign_the_region_is_beyond_the_spectrum()
    {
        string text = ShareText.Build(new ShareContext(ShareKind.StoryWin, Level: 250, MovesLeft: 2));
        Assert.Contains("Beyond the Spectrum", text);
    }

    [Fact]
    public void Menu_text_invites_without_numbers()
    {
        string text = ShareText.Build(new ShareContext(ShareKind.Menu));
        Assert.StartsWith("I've been playing PopBlocks", text);
        Assert.Contains("No internet needed", text);
    }

    [Fact]
    public void End_beat_holds_longer_for_a_win_than_an_overflow_and_shortens_under_reduced_motion()
    {
        Assert.Equal(900, EndBeat.HoldMs(overflow: false, reducedMotion: false));
        Assert.Equal(600, EndBeat.HoldMs(overflow: true, reducedMotion: false));
        Assert.Equal(600, EndBeat.HoldMs(overflow: false, reducedMotion: true));
        Assert.Equal(600, EndBeat.HoldMs(overflow: true, reducedMotion: true));
    }
}
