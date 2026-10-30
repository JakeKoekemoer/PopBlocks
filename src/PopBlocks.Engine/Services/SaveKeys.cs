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
    public const string UpgradeInsight = "upgrade_insight";         // Pip's Insight (v1.1)
    public const string InsightAlwaysOn = "insight_always_on";      // Insight level 3: show the suggestion for every piece

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

    // ---- Audio (AudioSettings). Sound-effects on/off is the existing SoundEffects key above ----
    /// <summary>Volumes are whole numbers from 0 to 100.</summary>
    public const string AudioMaster = "audio_master";
    public const string AudioMusic = "audio_music";
    public const string AudioSfx = "audio_sfx";
    public const string AudioAmbience = "audio_ambience";
    public const string AudioMuteMaster = "audio_mute_master";
    public const string AudioMuteMusic = "audio_mute_music";
    public const string AudioMuteAmbience = "audio_mute_ambience";

    /// <summary>A calmer mix: no ambience, fewer music layers, no chain flourishes, softer effects. Off by default.</summary>
    public const string AudioReduced = "audio_reduced";

    /// <summary>The song the player chose in the pause screen (1 to 25, see Soundtrack). 0 means automatic: the music of the place.</summary>
    public const string AudioTrack = "audio_track";

    // ---- Story (UnlockStore, CosmeticLoadout) ----
    /// <summary>Comma-separated ids: scenes seen, fragments, lore, achievements, cosmetics owned.</summary>
    public const string Unlocks = "unlocks_v1";

    /// <summary>The equipped board accent (a cosmetic id, empty for none).</summary>
    public const string CosmeticBoard = "cos_board";

    /// <summary>1 when the player has chosen a board by hand (the board then stays whatever the area is), 0 to follow the area. Absent: follows the area unless a board was saved before this key existed.</summary>
    public const string CosmeticBoardPinned = "cos_board_pinned";

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

    /// <summary>Failures on the current level. Back to 0 on a successful reroll (v1.1) and when the level is cleared. The never-resetting count of losses is the level-losses key.</summary>
    public const string CurrentLevelFailures = "current_level_failures";

    /// <summary>Comma-separated <c>level:seed</c> pairs: the seed each cleared level was first cleared with.</summary>
    public const string FirstClearSeeds = "first_clear_seeds";

    // ---- v1.1: losses and eased levels ----
    /// <summary>Comma-separated <c>level:count</c> pairs: how many times each level has been lost. Never resets.</summary>
    public const string LevelLosses = "level_losses";

    /// <summary>Comma-separated <c>level:1</c> pairs: levels the player chose to ease (a feather on the Map).</summary>
    public const string EasedLevels = "eased_levels";

    /// <summary>Comma-separated <c>level:gold</c> pairs: the progress half of the loss consolation already paid for each uncleared level (never above that level's first-clear reward).</summary>
    public const string ConsolationPaid = "consolation_paid";

    // ---- v1.1: the player's own rules (GameRulesStore) and a few settings ----
    public const string RuleFallSpeed = "rule_fall_speed";          // percent: 50 to 200
    public const string RuleColourCount = "rule_colour_count";      // 3 to 5
    public const string RuleColours = "rule_colours";               // digits 1 to 5, the enabled colours
    public const string RuleShapes = "rule_shapes";                 // comma-separated shape names
    public const string RuleMinGroup = "rule_min_group";            // 3 to 6
    public const string RuleSolidChance = "rule_solid_chance";      // percent: 0, 15 or 40
    public const string RuleBoardRows = "rule_board_rows";          // 16 to 32
    public const string RuleFracture = "rule_fracture";
    public const string RuleOrbs = "rule_orbs";                     // 0 auto, 1 always, 2 never
    public const string RuleOrbTimer = "rule_orb_timer";            // 6, 8, 10 or 12
    public const string RuleUseInStory = "rule_use_in_story";
    public const string CosmeticMixCyan = "cosmetic_mix_cyan";      // Patchwork: the block set used for each colour
    public const string CosmeticMixCoral = "cosmetic_mix_coral";
    public const string CosmeticMixYellow = "cosmetic_mix_yellow";
    public const string CosmeticMixPurple = "cosmetic_mix_purple";
    public const string CosmeticMixLime = "cosmetic_mix_lime";
    public const string CosmeticSeen = "cosmetic_seen";             // new looks the player has looked at (comma-separated ids)
    public const string SeenOrbIntro = "seen_orb_intro";            // the first Orb has been explained
    public const string StoryBookTab = "storybook_tab";             // the tab the Story Book opens on
    public const string SettingsTab = "settings_tab";               // the tab the settings page opens on
    public const string ShowHanging = "show_hanging";               // the cue under blocks that hang over a gap
    public const string ColourAssist = "colour_assist";             // symbols on every block, whatever the skin

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

    /// <summary>Levels rerolled, in total (v1.1).</summary>
    public const string StatRerolls = "stat_rerolls";

    /// <summary>Continues bought, in total (v1.1).</summary>
    public const string StatContinues = "stat_continues";

    /// <summary>Orbs broken, blocks broken by a third crack, times the share sheet was opened, and Story wins without a continue (v1.1).</summary>
    public const string StatOrbsBroken = "stat_orbs_broken";
    public const string StatFractureBreaks = "stat_fracture_breaks";
    public const string StatShares = "stat_shares";
    public const string StatCleanWins = "stat_clean_wins";

    /// <summary>Levels played in one region (the journal's per-region count): <c>stat_plays_r1</c> to <c>stat_plays_r8</c>.</summary>
    public static string StatRegionPlays(int region) => "stat_plays_r" + region;

    public static IReadOnlyList<string> StatKeys { get; } = new[]
    {
        StatLargestGroup, StatLongestChain, StatMostCleared, StatMostMovesLeft,
        StatAbilitiesUsed, StatBoardsCleared, StatCellsPopped, StatLevelsWon, StatMostGoals,
        StatRegionPlays(1), StatRegionPlays(2), StatRegionPlays(3), StatRegionPlays(4),
        StatRegionPlays(5), StatRegionPlays(6), StatRegionPlays(7), StatRegionPlays(8),
        StatRerolls, StatContinues, StatOrbsBroken, StatFractureBreaks, StatShares, StatCleanWins,
    };

    // Not listed here on purpose: PersistentSettings.EnabledKey ("save_file_enabled"). It records whether this phone keeps a
    // save file outside the app. That is a choice about the phone, not progress, so it is never carried in a save code.

    /// <summary>Every key, for tests and documentation.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        SaveVersion, ProgressHighest, Gold, UpgradePreview, UpgradePopFlair, UpgradeExtraMoves, UpgradeColourLens, UpgradeChainSense, UpgradePrismReserve, UpgradeInsight, InsightAlwaysOn, ConsumableSecondChance, ConsumablePrismShift, BestScore,
        BlastEffects, ShowGhost, LandingGuideStyle, ReducedMotion, ControlsCollapsed, SoundEffects,
        AudioMaster, AudioMusic, AudioSfx, AudioAmbience, AudioMuteMaster, AudioMuteMusic, AudioMuteAmbience, AudioReduced, AudioTrack,
        Unlocks, CosmeticBoard, CosmeticBoardPinned, CosmeticSkin, CosmeticBurst, CosmeticFrame, CosmeticAccessory, EndingTheme,
        PlayerName, PlayerSalt, CurrentLevelFor, CurrentLevelSeed, CurrentLevelRerolls, CurrentLevelFailures, FirstClearSeeds, ClassicGoldCarry,
        LevelLosses, EasedLevels, ConsolationPaid,
        RuleFallSpeed, RuleColourCount, RuleColours, RuleShapes, RuleMinGroup, RuleSolidChance, RuleBoardRows, RuleFracture,
        RuleOrbs, RuleOrbTimer, RuleUseInStory, SettingsTab, ShowHanging, ColourAssist,
        CosmeticMixCyan, CosmeticMixCoral, CosmeticMixYellow, CosmeticMixPurple, CosmeticMixLime, CosmeticSeen, StoryBookTab, SeenOrbIntro,
        StatLargestGroup, StatLongestChain, StatMostCleared, StatMostMovesLeft,
        StatAbilitiesUsed, StatBoardsCleared, StatCellsPopped, StatLevelsWon, StatMostGoals,
        StatRegionPlays(1), StatRegionPlays(2), StatRegionPlays(3), StatRegionPlays(4),
        StatRegionPlays(5), StatRegionPlays(6), StatRegionPlays(7), StatRegionPlays(8),
        StatRerolls, StatContinues, StatOrbsBroken, StatFractureBreaks, StatShares, StatCleanWins,
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
