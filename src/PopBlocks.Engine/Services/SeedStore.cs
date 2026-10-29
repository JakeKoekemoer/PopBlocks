using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Services;

/// <summary>The saved state of the player's current Story level: its seed, the rerolls used and the failures so far.</summary>
public readonly record struct CurrentLevelState(int Level, int Seed, int Rerolls, int Failures);

/// <summary>
/// Reads and writes the seed-related save keys. Everything is stored through <see cref="ILocalSettings"/> the moment it
/// changes, so an app kill cannot lose a failure or hand out a different level.
/// </summary>
public static class SeedStore
{
    private const int MaxPairs = 5_000;

    /// <summary>
    /// The saved state for the player's current level (<paramref name="currentLevel"/> = HighestCleared + 1). If the saved
    /// values belong to some other level (a restored code with mismatched fields, or progress moved on), they are ignored.
    /// </summary>
    public static CurrentLevelState ReadCurrent(ILocalSettings settings, int currentLevel)
    {
        if (settings.GetInt(SaveKeys.CurrentLevelFor, 0) != currentLevel) return new CurrentLevelState(currentLevel, 0, 0, 0);
        return new CurrentLevelState(
            currentLevel,
            Math.Clamp(settings.GetInt(SaveKeys.CurrentLevelSeed, 0), 0, SeedingConfig.SeedMask),
            Math.Clamp(settings.GetInt(SaveKeys.CurrentLevelRerolls, 0), 0, 100_000),
            Math.Clamp(settings.GetInt(SaveKeys.CurrentLevelFailures, 0), 0, 100_000));
    }

    public static void WriteCurrent(ILocalSettings settings, CurrentLevelState state)
    {
        // The level number goes last: if the app dies part-way, a half-written state reads as stale, never as wrong.
        settings.SetInt(SaveKeys.CurrentLevelSeed, Math.Clamp(state.Seed, 0, SeedingConfig.SeedMask));
        settings.SetInt(SaveKeys.CurrentLevelRerolls, Math.Clamp(state.Rerolls, 0, 100_000));
        settings.SetInt(SaveKeys.CurrentLevelFailures, Math.Clamp(state.Failures, 0, 100_000));
        settings.SetInt(SaveKeys.CurrentLevelFor, Math.Clamp(state.Level, 0, 100_000));
    }

    /// <summary>The level was cleared: nothing about its attempts carries over to the next level.</summary>
    public static void ResetCurrent(ILocalSettings settings)
    {
        settings.SetInt(SaveKeys.CurrentLevelFor, 0);
        settings.SetInt(SaveKeys.CurrentLevelSeed, 0);
        settings.SetInt(SaveKeys.CurrentLevelRerolls, 0);
        settings.SetInt(SaveKeys.CurrentLevelFailures, 0);
    }

    // ---- v1.1: losses and eased levels (level:value lists, the same text format as the first-clear seeds) ----

    /// <summary>How many times a level has been lost, ever. Never resets: clearing the level does not clear it.</summary>
    public static int ReadLosses(ILocalSettings settings, int level) =>
        SaveCode.ParseSeedList(settings.GetString(SaveKeys.LevelLosses, "")).TryGetValue(level, out int n) ? n : 0;

    /// <summary>Adds one loss to a level. Returns the new count.</summary>
    public static int AddLoss(ILocalSettings settings, int level)
    {
        if (level < 1) return 0;
        var map = new Dictionary<int, int>(SaveCode.ParseSeedList(settings.GetString(SaveKeys.LevelLosses, "")));
        if (!map.ContainsKey(level) && map.Count >= MaxPairs) return 0;
        int next = Math.Min(100_000, (map.TryGetValue(level, out int n) ? n : 0) + 1);
        map[level] = next;
        settings.SetString(SaveKeys.LevelLosses, SaveCode.FormatSeedList(map));
        return next;
    }

    /// <summary>The levels the player has chosen to ease.</summary>
    public static bool IsEased(ILocalSettings settings, int level) =>
        SaveCode.ParseSeedList(settings.GetString(SaveKeys.EasedLevels, "")).ContainsKey(level);

    public static void SetEased(ILocalSettings settings, int level, bool eased)
    {
        if (level < 1) return;
        var map = new Dictionary<int, int>(SaveCode.ParseSeedList(settings.GetString(SaveKeys.EasedLevels, "")));
        if (eased) { if (map.Count >= MaxPairs && !map.ContainsKey(level)) return; map[level] = 1; }
        else if (!map.Remove(level)) return;
        settings.SetString(SaveKeys.EasedLevels, SaveCode.FormatSeedList(map));
    }

    /// <summary>The seed each cleared level was first cleared with. A cleared level missing here replays the original (legacy) level.</summary>
    public static IReadOnlyDictionary<int, int> ReadFirstClear(ILocalSettings settings) =>
        SaveCode.ParseSeedList(settings.GetString(SaveKeys.FirstClearSeeds, ""));

    /// <summary>
    /// Records the seed a level was first cleared with. Never overwrites an entry, and ignores the legacy seed (a level
    /// without an entry already replays as the original). Returns true if something was written.
    /// </summary>
    public static bool RecordFirstClear(ILocalSettings settings, int level, int seed)
    {
        if (level < 1 || seed < 1 || seed > SeedingConfig.SeedMask) return false;

        var map = new Dictionary<int, int>(ReadFirstClear(settings));
        if (map.ContainsKey(level) || map.Count >= MaxPairs) return false;

        map[level] = seed;
        settings.SetString(SaveKeys.FirstClearSeeds, SaveCode.FormatSeedList(map));
        return true;
    }
}
