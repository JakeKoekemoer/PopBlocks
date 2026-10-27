using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

public enum GoalKind
{
    /// <summary>Have at least this score.</summary>
    Score,

    /// <summary>Pop this many blocks in total.</summary>
    PopBlocks,

    /// <summary>Pop this many blocks of one colour.</summary>
    PopColour,

    /// <summary>Make a chain of this many waves from a single piece.</summary>
    Chain,

    /// <summary>Pop one group of at least this size.</summary>
    BigGroup,

    /// <summary>Leave this many blocks or fewer on the board (0 = clear it completely).</summary>
    ClearDown,

    /// <summary>Make this many separate chains, each of at least <see cref="Goal.MinWaves"/> waves.</summary>
    ChainSet,
}

/// <param name="MinWaves">Chain sets only: how many waves one chain needs to count.</param>
public sealed record Goal(GoalKind Kind, int Target, CellColour Colour = CellColour.Empty, int MinWaves = 0)
{
    public static string ColourName(CellColour c) => c switch
    {
        CellColour.Cyan => "blue",
        CellColour.Coral => "red",
        CellColour.Yellow => "yellow",
        CellColour.Purple => "purple",
        CellColour.Lime => "green",
        _ => "",
    };

    /// <summary>Player-facing sentence for this goal.</summary>
    public string Describe() => Kind switch
    {
        GoalKind.Score => $"Score {Target:N0}",
        GoalKind.PopBlocks => $"Pop {Target} blocks",
        GoalKind.PopColour => $"Pop {Target} {ColourName(Colour)} blocks",
        GoalKind.Chain => $"Make a x{Target} chain",
        GoalKind.BigGroup => $"Pop a group of {Target}",
        GoalKind.ClearDown => Target == 0 ? "Clear the board" : $"Leave {Target} blocks or fewer",
        GoalKind.ChainSet => $"Make {Target} separate chains of x{MinWaves} or more",
        _ => "",
    };
}

/// <summary>A goal together with how far along it is.</summary>
public sealed record GoalState(Goal Goal, int Current, int Start, bool IsComplete)
{
    public string Text => Goal.Describe();

    /// <summary>0..1 for a progress bar.</summary>
    public double Fraction
    {
        get
        {
            if (IsComplete) return 1.0;
            if (Goal.Kind == GoalKind.ClearDown)
            {
                int span = Math.Max(1, Start - Goal.Target);
                return Math.Clamp((Start - Current) / (double)span, 0.0, 1.0);
            }
            return Math.Clamp(Current / (double)Math.Max(1, Goal.Target), 0.0, 1.0);
        }
    }

    /// <summary>"12 / 40", or "9 left" for clear-down goals.</summary>
    public string ProgressText => Goal.Kind switch
    {
        GoalKind.ClearDown => $"{Current} left",
        GoalKind.ChainSet => $"{Math.Min(Current, Goal.Target)} / {Goal.Target} chains",
        _ => $"{Math.Min(Current, Goal.Target):N0} / {Goal.Target:N0}",
    };
}

/// <summary>Tracks a level's goals. Fed one burst wave at a time, then asked to evaluate against the settled board.</summary>
public sealed class GoalTracker
{
    private readonly IReadOnlyList<Goal> _goals;
    private readonly int _startBlocks;
    private readonly int[] _poppedByColour = new int[6];
    private readonly List<int> _closedChains = new();
    private int _openChain;

    public GoalTracker(IReadOnlyList<Goal> goals, int startBlocks)
    {
        _goals = goals;
        _startBlocks = startBlocks;
    }

    public int TotalPopped { get; private set; }
    public int BestChain { get; private set; }
    public int BestGroup { get; private set; }

    /// <summary>An independent copy of everything tracked so far (for undo). Goals and the start count are shared; they never change.</summary>
    public GoalTracker Clone()
    {
        var copy = new GoalTracker(_goals, _startBlocks)
        {
            TotalPopped = TotalPopped,
            BestChain = BestChain,
            BestGroup = BestGroup,
            _openChain = _openChain,
        };
        Array.Copy(_poppedByColour, copy._poppedByColour, _poppedByColour.Length);
        copy._closedChains.AddRange(_closedChains);
        return copy;
    }

    public void OnWave(BurstWave wave)
    {
        TotalPopped += wave.ClearedCells.Count;
        BestChain = Math.Max(BestChain, wave.ChainIndex);

        // A wave with index 1 is the first of a new chain, so it closes the one before.
        if (wave.ChainIndex <= 1 && _openChain > 0) _closedChains.Add(_openChain);
        _openChain = wave.ChainIndex;
        foreach (var g in wave.Groups)
        {
            _poppedByColour[(int)g.Colour] += g.Size;
            BestGroup = Math.Max(BestGroup, g.Size);
        }
    }

    /// <summary>Separate chains so far (the one still open included) with at least this many waves.</summary>
    public int ChainsOfAtLeast(int waves)
    {
        int count = 0;
        foreach (int length in _closedChains)
            if (length >= waves) count++;
        if (_openChain >= waves) count++;
        return count;
    }

    public IReadOnlyList<GoalState> Evaluate(int score, int blocksOnBoard)
    {
        var states = new GoalState[_goals.Count];
        for (int i = 0; i < states.Length; i++)
        {
            var goal = _goals[i];
            int current = goal.Kind switch
            {
                GoalKind.Score => score,
                GoalKind.PopBlocks => TotalPopped,
                GoalKind.PopColour => _poppedByColour[(int)goal.Colour],
                GoalKind.Chain => BestChain,
                GoalKind.BigGroup => BestGroup,
                GoalKind.ChainSet => ChainsOfAtLeast(Math.Max(2, goal.MinWaves)),
                _ => blocksOnBoard,
            };
            bool done = goal.Kind == GoalKind.ClearDown ? current <= goal.Target : current >= goal.Target;
            states[i] = new GoalState(goal, current, _startBlocks, done);
        }
        return states;
    }

    public static bool AllComplete(IReadOnlyList<GoalState> states)
    {
        if (states.Count == 0) return false;
        foreach (var s in states)
            if (!s.IsComplete) return false;
        return true;
    }
}
