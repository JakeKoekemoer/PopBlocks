using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Services;

/// <summary>
/// What happened during one play of a level: the biggest group, the longest chain, how many blocks one piece cleared,
/// and how the level finished. It only watches the engine's events; it never changes a rule. It feeds the optional
/// memory objectives, achievements and the journal. Nothing here is saved by itself (see <see cref="LifetimeStats"/>).
/// </summary>
public sealed class RunStats
{
    private GameEngine? _engine;
    private int _clearedByThisPiece;

    public int LargestGroup { get; private set; }
    public int LongestChain { get; private set; }

    /// <summary>The most blocks cleared by a single piece (every wave of its chain added up).</summary>
    public int MostClearedByOnePiece { get; private set; }

    public int CellsPopped { get; private set; }
    public int Waves { get; private set; }
    public int PiecesPlaced { get; private set; }

    /// <summary>Times a shop ability or consumable was used this run.</summary>
    public int AbilitiesUsed { get; private set; }

    public bool Completed { get; private set; }
    public bool Failed { get; private set; }

    /// <summary>Moves left when the level was won, counted against the level's own limit (upgrades do not add to it). 0 if unlimited.</summary>
    public int MovesLeftAtFinish { get; private set; }

    /// <summary>The level was won with nothing left on the board.</summary>
    public bool BoardCleared { get; private set; }

    /// <summary>Blocks of each colour on the board when the level was won (indexed by <see cref="CellColour"/>).</summary>
    public IReadOnlyList<int> ColourCountsAtFinish => _colourCounts;
    private readonly int[] _colourCounts = new int[8];

    /// <summary>Starts watching an engine. Safe to call again for the next level: the run starts over.</summary>
    public void Attach(GameEngine engine)
    {
        Detach();
        _engine = engine;
        Reset();
        engine.WaveResolved += OnWave;
        engine.EventRaised += OnEvent;
    }

    public void Detach()
    {
        if (_engine is null) return;
        _engine.WaveResolved -= OnWave;
        _engine.EventRaised -= OnEvent;
        _engine = null;
    }

    /// <summary>Forgets the run so far (a restart of the same level).</summary>
    public void Reset()
    {
        LargestGroup = LongestChain = MostClearedByOnePiece = CellsPopped = Waves = PiecesPlaced = AbilitiesUsed = 0;
        MovesLeftAtFinish = 0;
        Completed = Failed = BoardCleared = false;
        _clearedByThisPiece = 0;
        _beforeLastLock = null;
        Array.Clear(_colourCounts);
    }

    public void RecordAbilityUsed() => AbilitiesUsed++;

    // Second Chance takes a placement back, so what that placement did must be taken back too (a group popped and then
    // undone does not count). The state just before each piece locked is kept; undo restores it. Helpers used stay used.
    private readonly record struct Checkpoint(
        int LargestGroup, int LongestChain, int MostClearedByOnePiece, int CellsPopped, int Waves, int PiecesPlaced);

    private Checkpoint? _beforeLastLock;

    private Checkpoint Capture() => new(LargestGroup, LongestChain, MostClearedByOnePiece, CellsPopped, Waves, PiecesPlaced);

    private void Restore(Checkpoint c)
    {
        LargestGroup = c.LargestGroup;
        LongestChain = c.LongestChain;
        MostClearedByOnePiece = c.MostClearedByOnePiece;
        CellsPopped = c.CellsPopped;
        Waves = c.Waves;
        PiecesPlaced = c.PiecesPlaced;
        _clearedByThisPiece = 0;
    }

    private void OnWave(BurstWave wave)
    {
        Waves++;
        LongestChain = Math.Max(LongestChain, wave.ChainIndex);
        CellsPopped += wave.ClearedCells.Count;
        _clearedByThisPiece += wave.ClearedCells.Count;
        MostClearedByOnePiece = Math.Max(MostClearedByOnePiece, _clearedByThisPiece);
        foreach (var g in wave.Groups) LargestGroup = Math.Max(LargestGroup, g.Size);
    }

    private void OnEvent(GameEvent e)
    {
        var engine = _engine;
        if (engine is null) return;

        switch (e)
        {
            case GameEvent.PieceLocked:
                _beforeLastLock = Capture();
                _clearedByThisPiece = 0;   // the next waves belong to the piece that has just locked
                PiecesPlaced++;
                break;

            case GameEvent.SecondChanceUsed:
                AbilitiesUsed++;
                if (_beforeLastLock is { } before) Restore(before);
                _beforeLastLock = null;
                break;

            case GameEvent.PrismShifted:
                AbilitiesUsed++;
                break;

            case GameEvent.LevelComplete:
                Completed = true;
                BoardCleared = engine.Board.CountOccupied() == 0;
                MovesLeftAtFinish = engine.Story is { MoveLimit: > 0 } s ? Math.Max(0, s.MoveLimit - engine.PiecesPlaced) : 0;
                CountColours(engine.Board);
                break;

            case GameEvent.LevelFailed:
                Failed = true;
                break;
        }
    }

    private void CountColours(Board board)
    {
        Array.Clear(_colourCounts);
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                int c = (int)board[x, y];
                if (c > 0 && c < _colourCounts.Length) _colourCounts[c]++;
            }
    }
}
