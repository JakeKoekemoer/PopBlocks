using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>How long the app wants each phase of a pop to last. Zero highlight time means "instant" (no phases).</summary>
public readonly record struct PopTiming(int HighlightMs, int SettleMs)
{
    public static PopTiming Instant => default;
    public bool IsInstant => HighlightMs <= 0;
}

/// <summary>
/// Owns the gameplay rules and state. It has no clock and no UI dependencies: the application layer
/// calls <see cref="Tick"/> on a timer, using <see cref="TimerIntervalMs"/> as the delay.
/// All operations run on a single thread (the UI thread).
/// </summary>
public sealed class GameEngine
{
    private enum Phase { None, Instant, Highlight, Settle }

    private static readonly IReadOnlyList<ColourGroup> NoGroups = Array.Empty<ColourGroup>();

    private readonly IPieceGenerator _defaultGenerator;
    private readonly List<Piece> _queue = new();
    private IPieceGenerator _generator;
    private GoalTracker? _goals;
    private int _extraMoves;
    private int _previewCount = 1;
    private Phase _phase = Phase.None;
    private GameState _stateBeforePause = GameState.Playing;

    public GameEngine(IPieceGenerator generator, Board? board = null)
    {
        _defaultGenerator = generator;
        _generator = generator;
        Board = board ?? new Board();
        State = GameState.Ready;
    }

    public Board Board { get; }
    public GameState State { get; private set; }

    /// <summary>The mode (and Story level) of the current game. Classic until a Story game is started.</summary>
    public GameSetup Setup { get; private set; } = GameSetup.Classic;

    /// <summary>The level being played in Story mode, otherwise null.</summary>
    public LevelDefinition? Story { get; private set; }

    public FailReason FailReason { get; private set; }

    /// <summary>The piece the player controls, or null between pieces and after the game ends.</summary>
    public Piece? CurrentPiece { get; private set; }

    /// <summary>The next piece (the first of <see cref="Upcoming"/>).</summary>
    public Piece? NextPiece { get; private set; }

    /// <summary>Pieces coming after the current one, soonest first. Holds <see cref="PreviewCount"/> pieces.</summary>
    public IReadOnlyList<Piece> Upcoming => _queue;

    /// <summary>How many upcoming pieces to keep visible (1-5). Takes effect from the next game.</summary>
    public int PreviewCount
    {
        get => _previewCount;
        set => _previewCount = Math.Clamp(value, 1, 5);
    }

    /// <summary>Bonus moves added to non-tutorial Story levels (a shop upgrade). Takes effect from the next game.</summary>
    public int ExtraMoves
    {
        get => _extraMoves;
        set => _extraMoves = Math.Max(0, value);
    }

    /// <summary>How the app wants pops paced. Instant (the default) resolves a wave the moment a piece locks.</summary>
    public PopTiming Timing { get; set; } = PopTiming.Instant;

    public int Score { get; private set; }
    public int PiecesPlaced { get; private set; }

    /// <summary>Blocks popped so far this game.</summary>
    public int TotalPopped => _poppedClassic;
    private int _poppedClassic;

    /// <summary>Burst waves so far in the current resolution; returns to 0 when the board stabilises.</summary>
    public int Chain { get; private set; }

    /// <summary>Length of the most recent chain (kept after it ends so the UI can show it).</summary>
    public int LastChain { get; private set; }

    public BurstWave? LastWave { get; private set; }

    /// <summary>Points taken off by the most recent lock (Extreme levels), else 0.</summary>
    public int LastPenalty { get; private set; }

    /// <summary>Groups that are about to pop. Only filled during the highlight phase of a timed pop.</summary>
    public IReadOnlyList<ColourGroup> PendingGroups { get; private set; } = NoGroups;

    public bool IsHighlighting => State == GameState.ResolvingBursts && _phase == Phase.Highlight;

    // ---- Story: moves and goals ----

