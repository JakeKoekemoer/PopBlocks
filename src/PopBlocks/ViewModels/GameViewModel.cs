using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PopBlocks.Engine;
using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;
using PopBlocks.Graphics;

namespace PopBlocks.ViewModels;

/// <summary>
/// Bridges the engine to the UI: exposes observable state and commands, and drives the engine with a
/// dispatcher timer. Falling speed comes from the engine's interval, never from the frame rate, and
/// nothing accumulates while paused, so returning from the background applies no backlog of ticks.
///
/// Story mode adds the move counter, goals, level complete (which pays gold the first time a level is cleared)
/// and level failed. The tutorial lessons play like Story levels with coaching, but sit outside the campaign:
/// finishing one records only tutorial progress. Classic keeps its score and personal best.
/// </summary>
public sealed class GameViewModel : ObservableObject
{
    private readonly GameEngine _game;
    private readonly HighScoreService _highScores;
    private readonly PlayerProgress _progress;
    private readonly ShopService _shop;
    private readonly GameSettings _settings;
    private readonly GameAudio _audio;
    private readonly AudioSettings _audioSettings;
    private readonly CampaignService _campaign;
    private readonly CosmeticLoadout _loadout;
    private readonly DevTools _dev;
    private readonly LifetimeStats _lifetime;
    private readonly EndingService _ending;
    private readonly AchievementService _achievements;
    private readonly RunStats _run = new();
    private readonly ConsumableInventory _stock;
    private readonly TutorialProgress _tutorial;
    private readonly StoryLevels _levels;
    private readonly ClassicGold _classicGold;
    private readonly Consolation _consolation;
    private readonly GameRulesStore _rules;
    private int _popGoldPaid;                // Story: the pop gold banked on a win
    private ConsolationResult _lossPaid;     // Story: what a loss paid
    private bool _lossHandled;
    private int _chipHold;                   // gold already paid that the HUD chip has not counted up to yet
    private readonly UnlockStore _unlocks;
    private List<Achievement> _earned = new();
    private bool _checkAchievements;

    private IDispatcherTimer? _timer;
    private IDispatcher? _dispatcher;
    private IDispatcherTimer? _beatTimer;   // the hold after the last pop, before the result card fades in
    private bool _endBeat;                  // true while the board is being held
    private bool _beatStarted;              // the hold for this finish has begun (it runs once per finish)
    private bool _droppedHard;   // the lock that follows a hard drop gets the whoosh instead of the plain thud
    private GameState _lastState = GameState.Ready;
    private int _bestAtStart;
    private GameSetup _setup = GameSetup.Classic;
    private bool _coachSeen;
    private bool _promptOpen;   // level 1, first time: "want the tutorial?" is waiting for an answer
    private bool _completionHandled;
    private TutorialController? _coach;
    private CompletionResult _result;
    private string _goalSignature = string.Empty;
    private bool _runRecorded;
    private List<LoreEntry> _foundLore = new();

    // After the ending: the cosmetic choice of how the world looks (never changes what happened).
    private bool _choiceOpen;
    private string _choiceNote = string.Empty;

    // Story scene panel: the scenes waiting to be shown, the one on screen and which line it is on.
    private readonly Queue<StoryScene> _sceneQueue = new();
    private StoryScene? _scene;
    private int _sceneLine;
    private CampaignOutcome _outcome = CampaignOutcome.None;
    private bool _boardStyleApplied;

    private int _score;
    private string _chainText = string.Empty;
    private int _bestScore;
    private bool _isPaused;
    private bool _isFailed;
    private bool _isComplete;
    private bool _isClassicOver;
    private bool _isNewBest;
    private IReadOnlyList<GoalState> _goals = Array.Empty<GoalState>();

    public GameViewModel(HighScoreService highScores, PlayerProgress progress, ShopService shop, GameSettings settings, GameAudio audio, AudioSettings audioSettings,
        CampaignService campaign, CosmeticLoadout loadout, DevTools dev, LifetimeStats lifetime, EndingService ending,
        AchievementService achievements, ConsumableInventory stock, TutorialProgress tutorial,
        StoryLevels levels, ClassicGold classicGold, UnlockStore unlocks, Consolation consolation, GameRulesStore rules)
    {
        _rules = rules;
        _consolation = consolation;
        _levels = levels;
        _classicGold = classicGold;
        _unlocks = unlocks;
        _tutorial = tutorial;
        _stock = stock;
        _achievements = achievements;
        _dev = dev;
        _lifetime = lifetime;
        _ending = ending;
        _campaign = campaign;
        _loadout = loadout;
        _audio = audio;
        _audioSettings = audioSettings;
        _highScores = highScores;
        _progress = progress;
        _shop = shop;
        _settings = settings;

        _game = new GameEngine(new PieceGenerator());
        _game.Changed += OnEngineChanged;
        _game.WaveResolved += wave =>
        {
            _audio.Popped(wave);   // the pops sing in the key of the region, one colour at a time
            WaveResolved?.Invoke(wave);
        };
        _game.EventRaised += OnGameEvent;
        _run.Attach(_game);
        _stock.Attach(_game);   // takes one out of stock when the engine says a helper was used
        _stock.Changed += RaiseHelpers;

        PauseCommand = new Command(Pause);
        PreviousTrackCommand = new Command(() => StepTrack(-1));
        NextTrackCommand = new Command(() => StepTrack(+1));
        ResumeCommand = new Command(Resume);
        RestartCommand = new Command(NewGame);
        NextLevelCommand = new Command(GoToNextLevel);
        TutorialYesCommand = new Command(AcceptTutorial);
        TutorialNoCommand = new Command(DeclineTutorial);
        DismissCoachCommand = new Command(() => _coach?.Dismiss());
        SkipCoachCommand = new Command(() => _coach?.Skip());
        AdvanceSceneCommand = new Command(AdvanceScene);
        SkipSceneCommand = new Command(SkipScene);
        PreviousSceneCommand = new Command(PreviousScene);
        DevSkipLevelCommand = new Command(DevSkipLevel);
        DevGoldCommand = new Command(DevGold);
        ChooseLookCommand = new Command<string>(ChooseLook);
        ChoiceDoneCommand = new Command(FinishChoice);
        SecondChanceCommand = new Command(UseSecondChance);
        PrismShiftCommand = new Command(UsePrismShift);
        InsightCommand = new Command(() => RunInsight(countUse: true));
        InsightAlwaysCommand = new Command(() =>
        {
            _shop.InsightAlwaysOn = !_shop.InsightAlwaysOn;
            RaiseInsight();
            if (_shop.InsightAlwaysOn) RunInsight(countUse: false);
        });
        RerollCommand = new Command(Reroll);
        ToggleRerollHintCommand = new Command(() => { RerollHintOpen = !RerollHintOpen; });
        DismissLoadingCommand = new Command(DismissLoading);
        CopySeedCommand = new Command(CopySeed);
        EaseCommand = new Command(() => EaseRequested?.Invoke());
        UnlockNextCommand = new Command(UnlockNext);
        UnlockViewCommand = new Command(UnlockView);
        ShopCommand = new Command(() => ShopRequested?.Invoke());
        ShareCommand = new Command(() =>
        {
            _lifetime.AddShare();       // opening the share sheet is itself an achievement
            _achievements.CheckNow();
            ShareRequested?.Invoke(ShareMessage);
        });

        _levels.Changed += RaiseReroll;
        _progress.Changed += RaiseReroll;
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
        _seedPinned = setup.IsStory && !setup.Seed.IsLegacy;   // a seed typed into the dev menu is used as given
        _coachSeen = false;
        BestScore = _highScores.GetBest(_setup.BestScoreKey);
        RaiseHeader();
    }

    /// <summary>The view should repaint the board.</summary>
    public event Action? Redraw;

    /// <summary>A burst wave just resolved (for visual effects).</summary>
    public event Action<BurstWave>? WaveResolved;

    /// <summary>The Orb appeared, broke or ran out of time, and where (its top-left cell): the board draws the effect that goes with it.</summary>
    public event Action<GameEvent, int, int>? OrbHappened;

    /// <summary>A new game (or level) has just started: the view should refresh anything that depends on the level.</summary>
    public event Action? GameStarted;

    /// <summary>A short message to flash over the board ("No pop! -15").</summary>
    public event Action<string>? Toast;

    public ICommand PauseCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand NextLevelCommand { get; }
    public ICommand TutorialYesCommand { get; }
    public ICommand TutorialNoCommand { get; }
    public ICommand DismissCoachCommand { get; }
    public ICommand SkipCoachCommand { get; }
    public ICommand AdvanceSceneCommand { get; }
    public ICommand SkipSceneCommand { get; }
    public ICommand PreviousSceneCommand { get; }
    public ICommand DevSkipLevelCommand { get; }
    public ICommand DevGoldCommand { get; }
    public ICommand ChooseLookCommand { get; }
    public ICommand ChoiceDoneCommand { get; }
    public ICommand SecondChanceCommand { get; }
    public ICommand PrismShiftCommand { get; }
    public ICommand RerollCommand { get; }
    public ICommand ToggleRerollHintCommand { get; }
    public ICommand DismissLoadingCommand { get; }
    public ICommand CopySeedCommand { get; }
    public ICommand ShopCommand { get; }
    public ICommand InsightCommand { get; }
    public ICommand InsightAlwaysCommand { get; }
    public ICommand EaseCommand { get; }
    public ICommand UnlockNextCommand { get; }
    public ICommand UnlockViewCommand { get; }

    /// <summary>The player pressed EASE THIS LEVEL. The page asks them to confirm, then calls <see cref="ConfirmEase"/>.</summary>
    public event Action? EaseRequested;
    public ICommand ShareCommand { get; }

    /// <summary>The player asked for the workshop from a result card. The page decides how to get there.</summary>
    public event Action? ShopRequested;

    /// <summary>The player asked to share a result. Carries the text for the system share sheet.</summary>
    public event Action<string>? ShareRequested;

    /// <summary>The board is being held after the last pop. The page pulses the move counter (or flashes the top row).</summary>
    public event Action<EndBeatKind>? EndBeatStarted;

    // ---------------------------------------------------------------- helpers (Second Chance, Prism Shift)
    // A button shows only when the level allows helpers and the player brought one in. It is switched off, with a short
    // reason, when it cannot be used right now. Nothing here is ever needed to finish a level.

    private bool HelpersOnThisLevel => _setup.IsStory && _game.HelpersAllowed;

    public bool ShowSecondChance => HelpersOnThisLevel && (_game.SecondChancesLeft > 0 || _game.SecondChanceUsed > 0);
    public bool ShowPrismShift => HelpersOnThisLevel && (_game.PrismShiftsLeft > 0 || _game.PrismShiftsUsed > 0);
    public bool HelpersVisible => (ShowSecondChance || ShowPrismShift) && !SceneVisible && !_isComplete && !_isFailed;

    public bool CanSecondChance => _game.SecondChanceStatus == AbilityStatus.Ready;
    public bool CanPrismShift => _game.PrismShiftStatus == AbilityStatus.Ready;

