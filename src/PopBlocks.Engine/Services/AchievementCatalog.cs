using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Services;

/// <summary>
/// Everything an achievement rule may look at. Plain data, so a rule is a pure function that a test can feed with
/// made-up facts. <see cref="Run"/> is the play that has just ended (null when nothing has just ended).
/// </summary>
public sealed record AchievementFacts
{
    public RunStats? Run { get; init; }
    public LevelDefinition? Level { get; init; }
    public int HighestCleared { get; init; }
    public int RegionsRestored { get; init; }
    public int ArchiveEntries { get; init; }
    public int RegionsWithMemory { get; init; }
    public bool AnyRegionFullyRemembered { get; init; }
    public bool CampaignComplete { get; init; }

    /// <summary>The score of the Classic game that has just ended, else 0.</summary>
    public int ClassicScore { get; init; }
}

/// <summary>
/// One achievement. All are skill or discovery based and local to the device. None asks for grinding: thresholds are
/// things a player meets by playing well or exploring, not by repeating something many times.
/// </summary>
public sealed record Achievement(string Id, string Name, string Description, int Gold, Func<AchievementFacts, bool> IsMet);

public static class AchievementCatalog
{
    public const int LargeGroup = 8;
    public const int SparkingChain = 5;
    public const int SparedMoves = 3;
    public const int ArchiveGoal = 20;
    public const int ClassicGoal = 3000;

    public static IReadOnlyList<Achievement> All { get; } = new[]
    {
        new Achievement("first_connection", "First Connection", "Pop your first group of blocks.", 20,
            f => f.Run is { Waves: > 0 }),

        new Achievement("aftershock", "Aftershock", "Make your first chain: one pop that sets off another.", 20,
            f => f.Run is { LongestChain: >= 2 }),

        new Achievement("bigger_picture", "A Bigger Picture", $"Pop a single group of {LargeGroup} or more.", 40,
            f => f.Run is { LargestGroup: >= LargeGroup }),

        new Achievement("careful_planner", "Careful Planner", $"Win a Story level with {SparedMoves} or more moves to spare.", 40,
            f => f.Run is { Completed: true } r && f.Level is { IsTutorial: false, MoveLimit: > 0 } && r.MovesLeftAtFinish >= SparedMoves),

        new Achievement("restorer", "Restorer", "Restore your first region.", 50,
            f => f.RegionsRestored >= 1),

        new Achievement("keeper_of_memories", "Keeper of Memories", $"Collect {ArchiveGoal} entries in the Archive.", 60,
            f => f.ArchiveEntries >= ArchiveGoal),

        new Achievement("spectrum_seeker", "Spectrum Seeker", "Find a memory in every region.", 150,
            f => f.RegionsWithMemory >= 8),

        new Achievement("balanced_world", "Balanced World", "Finish the campaign.", 150,
            f => f.CampaignComplete),

        new Achievement("classic_champion", "Endless Champion", $"Score {ClassicGoal:N0} in Endless mode.", 100,
            f => f.ClassicScore >= ClassicGoal),

        new Achievement("chain_master", "Chain Master", $"Make a chain of {SparkingChain} waves from one piece.", 80,
            f => f.Run is { LongestChain: >= SparkingChain }),

        new Achievement("clean_sweep", "Clean Sweep", "Win a Story level with the board completely empty.", 80,
            f => f.Run is { Completed: true, BoardCleared: true } && f.Level is { IsTutorial: false }),

        new Achievement("perfectionist", "Perfectionist", "Find every memory in one region.", 100,
            f => f.AnyRegionFullyRemembered),

        new Achievement("puzzle_scholar", "Puzzle Scholar", "Clear the first ten levels.", 20,
            f => f.HighestCleared >= StoryConfig.TutorialLastLevel),
    };

    public static Achievement? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    public static int TotalGold => All.Sum(a => a.Gold);
}