    /// <summary>Pieces allowed in this Story level, shop bonus included. 0 when unlimited or not Story.</summary>
    public int MoveLimit => Story is { MoveLimit: > 0 } s ? s.MoveLimit + (s.IsTutorial ? 0 : _extraMoves) : 0;

    public bool HasMoveLimit => MoveLimit > 0;

    /// <summary>Pieces still available (not counting the one in play having been placed). Meaningful only when <see cref="HasMoveLimit"/>.</summary>
    public int MovesLeft => HasMoveLimit ? Math.Max(0, MoveLimit - PiecesPlaced) : 0;

    /// <summary>Progress of each goal against the settled board. Empty outside Story mode.</summary>
    public IReadOnlyList<GoalState> Goals =>
        _goals?.Evaluate(Score, Board.CountOccupied()) ?? Array.Empty<GoalState>();

    // ---- Classic level (speeds up with score) ----

    public int Level => ScoringSystem.LevelForScore(Score);

    /// <summary>Colours pieces may use: fixed by the level in Story mode, rising with the score in Classic.</summary>
    public int ColoursInPlay => Story?.Colours ?? ScoringSystem.ColourCountForLevel(Level);

    public int FallIntervalMs => Story?.FallIntervalMs ?? ScoringSystem.FallIntervalMs(Level);

    /// <summary>How long the application layer should wait before the next <see cref="Tick"/>.</summary>
    public int TimerIntervalMs => State != GameState.ResolvingBursts
        ? FallIntervalMs
        : _phase switch
        {
            Phase.Highlight => Timing.HighlightMs,
            Phase.Settle => Timing.SettleMs,
            _ => GameConfig.ResolveStepMs,
        };

    /// <summary>Raised after any change a view might need to redraw.</summary>
    public event Action? Changed;

    /// <summary>Raised once per burst wave, as the blocks are removed (for effects).</summary>
    public event Action<BurstWave>? WaveResolved;

    /// <summary>Raised for each <see cref="GameEvent"/> as it happens (tutorials, feedback).</summary>
    public event Action<GameEvent>? EventRaised;

    // ------------------------------------------------------------------ lifecycle

    /// <summary>Starts a fresh game with the current setup (Classic until another is given).</summary>
    public void Start() => Start(Setup);

    /// <summary>
    /// Starts a fresh game. Story mode loads the level's pile, pieces and goals; Classic starts empty
    /// with the generator the engine was built with.
    /// </summary>
    public void Start(GameSetup setup) =>
        StartCore(setup, setup.Mode == GameMode.Story ? LevelCatalog.Get(setup.Level) : null);

    /// <summary>Starts a Story game from an explicit level definition (previews, tests).</summary>
    public void StartLevel(LevelDefinition level) => StartCore(GameSetup.Story(level.Number), level);

    private void StartCore(GameSetup setup, LevelDefinition? level)
    {
        Setup = setup;
        Board.Clear();
        Story = level;
        _goals = null;

        if (level is not null)
        {
            Board.CopyFrom(level.CreateBoard());
            _generator = level.CreateGenerator();
            _goals = new GoalTracker(level.Goals, Board.CountOccupied());
        }
        else
        {
            _generator = _defaultGenerator;
        }

        Score = 0;
        PiecesPlaced = 0;
        _poppedClassic = 0;
        Chain = 0;
        LastChain = 0;
        LastPenalty = 0;
        LastWave = null;
        FailReason = FailReason.None;
        PendingGroups = NoGroups;
        _phase = Phase.None;
        CurrentPiece = null;
        NextPiece = null;

        _queue.Clear();
        FillQueue();
        SpawnNext();
        Notify();
    }

    /// <summary>Starts over with the same setup (the same level gives the same pile and pieces again).</summary>
    public void Restart()
    {
        if (Story is not null) StartLevel(Story);
        else Start(Setup);
    }

