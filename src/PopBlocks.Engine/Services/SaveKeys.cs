namespace PopBlocks.Engine.Services;

/// <summary>
/// Every key the game saves, in one place. A key that already exists on a player's phone must never be
/// renamed or reused: an existing install would silently lose or misread the value. Add new keys here
/// (and to <see cref="All"/>); the tests compare this list with the constants the services actually use.
/// </summary>
public static class SaveKeys
{
    // ---- Version ----
    /// <summary>Format version of the saved data. See <see cref="SaveMigrator"/>.</summary>
    public const string SaveVersion = "save_version";
    public const int CurrentSaveVersion = 1;

    // ---- Progress and gold (PlayerProgress) ----
    public const string ProgressHighest = "progress_highest";
    public const string Gold = "gold";

    // ---- Shop (ShopService, "upgrade_<name>") ----
    public const string UpgradePreview = "upgrade_preview";
    public const string UpgradePopFlair = "upgrade_popflair";
    public const string UpgradeExtraMoves = "upgrade_extramoves";
    public const string UpgradeColourLens = "upgrade_colourlens";
    public const string UpgradeChainSense = "upgrade_chainsense";
    public const string UpgradePrismReserve = "upgrade_prismreserve";

    // ---- Consumables (ConsumableInventory): how many of each are in stock, 0 to 3 ----
    public const string ConsumableSecondChance = "consumable_second_chance";
    public const string ConsumablePrismShift = "consumable_prism_shift";

    // ---- Scores (HighScoreService) ----
    public const string BestScore = "best_score";

    // ---- Settings (GameSettings) ----
    public const string BlastEffects = "blast_effects";
    public const string ShowGhost = "show_ghost";
    public const string ControlsCollapsed = "controls_collapsed";
    public const string SoundEffects = "sound_effects";

    /// <summary>0 off, 1 outline, 2 bold. Absent in older saves: <see cref="ShowGhost"/> then decides (true = 1, false = 0).</summary>
    public const string LandingGuideStyle = "landing_guide_style";

    /// <summary>Calms animation: no screen shake, flashes or confetti. Off by default.</summary>
    public const string ReducedMotion = "reduced_motion";

    // ---- Story (UnlockStore, CosmeticLoadout) ----
    /// <summary>Comma-separated ids: scenes seen, fragments, lore, achievements, cosmetics owned.</summary>
    public const string Unlocks = "unlocks_v1";

    /// <summary>The equipped board accent (a cosmetic id, empty for none).</summary>
    public const string CosmeticBoard = "cos_board";

    /// <summary>The equipped block skin, burst style, piece frame and Pip accessory (cosmetic ids; empty for the default).</summary>
    public const string CosmeticSkin = "cos_skin";
    public const string CosmeticBurst = "cos_burst";
    public const string CosmeticFrame = "cos_frame";
    public const string CosmeticAccessory = "cos_pip";

    /// <summary>The look chosen after the ending (a <c>FinalChoice</c> id, empty until chosen).</summary>
    public const string EndingTheme = "ending_theme";

    // ---- Seeded levels and rerolls (PlayerIdentity, LevelAttemptState, PlayerProgress) ----
    /// <summary>The in-game name (1 to 20 characters). Display only: it never changes a level once the salt is set.</summary>
    public const string PlayerName = "player_name";

    /// <summary>Hash of the name and a random install number, set once. 0 means not set (an existing player who has not been asked yet).</summary>
    public const string PlayerSalt = "player_salt";

    /// <summary>The level number the next three values belong to. Ignored unless it is the player's current level (HighestCleared + 1).</summary>
    public const string CurrentLevelFor = "current_level_for";

    /// <summary>Seed of the player's current level. 0 means not yet assigned.</summary>
    public const string CurrentLevelSeed = "current_level_seed";

    /// <summary>Rerolls used on the current level. Not reset by a reroll; reset when the level is cleared.</summary>
    public const string CurrentLevelRerolls = "current_level_rerolls";

