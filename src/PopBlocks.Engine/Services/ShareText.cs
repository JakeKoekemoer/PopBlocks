using System.Globalization;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>What is being shared.</summary>
public enum ShareKind
{
    /// <summary>The main menu's SHARE button: an invitation to the game.</summary>
    Menu,

    /// <summary>A cleared Story level.</summary>
    StoryWin,

    /// <summary>A finished Endless run.</summary>
    Endless,
}

/// <summary>The numbers a share text can mention. Unused fields stay at their defaults.</summary>
public readonly record struct ShareContext(
    ShareKind Kind, int Level = 0, string Region = "", int MovesLeft = 0, int Score = 0, int BestChain = 0);

/// <summary>
/// The words that go to the system share sheet. They contain no personal data, always carry the store link, and stay
/// short enough for a chat message. The game never needs the internet to share: the phone's own share sheet does the sending.
/// </summary>
public static class ShareText
{
    public const string Link = "https://play.google.com/store/apps/details?id=com.popblocks.game";
    public const int MaxLength = 400;

    public static string Build(ShareContext context) => context.Kind switch
    {
        ShareKind.StoryWin => string.Create(CultureInfo.InvariantCulture,
            $"Just cleared level {context.Level:N0} of PopBlocks in {RegionOrBeyond(context.Region)} with {context.MovesLeft:N0} moves to spare. " +
            $"Every level is solved by the computer before you see it, so there is always a way through. Think you can find it? {Link}"),
        ShareKind.Endless => string.Create(CultureInfo.InvariantCulture,
            $"{context.Score:N0} points in PopBlocks Endless mode, with a best chain of {context.BestChain:N0}. " +
            $"Match colours, set off chains, and see how long you can last. {Link}"),
        _ => "I've been playing PopBlocks: a calm, clever falling-block puzzle where you match colours to bring a faded world back to life. " +
             "Help Pip restore eight regions, chase chains, and find the memories hidden in every level. No internet needed, just good puzzles. " +
             $"Give it a go: {Link}",
    };

    private static string RegionOrBeyond(string region) => region.Length > 0 ? region : RegionCatalog.BeyondTitle;
}

/// <summary>
/// How long the board is held after the last pop of a level or run, before the result card fades in. The move counter
/// pulses during the hold. Reduced motion shortens it and drops the pulse.
/// </summary>
public static class EndBeat
{
    public const int WinOrOutOfMovesMs = 900;
    public const int OverflowMs = 600;
    public const int ReducedMotionMs = 600;
    public const int CardFadeMs = 250;

    public static int HoldMs(bool overflow, bool reducedMotion) =>
        reducedMotion ? ReducedMotionMs : overflow ? OverflowMs : WinOrOutOfMovesMs;
}