    public string SecondChanceText => $"UNDO  ×{_game.SecondChancesLeft}";
    public string PrismShiftText => $"SHIFT  ×{_game.PrismShiftsLeft}";

    public string SecondChanceDescription =>
        $"Second Chance, takes back your last block. {_game.SecondChancesLeft} left this level. " + StatusText(_game.SecondChanceStatus, secondChance: true);
    public string PrismShiftDescription =>
        $"Prism Shift, rearranges the colours of the next piece. {_game.PrismShiftsLeft} left this level. " + StatusText(_game.PrismShiftStatus, secondChance: false);

    /// <summary>Why a helper cannot be used right now, when the player has just tried or is likely to wonder.</summary>
    public string HelperHint =>
        _game.PrismShiftStatus == AbilityStatus.SingleColour && ShowPrismShift ? "The next piece is one colour: nothing to rearrange."
        : _game.SecondChanceStatus == AbilityStatus.NothingToUndo && ShowSecondChance ? "Place a block first, then you can take it back."
        : string.Empty;

    public bool HasHelperHint => HelperHint.Length > 0 && HelpersVisible;

    private static string StatusText(AbilityStatus status, bool secondChance) => status switch
    {
        AbilityStatus.Ready => "Ready.",
        AbilityStatus.NoCharges => "None left.",
        AbilityStatus.Busy => "Not right now.",
        AbilityStatus.NothingToUndo => "Nothing to take back yet.",
        AbilityStatus.SingleColour => "The next piece is one colour.",
        _ => secondChance ? "Not on this level." : "Not on this level.",
    };

    private void UseSecondChance()
    {
        if (!_game.UseSecondChance()) return;
        Toast?.Invoke("Second Chance");
        RaiseHelpers();
    }

    private void UsePrismShift()
    {
        if (!_game.ShiftNextPiece()) return;
        Toast?.Invoke("Prism Shift");
        RaiseHelpers();
    }

    private void RaiseHelpers()
    {
        OnPropertyChanged(nameof(ShowSecondChance));
        OnPropertyChanged(nameof(ShowPrismShift));
        OnPropertyChanged(nameof(HelpersVisible));
        OnPropertyChanged(nameof(CanSecondChance));
        OnPropertyChanged(nameof(CanPrismShift));
        OnPropertyChanged(nameof(SecondChanceText));
        OnPropertyChanged(nameof(PrismShiftText));
        OnPropertyChanged(nameof(SecondChanceBadge));
        OnPropertyChanged(nameof(PrismShiftBadge));
        OnPropertyChanged(nameof(HasAbilities));
        OnPropertyChanged(nameof(SecondChanceDescription));
        OnPropertyChanged(nameof(PrismShiftDescription));
        OnPropertyChanged(nameof(HelperHint));
        OnPropertyChanged(nameof(HasHelperHint));
    }

    /// <summary>Dev shortcuts on the pause / failed card. False in normal Release builds.</summary>
    public bool ShowDevTools => DevMenu.Enabled && _setup.HasLevel && (_isPaused || _isFailed);

    /// <summary>The board tools (spawn an Orb, crack every block, fill rows) work in Endless too, from the pause card. Development builds only.</summary>
    public bool ShowDevBoardTools => DevMenu.Enabled && _isPaused;

    public ICommand DevSpawnOrbCommand => new Command(() => { _game.SpawnOrbNow(); Redraw?.Invoke(); });
    public ICommand DevCrackAllCommand => new Command(() => { _game.DevCrackAll(); Redraw?.Invoke(); });
    public ICommand DevFillRowsCommand => new Command<string>(n => { if (int.TryParse(n, out int rows)) _game.DevFillRows(rows); Redraw?.Invoke(); });

    /// <summary>Dev: wins the current Story level now; the normal completion path (gold, story, rewards) then runs.</summary>
    private void DevSkipLevel()
    {
        if (!DevMenu.Enabled) return;
        _game.ForceComplete();
    }

    private void DevGold()
    {
        if (!DevMenu.Enabled) return;
        _dev.AddGold(1000);
        RaiseHeader();
        Toast?.Invoke("+1,000 gold");
    }

    /// <summary>Pip has something short to say (a first-burst or first-chain remark). Shown as a banner, not a panel.</summary>
    public event Action<StoryScene>? Banner;

    /// <summary>Id of the board style to draw (cosmetic): "classic" or an unlocked style such as "garden".</summary>
    public string BoardStyle => _loadout.BoardFor(_setup.IsStory ? _setup.Level : 0);

    // ---------------------------------------------------------------- bindable state: header

    public int Score { get => _score; private set { if (SetProperty(ref _score, value)) OnPropertyChanged(nameof(ScoreText)); } }
    public string ScoreText => _score.ToString("N0");

    public int BestScore { get => _bestScore; private set { if (SetProperty(ref _bestScore, value)) RaiseHeader(); } }

    /// <summary>"L-48", "LESSON 2 OF 5" or "ENDLESS".</summary>
    public string TitleText => _setup.IsLesson ? $"LESSON {_setup.Level} OF {StoryConfig.LessonCount}"
        : _setup.IsStory ? $"L-{_setup.Level}"
        : "ENDLESS";

    // ---- the play clock: how long this attempt has been played (paused, scenes, loading and the end hold do not count) ----

    private IDispatcherTimer? _clockTimer;
    private long _clockLastMs;
    private long _playMs;
    private int _shownSeconds = -1;

    /// <summary>"3:07", or "1:02:09" past an hour.</summary>
    public string PlayTimeText
    {
        get
        {
            var t = TimeSpan.FromMilliseconds(_playMs);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }
    }

    private void StartClock(IDispatcher dispatcher)
    {
        if (_clockTimer is null)
        {
            _clockTimer = dispatcher.CreateTimer();
            _clockTimer.Interval = TimeSpan.FromMilliseconds(500);
            _clockTimer.IsRepeating = true;
            _clockTimer.Tick += (_, _) => TickClock();
        }
        _clockLastMs = Environment.TickCount64;
        if (!_clockTimer.IsRunning) _clockTimer.Start();
    }

    private void TickClock()
    {
        long now = Environment.TickCount64;
        long delta = Math.Min(now - _clockLastMs, 1000);   // a long gap (the phone slept) is not play
        _clockLastMs = now;
        if (delta <= 0) return;
        bool playing = _game.State is GameState.Playing or GameState.ResolvingBursts
                       && !SceneVisible && !_promptOpen && !_isLoading && !_endBeat && _coach?.FreezesGame != true;
        if (!playing) return;
        _playMs += delta;
        int seconds = (int)(_playMs / 1000);
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;
        OnPropertyChanged(nameof(PlayTimeText));
    }

    private void ResetClock()
    {
        _playMs = 0;
        _shownSeconds = -1;
        _clockLastMs = Environment.TickCount64;
        OnPropertyChanged(nameof(PlayTimeText));
    }

    /// <summary>Best score in Endless, gold in Story, "TUTORIAL" in a lesson.</summary>
    public string SubText => _setup.IsLesson ? "TUTORIAL" : _setup.IsStory ? $"{Math.Max(0, _progress.Gold - _chipHold):N0} gold" : $"BEST {_bestScore:N0}  ·  {RunGoldText}";

    public string ChainText { get => _chainText; private set { if (SetProperty(ref _chainText, value)) OnPropertyChanged(nameof(HasChain)); } }
    public bool HasChain => _chainText.Length > 0;

    public bool HasMoves => _game.HasMoveLimit && !_isLoading;
    public string MovesText => _game.MovesLeft.ToString();

    public Color MovesColour => !_game.HasMoveLimit ? Colors.White
        : _game.MovesLeft <= 2 ? Color.FromArgb("#FB7185")
        : _game.MovesLeft <= 4 ? Color.FromArgb("#FBBF24")
        : Colors.White;

