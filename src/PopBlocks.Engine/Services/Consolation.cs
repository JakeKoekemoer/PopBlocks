namespace PopBlocks.Engine.Services;

/// <summary>What a lost level paid.</summary>
/// <param name="PopPart">Half of the pop gold the run earned.</param>
/// <param name="ProgressPart">Half of the first-clear reward in proportion to how far the goals were met (only while the level is uncleared, and capped for good per level).</param>
public readonly record struct ConsolationResult(int PopPart, int ProgressPart)
{
    public int Total => PopPart + ProgressPart;
}

/// <summary>
/// Losing a Story level still pays something, so a hard level never feels like wasted time:
/// <c>floor(runPopGold / 2) + floor(firstClearReward x goalProgress / 2)</c>. The second term is paid only while the level
/// is uncleared, and what it has paid for a level over all attempts never passes that level's first-clear reward
/// (<see cref="SaveKeys.ConsolationPaid"/>), so losing over and over can never out-earn clearing the level. Levels the
/// player eased, and lessons, pay no consolation.
/// </summary>
public sealed class Consolation
{
    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;

    public Consolation(ILocalSettings settings, PlayerProgress progress)
    {
        _settings = settings;
        _progress = progress;
    }

    /// <summary>The pop-gold half. <paramref name="runPopGold"/> is the whole gold the run earned by popping.</summary>
    public static int PopPart(int runPopGold) => Math.Max(0, runPopGold) / 2;

    /// <summary>The progress half, before the lifetime cap. <paramref name="goalProgress"/> is 0 to 1.</summary>
    public static int ProgressPart(int firstClearReward, double goalProgress) =>
        (int)Math.Floor(Math.Max(0, firstClearReward) * Math.Clamp(goalProgress, 0.0, 1.0) / 2.0);

    /// <summary>What has been paid so far as the progress half for this level, over all attempts.</summary>
    public int ProgressPaid(int level) =>
        SaveCode.ParseSeedList(_settings.GetString(SaveKeys.ConsolationPaid, "")).TryGetValue(level, out int n) ? n : 0;

    /// <summary>
    /// What a loss would pay now, without paying it. <paramref name="alreadyPaid"/> is what an earlier loss of the same attempt
    /// paid (the attempt went on after a continue and was lost again): only the difference is due.
    /// </summary>
    public ConsolationResult Preview(int level, int runPopGold, double goalProgress, bool levelCleared, bool eased,
        ConsolationResult alreadyPaid = default)
    {
        if (eased) return default;
        int pop = Math.Max(0, PopPart(runPopGold) - alreadyPaid.PopPart);
        if (levelCleared) return new ConsolationResult(pop, 0);

        int reward = GoldRewards.For(level, isTutorial: level <= StoryConfig.TutorialLastLevel);
        int remaining = Math.Max(0, reward - ProgressPaid(level) + alreadyPaid.ProgressPart);
        int target = Math.Min(ProgressPart(reward, goalProgress), remaining);
        return new ConsolationResult(pop, Math.Max(0, target - alreadyPaid.ProgressPart));
    }

    /// <summary>Pays the consolation for a lost attempt, once. Returns what was paid.</summary>
    public ConsolationResult Pay(int level, int runPopGold, double goalProgress, bool levelCleared, bool eased,
        ConsolationResult alreadyPaid = default)
    {
        var result = Preview(level, runPopGold, goalProgress, levelCleared, eased, alreadyPaid);
        if (result.Total <= 0) return result;

        if (result.ProgressPart > 0)
        {
            var map = new Dictionary<int, int>(SaveCode.ParseSeedList(_settings.GetString(SaveKeys.ConsolationPaid, "")));
            map[level] = Math.Min(100_000_000, (map.TryGetValue(level, out int n) ? n : 0) + result.ProgressPart);
            _settings.SetString(SaveKeys.ConsolationPaid, SaveCode.FormatSeedList(map));
        }
        _progress.AwardGold(result.Total);
        return result;
    }
}
