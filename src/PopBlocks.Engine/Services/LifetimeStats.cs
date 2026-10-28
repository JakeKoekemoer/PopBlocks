using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

/// <summary>
/// The player's personal bests and running totals across all play: shown in the journal and used by achievements.
/// Only maxima and counters are saved, under their own keys. Nothing leaves the device.
/// </summary>
public sealed class LifetimeStats
{
    private readonly ILocalSettings _settings;

    public LifetimeStats(ILocalSettings settings)
    {
        _settings = settings;
        LargestGroup = Read(SaveKeys.StatLargestGroup);
        LongestChain = Read(SaveKeys.StatLongestChain);
        MostClearedByOnePiece = Read(SaveKeys.StatMostCleared);
        MostMovesLeft = Read(SaveKeys.StatMostMovesLeft);
        AbilitiesUsed = Read(SaveKeys.StatAbilitiesUsed);
        BoardsCleared = Read(SaveKeys.StatBoardsCleared);
        CellsPopped = Read(SaveKeys.StatCellsPopped);
        LevelsWon = Read(SaveKeys.StatLevelsWon);
        MostGoalsOnClearedLevel = Read(SaveKeys.StatMostGoals);
        Rerolls = Read(SaveKeys.StatRerolls);
        Continues = Read(SaveKeys.StatContinues);
        OrbsBroken = Read(SaveKeys.StatOrbsBroken);
        FractureBreaks = Read(SaveKeys.StatFractureBreaks);
        Shares = Read(SaveKeys.StatShares);
        CleanWins = Read(SaveKeys.StatCleanWins);
    }

    public int LargestGroup { get; private set; }
    public int LongestChain { get; private set; }
    public int MostClearedByOnePiece { get; private set; }

    /// <summary>The most moves left over at the end of a won level.</summary>
    public int MostMovesLeft { get; private set; }

    public int AbilitiesUsed { get; private set; }
    public int BoardsCleared { get; private set; }
    public int CellsPopped { get; private set; }

    /// <summary>Levels won, replays included.</summary>
    public int LevelsWon { get; private set; }

    /// <summary>The most goals a level you cleared asked for at once.</summary>
    public int MostGoalsOnClearedLevel { get; private set; }

    /// <summary>Levels rerolled, in total (free rerolls of cleared levels count too).</summary>
    public int Rerolls { get; private set; }

    /// <summary>Continues bought, in total.</summary>
    public int Continues { get; private set; }

    /// <summary>Orbs broken, blocks broken by a third crack, times the share sheet was opened, and Story wins without a continue.</summary>
    public int OrbsBroken { get; private set; }
    public int FractureBreaks { get; private set; }
    public int Shares { get; private set; }
    public int CleanWins { get; private set; }

    /// <summary>The share sheet was opened.</summary>
    public void AddShare()
    {
        Shares = Grow(SaveKeys.StatShares, Shares, 1);
        Changed?.Invoke();
    }

    /// <summary>One more level has been rerolled.</summary>
    public void AddReroll()
    {
        Rerolls = Grow(SaveKeys.StatRerolls, Rerolls, 1);
        Changed?.Invoke();
    }

    /// <summary>Story levels played (started and at least one piece placed) in a region, 1 to 8.</summary>
    public int PlaysInRegion(int region) =>
        region is >= 1 and <= 8 ? Read(SaveKeys.StatRegionPlays(region)) : 0;

    /// <summary>Raised after a run has been added.</summary>
    public event Action? Changed;

    private int Read(string key) => Math.Max(0, _settings.GetInt(key, 0));

    /// <summary>Adds a finished (won or lost) run. Maxima only ever rise; counters only ever grow.</summary>
    public void Add(RunStats run, LevelDefinition? level = null)
    {
        if (level is not null)
        {
            var region = RegionCatalog.For(level.Number);
            if (region is not null && run.PiecesPlaced > 0)
                Grow(SaveKeys.StatRegionPlays(region.Number), PlaysInRegion(region.Number), 1);
            if (run.Completed)
                MostGoalsOnClearedLevel = Raise(SaveKeys.StatMostGoals, MostGoalsOnClearedLevel, level.Goals.Count);
        }

        LargestGroup = Raise(SaveKeys.StatLargestGroup, LargestGroup, run.LargestGroup);
        LongestChain = Raise(SaveKeys.StatLongestChain, LongestChain, run.LongestChain);
        MostClearedByOnePiece = Raise(SaveKeys.StatMostCleared, MostClearedByOnePiece, run.MostClearedByOnePiece);
        CellsPopped = Grow(SaveKeys.StatCellsPopped, CellsPopped, run.CellsPopped);
        AbilitiesUsed = Grow(SaveKeys.StatAbilitiesUsed, AbilitiesUsed, run.AbilitiesUsed);
        Continues = Grow(SaveKeys.StatContinues, Continues, run.Continues);
        OrbsBroken = Grow(SaveKeys.StatOrbsBroken, OrbsBroken, run.OrbsBroken);
        FractureBreaks = Grow(SaveKeys.StatFractureBreaks, FractureBreaks, run.FractureBreaks);
        if (run.Completed && level is { IsTutorial: false } && run.Continues == 0) CleanWins = Grow(SaveKeys.StatCleanWins, CleanWins, 1);

        if (run.Completed)
        {
            MostMovesLeft = Raise(SaveKeys.StatMostMovesLeft, MostMovesLeft, run.MovesLeftAtFinish);
            LevelsWon = Grow(SaveKeys.StatLevelsWon, LevelsWon, 1);
            if (run.BoardCleared) BoardsCleared = Grow(SaveKeys.StatBoardsCleared, BoardsCleared, 1);
        }
        Changed?.Invoke();
    }

    /// <summary>Forgets everything. For testing (see DevTools).</summary>
    public void DevClear()
    {
        foreach (var key in SaveKeys.StatKeys) _settings.SetInt(key, 0);
        LargestGroup = LongestChain = MostClearedByOnePiece = MostMovesLeft = 0;
        AbilitiesUsed = BoardsCleared = CellsPopped = LevelsWon = MostGoalsOnClearedLevel = Rerolls = Continues = OrbsBroken = FractureBreaks = Shares = CleanWins = 0;
        Changed?.Invoke();
    }

    private int Raise(string key, int current, int candidate)
    {
        if (candidate <= current) return current;
        _settings.SetInt(key, candidate);
        return candidate;
    }

    private int Grow(string key, int current, int by)
    {
        if (by <= 0) return current;
        long next = Math.Min(int.MaxValue, (long)current + by);
        _settings.SetInt(key, (int)next);
        return (int)next;
    }
}