    public IReadOnlyList<GoalState> Goals { get => _goals; private set { _goals = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGoals)); } }
    public bool HasGoals => _goals.Count > 0;

    // ---------------------------------------------------------------- bindable state: optional memory objective

    private BonusObjective? Bonus => _isLoading ? null : _game.Story?.Bonus;

    /// <summary>This level carries an optional objective that uncovers a memory.</summary>
    public bool HasBonus => Bonus is not null;

    public string BonusText => Bonus is { } b ? "Optional: " + b.Describe() : string.Empty;

    private BonusState? BonusNow => Bonus?.Evaluate(_game, _run);

    public string BonusProgress => BonusNow?.Progress ?? string.Empty;
    public bool BonusMet => BonusNow?.Met == true;

    private void RaiseBonus()
    {
        OnPropertyChanged(nameof(HasBonus));
        OnPropertyChanged(nameof(BonusText));
        OnPropertyChanged(nameof(BonusProgress));
        OnPropertyChanged(nameof(BonusMet));
    }

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
    public bool IsOverlayVisible => !_endBeat && !UnlockPopupVisible && (_isPaused || _isFailed || _isComplete || _isClassicOver);

    public string OverlayTitle => _isComplete ? (_setup.IsLesson ? "LESSON COMPLETE" : $"LEVEL {_setup.Level} COMPLETE")
        : _isFailed ? (_setup.IsLesson ? FailReasonText : $"LEVEL {_setup.Level}")
        : _isClassicOver ? "GAME OVER"
        : "PAUSED";

    /// <summary>On a lost Story level: why, on a line of its own under "LEVEL 48". Empty everywhere else (a lesson puts the reason in its title).</summary>
    public string OverlayReason => _isFailed && !_setup.IsLesson ? FailReasonText : string.Empty;
    public bool HasOverlayReason => OverlayReason.Length > 0;

    private string FailReasonText => _game.FailReason == FailReason.OutOfMoves
        ? "OUT OF MOVES"
        : _setup.IsLesson ? "BOARD FULL" : "STACK REACHED THE TOP";

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

    // ---- help after losing (v1.1) ----

    /// <summary>
    /// "There is always a way through": from the fifth loss on a level, on the failed card. Only while the level is the
    /// solved, normal one: an eased level, a lesson, and (later) a level played on the player's own rules do not get it.
    /// </summary>
    public bool ShowSolvableNote => _isFailed && _setup.IsStory && !_setup.Eased && _setup.Rules.IsDefault && _levels.ShowsSolvableNote(_setup.Level);

    /// <summary>"Custom rules" (or "Custom rules · half gold") on the HUD while the player's own rules are in force.</summary>
    public bool HasRulesChip => !_setup.Rules.IsDefault;
    public string RulesChipText => _setup.Rules.IsEasierThanDefault ? "Custom rules · half gold" : "Custom rules";

    /// <summary>EASE THIS LEVEL: from the fifteenth loss, on the current level only (never a finale).</summary>
    public bool ShowEase => _isFailed && _setup.IsStory && _levels.CanEase(_setup.Level);

    public string EaseNoteText => "Want an easier version of this level? You'll still move on in the story, but this level won't pay any gold.";

    /// <summary>The player confirmed: swap in an easier version of the level (more moves, one band lower, no gold).</summary>
    public void ConfirmEase()
    {
        if (!_setup.IsStory || _isLoading || !_levels.TryEase(_setup.Level)) return;
        _failureCounted = true;   // leaving the run to ease it is the player's choice, not another loss
        _seedPinned = false;
        RerollHintOpen = false;
        _setup = GameSetup.Story(_setup.Level);
        NewGame();
    }

    /// <summary>SHOP beside NEXT LEVEL on a cleared level, under TRY AGAIN on a failed one. Lessons have no shop button.</summary>
    public bool ShowShop => _isComplete && !_setup.IsLesson;
    public bool ShowShopOnFailed => _isFailed && !_setup.IsLesson;

    /// <summary>The share button: on a cleared Story level and on the Endless game-over card.</summary>
    public bool ShowShare => (_isComplete && _setup.IsStory) || _isClassicOver;

    private string ShareMessage => _isClassicOver
        ? ShareText.Build(new ShareContext(ShareKind.Endless, Score: _score, BestChain: _run.LongestChain))
        : ShareText.Build(new ShareContext(ShareKind.StoryWin, Level: _setup.Level,
            Region: RegionCatalog.For(_setup.Level)?.Title ?? string.Empty, MovesLeft: _game.MovesLeft));

    /// <summary>"+25 gold", shown when a level is cleared for the first time.</summary>
    public bool HasGoldReward => _isComplete && _result.GoldAwarded > 0;
    public string GoldRewardText => $"+{_result.GoldAwarded:N0} gold";
    public int GoldRewardValue => _result.GoldAwarded;

    // ---- pop gold and consolation (v1.1) ----

    /// <summary>The pop gold of a won Story level, banked with the win (shown on its own line next to the first-clear gold).</summary>
    public bool HasPopGold => _isComplete && _popGoldPaid > 0;
    public int PopGoldValue => _popGoldPaid;
    public string PopGoldText => $"Pop gold  +{_popGoldPaid:N0}";

    /// <summary>A lost Story level keeps half the pop gold and part of the first-clear reward; with none of either it says so.</summary>
    public bool ShowConsolation => _isFailed && _setup.IsStory && !_setup.Eased;
    public int ConsolationValue => _lossPaid.Total;
    public string ConsolationText => _lossPaid.Total > 0 ? "Kept half your gold" : "No gold this time";

    /// <summary>The HUD gold chip counts up to the gold just paid instead of jumping: the page releases it as coins land.</summary>
    public void SetChipHold(int remaining)
    {
        int hold = Math.Min(remaining, Math.Max(0, _progress.Gold));   // a negative hold shows the chip above the real gold: it counts down after a purchase
        if (hold == _chipHold) return;
        _chipHold = hold;
        OnPropertyChanged(nameof(SubText));
    }

    // ---- Pip's Insight (v1.1) ----
    // A button below the board (and, at the top level, an Always on switch). It asks the advisor where the piece in hand
    // would do the most good and the board draws that spot. It never plays the move. Off in lessons and finales.

    private PlacementSuggestion? _insight;
    private int _insightIndex = -1;      // the piece (by how many have been placed) the suggestion is for
    private int _insightUses;            // uses spent on this level
    private bool _insightUsedInRun;      // Insight was shown this run: an Endless run like that never sets a best score
    private int _insightRequest;

    /// <summary>The suggestion changed (or was taken away). The page hands it to the board to draw.</summary>
    public event Action<PlacementSuggestion?>? InsightChanged;

    public bool InsightRowVisible => _shop.InsightLevel > 0 && _game.InsightAllowed && !_isComplete && !_isFailed && !SceneVisible;
    public bool AlwaysOnActive => _shop.InsightAlwaysOn && _game.InsightAllowed;
    public bool ShowInsightButton => InsightRowVisible && !AlwaysOnActive;
    public bool ShowAlwaysOn => InsightRowVisible && _shop.InsightLevel >= 3;
    public int InsightUsesLeft => _shop.InsightUsesPerLevel == int.MaxValue ? int.MaxValue : Math.Max(0, _shop.InsightUsesPerLevel - _insightUses);
    public bool CanInsight => ShowInsightButton && InsightUsesLeft > 0 && _game.State == GameState.Playing;
    public string InsightText => InsightUsesLeft == int.MaxValue ? "INSIGHT" : $"INSIGHT · {InsightUsesLeft} LEFT";
    public string AlwaysOnText => _shop.InsightAlwaysOn ? "ALWAYS ON: ON" : "ALWAYS ON: OFF";

    // The ability keys at the side of the board show a count instead of words.
    public string InsightBadge => InsightUsesLeft == int.MaxValue ? "∞" : InsightUsesLeft.ToString();
    public string SecondChanceBadge => _game.SecondChancesLeft.ToString();
    public string PrismShiftBadge => _game.PrismShiftsLeft.ToString();
    public bool AlwaysOnIsOn => _shop.InsightAlwaysOn;

    /// <summary>The rail beside the board has abilities to show (the NEXT pieces are always there).</summary>
    public bool HasAbilities => ShowSecondChance || ShowPrismShift || InsightRowVisible;

    private void RaiseInsight()
    {
        OnPropertyChanged(nameof(InsightRowVisible));
        OnPropertyChanged(nameof(AlwaysOnActive));
        OnPropertyChanged(nameof(ShowInsightButton));
        OnPropertyChanged(nameof(ShowAlwaysOn));
        OnPropertyChanged(nameof(InsightUsesLeft));
        OnPropertyChanged(nameof(CanInsight));
        OnPropertyChanged(nameof(InsightText));
        OnPropertyChanged(nameof(AlwaysOnText));
        OnPropertyChanged(nameof(InsightBadge));
        OnPropertyChanged(nameof(AlwaysOnIsOn));
        OnPropertyChanged(nameof(HasAbilities));
    }

    /// <summary>Asks the advisor (off the UI thread) where the piece in hand should go. A suggestion already made for this piece is shown again for free.</summary>
    private async void RunInsight(bool countUse)
    {
        if (!_game.InsightAllowed || _shop.InsightLevel == 0 || _game.State != GameState.Playing) return;
        int index = _game.PiecesPlaced;

        if (_insight is not null && _insightIndex == index)
        {
            InsightChanged?.Invoke(_insight);   // already worked out for this piece: show it again, no use spent
            return;
        }
        if (countUse && InsightUsesLeft <= 0) return;

        var request = _game.CreateAdviceRequest();
        if (request is null) return;
        int ticket = ++_insightRequest;
        var result = await Task.Run(() => PlacementAdvisor.Best(request));
        if (ticket != _insightRequest || _game.PiecesPlaced != index || _game.State != GameState.Playing) return;   // stale: the piece moved on

        _insight = result;
        _insightIndex = index;
        if (countUse && _shop.InsightUsesPerLevel != int.MaxValue) _insightUses++;
        _insightUsedInRun = true;
        RaiseInsight();
        InsightChanged?.Invoke(result);
    }

    /// <summary>A new piece has appeared: the old suggestion goes, and the new one is made at once if Always on is set.</summary>
    private void OnPieceChangedForInsight()
    {
        if (_insightIndex == _game.PiecesPlaced && _insight is not null) return;
        if (_insight is not null)
        {
            _insight = null;
            InsightChanged?.Invoke(null);
        }
        RaiseInsight();
        if (AlwaysOnActive && _game.State == GameState.Playing) RunInsight(countUse: false);
    }

    // ---- continues (v1.1) ----

    /// <summary>Five more moves for gold, on a Story level lost by running out of moves (not a finale, not an eased level).</summary>
    public bool ShowContinue => _isFailed && _setup.IsStory && _game.CanContinue;
    public int ContinuePrice => ContinuePricing.Price(_game.ContinuesUsed + 1);
    public bool CanAffordContinue => ShowContinue && _progress.Gold >= ContinuePrice;
    public string ContinueText => $"+{ContinuePricing.ExtraMoves} MOVES · {ContinuePrice} GOLD";
    public bool ShowContinueHint => ShowContinue && !CanAffordContinue;
    public string ContinueHintText => "Earn gold in Endless mode";
    public int ContinuesLeft => ContinuePricing.MaxPerAttempt - _game.ContinuesUsed;
    public string ContinuesLeftText => $"{ContinuesLeft} of {ContinuePricing.MaxPerAttempt} continues left";

    /// <summary>
    /// Buys the continue: takes the gold once, then gives five more moves. Nothing is taken if the engine will not continue.
    /// The loss is not paid out again; if the level is then lost once more, only what is new is paid.
    /// </summary>
    public bool BuyContinue()
    {
        if (!ShowContinue || _buyingContinue) return false;
        _buyingContinue = true;
        try
        {
            int price = ContinuePrice;
            if (price <= 0 || !_progress.TrySpend(price)) return false;
            if (!_game.Continue(ContinuePricing.ExtraMoves))
            {
                _progress.AwardGold(price);
                return false;
            }

            _lossHandled = false;   // a second loss pays only the difference
            _chipHold = -price;     // the chip shows the gold from before and counts down
            OnPropertyChanged(nameof(SubText));
            return true;
        }
        finally
        {
            _buyingContinue = false;
        }
    }

    private bool _buyingContinue;

    /// <summary>One tick of the count-up (a coin sound).</summary>
    public void PlayCoinTick() => _audio.Ui(UiSound.Coin);

    /// <summary>How far the HUD chip is held away from the real gold (negative: above it, counting down after a purchase).</summary>
    public int ChipHoldNow => _chipHold;

    /// <summary>Small note under the card explaining why no gold was paid.</summary>
    public string GoldNote => !_isComplete ? string.Empty
        : _result.GoldAwarded > 0 ? string.Empty
        : _setup.Eased ? "This was the easier version, so it paid no gold. Win the normal level any time to earn it."
        : _setup.IsLesson ? "Lessons don't pay gold."
        : _setup.Level <= StoryConfig.TutorialLastLevel ? "The first few levels don't pay gold."
        : "Gold is only paid the first time a level is cleared.";
    public bool HasGoldNote => GoldNote.Length > 0;

    private void UpdateOverlay()
    {
        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(OverlayTitle));
        OnPropertyChanged(nameof(OverlayReason));
        OnPropertyChanged(nameof(HasOverlayReason));
        OnPropertyChanged(nameof(OverlayDetail));
        OnPropertyChanged(nameof(UnlockPopupVisible));
        OnPropertyChanged(nameof(NextText));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(RetryText));
        OnPropertyChanged(nameof(ShowShop));
        OnPropertyChanged(nameof(ShowShopOnFailed));
        OnPropertyChanged(nameof(ShowShare));
        OnPropertyChanged(nameof(ShowContinue));
        OnPropertyChanged(nameof(ContinuePrice));
        OnPropertyChanged(nameof(CanAffordContinue));
        OnPropertyChanged(nameof(ContinueText));
        OnPropertyChanged(nameof(ShowContinueHint));
        OnPropertyChanged(nameof(ContinuesLeft));
        OnPropertyChanged(nameof(ContinuesLeftText));
        OnPropertyChanged(nameof(ShowSolvableNote));
        OnPropertyChanged(nameof(HasRulesChip));
        OnPropertyChanged(nameof(RulesChipText));
        OnPropertyChanged(nameof(ShowEase));
        OnPropertyChanged(nameof(HasGoldReward));
        OnPropertyChanged(nameof(GoldRewardText));
        OnPropertyChanged(nameof(GoldRewardValue));
        OnPropertyChanged(nameof(HasPopGold));
        OnPropertyChanged(nameof(PopGoldValue));
        OnPropertyChanged(nameof(PopGoldText));
        OnPropertyChanged(nameof(ShowConsolation));
        OnPropertyChanged(nameof(ConsolationValue));
        OnPropertyChanged(nameof(ConsolationText));
        OnPropertyChanged(nameof(GoldNote));
        OnPropertyChanged(nameof(HasGoldNote));
        OnPropertyChanged(nameof(HasRegionProgress));
        OnPropertyChanged(nameof(RegionProgressText));
        OnPropertyChanged(nameof(RegionFraction));
        OnPropertyChanged(nameof(RewardLines));
        OnPropertyChanged(nameof(HasRewardLines));
        OnPropertyChanged(nameof(BonusResultText));
        OnPropertyChanged(nameof(HasBonusResult));
        OnPropertyChanged(nameof(ShowDevTools));
        OnPropertyChanged(nameof(ShowDevBoardTools));
        OnPropertyChanged(nameof(ClassicOverGoldText));
        OnPropertyChanged(nameof(HasClassicOverGold));
        OnPropertyChanged(nameof(ClassicSeedText));
        OnPropertyChanged(nameof(HasClassicSeed));
        RaiseReroll();
    }

    // ---------------------------------------------------------------- bindable state: story rewards (level-complete card)
    // Layer A is the gold above. Layer B is progress through the region. Layer C is the story rewards.

    private Region? CompletedRegion => _isComplete && _setup.IsStory ? RegionCatalog.For(_setup.Level) : null;   // lessons have none

    public bool HasRegionProgress => CompletedRegion is not null;

    public string RegionProgressText
    {
        get
        {
            if (CompletedRegion is not { } region) return string.Empty;
            var p = _campaign.ProgressOf(region);
            return p.IsRestored ? $"{region.Title} · restored" : $"{region.Title} · {p.LevelsCleared} / {region.LevelCount}";
        }
    }

    public double RegionFraction => CompletedRegion is { } region ? _campaign.ProgressOf(region).Fraction : 0;

    public IReadOnlyList<string> RewardLines
    {
        get
        {
            var lines = new List<string>();
            if (_outcome.RegionGold > 0) lines.Add($"Region reward: +{_outcome.RegionGold} gold");
            if (_outcome.FragmentAwarded) lines.Add("Prism fragment recovered");
            foreach (var entry in _outcome.NewLore.Where(l => l.Kind == LoreKind.Journal))
                lines.Add($"Journal entry: {entry.Title}");
            if (_outcome.CosmeticUnlocked != null)
                lines.Add(_boardStyleApplied ? "New board style: Garden (applied)" : "New board style: Garden");
            foreach (var found in _outcome.NewCosmetics.Where(c => c.Id != "garden" || _outcome.CosmeticUnlocked == null))
                lines.Add($"New in the workshop: {found.Name}");
            foreach (var entry in _foundLore)
                lines.Add(entry.Kind == LoreKind.Memory ? $"Memory found: {entry.Title}" : $"Journal entry: {entry.Title}");
            foreach (var a in _earned)
                lines.Add($"Achievement: {a.Name} (+{a.Gold} gold)");
            return lines;
        }
    }

    /// <summary>On a level with an optional objective: a note when the memory was not found this time.</summary>
    public string BonusResultText
    {
        get
        {
            if (!_isComplete || Bonus is not { } b) return string.Empty;
            if (_foundLore.Any(l => l.Kind == LoreKind.Memory)) return string.Empty;   // already shown as a reward line
            var memory = StoryContent.FindLore(b.MemoryId);
            return memory is not null && _campaign.HasLore(memory)
                ? "The memory on this level is already in your Story Book."
                : $"Optional memory not found: {b.Describe()}.";
        }
    }

    public bool HasBonusResult => BonusResultText.Length > 0;

    public bool HasRewardLines => RewardLines.Count > 0;

    // ---------------------------------------------------------------- bindable state: story scene panel

    public bool SceneVisible => _scene is not null;
    public string SceneTitle => _scene?.Title ?? string.Empty;

    private StoryLine? CurrentLine => _scene is not null && _sceneLine < _scene.Lines.Count ? _scene.Lines[_sceneLine] : null;

    public string SceneSpeaker => CurrentLine is { IsNarration: false } l ? Cast.Find(l.SpeakerId)?.Name ?? string.Empty : string.Empty;
    public bool SceneHasSpeaker => SceneSpeaker.Length > 0;
    public string SceneText => CurrentLine?.Text ?? string.Empty;
    public bool SceneIsNarration => CurrentLine?.IsNarration == true;
    public string SceneProgressText => _scene is null ? string.Empty : $"{_sceneLine + 1} / {_scene.Lines.Count}";
    public string SceneButtonText => _scene is not null && _sceneLine >= _scene.Lines.Count - 1 ? "DONE" : "NEXT";

    // Who is on screen, and on which side. Narration has no portrait. Pip is always on the left; everyone else alternates,
    // in the order they first speak in the scene (right, left, right...), so two speakers never share a side by accident.
    public string SceneSpeakerId => CurrentLine is { IsNarration: false } l ? l.SpeakerId : string.Empty;
    public bool SceneHasPortrait => SceneSpeakerId.Length > 0;
    public bool ScenePortraitOnRight => SceneHasPortrait && SpeakerOnRight(SceneSpeakerId);
    public bool ScenePortraitLeft => SceneHasPortrait && !ScenePortraitOnRight;
    public bool ScenePortraitRight => ScenePortraitOnRight;
    public TextAlignment SceneNameAlignment => ScenePortraitOnRight ? TextAlignment.End : TextAlignment.Start;

    private bool SpeakerOnRight(string speakerId)
    {
        if (_scene is null || speakerId == Cast.Pip) return false;
        int order = 0;
        foreach (var id in _scene.Lines.Select(l => l.SpeakerId).Where(id => id != Cast.Narrator && id != Cast.Pip).Distinct())
        {
            if (id == speakerId) return order % 2 == 0;
            order++;
        }
        return false;
    }

    public bool SceneCanGoBack => _scene is not null && _sceneLine > 0;

    /// <summary>Back to the message before this one. Does nothing on the first message.</summary>
    public void PreviousScene()
    {
        if (!SceneCanGoBack) return;
        _sceneLine--;
        RaiseScene();
    }

    public PipMood SceneMood => _scene?.Trigger is SceneTrigger.RegionCompletion or SceneTrigger.Reaction ? PipMood.Happy : PipMood.Calm;

    /// <summary>What the stage strip above the panel should show for the current line; null when no scene is open.</summary>
    public StageInfo? SceneStage => _scene is null ? null : StageDirections.Describe(_scene, _sceneLine);

    /// <summary>How bright Pip is drawn: grows with the number of regions restored.</summary>
    /// <summary>What Pip wears and how many small lights circle Pip (one per restored region).</summary>
    public AccessoryStyle PipAccessory => _loadout.Accessory;

    public int PipDetail => _campaign.RestoredRegionCount;

    public float PipBrightness => 0.35f + 0.65f * _campaign.RestoredRegionCount / RegionCatalog.All.Count;

    private void ShowNextScene()
    {
        if (_scene is null && _sceneQueue.Count > 0)
        {
            _scene = _sceneQueue.Dequeue();
            _sceneLine = 0;
        }
        RaiseScene();
    }

    private void AdvanceScene()
    {
        if (_scene is null) return;
        if (_sceneLine < _scene.Lines.Count - 1)
        {
            _sceneLine++;
            RaiseScene();
        }
        else FinishScene();
    }

    /// <summary>Skipping counts as seeing the scene: the player is not shown it again, and no reward depends on it.</summary>
    public void SkipScene() => FinishScene();

    private void FinishScene()
    {
        if (_scene is null) return;
        _campaign.MarkSeen(_scene.Id);
        _scene = null;
        _sceneLine = 0;
        ShowNextScene();   // another queued scene, or nothing: then the tutorial card (if any) comes back
        RaiseCoach();
        MaybeOpenChoice();
        RaiseChoice();
    }

    // ---------------------------------------------------------------- the ending's cosmetic choice

    public bool ChoiceVisible => _choiceOpen && !SceneVisible;

    // The three looks, in the order the page lists them. The ids are fixed in the page; a test keeps them in step.
    public string LookOneText => LookText("lanterns");
    public string LookTwoText => LookText("aurora");
    public string LookThreeText => LookText("meadow");

    private string LookText(string id)
    {
        var look = StoryContent.FindFinalChoice(id);
        if (look is null) return string.Empty;
        string mark = _ending.Choice?.Id == id ? "✓  " : string.Empty;
        return $"{mark}{look.Name}  ·  {look.Description}";
    }

    /// <summary>Pip's line about the look just picked; empty until one is picked.</summary>
    public string ChoiceNote => _choiceNote;
    public bool ChoiceHasNote => _choiceNote.Length > 0;
    public string ChoiceDoneText => _ending.HasChosen ? "DONE" : "DECIDE LATER";

    private void MaybeOpenChoice()
    {
        if (_choiceOpen || SceneVisible || !_setup.IsStory || !_isComplete) return;
        if (_setup.Level != RegionCatalog.LastCampaignLevel || !_ending.NeedsChoice) return;
        _choiceNote = string.Empty;
        _choiceOpen = true;
    }

    private void ChooseLook(string? id)
    {
        if (id is null || !_ending.Choose(id)) return;
        _loadout.Select(id);   // the chosen look is also a board; wear it now (it can be changed again in the wardrobe)
        _choiceNote = _ending.Choice?.EpilogueLine ?? string.Empty;
        OnPropertyChanged(nameof(BoardStyle));
        RaiseChoice();
    }

    private void FinishChoice()
    {
        _choiceOpen = false;
        if (_ending.EpilogueDue() is { } epilogue)
        {
            _sceneQueue.Enqueue(epilogue);
            ShowNextScene();
        }
        RaiseChoice();
    }

    private void RaiseChoice()
    {
        OnPropertyChanged(nameof(ChoiceVisible));
        OnPropertyChanged(nameof(ChoiceNote));
        OnPropertyChanged(nameof(ChoiceHasNote));
        OnPropertyChanged(nameof(ChoiceDoneText));
        OnPropertyChanged(nameof(LookOneText));
        OnPropertyChanged(nameof(LookTwoText));
        OnPropertyChanged(nameof(LookThreeText));
    }

    private void RaiseScene()
    {
        OnPropertyChanged(nameof(SceneVisible));
        OnPropertyChanged(nameof(HelpersVisible));
        OnPropertyChanged(nameof(SceneTitle));
        OnPropertyChanged(nameof(SceneSpeaker));
        OnPropertyChanged(nameof(SceneHasSpeaker));
        OnPropertyChanged(nameof(SceneText));
        OnPropertyChanged(nameof(SceneIsNarration));
        OnPropertyChanged(nameof(SceneProgressText));
        OnPropertyChanged(nameof(SceneButtonText));
        OnPropertyChanged(nameof(SceneSpeakerId));
        OnPropertyChanged(nameof(SceneHasPortrait));
        OnPropertyChanged(nameof(ScenePortraitLeft));
        OnPropertyChanged(nameof(ScenePortraitRight));
        OnPropertyChanged(nameof(SceneNameAlignment));
        OnPropertyChanged(nameof(SceneCanGoBack));
        OnPropertyChanged(nameof(PipBrightness));
        OnPropertyChanged(nameof(SceneMood));
        OnPropertyChanged(nameof(SceneStage));
        OnPropertyChanged(nameof(CoachVisible));
        NotifyAudioScene();
        Redraw?.Invoke();
    }

    // ---------------------------------------------------------------- audio

    private string _audioSceneKey = string.Empty;
    private float _audioTension = -1f;
    private bool _finalePending;
    private int _unlockPending;

    /// <summary>
    /// Tells the music what the story panel is doing: it ducks under the dialogue, the stage cues (a memory crystal, the colour
    /// sweeping back, the Warden stepping in) get their one-time sounds, and the end-of-game finale starts once the player is
    /// looking at the last picture of the ending.
    /// </summary>
    private void NotifyAudioScene()
    {
        _audio.DialogueChanged(SceneVisible);

        if (SceneStage is { } info)
        {
            string key = info.SceneId + ":" + info.Line;
            bool started = key != _audioSceneKey && info.Cue != StageCue.None && info.Line == info.CueStartLine;
            _audioSceneKey = key;
            _audio.StageLine(info.Cue, started, info.RegionNumber, info.SweepTo);
            if (info.SceneId == "r8_complete" && info.Line >= 1) _finalePending = true;
        }
        else _audioSceneKey = string.Empty;

        FlushPendingAudio();
    }

    /// <summary>The unlock chime and the finale wait until no story scene is on screen (the finale, until the ending has begun).</summary>
    private void FlushPendingAudio()
    {
        if (_finalePending && !(SceneVisible && SceneStage is { SceneId: "r8_complete", Line: < 1 }))
        {
            _finalePending = false;
            _unlockPending = 0;
            _audio.Finale();
        }
        else if (_unlockPending > 0 && !SceneVisible)
        {
            int region = _unlockPending;
            _unlockPending = 0;
            _audio.RegionUnlocked(region);
        }
    }

    /// <summary>The music for the level that has just started: a region's own tune (faded, recovering or restored), Classic's garden, or the menu theme for a lesson.</summary>
    private void StartMusic()
    {
        _finalePending = false;
        _unlockPending = 0;
        _audioTension = -1f;
        _audioSceneKey = string.Empty;
        _audio.ClearPause();

        // A chosen song that is no longer earned (a restored or reset save) goes back to automatic.
        if (Soundtrack.Get(_audioSettings.SelectedTrack) is { } chosen && !Soundtrack.IsUnlocked(chosen, _progress.HighestCleared))
            _audioSettings.SelectedTrack = 0;

        if (_setup.IsLesson)
        {
            _naturalTrack = Soundtrack.All[0].Number;
            _audio.ShowMenu();   // a tutorial lesson keeps the quiet theme
        }
        else if (_setup.IsStory)
        {
            var region = RegionCatalog.For(_setup.Level);
            if (region is null)
            {
                _naturalTrack = Soundtrack.For(RegionCatalog.All[^1].Number, RegionState.Restored).Number;
                _audio.ShowRegion(RegionCatalog.All[^1].Number, RegionState.Restored);   // past the campaign: the Heart, restored
            }
            else
            {
                var state = _campaign.ProgressOf(region).State;
                _naturalTrack = Soundtrack.For(region.Number, state).Number;
                _audio.ShowRegion(region.Number, state);
            }
            _audio.LevelStarted();
        }
        else
        {
            _naturalTrack = Soundtrack.For(1, RegionState.Restored).Number;   // Classic: the Garden, restored
            _audio.ShowClassic();
        }
        RaiseTrack();
    }

    // ---------------------------------------------------------------- the song picker on the pause screen

    private int _naturalTrack = 1;

    private bool TrackChosen => Soundtrack.Get(_audioSettings.SelectedTrack) is not null;
    private Track? PlayingTrack => Soundtrack.Get(_audioSettings.SelectedTrack) ?? Soundtrack.Get(_naturalTrack);

    public string TrackCaption => TrackChosen ? "NOW PLAYING · YOUR CHOICE" : "NOW PLAYING · AUTOMATIC";
    public string TrackTitle => PlayingTrack?.Title ?? string.Empty;
    public string TrackDetail => PlayingTrack?.Detail ?? string.Empty;
    public string TrackHint => $"{Soundtrack.Unlocked(_progress.HighestCleared).Count} of {Soundtrack.TrackCount} songs found · tap to choose";

    private void RaiseTrack()
    {
        OnPropertyChanged(nameof(TrackCaption));
        OnPropertyChanged(nameof(TrackTitle));
        OnPropertyChanged(nameof(TrackDetail));
        OnPropertyChanged(nameof(TrackHint));
    }

    /// <summary>The choices in order: automatic first, then every song earned so far.</summary>
    private List<int> TrackOrder()
    {
        var order = new List<int> { 0 };
        order.AddRange(Soundtrack.Unlocked(_progress.HighestCleared).Select(t => t.Number));
        return order;
    }

    /// <summary>The previous or next song earned (wrapping round, with automatic as one of the stops).</summary>
    private void StepTrack(int direction)
    {
        var order = TrackOrder();
        int at = Math.Max(0, order.IndexOf(_audioSettings.SelectedTrack));
        ChooseTrack(order[(at + direction + order.Count) % order.Count]);
    }

    /// <summary>Plays this song from now on (0 is automatic). Songs that are not earned are ignored.</summary>
    public void ChooseTrack(int number)
    {
        if (number != 0 && !TrackOrder().Contains(number)) return;
        _audioSettings.SelectedTrack = number;   // GameAudio notices and switches the music at once
        RaiseTrack();
    }

    /// <summary>The list shown when the player taps the song: automatic, then each song earned. The current one is marked.</summary>
    public IReadOnlyList<(int Number, string Label)> TrackChoices()
    {
        var list = new List<(int, string)>
        {
            (0, (TrackChosen ? "" : "♪ ") + "Automatic (the music of the place)"),
        };
        foreach (var t in Soundtrack.Unlocked(_progress.HighestCleared))
            list.Add((t.Number, (t.Number == _audioSettings.SelectedTrack ? "♪ " : "") + $"{t.Title}  ·  {t.Detail}"));
        return list;
    }

    /// <summary>Few moves left on a Story level adds a little unease to the music (never more than a hint).</summary>
    private void UpdateAudioTension()
    {
        float level = 0f;
        if (_setup.IsStory && _game.HasMoveLimit && _game.State == GameState.Playing)
        {
            int left = _game.MovesLeft;
            level = left <= 1 ? 1f : left <= 2 ? 0.7f : left <= 4 ? 0.35f : 0f;
        }
        if (Math.Abs(level - _audioTension) < 0.001f) return;
        _audioTension = level;
        _audio.Tension(level);
    }

    /// <summary>The short stings for pausing, finishing and failing a level.</summary>
    private void AudioStateChanged(GameState from, GameState to)
    {
        if (to == GameState.Paused) _audio.GamePaused();
        else if (to == GameState.LevelComplete) _audio.LevelCompleted();
        else if (to == GameState.GameOver) _audio.LevelFailed();
        else if (from == GameState.Paused) _audio.GameResumed();
    }

    // ---------------------------------------------------------------- bindable state: coaching

    public TutorialStep? CoachStep => _coach?.Current;
    public bool CoachVisible => CoachStep is not null && !SceneVisible;
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

    private bool InputBlocked => SceneVisible || _promptOpen || _isLoading || CoachStep is { Mode: StepMode.Tap };

    // ---------------------------------------------------------------- the first-level tutorial offer
    // A brand-new player starting Story level 1 is asked once whether they want the tutorial. The game is held still
    // (and the opening scene waits) until they answer. Yes plays the lessons here; No carries straight on with level 1.

    public bool TutorialPromptVisible => _promptOpen && !SceneVisible;

    private bool ShouldOfferTutorial() =>
        _setup is { IsStory: true, Level: 1 } && _tutorial.ShouldOffer(_progress.HighestCleared);

    private void AcceptTutorial()
    {
        if (!_promptOpen) return;
        _promptOpen = false;
        _tutorial.MarkOffered();
        _setup = GameSetup.Lesson(1);
        _coachSeen = false;
        NewGame();
    }

    private void DeclineTutorial()
    {
        if (!_promptOpen) return;
        _promptOpen = false;
        _tutorial.MarkOffered();
        NewGame();   // the same level again, this time with its opening scene and no question
    }

    /// <summary>"NEXT LEVEL", or the lesson equivalents: the last lesson hands over to Story level 1.</summary>
    public string NextText => !_setup.IsLesson ? "NEXT LEVEL"
        : _setup.Level >= StoryConfig.LessonCount ? "START PLAYING"
        : "NEXT LESSON";

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
        OnPropertyChanged(nameof(TutorialPromptVisible));
        Redraw?.Invoke();
    }

    // ---------------------------------------------------------------- seeded levels: failures, reroll, loading card
    // Every Story level is built from the player's own seed. Most of the time the seed is already known (stored, recorded,
    // or rolled ahead) and the level starts at once. If it is not, the search runs off the UI thread and, after a moment,
    // a skippable recap card covers the wait. A reroll swaps the level for a new seed: it costs gold that falls with each
    // failure and is free from the tenth. Nothing here is a timer or a countdown, and none of it is ever needed to win.

    private bool _seedPinned;       // a seed typed into the dev menu is used as given
    private bool _failureCounted;   // this run has already been counted as a failure
    private bool _isLoading, _cardVisible, _levelReady, _rerollHintOpen;
    private CancellationTokenSource? _loadCts;
    private TaskCompletionSource? _dismissCard;
    private DateTime _cardShownAt;
    private IReadOnlyList<RecapItem> _recap = Array.Empty<RecapItem>();

    /// <summary>REROLL is offered on the failed panel and the pause menu once the level has failed, and always (free) on a cleared level.</summary>
    public bool ShowReroll => _setup.IsStory && !_isLoading && (_isFailed || _isPaused) && _levels.CanOfferReroll(_setup.Level);

    private int RerollPrice => _levels.RerollPrice(_setup.Level);
    private int FailuresNow => _levels.Failures(_setup.Level);
    private bool StuckEnough => _levels.IsCurrent(_setup.Level) && FailuresNow >= SeedingConfig.FailuresForFreeReroll;

    public bool CanReroll => ShowReroll && _levels.CanAffordReroll(_setup.Level);

    /// <summary>"REROLL · 11 GOLD", "REROLL · FREE" or, from the tenth failure, "FRESH LEVEL, FREE". Words, never colour alone.</summary>
    public string RerollText => !ShowReroll ? string.Empty
        : StuckEnough ? "FRESH LEVEL, FREE"
        : RerollPrice == 0 ? "REROLL · FREE"
        : $"REROLL · {RerollPrice:N0} GOLD";

    public string RerollNote
    {
        get
        {
            if (!ShowReroll) return string.Empty;
            if (StuckEnough) return "Stuck? Take a new level on the house.";
            if (_levels.IsCleared(_setup.Level) || RerollPrice == 0) return "A different version of this level. Free.";
            int left = _levels.FailuresUntilFree(_setup.Level);
            return $"Free in {left} more {(left == 1 ? "try" : "tries")}";
        }
    }

    public bool HasRerollNote => RerollNote.Length > 0;

    /// <summary>The "?" button appears only when the player cannot pay. It explains where gold comes from and is never the only way forward.</summary>
    public bool ShowRerollHint => ShowReroll && !CanReroll;

    public string RerollHintText => "Short on gold? Pop blocks in Endless mode to earn some.";

    public bool RerollHintOpen { get => _rerollHintOpen; set { if (SetProperty(ref _rerollHintOpen, value)) OnPropertyChanged(nameof(ShowRerollHintText)); } }
    public bool ShowRerollHintText => _rerollHintOpen && ShowRerollHint;

    public string RerollDescription => !ShowReroll ? string.Empty
        : $"{RerollText.Replace("·", ",").ToLowerInvariant()}. {RerollNote}" + (CanReroll ? string.Empty : " You do not have enough gold yet.");

    private void RaiseReroll()
    {
        OnPropertyChanged(nameof(ShowReroll));
        OnPropertyChanged(nameof(CanReroll));
        OnPropertyChanged(nameof(RerollText));
        OnPropertyChanged(nameof(RerollNote));
        OnPropertyChanged(nameof(HasRerollNote));
        OnPropertyChanged(nameof(ShowRerollHint));
        OnPropertyChanged(nameof(ShowRerollHintText));
        OnPropertyChanged(nameof(RerollDescription));
        if (!ShowRerollHint && _rerollHintOpen) RerollHintOpen = false;
        RaiseHeader();
    }

    /// <summary>Swaps the level for a new one. Takes the price, then starts the new level (at once if it was rolled ahead).</summary>
    private void Reroll()
    {
        if (!_setup.IsStory || _isLoading) return;   // one reroll at a time: the new level is already being found
        if (!_levels.TryReroll(_setup.Level))
        {
            Toast?.Invoke("Not enough gold yet");
            return;
        }

        _lifetime.AddReroll();

        // Abandoning the run to reroll is the player's choice, not another failure.
        _failureCounted = true;
        _seedPinned = false;
        RerollHintOpen = false;
        _setup = GameSetup.Story(_setup.Level);
        NewGame();
    }

    /// <summary>A loss counts as a failure on the player's current level (and only there, and only once per run).</summary>
    private void CountLoss()
    {
        if (_failureCounted || !_setup.IsStory) return;
        _failureCounted = true;
        if (_levels.RecordFailure(_setup.Level) > 0) _levels.PrefetchReroll(_setup.Level);
    }

    /// <summary>A lost Story level keeps half its pop gold and part of the first-clear reward (see <see cref="Consolation"/>). Once per run.</summary>
    private void PayConsolation()
    {
        if (_lossHandled || !_setup.IsStory || _game.Story is not { IsTutorial: false } story) return;
        _lossHandled = true;
        var due = _consolation.Pay(story.Number, _game.ClassicGoldTenths / 10, _game.GoalProgress,
            levelCleared: _progress.IsCleared(story.Number), eased: story.Eased, alreadyPaid: _lossPaid, goldFactor: _setup.Rules.GoldFactor);
        _lossPaid = new ConsolationResult(_lossPaid.PopPart + due.PopPart, _lossPaid.ProgressPart + due.ProgressPart);
        _chipHold = due.Total;
        UpdateOverlay();
    }

    /// <summary>Restarting a level after placing at least one piece counts as a failure; leaving to the menu does not.</summary>
    private void CountAbandonedRun()
    {
        if (_failureCounted || !_game.Setup.IsStory || _game.Story is not { IsTutorial: false } story) return;
        if (_game.State is not (GameState.Playing or GameState.Paused or GameState.ResolvingBursts) || _game.PiecesPlaced == 0) return;

        _failureCounted = true;
        if (_levels.RecordFailure(story.Number) > 0) _levels.PrefetchReroll(story.Number);
    }

    // ---- loading card

    public bool IsLoading => _isLoading;
    public bool LoadingCardVisible => _isLoading && _cardVisible;
    public string LoadingTitle => "Finding your level…";
    public string LoadingTapText => _levelReady ? "TAP TO CONTINUE" : string.Empty;
    public bool HasLoadingTap => _levelReady;
    public IReadOnlyList<RecapItem> RecapItems => _recap;
    public bool HasLoadingRecap => _recap.Count > 0;

    private void RaiseLoading()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadingCardVisible));
        OnPropertyChanged(nameof(LoadingTapText));
        OnPropertyChanged(nameof(HasLoadingTap));
        OnPropertyChanged(nameof(RecapItems));
        OnPropertyChanged(nameof(HasLoadingRecap));
        OnPropertyChanged(nameof(IsPlaying));
        RaiseReroll();   // REROLL hides while a level is being found
        Redraw?.Invoke();
    }

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts = null;
        _dismissCard?.TrySetResult();
        _isLoading = _cardVisible = _levelReady = false;
    }

    /// <summary>The level's seed is not ready: roll it off the UI thread, and show the recap card if that takes a moment.</summary>
    private async void BeginLoad()
    {
        CancelLoad();
        var cts = _loadCts = new CancellationTokenSource();
        var ct = cts.Token;
        int level = _setup.Level;
        var requested = DateTime.UtcNow;

        _isLoading = true;
        _recap = PopBlocks.Engine.Story.LoadingRecap.Build(_unlocks);
        RaiseLoading();
        LoadLevelState();   // the old level's goals, moves and objective must not stay up while the new level is found

        try
        {
            var resolve = _levels.ResolveAsync(level, ct);

            // The card only appears if the wait is long enough to notice, so a fast roll never flashes it.
            var delay = Task.Delay(SeedingConfig.LoadingCardDelay, ct);
            if (await Task.WhenAny(resolve, delay) == delay && LoadingPolicy.ShouldShowCard(DateTime.UtcNow - requested, resolve.IsCompleted))
            {
                _cardVisible = true;
                _cardShownAt = DateTime.UtcNow;
                RaiseLoading();
            }

            var seed = await resolve;

            // A card that is up stays for a moment so it never flashes; the player can tap it away once the level is ready.
            if (_cardVisible)
            {
                _dismissCard = new TaskCompletionSource();
                _levelReady = true;
                RaiseLoading();
                var hold = LoadingPolicy.HoldRemaining(DateTime.UtcNow - _cardShownAt);
                if (hold > TimeSpan.Zero) await Task.WhenAny(Task.Delay(hold, ct), _dismissCard.Task);
            }

            if (ct.IsCancellationRequested) return;
            _isLoading = _cardVisible = _levelReady = false;
            _loadCts = null;
            RaiseLoading();

            _setup = _setup with { Seed = seed, Eased = _levels.IsEasedPlay(_setup.Level) };
            NewGame();
        }
        catch (OperationCanceledException)
        {
            // The player left the level, or asked for another one: nothing to do.
        }
        catch (Exception)
        {
            // A roll should never fail. If it does, say so rather than leaving the player on a blank screen.
            _isLoading = _cardVisible = _levelReady = false;
            RaiseLoading();
            Toast?.Invoke("Could not load the level. Try again.");
        }
    }

    private void DismissLoading()
    {
        if (_levelReady) _dismissCard?.TrySetResult();
    }

    // ---------------------------------------------------------------- Classic gold and seed

    private int _runGoldPaid;        // whole gold paid so far in this Classic run
    private int _classicOverGold;    // shown on the game-over card
    private bool _classicBanked;     // the game-over banking has happened for this run

    public bool IsClassic => !_setup.HasLevel;

    /// <summary>Classic HUD: the gold this run is worth so far. Display only; payment happens when the run ends, the player leaves, or the app goes to the background.</summary>
    public string RunGoldText => $"+{_runGoldPaid + _classicGold.Preview(_game.ClassicGoldTenths)} gold";

    public string ClassicOverGoldText => $"+{_classicOverGold:N0} gold";
    public bool HasClassicOverGold => _isClassicOver;
    public string ClassicSeedText => _setup.ClassicSeed is { } seed ? $"Seed {seed}" : string.Empty;
    public bool HasClassicSeed => _isClassicOver && ClassicSeedText.Length > 0;

    private static int RandomClassicSeed() => Random.Shared.Next(1, int.MaxValue);

    /// <summary>Pays out the Classic gold earned so far in this run. Safe to call at every opportunity: it only ever pays what is new.</summary>
    public void BankClassicGold()
    {
        if (_game.Setup.HasLevel) return;
        _runGoldPaid += _classicGold.Bank(_game.ClassicGoldTenths);
        OnPropertyChanged(nameof(SubText));
    }

    /// <summary>The app lost focus or went to the background: pause, and bank Classic gold (a swipe-away cannot be caught afterwards).</summary>
    public void OnAppInactive()
    {
        Pause();
        BankClassicGold();
    }

    private async void CopySeed()
    {
        if (_setup.ClassicSeed is not { } seed) return;
        try
        {
            await Clipboard.Default.SetTextAsync(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Toast?.Invoke("Seed copied");
        }
        catch
        {
            // Copying is a convenience; the seed is on screen.
        }
    }

    // ---------------------------------------------------------------- lifecycle

    /// <summary>Creates the game timer. Call once the page has a dispatcher.</summary>
    public void Attach(IDispatcher dispatcher)
    {
        StartClock(dispatcher);
        if (_timer is not null) return;
        _dispatcher = dispatcher;
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) =>
        {
            // A story panel (or the tutorial offer) holds everything still, so reading never costs the player a move.
            if (SceneVisible || _promptOpen || _isLoading) return;

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
            if (cell.Y < _game.ViewTopRow) return false;
        return true;
    }

    /// <summary>
    /// After the last pop of a level or run the board is held for a beat (the move counter pulses; an overflow flashes the
    /// top row) before the result card fades in. Input is already closed because the game is over; a tap skips the wait.
    /// </summary>
    private void BeginEndBeat()
    {
        _beatStarted = true;
        if (_dispatcher is null) return;

        bool overflow = _game.State == GameState.GameOver && _game.FailReason == FailReason.StackOverflow;
        bool reduced = _settings.ReducedMotion;
        var kind = reduced ? EndBeatKind.Still
            : overflow ? EndBeatKind.Overflow
            : _game.State == GameState.LevelComplete ? EndBeatKind.Win : EndBeatKind.OutOfMoves;

        _beatTimer ??= _dispatcher.CreateTimer();
        _beatTimer.IsRepeating = false;
        _beatTimer.Interval = TimeSpan.FromMilliseconds(EndBeat.HoldMs(overflow, reduced));
        _beatTimer.Tick -= OnEndBeatTick;
        _beatTimer.Tick += OnEndBeatTick;
        _endBeat = true;
        _beatTimer.Start();
        EndBeatStarted?.Invoke(kind);
    }

    private void OnEndBeatTick(object? sender, EventArgs e) => SkipEndBeat();

    /// <summary>Ends the hold now (its time is up, or the player tapped). The result card shows.</summary>
    public void SkipEndBeat()
    {
        _beatTimer?.Stop();
        if (!_endBeat) return;
        _endBeat = false;
        UpdateOverlay();
    }

    /// <summary>Adds the run that is ending to the lifetime bests, if that has not happened yet (a lesson never counts).</summary>
    private void RecordPendingRun()
    {
        if (_runRecorded || _run.PiecesPlaced == 0 || _game.Story?.IsTutorial == true) return;
        _runRecorded = true;
        _lifetime.Add(_run, _game.Story);
    }

    /// <summary>Stops the timer and pauses. Safe to call repeatedly.</summary>
    public void Detach()
    {
        if (_game.State is GameState.GameOver or GameState.LevelComplete) RecordPendingRun();   // a level just lost or won counts even if the page is left at once
        _beatTimer?.Stop();
        _clockTimer?.Stop();
        _endBeat = false;
        _chipHold = 0;
        Pause();
        _timer?.Stop();
        BankClassicGold();   // leaving Classic pays out what the run earned
        CancelLoad();
        _levels.CancelPrefetches();   // nobody is waiting for them any more
    }

    public void NewGame()
    {
        // The run being left behind: restarting a Story level after placing a piece is a failure, and a Classic run pays out.
        CountAbandonedRun();
        BankClassicGold();

        // A Story level plays with this player's seed. It is usually known already (stored, recorded, or rolled ahead);
        // otherwise the search runs behind the loading card and this method is called again when it is done.
        if (_setup.IsStory && !_seedPinned)
        {
            if (!_levels.TryGetReady(_setup.Level, out var ready))
            {
                BeginLoad();
                return;
            }
            // An eased level is the player's current level that they chose to ease; anything else is played as rolled.
            _setup = _setup with { Seed = ready, Eased = _levels.IsEasedPlay(_setup.Level) };
        }
        else if (_setup.IsStory)
        {
            _setup = _setup with { Eased = false };
        }
        CancelLoad();

        // A Classic run draws its pieces from a seed: the one the player typed, or a fresh random one each time.
        if (!_setup.HasLevel)
        {
            if (!_setup.SeedChosen) _setup = GameSetup.ClassicRun(RandomClassicSeed(), chosen: false);
            _classicGold.BeginRun();
            _runGoldPaid = 0;
            _classicOverGold = 0;
            _classicBanked = false;
        }
        _failureCounted = false;
        ResetClock();
        _unlockOpen = false;
        _unlockItems = new List<UnlockItem>();
        RerollHintOpen = false;
        _insight = null;
        _insightIndex = -1;
        _insightUses = 0;
        _insightUsedInRun = false;
        _insightRequest++;
        InsightChanged?.Invoke(null);
        if (_setup.IsStory) _classicGold.BeginRun();   // Story pops pay too: this run starts with nothing banked
        _popGoldPaid = 0;
        _lossPaid = default;
        _lossHandled = false;
        _chipHold = 0;

        // Shop upgrades and settings apply from the start of each game.
        _game.PreviewCount = _shop.PreviewCount;
        _game.ExtraMoves = _shop.ExtraMoves;
        _stock.ApplyTo(_game, _shop);   // helpers carried in; they apply to Story levels that allow them
        _game.Timing = _settings.BlastEffects
            ? new PopTiming(VisualDesign.HighlightMs, VisualDesign.SettleMs)
            : PopTiming.Instant;

        // A level left half-played still counts towards the player's personal bests (once). Tutorial lessons never do.
        RecordPendingRun();
        _run.Reset();
        _runRecorded = false;
        _beatTimer?.Stop();
        _endBeat = false;
        _beatStarted = false;
        _earned = new List<Achievement>();
        _checkAchievements = false;
        _foundLore = new List<LoreEntry>();
        _choiceOpen = false;
        _choiceNote = string.Empty;

        _bestAtStart = _highScores.GetBest(_setup.BestScoreKey);
        BestScore = _bestAtStart;
        IsNewBest = false;
        _completionHandled = false;
        _result = default;
        _goalSignature = string.Empty;
        _lastState = GameState.Ready;

        // Music first, so that anything the opening scene asks of it (ducking under the dialogue) lands on the right piece.
        StartMusic();

        // The very first Story level asks once whether the player wants the tutorial. Until they answer, the opening scene waits.
        _promptOpen = ShouldOfferTutorial();

        // Coaching: first time through a level that has steps. Story levels (and the intro cards of new features) coach only
        // until cleared; a lesson coaches every time it is entered fresh, because replaying the tutorial is the point.
        _coach = null;
        if (_setup.HasLevel)
        {
            var level = _setup.IsLesson ? LevelCatalog.Lesson(_setup.Level) : LevelCatalog.Get(_setup.Level, _setup.Seed);
            if (level.Steps.Count > 0 && !_coachSeen && (_setup.IsLesson || !_progress.IsCleared(level.Number)))
            {
                _coach = new TutorialController(level.Steps);
                _coach.Changed += RaiseCoach;
            }
        }
        _coachSeen = true;

        // Story: a region's opening scene comes before any tutorial card, and never on a replay. Lessons have no story.
        _sceneQueue.Clear();
        _scene = null;
        _outcome = CampaignOutcome.None;
        _boardStyleApplied = false;
        if (_setup.IsStory && !_promptOpen && _campaign.OnLevelEntered(_setup.Level) is { } opening)
            _sceneQueue.Enqueue(opening);
        ShowNextScene();
        RaiseCoach();

        // Going back to a region's first level: one brief Pip line instead of the opening scene again.
        if (_setup.IsStory && !_promptOpen && !SceneVisible && _campaign.ReplayRemark(_setup.Level) is { } remark)
            Banner?.Invoke(remark);

        _setup = _setup with { Rules = _rules.EffectiveFor(_setup) };   // the player's own rules, where they apply
        _game.Start(_setup);

        LoadLevelState();
        PrefetchNextLevel();
        GameStarted?.Invoke();
    }

    /// <summary>
    /// The one place that makes every level-dependent binding match the engine: goals (rebuilt from scratch, never diffed
    /// against the previous level), the optional objective, the move counter, the header, the result card, the choice panel
    /// and the helper buttons. A new game, a reroll and the loading card all go through it, so none of them can leave the
    /// previous level's goal bar behind.
    /// </summary>
    private void LoadLevelState()
    {
        _goalSignature = string.Empty;
        UpdateGoals();
        RaiseBonus();
        OnPropertyChanged(nameof(HasMoves));
        OnPropertyChanged(nameof(MovesText));
        OnPropertyChanged(nameof(MovesColour));
        RaiseHeader();
        UpdateOverlay();
        RaiseChoice();
        RaiseHelpers();
    }

    public void GoToNextLevel()
    {
        if (_setup.IsLesson)
        {
            // The last lesson hands over to the Story. A player who has not started it begins at level 1.
            _setup = _setup.Level >= StoryConfig.LessonCount ? GameSetup.Story(_progress.NextLevel) : GameSetup.Lesson(_setup.Level + 1);
            _coachSeen = false;
            NewGame();
            return;
        }

        if (!_setup.IsStory) return;
        _setup = GameSetup.Story(_setup.Level + 1);
        _seedPinned = false;
        _coachSeen = false;
        NewGame();
    }

    /// <summary>Finding a level takes a moment on the hardest tiers, so the next one is rolled in the background.</summary>
    private void PrefetchNextLevel()
    {
        if (!_setup.IsStory) return;   // lessons are hand-made and quick to build
        _levels.PrefetchAfter(_setup.Level);
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

    /// <summary>All the way to the left wall of the board (the page swaps these on an upside-down level).</summary>
    public void MoveFarLeft()
    {
        if (InputBlocked) return;
        for (int guard = 0; guard < GameConfig.Columns && _game.MoveLeft(); guard++) { }
    }

    public void MoveFarRight()
    {
        if (InputBlocked) return;
        for (int guard = 0; guard < GameConfig.Columns && _game.MoveRight(); guard++) { }
    }
    public void Rotate() { if (!InputBlocked) _game.Rotate(); }
    public void SoftDrop() { if (!InputBlocked) _game.SoftDrop(); }
    public void HardDrop() { if (!InputBlocked) _game.HardDrop(); }

    // ---------------------------------------------------------------- engine feedback

    private void OnGameEvent(GameEvent e)
    {
        PlaySound(e);
        if (e is GameEvent.OrbSpawned or GameEvent.OrbBroken or GameEvent.OrbCorrupted && _game.OrbEventAt is { } at)
            OrbHappened?.Invoke(e, at.X, at.Y);
        _coach?.Notify(e);
        if (e == GameEvent.PenaltyApplied)
            Toast?.Invoke($"No pop!  −{_game.LastPenalty}");

        // Pip's first reactions to a burst and a chain: a quiet banner, never a panel, and never over a story scene.
        if (e == GameEvent.Popped && _setup.IsStory && !SceneVisible && _campaign.OnBurst(_game.Chain) is { } reaction)
            Banner?.Invoke(reaction);
    }

    /// <summary>
    /// Sound for the things the player hears: turn, place, drop, pop. Moving sideways and soft-dropping stay silent
    /// (they repeat too fast to be pleasant). Each step of a chain pops a little higher.
    /// </summary>
    private void PlaySound(GameEvent e)
    {
        // Whether effects are on is decided inside GameAudio (the Sound effects switch and the volumes). Pops are played from
        // WaveResolved, once per colour group, so they are not repeated here.
        switch (e)
        {
            case GameEvent.Rotated:
                _audio.Rotated();
                break;

            case GameEvent.OrbSpawned:
                _audio.OrbSound(OrbSoundKind.Appeared);
                if (_settings.SeenOrbIntro) break;
                _settings.SeenOrbIntro = true;
                Toast?.Invoke(_setup.IsStory
                    ? "An Orb! Pop a block touching it to break it and clear its rows and columns. If its timer runs out, it cracks the blocks around it."
                    : "An Orb! Pop a block touching it to break it and clear its rows and columns for a big gold bonus. If its timer runs out, it cracks the blocks around it.");
                break;

            case GameEvent.OrbBroken:
                _audio.OrbSound(OrbSoundKind.Broke);
                break;

            case GameEvent.OrbCorrupted:
                _audio.OrbSound(OrbSoundKind.Corrupted);
                break;

            case GameEvent.HardDropped:
                _droppedHard = true;   // the engine raises PieceLocked straight after this
                break;

            case GameEvent.PieceLocked:
                _audio.Placed(_droppedHard);
                _droppedHard = false;
                break;
        }
    }

    /// <summary>One whole tone up per chain step: x1.0, x1.12, x1.26, x1.41, x1.59 ...</summary>
    internal static float ChainPitch(int chain) =>
        MathF.Pow(2f, Math.Clamp(chain - 1, 0, 6) * 2f / 12f);

    private void OnEngineChanged()
    {
        Score = _game.Score;
        ChainText = _game.LastChain >= 2 ? $"CHAIN ×{ScoringSystem.Multiplier(_game.LastChain)}" : string.Empty;

        OnPropertyChanged(nameof(HasMoves));
        OnPropertyChanged(nameof(MovesText));
        OnPropertyChanged(nameof(MovesColour));
        UpdateGoals();
        UpdateAudioTension();

        if (!_setup.HasLevel)
        {
            // Save the personal best the moment it is beaten, so an unexpected exit cannot lose it. A run on a seed the
            // player typed in pays gold like any other, but never sets a record (an easy seed would make it meaningless).
            if (_setup.CountsForBest && !_insightUsedInRun && _highScores.Submit(_setup.BestScoreKey, _game.Score))
                BestScore = _highScores.GetBest(_setup.BestScoreKey);
            IsNewBest = _setup.CountsForBest && !_insightUsedInRun && _highScores.GetBest(_setup.BestScoreKey) > _bestAtStart;

            // Game over pays out what the run earned, once, and says how much.
            if (_game.State == GameState.GameOver && !_classicBanked)
            {
                _classicBanked = true;
                BankClassicGold();
                _classicOverGold = _runGoldPaid;
                OnPropertyChanged(nameof(ClassicOverGoldText));
                OnPropertyChanged(nameof(HasClassicOverGold));
                OnPropertyChanged(nameof(ClassicSeedText));
                OnPropertyChanged(nameof(HasClassicSeed));
            }
            OnPropertyChanged(nameof(SubText));
        }
        else if (_game.State == GameState.GameOver)
        {
            CountLoss();   // a failed Story level: counted once, on the player's current level only
            PayConsolation();
        }

        // The run is over (won, lost, or Classic game over): add it to the lifetime bests, once.
        if (_game.State is GameState.GameOver or GameState.LevelComplete && !_runRecorded && !(_setup.IsStory && _game.CanContinue))
        {
            // (A level lost by running out of moves may still be continued; its run is recorded when the attempt really ends.)
            _runRecorded = true;
            if (!_setup.IsLesson)   // a tutorial lesson is practice: it adds nothing to the bests or the achievements
            {
                _lifetime.Add(_run, _game.Story);
                _checkAchievements = true;
            }
        }

        // The first time a finish is seen, hold the board for a beat before any result card shows.
        bool finished = _game.State is GameState.LevelComplete or GameState.GameOver;
        if (finished && !_beatStarted) BeginEndBeat();
        else if (!finished) _beatStarted = false;

        HandleCompletion();
        RaiseBonus();
        OnPieceChangedForInsight();

        IsPaused = _game.State == GameState.Paused;
        IsFailed = _setup.HasLevel && _game.State == GameState.GameOver;
        IsClassicOver = !_setup.HasLevel && _game.State == GameState.GameOver;
        IsComplete = _game.State == GameState.LevelComplete;
        OnPropertyChanged(nameof(IsPlaying));
        RaiseHelpers();
        if (_checkAchievements) CheckAchievements();

        if (_game.State != _lastState)
        {
            var previousState = _lastState;
            _lastState = _game.State;
            AudioStateChanged(previousState, _lastState);
            RearmTimer();
        }

        Redraw?.Invoke();
    }

    private void UpdateGoals()
    {
        IReadOnlyList<GoalState> goals = _isLoading ? Array.Empty<GoalState>() : _game.Goals;
        string signature = string.Join(",", goals.Select(g => g.Current));
        if (signature == _goalSignature && goals.Count == _goals.Count) return;
        _goalSignature = signature;
        Goals = goals;
    }

    /// <summary>
    /// Once per finished run, after the level has been paid and the story rewards granted: earn any achievement the
    /// run (or the progress it just made) qualifies for. Each pays its gold once, ever.
    /// </summary>
    private void CheckAchievements()
    {
        _checkAchievements = false;
        _earned = _achievements.OnRunEnded(_run, _game.Story, _setup.HasLevel ? 0 : _game.Score).ToList();
        if (_earned.Count == 0) return;
        RaiseHeader();
        UpdateOverlay();
    }

    /// <summary>Pays out and saves progress, once, the moment a level is cleared.</summary>
    private void HandleCompletion()
    {
        if (_game.State != GameState.LevelComplete || _completionHandled || _game.Story is null) return;
        _completionHandled = true;

        // A tutorial lesson is outside the Story: remember it was finished and nothing else (no progress, gold or scenes).
        if (_setup.IsLesson)
        {
            _tutorial.Complete(_setup.Level);
            _result = default;
            RaiseHeader();
            UpdateOverlay();
            return;
        }

        _result = _progress.Complete(_game.Story, _setup.Rules.GoldFactor);
        int banked = _game.EarnsPopGold ? _classicGold.Bank(_game.ClassicGoldTenths) : 0;   // pop gold on top of the first clear, replays too
        int credit = Math.Min(banked, _lossPaid.PopPart);   // the attempt was lost once before a continue: that half was already paid
        if (credit > 0) _progress.TrySpend(credit);
        _popGoldPaid = banked - credit;
        _chipHold = _result.GoldAwarded + _popGoldPaid;                                      // the chip counts up to it as the coins land

        // Story rewards are granted here, before any scene is shown, so skipping a scene can never lose one.
        _outcome = _campaign.OnLevelCompleted(_game.Story.Number, _result);
        if (_outcome.CosmeticUnlocked == CampaignService.CosmeticGarden && _loadout.Select("garden"))
        {
            _boardStyleApplied = true;
            OnPropertyChanged(nameof(BoardStyle));
        }
        // An optional objective met on this win uncovers its memory (on a replay too; no gold, no progress).
        _foundLore = _campaign.OnBonusEarned(_game.Story, _run).ToList();

        // Anything new for the Story Book is shown first, on its own, before the story scenes and the result card.
        OpenUnlockPopup();

        if (_outcome.Scenes.Count > 0)
        {
            foreach (var scene in _outcome.Scenes) _sceneQueue.Enqueue(scene);
            if (!_unlockOpen) ShowNextScene();
        }

        // A restored region plays its unlock chime (or, for the last one, the finale) once the scenes have been seen.
        if (_outcome.RegionRestored && RegionCatalog.For(_game.Story.Number) is { } restoredRegion)
        {
            if (restoredRegion.Number >= RegionCatalog.All[^1].Number) _finalePending = true;
            else _unlockPending = restoredRegion.Number + 1;
            FlushPendingAudio();
        }

        MaybeOpenChoice();
        RaiseChoice();
        RaiseHeader();
        UpdateOverlay();
    }

    // ---------------------------------------------------------------- bindable state: "you found something" (before the result card)
    // Journal entries, memories, new people and Prism fragments are shown one at a time, each with a way to open the Story Book at the
    // right page. It appears after the end hold and before any story scene or the result card; nothing in it changes a reward.

    private sealed record UnlockItem(string Kind, string Title, string Text, int BookTab);

    private List<UnlockItem> _unlockItems = new();
    private int _unlockIndex;
    private bool _unlockOpen;

    /// <summary>Raised with the Story Book tab to open (0 Story, 1 Memories, 2 People, 3 World).</summary>
    public event Action<int>? StoryBookRequested;

    public bool UnlockPopupVisible => _unlockOpen && !_endBeat;
    private UnlockItem? CurrentUnlock => _unlockOpen && _unlockIndex < _unlockItems.Count ? _unlockItems[_unlockIndex] : null;
    public string UnlockKind => CurrentUnlock?.Kind ?? string.Empty;
    public string UnlockTitle => CurrentUnlock?.Title ?? string.Empty;
    public string UnlockText => CurrentUnlock?.Text ?? string.Empty;
    public string UnlockProgress => _unlockItems.Count > 1 ? $"{_unlockIndex + 1} / {_unlockItems.Count}" : string.Empty;
    public bool HasUnlockProgress => _unlockItems.Count > 1;
    public string UnlockNextText => _unlockIndex >= _unlockItems.Count - 1 ? "CONTINUE" : "NEXT";
    public string UnlockViewText => "VIEW IN STORY BOOK";

    private static string Brief(string text, int max = 230)
    {
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (text.Length <= max) return text;
        int cut = text.LastIndexOf(' ', max);
        return text[..(cut > max / 2 ? cut : max)].TrimEnd(' ', ',', ';', '.') + "…";
    }

    private static UnlockItem ItemFor(LoreEntry entry) => entry.Kind switch
    {
        LoreKind.Memory => new("MEMORY FOUND", entry.Title, Brief(entry.Text), 1),
        LoreKind.Character => new("NEW IN PEOPLE", entry.Title, Brief(entry.Text), 2),
        LoreKind.Journal => new("JOURNAL ENTRY", entry.Title, Brief(entry.Text), 0),
        _ => new("NEW IN THE STORY BOOK", entry.Title, Brief(entry.Text), 0),
    };

    private void OpenUnlockPopup()
    {
        var items = new List<UnlockItem>();
        if (_outcome.FragmentAwarded)
        {
            string place = _outcome.Region is { } r ? r.Title : "this place";
            items.Add(new("PRISM FRAGMENT", $"A fragment from {place}", "A piece of the Prism has been recovered. It is kept on the World page of your Story Book.", 3));
        }
        foreach (var entry in _outcome.NewLore) items.Add(ItemFor(entry));
        foreach (var entry in _foundLore) items.Add(ItemFor(entry));

        _unlockItems = items;
        _unlockIndex = 0;
        _unlockOpen = items.Count > 0;
        RaiseUnlock();
    }

    private void CloseUnlockPopup()
    {
        _unlockOpen = false;
        RaiseUnlock();
        UpdateOverlay();
        ShowNextScene();   // the scenes that were waiting behind it
    }

    private void UnlockNext()
    {
        if (!_unlockOpen) return;
        if (_unlockIndex < _unlockItems.Count - 1)
        {
            _unlockIndex++;
            RaiseUnlock();
            return;
        }
        CloseUnlockPopup();
    }

    private void UnlockView()
    {
        if (CurrentUnlock is not { } item) return;
        CloseUnlockPopup();
        StoryBookRequested?.Invoke(item.BookTab);
    }

    private void RaiseUnlock()
    {
        OnPropertyChanged(nameof(UnlockPopupVisible));
        OnPropertyChanged(nameof(UnlockKind));
        OnPropertyChanged(nameof(UnlockTitle));
        OnPropertyChanged(nameof(UnlockText));
        OnPropertyChanged(nameof(UnlockProgress));
        OnPropertyChanged(nameof(HasUnlockProgress));
        OnPropertyChanged(nameof(UnlockNextText));
        OnPropertyChanged(nameof(UnlockViewText));
        OnPropertyChanged(nameof(IsOverlayVisible));
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

/// <summary>How the board is held at the end of a level or run (the page animates the matching cue).</summary>
public enum EndBeatKind
{
    /// <summary>The move counter pulses.</summary>
    Win,

    /// <summary>The move counter pulses red.</summary>
    OutOfMoves,

    /// <summary>The top row flashes once.</summary>
    Overflow,

    /// <summary>Reduced motion: a short hold with no pulse.</summary>
    Still,
}
