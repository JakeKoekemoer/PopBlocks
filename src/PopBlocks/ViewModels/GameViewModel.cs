using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.ViewModels;

/// <summary>
/// Bridges the engine to the UI: exposes observable state and commands, and drives the engine with a
/// dispatcher timer. Falling speed comes from the engine's interval, never from the frame rate, and
/// nothing accumulates while paused, so returning from the background applies no backlog of ticks.
///
/// Story mode adds the move counter, goals, tutorial coaching, level complete (which pays gold the first
/// time a level is cleared) and level failed. Classic keeps its score and personal best.
/// </summary>
public sealed class GameViewModel : ObservableObject
{
    private readonly GameEngine _game;
    private readonly HighScoreService _highScores;
    private readonly PlayerProgress _progress;
    private readonly ShopService _shop;
    private readonly GameSettings _settings;

    private IDispatcherTimer? _timer;
    private GameState _lastState = GameState.Ready;
    private int _bestAtStart;
    private GameSetup _setup = GameSetup.Classic;
    private bool _coachSeen;
    private bool _completionHandled;
    private TutorialController? _coach;
    private CompletionResult _result;
    private string _goalSignature = string.Empty;

    private int _score;
    private string _chainText = string.Empty;
    private int _bestScore;
    private bool _isPaused;
    private bool _isFailed;
    private bool _isComplete;
    private bool _isClassicOver;
    private bool _isNewBest;
    private IReadOnlyList<GoalState> _goals = Array.Empty<GoalState>();

    public GameViewModel(HighScoreService highScores, PlayerProgress progress, ShopService shop, GameSettings settings)
    {
        _highScores = highScores;
        _progress = progress;
        _shop = shop;
        _settings = settings;

        _game = new GameEngine(new PieceGenerator());
        _game.Changed += OnEngineChanged;
        _game.WaveResolved += wave => WaveResolved?.Invoke(wave);
        _game.EventRaised += OnGameEvent;

        PauseCommand = new Command(Pause);
        ResumeCommand = new Command(Resume);
        RestartCommand = new Command(NewGame);
        NextLevelCommand = new Command(GoToNextLevel);
        DismissCoachCommand = new Command(() => _coach?.Dismiss());
        SkipCoachCommand = new Command(() => _coach?.Skip());

        _bestScore = _highScores.GetBest(_setup.BestScoreKey);
    }

    public GameEngine Game => _game;
    public GameSetup Setup => _setup;
    public bool IsStory => _setup.IsStory;

    /// <summary>The level being played in Story mode (otherwise null).</summary>
    public LevelDefinition? Level => _game.Story;

    /// <summary>The board is drawn upside down on this level.</summary>
    public bool IsFlipped => _game.Story?.FlipView == true;

    /// <summary>Chooses the mode and level before the first game. Call before the page appears.</summary>
    public void Configure(GameSetup setup)
    {
        _setup = setup;
        _coachSeen = false;
        BestScore = _highScores.GetBest(_setup.BestScoreKey);
        RaiseHeader();
    }

    /// <summary>The view should repaint the board.</summary>
    public event Action? Redraw;

    /// <summary>A burst wave just resolved (for visual effects).</summary>
    public event Action<BurstWave>? WaveResolved;

    /// <summary>A new game (or level) has just started: the view should refresh anything that depends on the level.</summary>
    public event Action? GameStarted;

    /// <summary>A short message to flash over the board ("No pop! -15").</summary>
    public event Action<string>? Toast;

    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand NextLevelCommand { get; }
    public ICommand DismissCoachCommand { get; }
    public ICommand SkipCoachCommand { get; }

    // ---------------------------------------------------------------- bindable state: header

    public int Score { get => _score; private set { if (SetProperty(ref _score, value)) OnPropertyChanged(nameof(ScoreText)); } }
    public string ScoreText => _score.ToString("N0");

    public int BestScore { get => _bestScore; private set { if (SetProperty(ref _bestScore, value)) RaiseHeader(); } }

