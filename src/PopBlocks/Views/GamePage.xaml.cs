using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;
using PopBlocks.ViewModels;

namespace PopBlocks.Views;

public partial class GamePage : ContentPage
{
    // Touch tuning
    private const float DragStepInCells = 0.85f;      // horizontal travel needed per column
    private const float SoftDropStepInCells = 0.9f;   // vertical travel needed per soft-drop row
    private const float TapSlop = 12f;                // movement below this still counts as a tap
    private const float FlickMinCells = 2.5f;         // travel for a hard-drop flick
    private const long FlickMaxMs = 260;              // ...completed within this time

    private enum DragAxis { None, Horizontal, Vertical }

    private readonly GameViewModel _vm;
    private readonly GameSettings _settings;
    private readonly CosmeticLoadout _loadout;
    private readonly ShopService _shop;
    private readonly GameDrawable _board = new();
    private readonly UpcomingDrawable _upcoming = new();
    private readonly PortraitDrawable _portrait = new();   // the small portrait beside a line: Pip, the Warden, a crystal or the mark
    private readonly StageDrawable _stage = new();           // the picture above the panel
    private readonly PipDrawable _bannerPip = new() { Mood = PipMood.Happy };
    private int _bannerVersion;

    private IDispatcherTimer? _animationTimer;
    private IDispatcherTimer? _stageTimer;
    private IDispatcherTimer? _repeatTimer;
    private Action? _repeatAction;
    private Window? _hookedWindow;
    private bool _started;

    // Gesture state
    private float _touchStartX, _touchStartY, _anchorX, _anchorY, _lastY;
    private long _touchStartMs;
    private DragAxis _axis;
    private bool _dragged;

    public GamePage(GameViewModel viewModel, GameSettings settings, ShopService shop, CosmeticLoadout loadout)
    {
        InitializeComponent();

        _vm = viewModel;
        _settings = settings;
        _shop = shop;
        _loadout = loadout;
        BindingContext = _vm;

        _board.Game = _vm.Game;
        _upcoming.Game = _vm.Game;
        BoardView.Drawable = _board;
        NextView.Drawable = _upcoming;
        CoinView.Drawable = new GoldCoinDrawable();
        ScenePipView.Drawable = _portrait;
        ScenePipRightView.Drawable = _portrait;   // the same portrait, shown on whichever side the speaker stands
        StageView.Drawable = _stage;
        BannerPipView.Drawable = _bannerPip;

        _vm.Redraw += OnRedraw;
        _vm.WaveResolved += OnWaveResolved;
        _vm.GameStarted += OnGameStarted;
        _vm.Toast += OnToast;
        _vm.Banner += OnBanner;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        BoardView.StartInteraction += OnTouchStart;
        BoardView.DragInteraction += OnTouchDrag;
        BoardView.EndInteraction += OnTouchEnd;
        BoardView.CancelInteraction += OnTouchCancel;

        WireButton(LeftButton, MoveScreenLeft, repeat: true);
        WireButton(RightButton, MoveScreenRight, repeat: true);
        WireButton(DownButton, _vm.SoftDrop, repeat: true);
        WireButton(RotateButton, _vm.Rotate, repeat: false);
        WireButton(DropButton, _vm.HardDrop, repeat: false);
    }

    /// <summary>Picks Classic, a Story level or a tutorial lesson. Call before the page is shown.</summary>
    public void Configure(GameSetup setup) => _vm.Configure(setup);

    // ------------------------------------------------------------------ page lifecycle

    protected override void OnAppearing()
    {
        base.OnAppearing();

        HookWindow(Window);
        _vm.Attach(Dispatcher);
        if (_vm.SceneVisible) ApplyStage();   // coming back to a page with a story panel still open: restart its animation

        if (!_started)
        {
            _started = true;
            ApplyControlsCollapsed(_settings.ControlsCollapsed, animate: false);
            _vm.NewGame();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopRepeat();
        StopAnimation();
        StopStage();
        UnhookWindow();
        _vm.Detach();
    }

    /// <summary>Back button: pause first, and only leave the game when it is already paused or over.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_vm.TutorialPromptVisible) return base.OnBackButtonPressed();   // leave without answering: it is asked again next time
        if (_vm.SceneVisible)
        {
            // Back steps to the previous message; on the first message it skips the panel rather than leaving the level.
            if (_vm.SceneCanGoBack) _vm.PreviousScene();
            else _vm.SkipScene();
            return true;
        }
        if (_vm.IsPlaying)
        {
            _vm.Pause();
            return true;
        }
        return base.OnBackButtonPressed();
    }