    public void Pause()
    {
        if (State != GameState.Playing && State != GameState.ResolvingBursts) return;
        _stateBeforePause = State;
        State = GameState.Paused;
        Notify();
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = _stateBeforePause;
        Notify();
    }

    // ------------------------------------------------------------------ timed advance

    /// <summary>
    /// Advances the game by one timed step: the piece falls one row (or locks), or the next phase of a
    /// pop plays. Does nothing while paused, ready, game over or level complete.
    /// </summary>
    public bool Tick()
    {
        switch (State)
        {
            case GameState.Playing when CurrentPiece is not null:
                if (CollisionDetector.CanMove(Board, CurrentPiece, 0, 1))
                    CurrentPiece = CurrentPiece.Moved(0, 1);
                else
                    LockPiece();
                Notify();
                return true;

            case GameState.ResolvingBursts:
                AdvanceResolution();
                Notify();
                return true;

            default:
                return false;
        }
    }

    // ------------------------------------------------------------------ player input

    public bool MoveLeft() => Shift(-1, 0, 0);
    public bool MoveRight() => Shift(1, 0, 0);

    /// <summary>Moves down one row for a small score bonus. Does not lock; the next tick does that.</summary>
    public bool SoftDrop() => Shift(0, 1, GameConfig.SoftDropPointsPerRow);

    public bool Rotate(bool clockwise = true)
    {
        if (State != GameState.Playing || CurrentPiece is null) return false;
        if (!CollisionDetector.TryRotate(Board, CurrentPiece, clockwise, out var rotated)) return false;
        CurrentPiece = rotated;
        Raise(GameEvent.Rotated);
        Notify();
        return true;
    }

    /// <summary>Drops the piece to its landing position and locks it immediately.</summary>
    public bool HardDrop()
    {
        if (State != GameState.Playing || CurrentPiece is null) return false;
        int distance = CollisionDetector.HardDropDistance(Board, CurrentPiece);
        CurrentPiece = CurrentPiece.Moved(0, distance);
        Score += distance * GameConfig.HardDropPointsPerRow;
        Raise(GameEvent.HardDropped);
        LockPiece();
        Notify();
        return true;
    }

    /// <summary>Row offset between the active piece and where it would land (0 if none).</summary>
    public int LandingDistance =>
        State == GameState.Playing && CurrentPiece is not null
            ? CollisionDetector.HardDropDistance(Board, CurrentPiece)
            : 0;

    // ------------------------------------------------------------------ internals

    private bool Shift(int dx, int dy, int points)
    {
        if (State != GameState.Playing || CurrentPiece is null) return false;
        if (!CollisionDetector.CanMove(Board, CurrentPiece, dx, dy)) return false;
        CurrentPiece = CurrentPiece.Moved(dx, dy);
        Score += points;
        Raise(dy > 0 ? GameEvent.SoftDropped : GameEvent.Moved);
        Notify();
        return true;
    }

    private void LockPiece()
    {
        var piece = CurrentPiece!;
        foreach (var c in piece.Cells)
            Board[piece.X + c.X, piece.Y + c.Y] = c.Colour;

        CurrentPiece = null;
        PiecesPlaced++;
        Chain = 0;
        LastChain = 0;
        LastPenalty = 0;
        Raise(GameEvent.PieceLocked);

        var groups = ColourMatcher.FindGroups(Board);
        if (groups.Count == 0)
        {
            ApplyPenalty();
            FinishResolution();
            return;
        }

        if (Timing.IsInstant)
        {
            // First wave resolves straight away; further waves (a chain) are paced by Tick.
            ApplyWave(groups);
            if (ColourMatcher.FindGroups(Board).Count > 0)
            {
                _phase = Phase.Instant;
                State = GameState.ResolvingBursts;
            }
            else
            {
                FinishResolution();
            }
        }
        else
        {
            BeginHighlight(groups);
        }
    }

