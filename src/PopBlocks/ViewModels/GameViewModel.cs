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
    private readonly ISoundPlayer _sound;
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
    private readonly UnlockStore _unlocks;
    private List<Achievement> _earned = new();
    private bool _checkAchievements;

    private IDispatcherTimer? _timer;
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

    public GameViewModel(HighScoreService highScores, PlayerProgress progress, ShopService shop, GameSettings settings, ISoundPlayer sound,
        CampaignService campaign, CosmeticLoadout loadout, DevTools dev, LifetimeStats lifetime, EndingService ending,
        AchievementService achievements, ConsumableInventory stock, TutorialProgress tutorial,
        StoryLevels levels, ClassicGold classicGold, UnlockStore unlocks)
    {
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
        _sound = sound;
        _highScores = highScores;
        _progress = progress;
        _shop = shop;
        _settings = settings;

        _game = new GameEngine(new PieceGenerator());
        _game.Changed += OnEngineChanged;
        _game.WaveResolved += wave => WaveResolved?.Invoke(wave);
        _game.EventRaised += OnGameEvent;
        _run.Attach(_game);
        _stock.Attach(_game);   // takes one out of stock when the engine says a helper was used
        _stock.Changed += RaiseHelpers;

        PauseCommand = new Command(Pause);
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
        RerollCommand = new Command(Reroll);
        ToggleRerollHintCommand = new Command(() => { RerollHintOpen = !RerollHintOpen; });
        DismissLoadingCommand = new Command(DismissLoading);
        CopySeedCommand = new Command(CopySeed);

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

    /// <summary>A new game (or level) has just started: the view should refresh anything that depends on the level.</summary>
    public event Action? GameStarted;

    /// <summary>A short message to flash over the board ("No pop! -15").</summary>
    public event Action<string>? Toast;

    public ICommand PauseCommand { get; }
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
        OnPropertyChanged(nameof(SecondChanceDescription));
        OnPropertyChanged(nameof(PrismShiftDescription));
        OnPropertyChanged(nameof(HelperHint));
        OnPropertyChanged(nameof(HasHelperHint));
    }

    /// <summary>Dev shortcuts on the pause / failed card. False in normal Release builds.</summary>
    public bool ShowDevTools => DevMenu.Enabled && _setup.HasLevel && (_isPaused || _isFailed);

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
    public string BoardStyle => _loadout.SelectedBoard;

    // ---------------------------------------------------------------- bindable state: header

    public int Score { get => _score; private set { if (SetProperty(ref _score, value)) OnPropertyChanged(nameof(ScoreText)); } }
    public string ScoreText => _score.ToString("N0");

    public int BestScore { get => _bestScore; private set { if (SetProperty(ref _bestScore, value)) RaiseHeader(); } }

    /// <summary>"LEVEL 12 · EASY" or "CLASSIC".</summary>
    public string TitleText => _setup.IsLesson ? $"LESSON {_setup.Level} OF {StoryConfig.LessonCount}"
        : _setup.IsStory ? $"LEVEL {_setup.Level} · {StoryConfig.Name(StoryConfig.TierFor(_setup.Level)).ToUpperInvariant()}"
        : "CLASSIC";

    /// <summary>Best score in Classic, gold in Story, "TUTORIAL" in a lesson.</summary>
    public string SubText => _setup.IsLesson ? "TUTORIAL" : _setup.IsStory ? $"{_progress.Gold:N0} gold" : $"BEST {_bestScore:N0}  ·  {RunGoldText}";

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

    // ---------------------------------------------------------------- bindable state: optional memory objective

    private BonusObjective? Bonus => _game.Story?.Bonus;

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
    public bool IsOverlayVisible => _isPaused || _isFailed || _isComplete || _isClassicOver;

    public string OverlayTitle => _isComplete ? (_setup.IsLesson ? "LESSON COMPLETE" : "LEVEL COMPLETE")
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
        : _setup.IsLesson ? "Lessons don't pay gold."
        : _setup.Level <= StoryConfig.TutorialLastLevel ? "The first few levels don't pay gold."
        : "Gold is only paid the first time a level is cleared.";
    public bool HasGoldNote => GoldNote.Length > 0;

    private void UpdateOverlay()
    {
        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(OverlayTitle));
        OnPropertyChanged(nameof(OverlayDetail));
        OnPropertyChanged(nameof(NextText));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(RetryText));
        OnPropertyChanged(nameof(HasGoldReward));
        OnPropertyChanged(nameof(GoldRewardText));
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
                ? "The memory on this level is already in your Archive."
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
        Redraw?.Invoke();
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

    public string RerollHintText => "Short on gold? Pop blocks in Classic mode to earn some.";

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

            _setup = _setup with { Seed = seed };
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
        if (_timer is not null) return;
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
            if (cell.Y < PopBlocks.Engine.GameConfig.HiddenRows) return false;
        return true;
    }

    /// <summary>Stops the timer and pauses. Safe to call repeatedly.</summary>
    public void Detach()
    {
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
            _setup = _setup with { Seed = ready };
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
        RerollHintOpen = false;

        // Shop upgrades and settings apply from the start of each game.
        _game.PreviewCount = _shop.PreviewCount;
        _game.ExtraMoves = _shop.ExtraMoves;
        _stock.ApplyTo(_game, _shop);   // helpers carried in; they apply to Story levels that allow them
        _game.Timing = _settings.BlastEffects
            ? new PopTiming(VisualDesign.HighlightMs, VisualDesign.SettleMs)
            : PopTiming.Instant;

        // A level left half-played still counts towards the player's personal bests (once). Tutorial lessons never do.
        if (!_runRecorded && _run.PiecesPlaced > 0 && _game.Story?.IsTutorial != true) _lifetime.Add(_run, _game.Story);
        _run.Reset();
        _runRecorded = false;
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

        _game.Start(_setup);

        RaiseHeader();
        UpdateOverlay();
        RaiseBonus();
        RaiseChoice();
        RaiseHelpers();
        PrefetchNextLevel();
        GameStarted?.Invoke();
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
    public void Rotate() { if (!InputBlocked) _game.Rotate(); }
    public void SoftDrop() { if (!InputBlocked) _game.SoftDrop(); }
    public void HardDrop() { if (!InputBlocked) _game.HardDrop(); }

    // ---------------------------------------------------------------- engine feedback

    private void OnGameEvent(GameEvent e)
    {
        PlaySound(e);
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
        if (!_settings.SoundEffects) return;

        switch (e)
        {
            case GameEvent.Rotated:
                _sound.Play(SoundEffect.Rotate);
                break;

            case GameEvent.HardDropped:
                _droppedHard = true;   // the engine raises PieceLocked straight after this
                break;

            case GameEvent.PieceLocked:
                _sound.Play(_droppedHard ? SoundEffect.HardDrop : SoundEffect.Place);
                _droppedHard = false;
                break;

            case GameEvent.Popped:
                _sound.Play(SoundEffect.Pop, ChainPitch(_game.Chain));
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

        if (!_setup.HasLevel)
        {
            // Save the personal best the moment it is beaten, so an unexpected exit cannot lose it. A run on a seed the
            // player typed in pays gold like any other, but never sets a record (an easy seed would make it meaningless).
            if (_setup.CountsForBest && _highScores.Submit(_setup.BestScoreKey, _game.Score))
                BestScore = _highScores.GetBest(_setup.BestScoreKey);
            IsNewBest = _setup.CountsForBest && _highScores.GetBest(_setup.BestScoreKey) > _bestAtStart;

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
        }

        // The run is over (won, lost, or Classic game over): add it to the lifetime bests, once.
        if (_game.State is GameState.GameOver or GameState.LevelComplete && !_runRecorded)
        {
            _runRecorded = true;
            if (!_setup.IsLesson)   // a tutorial lesson is practice: it adds nothing to the bests or the achievements
            {
                _lifetime.Add(_run, _game.Story);
                _checkAchievements = true;
            }
        }

        HandleCompletion();
        RaiseBonus();

        IsPaused = _game.State == GameState.Paused;
        IsFailed = _setup.HasLevel && _game.State == GameState.GameOver;
        IsClassicOver = !_setup.HasLevel && _game.State == GameState.GameOver;
        IsComplete = _game.State == GameState.LevelComplete;
        OnPropertyChanged(nameof(IsPlaying));
        RaiseHelpers();
        if (_checkAchievements) CheckAchievements();

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

        _result = _progress.Complete(_game.Story);

        // Story rewards are granted here, before any scene is shown, so skipping a scene can never lose one.
        _outcome = _campaign.OnLevelCompleted(_game.Story.Number, _result);
        if (_outcome.CosmeticUnlocked == CampaignService.CosmeticGarden && _loadout.Select("garden"))
        {
            _boardStyleApplied = true;
            OnPropertyChanged(nameof(BoardStyle));
        }
        if (_outcome.Scenes.Count > 0)
        {
            foreach (var scene in _outcome.Scenes) _sceneQueue.Enqueue(scene);
            ShowNextScene();
        }

        // An optional objective met on this win uncovers its memory (on a replay too; no gold, no progress).
        _foundLore = _campaign.OnBonusEarned(_game.Story, _run).ToList();

        MaybeOpenChoice();
        RaiseChoice();
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