    private void HookWindow(Window? window)
    {
        if (window is null || ReferenceEquals(window, _hookedWindow)) return;
        UnhookWindow();
        _hookedWindow = window;
        window.Deactivated += OnWindowInactive;
        window.Stopped += OnWindowInactive;
    }

    private void UnhookWindow()
    {
        if (_hookedWindow is null) return;
        _hookedWindow.Deactivated -= OnWindowInactive;
        _hookedWindow.Stopped -= OnWindowInactive;
        _hookedWindow = null;
    }

    /// <summary>App went to the background or lost focus (phone call, app switch): pause.</summary>
    private void OnWindowInactive(object? sender, EventArgs e)
    {
        StopRepeat();
        _vm.OnAppInactive();   // pauses, and banks any Classic gold: a swipe-away cannot be caught afterwards
    }

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // ------------------------------------------------------------------ a new game or level begins

    private void OnGameStarted()
    {
        // Level-dependent drawing: upside-down board, landing guide, pop richness.
        _board.Flip = _vm.IsFlipped;
        _board.ShowGhost = _settings.LandingGuideStyle > 0 || (_vm.Level?.ForceGhost ?? false);
        _board.GuideBold = _settings.LandingGuideStyle == 2;
        _board.ReducedMotion = _settings.ReducedMotion;
        _board.Effects.Enabled = _settings.BlastEffects;
        _board.Effects.ReducedMotion = _settings.ReducedMotion;
        _board.Effects.Flair = _shop.FlairLevel;

        // Looks from the wardrobe. They are read once here, so nothing changes in the middle of a level.
        VisualDesign.UseSkin(_loadout.Skin);
        _board.Effects.Style = _loadout.Burst;
        _board.Frame = _loadout.Frame;
        _bannerPip.Accessory = _vm.PipAccessory;
        _bannerPip.Detail = _vm.PipDetail;
        ApplySpeaker();
        BannerPipView.Invalidate();

        // Helpers from the workbench. They only show what is on the board; none of them plays a move.
        _board.LensLevel = _shop.LensLevel;
        _board.ChainGlow = _shop.ChainSenseOwned;
        _board.Theme = BoardPalette.For(_vm.BoardStyle);

        // The DOWN button points up the screen on an upside-down board.
        DownButton.Text = _vm.IsFlipped ? "UP" : "DOWN";

        // Room for as many upcoming pieces as the shop has unlocked.
        int count = Math.Clamp(_shop.PreviewCount, 1, 5);
        NextView.WidthRequest = count switch { 1 => 64, 2 => 100, _ => 132 };

        BoardView.Invalidate();
        NextView.Invalidate();
    }

    // ------------------------------------------------------------------ drawing and effects

    private void OnRedraw()
    {
        _board.HintColumn = _vm.HintColumn;
        _board.HintWidth = _vm.HintWidth;

        BoardView.Invalidate();
        NextView.Invalidate();

        if (_board.NeedsAnimation(Environment.TickCount64))
            StartAnimation();
    }

    private void OnWaveResolved(BurstWave wave)
    {
        _board.Effects.Trigger(wave, Environment.TickCount64);
        StartAnimation();
    }

    /// <summary>
    /// Pop phases and effects need repaints between game ticks, so a ~60 fps timer runs only while
    /// something is animating. When the board is idle nothing redraws except on real state changes.
    /// </summary>
    private void StartAnimation()
    {
        if (_animationTimer is not null) return;
        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _animationTimer.IsRepeating = true;
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        BoardView.Invalidate();
        if (!_board.NeedsAnimation(Environment.TickCount64))
            StopAnimation();
    }