    private void BeginHighlight(IReadOnlyList<ColourGroup> groups)
    {
        PendingGroups = groups;
        _phase = Phase.Highlight;
        State = GameState.ResolvingBursts;
    }

    /// <summary>Plays the next step of a pop. Timed pops go highlight, then pop, then settle; instant pops just pop.</summary>
    private void AdvanceResolution()
    {
        switch (_phase)
        {
            case Phase.Highlight:
                ApplyWave(PendingGroups);
                PendingGroups = NoGroups;
                _phase = Phase.Settle;
                break;

            case Phase.Settle:
            {
                var groups = ColourMatcher.FindGroups(Board);
                if (groups.Count == 0) FinishResolution();
                else BeginHighlight(groups);
                break;
            }

            default:
            {
                var groups = ColourMatcher.FindGroups(Board);
                if (groups.Count == 0)
                {
                    FinishResolution();
                    break;
                }
                ApplyWave(groups);
                if (ColourMatcher.FindGroups(Board).Count == 0) FinishResolution();
                break;
            }
        }
    }

    /// <summary>Clears the given groups together, applies gravity and scores the wave.</summary>
    private void ApplyWave(IReadOnlyList<ColourGroup> groups)
    {
        Chain++;
        LastChain = Chain;

        int multiplier = ScoringSystem.Multiplier(Chain);
        int points = ScoringSystem.WavePoints(groups.Select(g => g.Size), Chain);
        var cleared = groups.SelectMany(g => g.Cells).ToList();

        GravityResolver.Clear(Board, cleared);
        var moves = GravityResolver.Apply(Board);
        Score += points;
        _poppedClassic += cleared.Count;

        var wave = new BurstWave(Chain, multiplier, points, groups, cleared, moves);
        LastWave = wave;
        _goals?.OnWave(wave);
        WaveResolved?.Invoke(wave);
        Raise(GameEvent.Popped);
    }

    private void ApplyPenalty()
    {
        if (Story is not { NoPopPenalty: > 0 } s || Score <= 0) return;
        LastPenalty = Math.Min(Score, s.NoPopPenalty);
        Score -= LastPenalty;
        Raise(GameEvent.PenaltyApplied);
    }

    /// <summary>The board has settled after a lock: decide between won, lost, or the next piece.</summary>
    private void FinishResolution()
    {
        Chain = 0;
        PendingGroups = NoGroups;
        _phase = Phase.None;
        State = GameState.Playing;   // overwritten below; keeps State sane if nothing else applies

        if (_goals is not null && GoalTracker.AllComplete(Goals))
        {
            CurrentPiece = null;
            State = GameState.LevelComplete;
            Raise(GameEvent.LevelComplete);
            return;
        }

        // Anything left in the hidden spawn rows means the stack has overflowed the board.
        if (Board.AnyInRows(0, GameConfig.HiddenRows))
        {
            Fail(FailReason.StackOverflow);
            return;
        }

        if (HasMoveLimit && PiecesPlaced >= MoveLimit)
        {
            Fail(FailReason.OutOfMoves);
            return;
        }

        SpawnNext();
    }

    private void Fail(FailReason reason)
    {
        CurrentPiece = null;
        FailReason = reason;
        State = GameState.GameOver;
        if (Story is not null) Raise(GameEvent.LevelFailed);
    }

    private void FillQueue()
    {
        while (_queue.Count < _previewCount)
            _queue.Add(_generator.Next(ColoursInPlay));
    }

    private void SpawnNext()
    {
        var piece = _queue[0];
        _queue.RemoveAt(0);
        FillQueue();
        NextPiece = _queue[0];

        if (!CollisionDetector.IsValid(Board, piece))
        {
            Fail(FailReason.StackOverflow);
            return;
        }

        CurrentPiece = piece;
        State = GameState.Playing;
    }

    private void Raise(GameEvent e) => EventRaised?.Invoke(e);

    private void Notify() => Changed?.Invoke();
}
