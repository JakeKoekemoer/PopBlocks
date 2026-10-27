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
    private RewindableGenerator? _rewindable;
    private GoalTracker? _goals;
    private int _extraMoves;
    private int _previewCount = 1;
    private Phase _phase = Phase.None;
    private GameState _stateBeforePause = GameState.Playing;

    // Undo: the state when the piece in play appeared, and when the one before it did. Going back restores the latter.
    private EngineSnapshot? _atSpawn;
    private EngineSnapshot? _beforeLast;

    // Helpers (see "Helpers" below). Stock is what the app carries in; the "Left" counts are for the level being played.
    private int _secondChanceStock, _prismShiftStock, _prismFreeCharges;
    private int _secondChanceLeft, _shiftPaidLeft, _shiftFreeLeft;

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

    /// <summary>
    /// Helpers carried into the next game (Story levels that allow them). Like <see cref="ExtraMoves"/> they are copied
    /// when a game starts, so buying one mid-level changes nothing. At most one Second Chance and one paid Prism Shift
    /// can be used per level, however many are in stock; free Prism Shifts (a shop upgrade) come on top.
    /// </summary>
    public int SecondChanceStock
    {
        get => _secondChanceStock;
        set => _secondChanceStock = Math.Max(0, value);
    }

    public int PrismShiftStock
    {
        get => _prismShiftStock;
        set => _prismShiftStock = Math.Max(0, value);
    }

    public int PrismFreeCharges
    {
        get => _prismFreeCharges;
        set => _prismFreeCharges = Math.Max(0, value);
    }

    /// <summary>How the app wants pops paced. Instant (the default) resolves a wave the moment a piece locks.</summary>
    public PopTiming Timing { get; set; } = PopTiming.Instant;

    public int Score { get; private set; }
    public int PiecesPlaced { get; private set; }

    /// <summary>Blocks popped so far this game.</summary>
    public int TotalPopped => _poppedClassic;
    private int _poppedClassic;

    /// <summary>
    /// Tenths of a gold earned so far in this Classic game (see <see cref="SeedingConfig.ClassicTenthsPerBlock"/>). Always 0
    /// in Story. The app pays it out through <c>ClassicGold</c>.
    /// </summary>
    public int ClassicGoldTenths { get; private set; }

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
        StartCore(setup, setup.Mode switch
        {
            GameMode.Story => LevelCatalog.Get(setup.Level, setup.Seed),
            GameMode.Tutorial => LevelCatalog.Lesson(setup.Level),
            _ => null,
        });

    /// <summary>Starts a game from an explicit level definition (previews, tests). A tutorial level starts as a lesson.</summary>
    public void StartLevel(LevelDefinition level) =>
        StartCore(level.IsTutorial ? GameSetup.Lesson(level.Number) : GameSetup.Story(level.Number, new LevelSeed(level.Seed)), level);

    private void StartCore(GameSetup setup, LevelDefinition? level)
    {
        Setup = setup;
        Board.Clear();
        Story = level;
        _goals = null;

        if (level is not null)
        {
            Board.CopyFrom(level.CreateBoard());
            _rewindable = new RewindableGenerator(level.CreateGenerator());
            _goals = new GoalTracker(level.Goals, Board.CountOccupied());
        }
        else
        {
            // A seeded Classic run draws its pieces from the seed; any other starts with the generator the engine was built with.
            IPieceGenerator source = setup.ClassicSeed is { } classicSeed ? new SeededPieceGenerator(classicSeed) : _defaultGenerator;
            _rewindable = new RewindableGenerator(source);
        }
        _generator = _rewindable;

        _atSpawn = null;
        _beforeLast = null;
        bool helpers = level is { AllowAbilities: true, IsTutorial: false };
        _secondChanceLeft = helpers && _secondChanceStock > 0 ? 1 : 0;
        _shiftPaidLeft = helpers && _prismShiftStock > 0 ? 1 : 0;
        _shiftFreeLeft = helpers ? _prismFreeCharges : 0;
        SecondChanceUsed = 0;
        PrismShiftsUsed = 0;
        LastShiftWasPaid = false;

        Score = 0;
        PiecesPlaced = 0;
        _poppedClassic = 0;
        ClassicGoldTenths = 0;
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

    /// <summary>
    /// Development tool: ends the current Story level as won, as if every goal had just been met, so the normal
    /// completion path (gold, story, rewards) runs. Returns false when there is no Story level in progress.
    /// </summary>
    public bool ForceComplete()
    {
        if (Story is null || State is GameState.Ready or GameState.LevelComplete) return false;

        Chain = 0;
        PendingGroups = NoGroups;
        _phase = Phase.None;
        CurrentPiece = null;
        State = GameState.LevelComplete;
        Raise(GameEvent.LevelComplete);
        Notify();
        return true;
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

        // Classic pays by the block, at the Classic level the pop happens at (before this wave's points are counted).
        // Every wave counts, chain waves included, with no chain multiplier. Drop points never count.
        if (Story is null) ClassicGoldTenths += cleared.Count * SeedingConfig.ClassicTenthsPerBlock(Level);

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

        _beforeLast = _atSpawn;
        _atSpawn = Capture(piece);
        // Nothing can rewind further back than the older of the two snapshots.
        _rewindable?.Trim(_beforeLast?.GeneratorPosition ?? _atSpawn.GeneratorPosition);
    }

    private EngineSnapshot Capture(Piece piece) => new(
        Board.Clone(), piece, _queue.ToArray(), Score, PiecesPlaced, _poppedClassic,
        LastChain, LastPenalty, LastWave, _goals?.Clone(), _rewindable?.Position ?? 0);

    // ------------------------------------------------------------------ helpers

    /// <summary>Second Chances used this level (0 or 1).</summary>
    public int SecondChanceUsed { get; private set; }

    /// <summary>Prism Shifts used this level, free ones included.</summary>
    public int PrismShiftsUsed { get; private set; }

    /// <summary>The most recent Prism Shift came out of the player's stock (not the free allowance), so the app should take one off.</summary>
    public bool LastShiftWasPaid { get; private set; }

    /// <summary>Second Chances still available this level (0 or 1).</summary>
    public int SecondChancesLeft => _secondChanceLeft;

    /// <summary>Prism Shifts still available this level.</summary>
    public int PrismShiftsLeft => _shiftFreeLeft + _shiftPaidLeft;

    /// <summary>Helpers are for Story levels that allow them: not tutorials, not finales, not Classic.</summary>
    public bool HelpersAllowed => Story is { AllowAbilities: true, IsTutorial: false };

    /// <summary>The piece in play can be swapped for the one before it and the last placement taken back (no charge needed).</summary>
    public bool CanRewind => State == GameState.Playing && CurrentPiece is not null && _beforeLast is not null;

    public AbilityStatus SecondChanceStatus =>
        !HelpersAllowed ? AbilityStatus.NotAllowed
        : _secondChanceLeft <= 0 ? AbilityStatus.NoCharges
        : State != GameState.Playing || CurrentPiece is null ? AbilityStatus.Busy
        : _beforeLast is null ? AbilityStatus.NothingToUndo
        : AbilityStatus.Ready;

    public AbilityStatus PrismShiftStatus =>
        !HelpersAllowed ? AbilityStatus.NotAllowed
        : PrismShiftsLeft <= 0 ? AbilityStatus.NoCharges
        : State != GameState.Playing || NextPiece is null ? AbilityStatus.Busy
        : NextPiece.DistinctColourOrders < 2 ? AbilityStatus.SingleColour
        : AbilityStatus.Ready;

    /// <summary>
    /// Second Chance: takes back the last placement. The board, score, goals, move count and pieces return to how they
    /// were when that piece appeared, and the same pieces come again (the level is never rerolled). One step only, and
    /// only while a piece is in play, never mid-pop. Uses up the level's charge; does not refund the move twice.
    /// </summary>
    public bool UseSecondChance()
    {
        if (SecondChanceStatus != AbilityStatus.Ready) return false;
        _secondChanceLeft--;
        SecondChanceUsed++;
        Rewind();
        Raise(GameEvent.SecondChanceUsed);
        Notify();
        return true;
    }

    /// <summary>The undo itself, without the helper rules (tests and tools). False when there is nothing to take back.</summary>
    public bool Rewind()
    {
        if (!CanRewind) return false;

        var snap = _beforeLast!;
        Board.CopyFrom(snap.Board);
        _queue.Clear();
        _queue.AddRange(snap.Queue);
        NextPiece = _queue[0];
        CurrentPiece = snap.Current;
        Score = snap.Score;
        PiecesPlaced = snap.PiecesPlaced;
        _poppedClassic = snap.PoppedBlocks;
        Chain = 0;
        LastChain = snap.LastChain;
        LastPenalty = snap.LastPenalty;
        LastWave = snap.LastWave;
        _goals = snap.Goals?.Clone();
        PendingGroups = NoGroups;
        _phase = Phase.None;
        _rewindable?.Rewind(snap.GeneratorPosition);

        // Only one step back: the restored state is now the newest, and nothing older is kept.
        _atSpawn = snap;
        _beforeLast = null;
        return true;
    }

    /// <summary>
    /// Prism Shift: deals the colours of the NEXT piece out again in a different order. The shape never changes and
    /// nothing else is touched. Each use moves to the next different colouring, so a piece with several can be cycled.
    /// Free charges (the Prism Reserve upgrade) are spent before stock.
    /// </summary>
    public bool ShiftNextPiece()
    {
        if (PrismShiftStatus != AbilityStatus.Ready) return false;

        var shifted = NextPiece!.WithNextColourOrder();
        if (shifted is null) return false;

        if (_shiftFreeLeft > 0) { _shiftFreeLeft--; LastShiftWasPaid = false; }
        else { _shiftPaidLeft--; LastShiftWasPaid = true; }
        PrismShiftsUsed++;

        _queue[0] = shifted;
        NextPiece = shifted;
        Raise(GameEvent.PrismShifted);
        Notify();
        return true;
    }

    private void Raise(GameEvent e) => EventRaised?.Invoke(e);

    private void Notify() => Changed?.Invoke();
}