    private void StopAnimation()
    {
        if (_animationTimer is null) return;
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
        BoardView.Invalidate();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameViewModel.PipBrightness) or nameof(GameViewModel.SceneMood) or nameof(GameViewModel.SceneSpeakerId))
        {
            _bannerPip.Brightness = _vm.PipBrightness;
            ApplySpeaker();
            BannerPipView.Invalidate();
        }
        else if (e.PropertyName == nameof(GameViewModel.SceneStage))
        {
            ApplyStage();
        }
    }

    /// <summary>
    /// Sets up the portrait for whoever is speaking. Pip keeps their accessory and lights; Warden lines show the Warden's
    /// silhouette; recordings show a memory crystal (or the Warden's mark for a message), never a character; Mara and Bram
    /// are a plain tinted stand-in until they are designed. (Which side it is on is decided by the view model.)
    /// </summary>
    private void ApplySpeaker()
    {
        bool pip = _vm.SceneSpeakerId is "" or Cast.Pip;
        _portrait.SpeakerId = _vm.SceneSpeakerId;

        var face = _portrait.Pip;
        face.Tint = PortraitTints.For(_vm.SceneSpeakerId);
        face.Brightness = pip ? _vm.PipBrightness : 0.75f;
        face.Accessory = pip ? _vm.PipAccessory : AccessoryStyle.None;
        face.Detail = pip ? _vm.PipDetail : 0;
        face.Mood = _vm.SceneMood;
        ScenePipView.Invalidate();
        ScenePipRightView.Invalidate();
    }

    // ------------------------------------------------------------------ the story stage

    /// <summary>
    /// Points the stage strip at the current line and starts or stops its animation timer. Under Reduced motion the
    /// stage and portraits are still pictures (final frames), so no timer runs at all.
    /// </summary>
    private void ApplyStage()
    {
        var info = _vm.SceneStage;
        bool reduced = _settings.ReducedMotion;

        _stage.ReducedMotion = reduced;
        _stage.PipBrightness = _vm.PipBrightness;
        _stage.SpeakerId = _vm.SceneSpeakerId;
        _stage.Info = info;
        _portrait.Animated = info is not null && !reduced;

        StageView.Invalidate();
        ScenePipView.Invalidate();
        ScenePipRightView.Invalidate();

        if (info is not null && !reduced) StartStage();
        else StopStage();
    }

    /// <summary>~30 fps is plenty for gentle motion, and it runs only while a scene panel is open.</summary>
    private void StartStage()
    {
        if (_stageTimer is not null) return;
        _stageTimer = Dispatcher.CreateTimer();
        _stageTimer.Interval = TimeSpan.FromMilliseconds(33);
        _stageTimer.IsRepeating = true;
        _stageTimer.Tick += OnStageTick;
        _stageTimer.Start();
    }

    private void OnStageTick(object? sender, EventArgs e)
    {
        StageView.Invalidate();
        ScenePipView.Invalidate();
        ScenePipRightView.Invalidate();
    }

    private void StopStage()
    {
        if (_stageTimer is null) return;
        _stageTimer.Stop();
        _stageTimer.Tick -= OnStageTick;
        _stageTimer = null;
    }

    /// <summary>Pip's first-burst / first-chain remark: fades in over the lower board, then away. Touches pass through it.</summary>
    private void OnBanner(StoryScene scene) => _ = ShowBannerAsync(scene);

    private async Task ShowBannerAsync(StoryScene scene)
    {
        int version = ++_bannerVersion;
        try
        {
            BannerLabel.Text = string.Join("\n", scene.Lines.Where(l => !l.IsNarration).Select(l => l.Text));
            _bannerPip.Brightness = _vm.PipBrightness;
            BannerPipView.Invalidate();
            PipBanner.IsVisible = true;
            PipBanner.Opacity = 0;
            await PipBanner.FadeToAsync(1, 180);

            // Long enough to read at a glance; a second line gets a little longer.
            await Task.Delay(2600 + 900 * scene.Lines.Count);
            if (version != _bannerVersion) return;   // a newer banner took over

            await PipBanner.FadeToAsync(0, 400);
            if (version == _bannerVersion) PipBanner.IsVisible = false;
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Banner", ex);
            PipBanner.IsVisible = false;
        }
    }

    private void OnToast(string message) => _ = ShowToastAsync(message);

    private async Task ShowToastAsync(string message)
    {
        try
        {
            ToastLabel.Text = message;
            ToastLabel.IsVisible = true;
            ToastLabel.Opacity = 1;
            await ToastLabel.FadeToAsync(0, 1300, Easing.CubicIn);
            ToastLabel.IsVisible = false;
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Toast", ex);
        }
    }

    // ------------------------------------------------------------------ controls drawer

    private void OnToggleControls(object? sender, EventArgs e) =>
        ApplyControlsCollapsed(!_settings.ControlsCollapsed, animate: true);

    private void ApplyControlsCollapsed(bool collapsed, bool animate)
    {
        _settings.ControlsCollapsed = collapsed;
        DrawerToggle.Text = collapsed ? "SHOW CONTROLS  ▲" : "HIDE CONTROLS  ▼";

        if (!animate)
        {
            ControlsGrid.IsVisible = !collapsed;
            ControlsGrid.Opacity = 1;
            return;
        }
        _ = AnimateDrawerAsync(collapsed);
    }

    private async Task AnimateDrawerAsync(bool collapsed)
    {
        try
        {
            if (collapsed)
            {
                await ControlsGrid.FadeToAsync(0, 120);
                ControlsGrid.IsVisible = false;
                ControlsGrid.Opacity = 1;
            }
            else
            {
                ControlsGrid.Opacity = 0;
                ControlsGrid.IsVisible = true;
                await ControlsGrid.FadeToAsync(1, 160);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Error("Drawer", ex);
            ControlsGrid.IsVisible = !collapsed;
            ControlsGrid.Opacity = 1;
        }
    }

    // ------------------------------------------------------------------ directions
    // On an upside-down board the picture is turned 180 degrees, so screen-left is board-right and
    // screen-up is board-down. Everything the player touches is mapped here.

    private void MoveScreenLeft()
    {
        if (_vm.IsFlipped) _vm.MoveRight(); else _vm.MoveLeft();
    }

    private void MoveScreenRight()
    {
        if (_vm.IsFlipped) _vm.MoveLeft(); else _vm.MoveRight();
    }

    /// <summary>+1 when "down the board" is down the screen, -1 when the board is upside down.</summary>
    private float DownSign => _vm.IsFlipped ? -1f : 1f;

    // ------------------------------------------------------------------ on-screen buttons

    private void WireButton(Button button, Action action, bool repeat)
    {
        button.Pressed += (_, _) =>
        {
            action();
            if (repeat) StartRepeat(action);
        };
        button.Released += (_, _) => StopRepeat();
    }

    private void StartRepeat(Action action)
    {
        StopRepeat();
        _repeatAction = action;
        _repeatTimer = Dispatcher.CreateTimer();
        _repeatTimer.Interval = TimeSpan.FromMilliseconds(220);   // delay before auto-repeat begins
        _repeatTimer.IsRepeating = true;
        _repeatTimer.Tick += OnRepeatTick;
        _repeatTimer.Start();
    }

    private void OnRepeatTick(object? sender, EventArgs e)
    {
        if (!_vm.IsPlaying)
        {
            StopRepeat();
            return;
        }
        if (_repeatTimer is not null)
            _repeatTimer.Interval = TimeSpan.FromMilliseconds(70);   // repeat rate
        _repeatAction?.Invoke();
    }

    private void StopRepeat()
    {
        if (_repeatTimer is not null)
        {
            _repeatTimer.Stop();
            _repeatTimer.Tick -= OnRepeatTick;
            _repeatTimer = null;
        }
        _repeatAction = null;
    }

    // ------------------------------------------------------------------ board gestures
    // Tap = rotate. Drag sideways = move. Slow drag down = soft drop. Fast downward flick = hard drop.
    // (On an upside-down board "down" is toward the top of the screen.)

    private void OnTouchStart(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        var p = e.Touches[0];
        _touchStartX = _anchorX = p.X;
        _touchStartY = _anchorY = _lastY = p.Y;
        _touchStartMs = Environment.TickCount64;
        _axis = DragAxis.None;
        _dragged = false;
    }

    private void OnTouchDrag(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        var p = e.Touches[0];
        _lastY = p.Y;
        float cell = MathF.Max(1f, _board.CellSize);

        if (_axis == DragAxis.None)
        {
            float adx = MathF.Abs(p.X - _touchStartX);
            float ady = MathF.Abs(p.Y - _touchStartY);
            if (MathF.Max(adx, ady) < TapSlop) return;
            _axis = adx >= ady ? DragAxis.Horizontal : DragAxis.Vertical;
            _dragged = true;
        }

        if (_axis == DragAxis.Horizontal)
        {
            float step = cell * DragStepInCells;
            while (p.X - _anchorX >= step) { MoveScreenRight(); _anchorX += step; }
            while (_anchorX - p.X >= step) { MoveScreenLeft(); _anchorX -= step; }
        }
        else
        {
            float sign = DownSign;
            float step = cell * SoftDropStepInCells;
            while (sign * (p.Y - _anchorY) >= step) { _vm.SoftDrop(); _anchorY += sign * step; }
            if (sign * (p.Y - _anchorY) < -step) _anchorY = p.Y + sign * step;   // dragging back never "banks" drops
        }
    }

    private void OnTouchEnd(object? sender, TouchEventArgs e)
    {
        float endY = e.Touches.Length > 0 ? e.Touches[0].Y : _lastY;

        if (!_dragged)
        {
            _vm.Rotate();   // a tap
            return;
        }

        if (_axis == DragAxis.Vertical)
        {
            float travel = DownSign * (endY - _touchStartY);
            long elapsed = Environment.TickCount64 - _touchStartMs;
            if (travel >= _board.CellSize * FlickMinCells && elapsed <= FlickMaxMs)
                _vm.HardDrop();
        }
    }

    private void OnTouchCancel(object? sender, EventArgs e)
    {
        _dragged = false;
        _axis = DragAxis.None;
    }
}
