using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

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

    public Board Board { get; private set; }

    // ---- The camera (tall boards) ----
    // The screen shows ViewportRows of the board. The camera sits so that the top of the stack is in the middle of the
    // screen, between the floor and the highest it can go. It is worked out only when the board has settled, just before a
    // piece spawns, never while a piece falls, and it depends on nothing but the board. On the standard 16-row board it is
    // always at the floor, so nothing ever scrolls.

    /// <summary>Playable rows of this board (the hidden spawn rows are extra).</summary>
    public int BoardRows => Board.Height - GameConfig.HiddenRows;

    /// <summary>How high the stack reaches, in rows up from the floor (0 for an empty board).</summary>
    public int StackTop
    {
        get
        {
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                    if (Board[x, y] != CellColour.Empty) return Board.Height - y;
            return 0;
        }
    }

    /// <summary>Rows between the floor and the bottom of the screen (0 when the screen is at the floor).</summary>
    public int CameraBottom { get; private set; }

    /// <summary>The highest the camera has been this game: above 0 means the board has scrolled.</summary>
    public int HighestCamera { get; private set; }

    /// <summary>The board row (y) at the top of the screen. Pieces spawn just above it.</summary>
    public int ViewTopRow => Board.Height - CameraBottom - GameConfig.ViewportRows;

    /// <summary>Camera rule: <c>clamp(stackTop - ViewportRows / 2, 0, Rows - ViewportRows)</c>.</summary>
    public static int CameraFor(int stackTop, int boardRows) =>
        Math.Clamp(stackTop - GameConfig.ViewportRows / 2, 0, Math.Max(0, boardRows - GameConfig.ViewportRows));

    private void UpdateCamera()
    {
        CameraBottom = CameraFor(StackTop, BoardRows);
        if (CameraBottom > HighestCamera) HighestCamera = CameraBottom;
    }
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
    /// Tenths of a gold earned by popping so far this game (see <see cref="SeedingConfig.ClassicTenthsPerBlock"/>). Endless
    /// and Story both earn it, at the same rate; lessons and eased levels earn none (<see cref="EarnsPopGold"/>). The app pays
    /// it out through <c>ClassicGold</c>: when the run ends in Endless, on a win in Story, and as half of it on a loss.
    /// </summary>
    public int ClassicGoldTenths { get; private set; }

    /// <summary>This game pays pop gold: Endless, and a normal Story level. Not a lesson, not a level the player eased.</summary>
    public bool EarnsPopGold => Story is null || (!Story.IsTutorial && !Story.Eased);

    /// <summary>How far the level's goals are met, 0 to 1: the average of each goal's progress. 0 for Endless.</summary>
    public double GoalProgress
    {
        get
        {
            var goals = Goals;
            if (goals.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < goals.Count; i++) sum += goals[i].Fraction;
            return Math.Clamp(sum / goals.Count, 0.0, 1.0);
        }
    }

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
    public int MoveLimit => Story is { MoveLimit: > 0 } s ? s.MoveLimit + (s.IsTutorial ? 0 : _extraMoves) + _continueMoves : 0;

    private int _continueMoves;   // moves bought with continues this attempt

    /// <summary>Continues bought in this attempt (0 to <see cref="ContinuePricing.MaxPerAttempt"/>). A new game, a retry or a reroll starts it again at 0.</summary>
    public int ContinuesUsed { get; private set; }

    /// <summary>
    /// The player may buy more moves: a normal Story level (not a lesson, a finale or a level the player eased) that was lost by
    /// running out of moves, with continues left. An overflow ends the attempt for good; there is nothing to buy.
    /// </summary>
    public bool CanContinue
    {
        get
        {
            if (State != GameState.GameOver || FailReason != FailReason.OutOfMoves) return false;
            if (Story is not { IsTutorial: false, Eased: false } story) return false;
            if (RegionCatalog.For(story.Number)?.LastLevel == story.Number) return false;   // a finale is played as it is
            return ContinuesUsed < ContinuePricing.MaxPerAttempt;
        }
    }

    /// <summary>
    /// Buys <paramref name="moves"/> more moves after running out. Only allowed when <see cref="CanContinue"/>. The game
    /// goes back to playing: the next piece is the next one from the same stream (nothing is skipped or rerolled) and an undo
    /// snapshot is taken as for any new piece. The app takes the gold; the engine only knows moves.
    /// </summary>
    public bool Continue(int moves = ContinuePricing.ExtraMoves)
    {
        if (!CanContinue || moves <= 0) return false;
        _continueMoves += moves;
        ContinuesUsed++;
        FailReason = FailReason.None;
        State = GameState.Playing;   // overwritten by SpawnNext if the next piece cannot be placed
        SpawnNext();
        Raise(GameEvent.Continued);
        Notify();
        return true;
    }

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

    /// <summary>The rules this game is played by. The normal rules unless the player set their own.</summary>
    public GameRules Rules => Setup.Rules;

    /// <summary>How long the falling piece waits between steps (the rules' fall speed applies to it, not to pops).</summary>
    public int FallIntervalMs => Rules.FallIntervalMs(Story?.FallIntervalMs ?? ScoringSystem.FallIntervalMs(Level));

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
            GameMode.Story => setup.Eased ? LevelCatalog.Get(setup.Level, setup.Seed).ToEased() : LevelCatalog.Get(setup.Level, setup.Seed),
            GameMode.Tutorial => LevelCatalog.Lesson(setup.Level),
            _ => null,
        });

    /// <summary>Starts a game from an explicit level definition (previews, tests). A tutorial level starts as a lesson.</summary>
    public void StartLevel(LevelDefinition level, GameRules? rules = null) =>
        StartCore(level.IsTutorial ? GameSetup.Lesson(level.Number)
            : GameSetup.Story(level.Number, new LevelSeed(level.Seed)) with { Eased = level.Eased, Rules = rules ?? GameRules.Default }, level);

    private void StartCore(GameSetup setup, LevelDefinition? level)
    {
        Setup = setup;
        _orbTimer = _piecesSinceOrb = _orbRolls = 0;
        _orbEver = _orbBrokeThisResolution = false;
        _orbSeed = Levels.SeedMix.Mix32(level?.PieceSeed ?? setup.ClassicSeed ?? 0, 0x0B0B);
        int wantedHeight = setup.Rules.BoardRows + GameConfig.HiddenRows;
        if (Board.Height != wantedHeight && RuleFeatures.TallBoards) Board = new Board(Board.Width, wantedHeight);
        Board.Clear();
        CameraBottom = 0;
        HighestCamera = 0;
        Story = level;
        _goals = null;

        if (level is not null)
        {
            // A goal that names a colour the player's rules switched off becomes "any colour": it could never be met otherwise.
            if (setup.Rules.RemapsColours && level.Goals.Any(g => g.Colour != CellColour.Empty && !setup.Rules.Allows(g.Colour)))
                level = level with { Goals = level.Goals.Select(g => g.Colour != CellColour.Empty && !setup.Rules.Allows(g.Colour) ? g with { Colour = CellColour.Empty } : g).ToList() };
            Story = level;
            var pile = level.CreateBoard();
            if (pile.Height == Board.Height) Board.CopyFrom(pile); else Board.CopyBottomFrom(pile);
            _rewindable = new RewindableGenerator(level.CreateGenerator(setup.Rules));
            _goals = new GoalTracker(level.Goals, Board.CountOccupied());
        }
        else
        {
            // A seeded Classic run draws its pieces from the seed; any other starts with the generator the engine was built with.
            IPieceGenerator source = setup.ClassicSeed is { } classicSeed ? new SeededPieceGenerator(classicSeed, setup.Rules)
                : setup.Rules.ChangesPieces ? new PieceGenerator(rules: setup.Rules)
                : _defaultGenerator;
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
        _continueMoves = 0;
        ContinuesUsed = 0;
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
        if (Story is not null) StartCore(Setup, Story);   // the same setup (eased, rules, seed) and the same level
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

        // A solid piece (one colour, burst-sized by itself) must not pop just by landing: it sleeps until something joins it.
        ColourMatcher.MarkDormant(Board, piece.Cells.Select(c => (piece.X + c.X, piece.Y + c.Y)), Rules.MinGroupSize);

        CurrentPiece = null;
        PiecesPlaced++;
        Chain = 0;
        LastChain = 0;
        LastPenalty = 0;
        Raise(GameEvent.PieceLocked);

        // An Orb counts pieces down; at zero it cracks the blocks round it.
        _orbBrokeThisResolution = false;
        if (OrbsActive)
        {
            _piecesSinceOrb++;
            if (OrbSystem.Find(Board) is not null && --_orbTimer <= 0) CorruptOrb();
        }

        var groups = ColourMatcher.FindGroups(Board, Rules.MinGroupSize);
        if (groups.Count == 0 && !(FractureActive && Board.AnyBroken()))
        {
            ApplyPenalty();
            FinishResolution();
            return;
        }

        if (Timing.IsInstant)
        {
            // First wave resolves straight away; further waves (a chain) are paced by Tick.
            ApplyWave(groups);
            if (MoreWaves())
            {
                _phase = Phase.Instant;
                State = GameState.ResolvingBursts;
            }
            else
            {
                FinishResolution();
            }
        }
        else if (groups.Count == 0)
        {
            // Nothing to highlight: only blocks that broke (an Orb's corruption). They go at once.
            ApplyWave(groups);
            if (MoreWaves()) { _phase = Phase.Settle; State = GameState.ResolvingBursts; }
            else FinishResolution();
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

    // ---- The Orb ----

    private int _orbTimer, _piecesSinceOrb, _orbRolls, _orbSeed;
    private bool _orbEver, _orbBrokeThisResolution;

    /// <summary>
    /// Orbs can appear in this game: Endless always (unless the rules say Never), the Story from level 40 once
    /// <see cref="RuleFeatures.OrbsInStory"/> is on, or on any level but a lesson when the rules say Always.
    /// </summary>
    public bool OrbsActive =>
        RuleFeatures.Orbs && Rules.Orbs != OrbMode.Never
        && (Rules.Orbs == OrbMode.Always
            ? Story is not { IsTutorial: true }
            : Story is null || (RuleFeatures.OrbsInStory && !Story.IsTutorial && Story.Number >= RegionRules.OrbIntro));

    /// <summary>The top-left cell of the Orb on the board, or null.</summary>
    public (int X, int Y)? OrbAt => OrbSystem.Find(Board);

    /// <summary>Pieces left before the Orb on the board cracks the blocks round it (0 when there is none).</summary>
    public int OrbTimerLeft => OrbSystem.Find(Board) is null ? 0 : _orbTimer;

    private bool NextOrbRoll()
    {
        double u = (Levels.SeedMix.Mix32(_orbSeed, ++_orbRolls) & 0xFFFFFF) / 16777216.0;
        return u < OrbSystem.SpawnChance;
    }

    /// <summary>
    /// Once per settle, before the next piece: an Orb may appear. It needs no Orb on the board and four pieces since the last
    /// one left (or since the level began); then a screen window with enough blocks, colours and lonely blocks; then a roll of
    /// 35% (always, when the rules say so). If none has appeared by the eighth piece, the next settle puts one at the best
    /// spot there is, thresholds or not. The roll comes from the engine's own seeded stream, never from the piece stream.
    /// </summary>
    private void MaybeSpawnOrb()
    {
        if (!OrbsActive || _orbBrokeThisResolution || OrbSystem.Find(Board) is not null) return;
        bool guarantee = !_orbEver && PiecesPlaced >= OrbSystem.GuaranteeByPiece;
        if (!guarantee && _piecesSinceOrb < OrbSystem.PiecesBetween) return;

        UpdateCamera();
        var spots = OrbSystem.Spots(Board, ViewTopRow, GameConfig.ViewportRows, relaxed: false);
        if (!guarantee)
        {
            if (spots.Count == 0) return;
            if (Rules.Orbs != OrbMode.Always && !NextOrbRoll()) return;
        }

        bool placed = false;
        foreach (var (x, y) in spots)
            if (placed = OrbSystem.TryPlace(Board, x, y)) break;
        if (!placed && guarantee)
            foreach (var (x, y) in OrbSystem.Spots(Board, ViewTopRow, GameConfig.ViewportRows, relaxed: true))
                if (placed = OrbSystem.TryPlace(Board, x, y)) break;
        if (!placed) return;

        GravityResolver.Apply(Board);   // what was pushed up settles on the Orb
        _orbTimer = Rules.OrbTimer;
        _orbEver = true;
        _piecesSinceOrb = 0;
        Raise(GameEvent.OrbSpawned);
    }

    /// <summary>
    /// Puts an Orb with its top-left cell at (x, y) right now, whatever the cooldown, the windows and the roll say (development
    /// tools and tests). Blocks are pushed up to make room, as for a real spawn. False if there is no room.
    /// </summary>
    public bool PlaceOrb(int x, int y, int? timer = null)
    {
        if (OrbSystem.Find(Board) is not null || !OrbSystem.TryPlace(Board, x, y)) return false;
        GravityResolver.Apply(Board);
        _orbTimer = timer ?? Rules.OrbTimer;
        _orbEver = true;
        _piecesSinceOrb = 0;
        Raise(GameEvent.OrbSpawned);
        Notify();
        return true;
    }

    /// <summary>Gives every block on the board one more crack, up to two (development tools).</summary>
    public void DevCrackAll()
    {
        for (int y = 0; y < Board.Height; y++)
            for (int x = 0; x < Board.Width; x++)
                if (Board[x, y] is not (CellColour.Empty or CellColour.Orb))
                    Board.SetFracture(x, y, Math.Min(2, Board.FractureAt(x, y) + 1));
        Notify();
    }

    /// <summary>Fills the bottom <paramref name="rows"/> rows (all but the last column) with blocks that never join, to reach the camera and the overflow (development tools).</summary>
    public void DevFillRows(int rows)
    {
        rows = Math.Clamp(rows, 0, BoardRows - 1);
        for (int r = 0; r < rows; r++)
            for (int x = 0; x < Board.Width - 1; x++)
            {
                int y = Board.Height - 1 - r;
                if (Board[x, y] == CellColour.Empty) Board[x, y] = (CellColour)(1 + (x + r * 2) % 3);
            }
        UpdateCamera();
        Notify();
    }

    /// <summary>Puts an Orb at the best spot there is, ignoring every condition (development tools).</summary>
    public bool SpawnOrbNow()
    {
        UpdateCamera();
        foreach (var (x, y) in OrbSystem.Spots(Board, ViewTopRow, GameConfig.ViewportRows, relaxed: true))
            if (PlaceOrb(x, y)) return true;
        return false;
    }

    /// <summary>The timer ran out: the twelve cells round the Orb take a crack each (they break at the third), the Orb goes, no gold.</summary>
    private void CorruptOrb()
    {
        if (OrbSystem.Find(Board) is not { } o) return;
        OrbSystem.Remove(Board, o.X, o.Y);
        if (FractureActive)
            foreach (var (x, y) in OrbSystem.Ring(o.X, o.Y))
                if (Board.IsInside(x, y) && Board[x, y] is not (CellColour.Empty or CellColour.Orb))
                    Board.SetFracture(x, y, Math.Min(Models.Board.Broken, Board.FractureAt(x, y) + 1));
        GravityResolver.Apply(Board);
        _piecesSinceOrb = 0;
        _orbBrokeThisResolution = true;
        Raise(GameEvent.OrbCorrupted);
    }

    /// <summary>Blocks crack in this game: the rules and the feature allow it, and (in the Story) the level is far enough in. Never in a lesson.</summary>
    public bool FractureActive =>
        RuleFeatures.Fracture && Rules.Fracture
        && (Story is null || (!Story.IsTutorial && Story.Number >= RegionRules.FractureIntro));

    /// <summary>Another wave is waiting: a burst-sized group, or a block that cracked a third time.</summary>
    private bool MoreWaves() => ColourMatcher.FindGroups(Board, Rules.MinGroupSize).Count > 0 || (FractureActive && Board.AnyBroken());

    /// <summary>Gold weight of a block by its cracks: whole when intact, three quarters with one crack, half with two, and half when it breaks.</summary>
    internal static double CrackGoldFactor(int cracks) => cracks switch { 0 => 1.0, 1 => 0.75, _ => 0.5 };

    /// <summary>The same weights, for the tests.</summary>
    public static double CrackGoldFactorForTests(int cracks) => CrackGoldFactor(cracks);

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
                var groups = ColourMatcher.FindGroups(Board, Rules.MinGroupSize);
                if (groups.Count > 0) BeginHighlight(groups);
                else if (FractureActive && Board.AnyBroken()) ApplyWave(NoGroups);   // a block that cracked a third time breaks now
                else FinishResolution();
                break;
            }

            default:
            {
                var groups = ColourMatcher.FindGroups(Board, Rules.MinGroupSize);
                if (groups.Count == 0 && !(FractureActive && Board.AnyBroken()))
                {
                    FinishResolution();
                    break;
                }
                ApplyWave(groups);
                if (!MoreWaves()) FinishResolution();
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

        // Blocks that cracked a third time in the wave before break now, with this one.
        bool fracture = FractureActive;
        var broken = fracture ? Board.TakeBrokenList() : new List<BlockCell>();
        var brokenGold = 0.0;
        if (broken.Count > 0)
        {
            var inGroups = new HashSet<(int, int)>(cleared.Select(c => (c.X, c.Y)));
            broken.RemoveAll(b => inGroups.Contains((b.X, b.Y)));   // one that is also part of a burst pops with it
            foreach (var b in broken)
            {
                cleared.Add(b);
                brokenGold += CrackGoldFactor(Board.Broken);
            }
        }

        // Pop gold is paid by the block, at the Endless level the pop happens at (before this wave's points are counted).
        // Every wave counts, chain waves included, with no chain multiplier. Drop points never count. Each group reports
        // what it paid and where it was, so the app can float the number over it.
        int rate = EarnsPopGold ? SeedingConfig.ClassicTenthsPerBlock(Level) : 0;
        bool scaled = Rules.GoldFactor < 1.0;   // easier rules pay half
        var payouts = new GroupGold[groups.Count + (broken.Count > 0 ? 1 : 0)];
        for (int g = 0; g < groups.Count; g++)
        {
            var cells = groups[g].Cells;
            float sx = 0, sy = 0;
            double weight = 0;   // a cracked block pays less (see CrackGoldFactor)
            for (int c = 0; c < cells.Count; c++)
            {
                sx += cells[c].X;
                sy += cells[c].Y;
                weight += fracture ? CrackGoldFactor(Board.FractureAt(cells[c].X, cells[c].Y)) : 1.0;
            }
            int tenths = (int)Math.Round(weight * rate, MidpointRounding.AwayFromZero);
            payouts[g] = new GroupGold(sx / cells.Count + 0.5f, sy / cells.Count + 0.5f, cells.Count, multiplier, scaled ? Rules.ScaleTenths(tenths) : tenths, groups[g].Colour);
            ClassicGoldTenths += payouts[g].GoldTenths;
        }
        if (broken.Count > 0)
        {
            float bx = 0, by = 0;
            foreach (var b in broken) { bx += b.X; by += b.Y; }
            int tenths = (int)Math.Round(brokenGold * rate, MidpointRounding.AwayFromZero);   // half pay for each broken block
            payouts[^1] = new GroupGold(bx / broken.Count + 0.5f, by / broken.Count + 0.5f, broken.Count, multiplier, scaled ? Rules.ScaleTenths(tenths) : tenths, broken[0].Colour);
            ClassicGoldTenths += payouts[^1].GoldTenths;
        }

        // An Orb touched by a pop (a group, or a block that broke) breaks: it takes every block in its two rows and two columns.
        var orbPopped = new List<BlockCell>();
        (int X, int Y)? orbAt = OrbSystem.Find(Board);
        if (orbAt is { } o && cleared.Any(c => (c.X >= o.X - 1 && c.X <= o.X + 2 && c.Y >= o.Y && c.Y <= o.Y + 1) || (c.Y >= o.Y - 1 && c.Y <= o.Y + 2 && c.X >= o.X && c.X <= o.X + 1)))
        {
            var already = new HashSet<(int, int)>(cleared.Select(c => (c.X, c.Y)));
            foreach (var c in OrbSystem.CrossCells(Board, o.X, o.Y))
                if (!already.Contains((c.X, c.Y))) orbPopped.Add(c);
            cleared.AddRange(orbPopped);
        }
        else
        {
            orbAt = null;
        }
        if (orbAt is { } hit)
        {
            double weight = 0;
            float ox = 0, oy = 0;
            foreach (var c in orbPopped) { weight += fracture ? CrackGoldFactor(Board.FractureAt(c.X, c.Y)) : 1.0; ox += c.X; oy += c.Y; }
            int tenths = (int)Math.Round(weight * rate, MidpointRounding.AwayFromZero) + (rate > 0 ? OrbSystem.BreakBonusGold * 10 : 0);   // the bonus pays whatever the rate
            float cxm = orbPopped.Count > 0 ? ox / orbPopped.Count + 0.5f : hit.X + 1f, cym = orbPopped.Count > 0 ? oy / orbPopped.Count + 0.5f : hit.Y + 1f;
            var list = payouts.ToList();
            list.Add(new GroupGold(cxm, cym, orbPopped.Count, multiplier, scaled ? Rules.ScaleTenths(tenths) : tenths, orbPopped.Count > 0 ? orbPopped[0].Colour : CellColour.Cyan));
            payouts = list.ToArray();
            ClassicGoldTenths += payouts[^1].GoldTenths;
        }

        GravityResolver.Clear(Board, cleared);
        if (orbAt is { } gone)
        {
            OrbSystem.Remove(Board, gone.X, gone.Y);
            _piecesSinceOrb = 0;
            _orbBrokeThisResolution = true;   // no new Orb in the resolution that broke one
        }
        var cracked = fracture ? CrackNeighbours(Board, cleared) : (IReadOnlyList<BlockCell>)Array.Empty<BlockCell>();
        var moves = GravityResolver.Apply(Board);
        Score += points;
        _poppedClassic += cleared.Count;

        var wave = new BurstWave(Chain, multiplier, points, groups, cleared, moves) { Gold = payouts, Broken = broken, Cracked = cracked, OrbPopped = orbPopped, OrbBroke = orbAt is not null, OrbAt = orbAt, OrbTimerAtBreak = _orbTimer };
        if (orbAt is not null) Raise(GameEvent.OrbBroken);
        LastWave = wave;
        _goals?.OnWave(wave);
        WaveResolved?.Invoke(wave);
        Raise(GameEvent.Popped);
    }

    /// <summary>
    /// After a wave has cleared its blocks: every surviving block with a cleared neighbour on two or more different sides gets one
    /// more crack (at most once per wave; corners and diagonals do not count). A third crack leaves the block waiting
    /// (<see cref="Board.Broken"/>) to break in the next step of the same resolution. Sleeping blocks crack like any other.
    /// </summary>
    internal static List<BlockCell> CrackNeighbours(Board board, List<BlockCell> cleared)
    {
        var result = new List<BlockCell>();
        int w = board.Width;
        var gone = new bool[w * board.Height];
        foreach (var c in cleared) gone[c.Y * w + c.X] = true;

        var hits = new List<(int X, int Y)>();
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < w; x++)
            {
                if (gone[y * w + x] || board[x, y] is CellColour.Empty or CellColour.Orb) continue;
                int sides = 0;
                if (x > 0 && gone[y * w + x - 1]) sides++;
                if (x < w - 1 && gone[y * w + x + 1]) sides++;
                if (y > 0 && gone[(y - 1) * w + x]) sides++;
                if (y < board.Height - 1 && gone[(y + 1) * w + x]) sides++;
                if (sides >= 2) hits.Add((x, y));
            }

        foreach (var (x, y) in hits)
        {
            int level = Math.Min(PopBlocks.Engine.Models.Board.Broken, board.FractureAt(x, y) + 1);
            board.SetFracture(x, y, level);
            result.Add(new BlockCell(x, y, board[x, y]));
        }
        return result;
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

        MaybeSpawnOrb();
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
            _queue.Add(NextFromGenerator());
    }

    /// <summary>
    /// The next piece from the generator. With the player's own colours the generator deals colours 1..n as usual and they are
    /// then mapped onto the enabled colours in order (so only n of them are dealt, never a disabled one). With the normal five
    /// the mapping is the identity and this is exactly the old call.
    /// </summary>
    private Piece NextFromGenerator()
    {
        var rules = Rules;
        if (!rules.RemapsColours) return _generator.Next(ColoursInPlay);

        var piece = _generator.Next(Math.Min(ColoursInPlay, rules.ColourCount));
        var cells = new BlockCell[piece.Cells.Count];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new BlockCell(piece.Cells[i].X, piece.Cells[i].Y, rules.MapColour(piece.Cells[i].Colour));
        return new Piece(cells, piece.X, piece.Y, piece.Pivot2X, piece.Pivot2Y);
    }

    private void SpawnNext()
    {
        UpdateCamera();   // the board has settled: decide what the screen shows before the next piece appears
        var piece = _queue[0];
        _queue.RemoveAt(0);
        FillQueue();
        NextPiece = _queue[0];
        int lift = ViewTopRow - GameConfig.HiddenRows;   // 0 on a standard board; on a tall one the piece appears at the top of the screen
        if (lift > 0) piece = piece.Moved(0, lift);

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
        LastChain, LastPenalty, LastWave, _goals?.Clone(), _rewindable?.Position ?? 0, ClassicGoldTenths,
        _orbTimer, _piecesSinceOrb, _orbRolls, _orbEver);

    // ------------------------------------------------------------------ helpers

    // ---- Pip's Insight ----

    /// <summary>Insight is not given in a lesson or on a region's finale (the helpers are off there too).</summary>
    public bool InsightAllowed => Story is null || (Story is { IsTutorial: false, AllowAbilities: true } s && RegionCatalog.For(s.Number)?.LastLevel != s.Number);

    /// <summary>A copy of what the advisor needs for the piece in hand, or null if there is no piece in play.</summary>
    public AdviceRequest? CreateAdviceRequest() =>
        CurrentPiece is { } piece && State == GameState.Playing
            ? new AdviceRequest(Board.Clone(), piece, _goals?.Clone(), Score, Rules.MinGroupSize)
            : null;

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
        UpdateCamera();
        _queue.Clear();
        _queue.AddRange(snap.Queue);
        NextPiece = _queue[0];
        CurrentPiece = snap.Current;
        Score = snap.Score;
        PiecesPlaced = snap.PiecesPlaced;
        _poppedClassic = snap.PoppedBlocks;
        ClassicGoldTenths = snap.GoldTenths;   // a rewound pop pays nothing, so undoing and popping again cannot farm gold
        _orbTimer = snap.OrbTimer;
        _piecesSinceOrb = snap.PiecesSinceOrb;
        _orbRolls = snap.OrbRolls;
        _orbEver = snap.OrbEver;
        _orbBrokeThisResolution = false;
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

        var shifted = NextShiftedPiece();
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

    /// <summary>
    /// The next colouring for Prism Shift. A shift never turns an ordinary piece into a solid one (a solid piece is a rare
    /// surprise from the generator, not something to dial in), so colourings that would be solid are skipped. Null when
    /// there is nothing to rearrange, or every other colouring would be solid.
    /// </summary>
    private Piece? NextShiftedPiece()
    {
        var next = NextPiece!;
        var shifted = next.WithNextColourOrder();
        if (shifted is null || next.IsSolidFor(Rules.MinGroupSize)) return shifted;

        // The last colouring wraps round to the current one, so there are DistinctColourOrders - 1 others to try.
        for (int tried = 1; tried < next.DistinctColourOrders - 1 && shifted!.IsSolidFor(Rules.MinGroupSize); tried++)
            shifted = shifted.WithNextColourOrder();
        return shifted!.IsSolidFor(Rules.MinGroupSize) ? null : shifted;
    }

    private void Raise(GameEvent e) => EventRaised?.Invoke(e);

    private void Notify() => Changed?.Invoke();
}