    /// <summary>"LEVEL 12 · EASY" or "CLASSIC".</summary>
    public string TitleText => _setup.IsStory
        ? $"LEVEL {_setup.Level} · {StoryConfig.Name(StoryConfig.TierFor(_setup.Level)).ToUpperInvariant()}"
        : "CLASSIC";

    /// <summary>Best score in Classic, gold in Story.</summary>
    public string SubText => _setup.IsStory ? $"{_progress.Gold:N0} gold" : $"BEST {_bestScore:N0}";

    public string ChainText { get => _chainText; private set { if (SetProperty(ref _chainText, value)) OnPropertyChanged(nameof(HasChain)); } }
    public bool HasChain => _chainText.Length > 0;

    public bool HasMoves => _game.HasMoveLimit;
    public string MovesText => _game.MovesLeft.ToString();

    public Color MovesColour => !_game.HasMoveLimit ? Colors.White
        : _game.MovesLeft <= 2 ? Color.FromArgb("#FB7185")
        : _game.MovesLeft <= 4 ? Color.FromArgb("#FBBF24")
        : Colors.White;

    public IReadOnlyList<GoalState> Goals { get => _goals; private set { _goals = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGoals)); } }
    public bool HasGoals => _goals.Count > 0;

    private void RaiseHeader()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubText));
        OnPropertyChanged(nameof(IsStory));
    }

    // ---------------------------------------------------------------- bindable state: overlays

    public bool IsPaused { get => _isPaused; private set { if (SetProperty(ref _isPaused, value)) UpdateOverlay(); } }
    public bool IsFailed { get => _isFailed; private set { if (SetProperty(ref _isFailed, value)) UpdateOverlay(); } }
    public bool IsComplete { get => _isComplete; private set { if (SetProperty(ref _isComplete, value)) UpdateOverlay(); } }
    public bool IsClassicOver { get => _isClassicOver; private set { if (SetProperty(ref _isClassicOver, value)) UpdateOverlay(); } }
    public bool IsNewBest { get => _isNewBest; private set => SetProperty(ref _isNewBest, value); }

    /// <summary>Retry button: failed Story level or finished Classic game.</summary>
    public bool CanRetry => _isFailed || _isClassicOver;
    public string RetryText => _isFailed ? "TRY AGAIN" : "PLAY AGAIN";

    public bool IsPlaying => _game.State is GameState.Playing or GameState.ResolvingBursts;
    public bool IsOverlayVisible => _isPaused || _isFailed || _isComplete || _isClassicOver;

    public string OverlayTitle => _isComplete ? "LEVEL COMPLETE"
        : _isFailed ? (_game.FailReason == FailReason.OutOfMoves ? "OUT OF MOVES" : "BOARD FULL")
        : _isClassicOver ? "GAME OVER"
        : "PAUSED";

    public string OverlayDetail
    {
        get
        {
            if (_isComplete) return $"Score {_score:N0}";
            if (_isFailed)
                return _game.FailReason == FailReason.OutOfMoves
                    ? "You ran out of pieces before the goals were met."
                    : "The blocks piled up to the top.";
            if (_isClassicOver)
                return _isNewBest ? $"New personal best!\n{_score:N0}" : $"Score {_score:N0}\nBest {_bestScore:N0}";
            return _setup.IsStory ? "Take a breather." : "Take a breather.";
        }
    }

    /// <summary>"+25 gold", shown when a level is cleared for the first time.</summary>
    public bool HasGoldReward => _isComplete && _result.GoldAwarded > 0;
    public string GoldRewardText => $"+{_result.GoldAwarded:N0} gold";

    /// <summary>Small note under the card explaining why no gold was paid.</summary>
    public string GoldNote => !_isComplete ? string.Empty
        : _result.GoldAwarded > 0 ? string.Empty
        : _game.Story?.IsTutorial == true ? "Tutorial levels don't pay gold."
        : "Gold is only paid the first time a level is cleared.";
    public bool HasGoldNote => GoldNote.Length > 0;

    private void UpdateOverlay()
    {
        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(OverlayTitle));
        OnPropertyChanged(nameof(OverlayDetail));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(RetryText));
        OnPropertyChanged(nameof(HasGoldReward));
        OnPropertyChanged(nameof(GoldRewardText));
        OnPropertyChanged(nameof(GoldNote));
        OnPropertyChanged(nameof(HasGoldNote));
    }

    // ---------------------------------------------------------------- bindable state: coaching

    public TutorialStep? CoachStep => _coach?.Current;
    public bool CoachVisible => CoachStep is not null;
    public string CoachTitle => CoachStep?.Title ?? string.Empty;
    public string CoachText => CoachStep?.Text ?? string.Empty;
    public bool CoachNeedsTap => CoachStep is { Mode: StepMode.Tap };
    public string CoachFooter => CoachStep?.Mode switch
    {
        StepMode.Do => "YOUR TURN",
        StepMode.Watch => "WATCH",
        _ => string.Empty,
    };
    public bool CoachHasFooter => CoachFooter.Length > 0;
    public bool CoachCanSkip => _coach is { IsActive: true };

    /// <summary>Column to pulse on the board while a Do step is waiting, or -1.</summary>
    public int HintColumn => CoachStep is { Mode: StepMode.Do, HighlightColumn: >= 0 } s ? s.HighlightColumn : -1;
    public int HintWidth => CoachStep?.HighlightWidth ?? 1;

    private bool InputBlocked => CoachStep is { Mode: StepMode.Tap };

    private void RaiseCoach()
    {
        OnPropertyChanged(nameof(CoachStep));
        OnPropertyChanged(nameof(CoachVisible));
        OnPropertyChanged(nameof(CoachTitle));
        OnPropertyChanged(nameof(CoachText));
        OnPropertyChanged(nameof(CoachNeedsTap));
        OnPropertyChanged(nameof(CoachFooter));
        OnPropertyChanged(nameof(CoachHasFooter));
        OnPropertyChanged(nameof(CoachCanSkip));
        OnPropertyChanged(nameof(HintColumn));
        OnPropertyChanged(nameof(HintWidth));
        Redraw?.Invoke();
    }

    // ---------------------------------------------------------------- lifecycle

    /// <summary>Creates the game timer. Call once the page has a dispatcher.</summary>
    public void Attach(IDispatcher dispatcher)
    {
        if (_timer is not null) return;
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) =>
        {
            // A tutorial card (or a "now do this" step) holds the piece still until the player acts.
            // The piece is allowed to step down out of the hidden spawn rows first, so it is on screen while it waits.
            if (_coach?.FreezesGame == true && _game.State == GameState.Playing && PieceIsVisible()) return;

            _game.Tick();
            // The interval changes with the level, with chain resolution and with each pop phase.
            if (_timer is not null && IsPlaying)
                _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, _game.TimerIntervalMs));
        };
    }

    private bool PieceIsVisible()
    {
        var piece = _game.CurrentPiece;
        if (piece is null) return true;
        foreach (var cell in piece.BoardCells())
            if (cell.Y < PopBlocks.Engine.GameConfig.HiddenRows) return false;
        return true;
    }

    /// <summary>Stops the timer and pauses. Safe to call repeatedly.</summary>
    public void Detach()
    {
        Pause();
        _timer?.Stop();
    }

    public void NewGame()
    {
        // Shop upgrades and settings apply from the start of each game.
        _game.PreviewCount = _shop.PreviewCount;
        _game.ExtraMoves = _shop.ExtraMoves;
        _game.Timing = _settings.BlastEffects
            ? new PopTiming(VisualDesign.HighlightMs, VisualDesign.SettleMs)
            : PopTiming.Instant;

        _bestAtStart = _highScores.GetBest(_setup.BestScoreKey);
        BestScore = _bestAtStart;
        IsNewBest = false;
        _completionHandled = false;
        _result = default;
        _goalSignature = string.Empty;
        _lastState = GameState.Ready;

        // Coaching: first time through a level that has steps, and only until it has been cleared.
        _coach = null;
        if (_setup.IsStory)
        {
            var level = LevelCatalog.Get(_setup.Level);
            if (level.Steps.Count > 0 && !_coachSeen && !_progress.IsCleared(level.Number))
            {
                _coach = new TutorialController(level.Steps);
                _coach.Changed += RaiseCoach;
            }
        }
        _coachSeen = true;
        RaiseCoach();

        _game.Start(_setup);

        RaiseHeader();
        UpdateOverlay();
        PrefetchNextLevel();
        GameStarted?.Invoke();
    }

    public void GoToNextLevel()
    {
        if (!_setup.IsStory) return;
        _setup = GameSetup.Story(_setup.Level + 1);
        _coachSeen = false;
        NewGame();
    }

    /// <summary>Generating a level takes a moment on the hardest tiers, so the next one is built in the background.</summary>
    private void PrefetchNextLevel()
    {
        if (!_setup.IsStory) return;
        int next = _setup.Level + 1;
        _ = Task.Run(() =>
        {
            try { LevelCatalog.Get(next); }
            catch { /* it will simply be built when it is needed */ }
        });
    }

    public void Pause() => _game.Pause();
    public void Resume() => _game.Resume();

    public void TogglePause()
    {
        if (_game.State == GameState.Paused) Resume();
        else Pause();
    }

    // ---------------------------------------------------------------- input (called by the page)
    // The page maps screen directions to these; on a flipped level it swaps left/right and up/down first.

    public void MoveLeft() { if (!InputBlocked) _game.MoveLeft(); }
    public void MoveRight() { if (!InputBlocked) _game.MoveRight(); }
    public void Rotate() { if (!InputBlocked) _game.Rotate(); }
    public void SoftDrop() { if (!InputBlocked) _game.SoftDrop(); }
    public void HardDrop() { if (!InputBlocked) _game.HardDrop(); }

    // ---------------------------------------------------------------- engine feedback

    private void OnGameEvent(GameEvent e)
    {
        _coach?.Notify(e);
        if (e == GameEvent.PenaltyApplied)
            Toast?.Invoke($"No pop!  −{_game.LastPenalty}");
    }

    private void OnEngineChanged()
    {
        Score = _game.Score;
        ChainText = _game.LastChain >= 2 ? $"CHAIN ×{ScoringSystem.Multiplier(_game.LastChain)}" : string.Empty;

        OnPropertyChanged(nameof(HasMoves));
        OnPropertyChanged(nameof(MovesText));
        OnPropertyChanged(nameof(MovesColour));
        UpdateGoals();

        if (!_setup.IsStory)
        {
            // Save the personal best the moment it is beaten, so an unexpected exit cannot lose it.
            if (_highScores.Submit(_setup.BestScoreKey, _game.Score))
                BestScore = _highScores.GetBest(_setup.BestScoreKey);
            IsNewBest = _highScores.GetBest(_setup.BestScoreKey) > _bestAtStart;
        }

        HandleCompletion();

        IsPaused = _game.State == GameState.Paused;
        IsFailed = _setup.IsStory && _game.State == GameState.GameOver;
        IsClassicOver = !_setup.IsStory && _game.State == GameState.GameOver;
        IsComplete = _game.State == GameState.LevelComplete;
        OnPropertyChanged(nameof(IsPlaying));

        if (_game.State != _lastState)
        {
            _lastState = _game.State;
            RearmTimer();
        }

        Redraw?.Invoke();
    }

    private void UpdateGoals()
    {
        var goals = _game.Goals;
        string signature = string.Join(",", goals.Select(g => g.Current));
        if (signature == _goalSignature && goals.Count == _goals.Count) return;
        _goalSignature = signature;
        Goals = goals;
    }

    /// <summary>Pays out and saves progress, once, the moment a level is cleared.</summary>
    private void HandleCompletion()
    {
        if (_game.State != GameState.LevelComplete || _completionHandled || _game.Story is null) return;
        _completionHandled = true;
        _result = _progress.Complete(_game.Story);
        RaiseHeader();
        UpdateOverlay();
    }

    private void RearmTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        if (IsPlaying)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, _game.TimerIntervalMs));
            _timer.Start();
        }
    }
}
