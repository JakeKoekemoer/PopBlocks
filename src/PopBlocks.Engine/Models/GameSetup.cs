namespace PopBlocks.Engine.Models;

public enum GameMode
{
    /// <summary>Empty board, endless, chase a high score.</summary>
    Classic,

    /// <summary>Numbered levels with goals, a move limit and a starting pile of blocks.</summary>
    Story,
}

/// <summary>Difficulty bands of Story mode (see <see cref="StoryConfig"/> for the level numbers).</summary>
public enum Tier
{
    Easy,
    Medium,
    Hard,
    Extreme,
}

/// <summary>What the player chose to play: Classic, or a particular Story level.</summary>
public sealed record GameSetup(GameMode Mode, int Level)
{
    public static GameSetup Classic { get; } = new(GameMode.Classic, 0);

    public static GameSetup Story(int level) => new(GameMode.Story, Math.Max(1, level));

    /// <summary>Settings key of the Classic personal best. Story has progress instead of a best score.</summary>
    public string BestScoreKey => "best_score";

    public bool IsStory => Mode == GameMode.Story;

    /// <summary>Short label for the HUD.</summary>
    public string Title => Mode == GameMode.Classic ? "CLASSIC" : $"LEVEL {Level}";
}
