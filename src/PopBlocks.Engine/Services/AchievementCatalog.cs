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

    // ---- v1.1: running totals and what the player is wearing ----
    public int Rerolls { get; init; }
    public int OrbsBrokenTotal { get; init; }
    public int FractureBreaksTotal { get; init; }
    public int Shares { get; init; }
    public int CleanWins { get; init; }

    /// <summary>How many times the level that has just been won was lost before (it never resets).</summary>
    public int LevelLosses { get; init; }

    /// <summary>A bought block set, board, frame and Pip look are all equipped together.</summary>
    public bool DressedUp { get; init; }
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
    public const int OrbCollectorGoal = 25;
    public const int ShattererGoal = 100;
    public const int FaultLinesGoal = 5;
    public const int CleanWinsGoal = 10;
    public const int RerollGoal = 10;
    public const int StubbornLosses = 10;
    public const int GoldRushTenths = 100;
    public const int EndlessNightPieces = 200;

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

        new Achievement("keeper_of_memories", "Keeper of Memories", $"Collect {ArchiveGoal} entries in the Story Book.", 60,
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

        // ---- v1.1. An eased clear never counts as a win, so none of the win rules below is met by one. ----
        new Achievement("orb_breaker", "Orb Breaker", "Break your first Orb.", 30,
            f => f.OrbsBrokenTotal >= 1),

        new Achievement("orb_collector", "Crystal Collector", $"Break {OrbCollectorGoal} Orbs.", 120,
            f => f.OrbsBrokenTotal >= OrbCollectorGoal),

        new Achievement("close_call", "Close Call", "Break an Orb with 1 move left on its timer.", 60,
            f => f.Run is { OrbCloseCall: true }),

        new Achievement("hairline", "Hairline", "Crack a block.", 20,
            f => f.Run is { Cracks: > 0 }),

        new Achievement("shatterer", "Shatterer", $"Break {ShattererGoal} blocks by cracks.", 100,
            f => f.FractureBreaksTotal >= ShattererGoal),

        new Achievement("fault_lines", "Fault Lines", $"Break {FaultLinesGoal} blocks by cracks with one piece.", 80,
            f => f.Run is { MostBreaksByOnePiece: >= FaultLinesGoal }),

        new Achievement("nine_lives", "Not Done Yet", "Win a level after buying a continue.", 40,
            f => f.Run is { Completed: true, Continues: > 0 } && f.Level is { IsTutorial: false }),

        new Achievement("full_hearts", "Full of Heart", $"Win {CleanWinsGoal} Story levels without a continue.", 50,
            f => f.CleanWins >= CleanWinsGoal),

        new Achievement("roll_with_it", "Roll With It", $"Reroll {RerollGoal} levels.", 40,
            f => f.Rerolls >= RerollGoal),

        new Achievement("stubborn", "Stubborn", $"Win a level you have lost {StubbornLosses} times or more, without easing it.", 80,
            f => f.Run is { Completed: true, Eased: false } && f.Level is { IsTutorial: false, Eased: false } && f.LevelLosses >= StubbornLosses),

        new Achievement("gold_rush", "Gold Rush", "Earn 10 pop gold in one Story level.", 60,
            f => f.Run is { PopGoldTenths: >= GoldRushTenths } && f.Level is { IsTutorial: false, Eased: false }),

        new Achievement("deep_dive", "Deep Dive", "Clear a board back down to its bottom row after it scrolled.", 60,
            f => f.Run is { DeepDive: true }),

        new Achievement("endless_night", "Endless Night", $"Last {EndlessNightPieces} pieces in one Endless run.", 100,
            f => f.Level is null && f.Run is { PiecesPlaced: >= EndlessNightPieces }),

        new Achievement("rule_maker", "Rule Maker", "Play an Endless run with your own rules.", 20,
            f => f.Level is null && f.Run is { CustomRules: true, PiecesPlaced: > 0 }),

        new Achievement("dressed_up", "Dressed Up", "Wear a bought block set, board, frame and Pip look together.", 50,
            f => f.DressedUp),

        new Achievement("spread_the_word", "Spread the Word", "Open the share sheet.", 20,
            f => f.Shares >= 1),
    };

    public static Achievement? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    public static int TotalGold => All.Sum(a => a.Gold);
}
