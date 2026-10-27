using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Models;

public enum GameMode
{
    /// <summary>Empty board, endless, chase a high score.</summary>
    Classic,

    /// <summary>Numbered levels with goals, a move limit and a starting pile of blocks.</summary>
    Story,

    /// <summary>The guided lessons. They are played like Story levels but sit outside the campaign: no progress, gold or story.</summary>
    Tutorial,
}

/// <summary>Difficulty bands of Story mode (see <see cref="StoryConfig"/> for the level numbers).</summary>
public enum Tier
{
    Easy,
    Medium,
    Hard,
    Extreme,
}

/// <summary>What the player chose to play: Classic, a particular Story level, or a tutorial lesson.</summary>
public sealed record GameSetup(GameMode Mode, int Level)
{
    public static GameSetup Classic { get; } = new(GameMode.Classic, 0);

    public static GameSetup Story(int level) => new(GameMode.Story, Math.Max(1, level));

    /// <summary>A Story level built from a particular seed. The legacy seed gives the original level.</summary>
    public static GameSetup Story(int level, LevelSeed seed) => new(GameMode.Story, Math.Max(1, level)) { Seed = seed };

    /// <summary>The seed a Story level is built from. Legacy unless the player's own seed is given.</summary>
    public LevelSeed Seed { get; init; }

    /// <summary>Lesson <paramref name="lesson"/> of the tutorial (1 to <see cref="StoryConfig.LessonCount"/>).</summary>
    public static GameSetup Lesson(int lesson) => new(GameMode.Tutorial, Math.Max(1, lesson));

    /// <summary>
    /// A Classic run whose pieces come from <paramref name="seed"/>. <paramref name="chosen"/> is true when the player typed
    /// the seed: such a run pays gold but never sets a personal best (so nobody picks an easy seed for a record).
    /// </summary>
    public static GameSetup ClassicRun(int seed, bool chosen) => new(GameMode.Classic, 0) { ClassicSeed = seed, SeedChosen = chosen };

    /// <summary>The piece-stream seed of a Classic run. Null means the engine's own (random) generator.</summary>
    public int? ClassicSeed { get; init; }

    /// <summary>The player typed the Classic seed in themselves.</summary>
    public bool SeedChosen { get; init; }

    /// <summary>Whether this run can set the Classic personal best. A typed seed cannot.</summary>
    public bool CountsForBest => !SeedChosen;

    /// <summary>Settings key of the Classic personal best. Story has progress instead of a best score.</summary>
    public string BestScoreKey => "best_score";

    public bool IsStory => Mode == GameMode.Story;

    /// <summary>A tutorial lesson. Not part of the campaign.</summary>
    public bool IsLesson => Mode == GameMode.Tutorial;

    /// <summary>Plays a level definition with goals (a Story level or a lesson), as opposed to Classic.</summary>
    public bool HasLevel => Mode != GameMode.Classic;

    /// <summary>Short label for the HUD.</summary>
    public string Title => Mode switch
    {
        GameMode.Classic => "CLASSIC",
        GameMode.Tutorial => $"LESSON {Level}",
        _ => $"LEVEL {Level}",
    };
}