    /// <summary>Failures on the current level. Not reset by a reroll; reset when the level is cleared.</summary>
    public const string CurrentLevelFailures = "current_level_failures";

    /// <summary>Comma-separated <c>level:seed</c> pairs: the seed each cleared level was first cleared with.</summary>
    public const string FirstClearSeeds = "first_clear_seeds";

    // ---- Classic gold (ClassicGold) ----
    /// <summary>Tenths of a gold earned in Classic and not yet paid (0 to 9).</summary>
    public const string ClassicGoldCarry = "classic_gold_carry";

    // ---- Lifetime stats (LifetimeStats): maxima and counters only ----
    public const string StatLargestGroup = "stat_largest_group";
    public const string StatLongestChain = "stat_longest_chain";
    public const string StatMostCleared = "stat_most_cleared";
    public const string StatMostMovesLeft = "stat_most_moves_left";
    public const string StatAbilitiesUsed = "stat_abilities_used";
    public const string StatBoardsCleared = "stat_boards_cleared";
    public const string StatCellsPopped = "stat_cells_popped";
    public const string StatLevelsWon = "stat_levels_won";
    public const string StatMostGoals = "stat_most_goals";

    /// <summary>Levels played in one region (the journal's per-region count): <c>stat_plays_r1</c> to <c>stat_plays_r8</c>.</summary>
    public static string StatRegionPlays(int region) => "stat_plays_r" + region;

    public static IReadOnlyList<string> StatKeys { get; } = new[]
    {
        StatLargestGroup, StatLongestChain, StatMostCleared, StatMostMovesLeft,
        StatAbilitiesUsed, StatBoardsCleared, StatCellsPopped, StatLevelsWon, StatMostGoals,
        StatRegionPlays(1), StatRegionPlays(2), StatRegionPlays(3), StatRegionPlays(4),
        StatRegionPlays(5), StatRegionPlays(6), StatRegionPlays(7), StatRegionPlays(8),
    };

    /// <summary>Every key, for tests and documentation.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        SaveVersion, ProgressHighest, Gold, UpgradePreview, UpgradePopFlair, UpgradeExtraMoves, UpgradeColourLens, UpgradeChainSense, UpgradePrismReserve, ConsumableSecondChance, ConsumablePrismShift, BestScore,
        BlastEffects, ShowGhost, LandingGuideStyle, ReducedMotion, ControlsCollapsed, SoundEffects, Unlocks, CosmeticBoard, CosmeticSkin, CosmeticBurst, CosmeticFrame, CosmeticAccessory, EndingTheme,
        PlayerName, PlayerSalt, CurrentLevelFor, CurrentLevelSeed, CurrentLevelRerolls, CurrentLevelFailures, FirstClearSeeds, ClassicGoldCarry,
        StatLargestGroup, StatLongestChain, StatMostCleared, StatMostMovesLeft,
        StatAbilitiesUsed, StatBoardsCleared, StatCellsPopped, StatLevelsWon, StatMostGoals,
        StatRegionPlays(1), StatRegionPlays(2), StatRegionPlays(3), StatRegionPlays(4),
        StatRegionPlays(5), StatRegionPlays(6), StatRegionPlays(7), StatRegionPlays(8),
    };
}

/// <summary>
/// Runs once at start-up. Version 1 is the first saved format that has a version, so there is nothing to
/// convert yet; the method exists so a future change to saved data has an explicit, tested place to go.
/// </summary>
public static class SaveMigrator
{
    public static void Run(ILocalSettings settings)
    {
        int version = settings.GetInt(SaveKeys.SaveVersion, 0);
        if (version >= SaveKeys.CurrentSaveVersion) return;

        // 0 -> 1: saves from before the story update carry no version. Their keys are unchanged.
        settings.SetInt(SaveKeys.SaveVersion, SaveKeys.CurrentSaveVersion);
    }
}
