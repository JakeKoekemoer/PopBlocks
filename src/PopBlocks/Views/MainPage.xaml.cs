using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

public partial class MainPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly HighScoreService _highScores;
    private readonly GameSettings _settings;
    private readonly PlayerProgress _progress;
    private readonly CosmeticLoadout _loadout;
    private bool _loadingSettings;
    private bool _showingFullMenu;   // false: just PLAY and MORE. MORE shows the full menu.

    public MainPage(IServiceProvider services, HighScoreService highScores, GameSettings settings, PlayerProgress progress, CosmeticLoadout loadout)
    {
        Diagnostics.Crumb("MainPage ctor: start");
        InitializeComponent();
        Diagnostics.Crumb("MainPage ctor: xaml loaded");
        _services = services;
        _highScores = highScores;
        _settings = settings;
        _progress = progress;
        _loadout = loadout;

        LogoView.Drawable = new LogoDrawable();
        StartLogoView.Drawable = new LogoDrawable();
        CoinView.Drawable = new GoldCoinDrawable();
        DevButton.IsVisible = DevMenu.Enabled;
        Diagnostics.Crumb("MainPage ctor: done");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Diagnostics.Crumb("MainPage.OnAppearing");

        _loadingSettings = true;
        EffectsSwitch.IsToggled = _settings.BlastEffects;
        ShowGuideStyle();
        MotionSwitch.IsToggled = _settings.ReducedMotion;
        SoundSwitch.IsToggled = _settings.SoundEffects;
        _loadingSettings = false;

        ShowMenu();
        Refresh();
    }

    /// <summary>The Android Back button steps from the full menu back to PLAY / MORE before it leaves the app.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_showingFullMenu) return base.OnBackButtonPressed();
        _showingFullMenu = false;
        ShowMenu();
        return true;
    }

    private void ShowMenu()
    {
        StartMenu.IsVisible = !_showingFullMenu;
        FullMenu.IsVisible = _showingFullMenu;
    }

    private void OnMoreClicked(object? sender, EventArgs e)
    {
        _showingFullMenu = true;
        ShowMenu();
    }

    private void OnBackToStartClicked(object? sender, EventArgs e)
    {
        _showingFullMenu = false;
        ShowMenu();
    }

    /// <summary>Repaints everything that changes as the player progresses.</summary>
    private void Refresh()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");

        int next = _progress.NextLevel;
        var tier = StoryConfig.TierFor(next);
        NextLevelLabel.Text = _progress.HighestCleared == 0 ? "Level 1" : $"Level {next}";
        string place = RegionCatalog.For(next)?.Title ?? RegionCatalog.BeyondTitle;
        NextLevelDetail.Text = _progress.HighestCleared == 0
            ? "Start here."
            : $"{place} · {StoryConfig.Name(tier)} · {_progress.HighestCleared} cleared";
        PlayButton.Text = _progress.HighestCleared == 0 ? "START" : "CONTINUE";

        // Board style is only offered once a style has been earned.
        BoardStyleRow.IsVisible = CosmeticLoadout.Themes.Any(t => _loadout.IsAvailable(t.Id));
        BoardStyleButton.Text = BoardName(_loadout.SelectedBoard).ToUpperInvariant();

        int best = _highScores.GetBest(GameSetup.Classic.BestScoreKey);
        BestLabel.Text = best > 0 ? $"Classic best  {best:N0}" : string.Empty;
        BestLabel.IsVisible = best > 0;
    }

    // ------------------------------------------------------------------ navigation

    private async void OnPlayClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Story(_progress.NextLevel));

    private async void OnClassicClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Classic);

    /// <summary>The tutorial: the guided lessons, from lesson 1. Separate from the Story, so nothing here touches progress.</summary>
    private async void OnTutorialClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Lesson(1));

    private async Task StartAsync(GameSetup setup)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(setup);
        await Navigation.PushAsync(game);
    }

    private async void OnMapClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<WorldMapPage>());

    private async void OnDevClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<DevMenuPage>());

    private async void OnArchiveClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ArchivePage>());

    private async void OnLevelsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<LevelSelectPage>());

    private async void OnShopClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<WorkshopPage>());

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<AboutPage>());

    // ------------------------------------------------------------------ settings

    private static string BoardName(string id) =>
        CosmeticLoadout.Themes.FirstOrDefault(t => t.Id == id)?.Name ?? "Classic";

    /// <summary>Cycles through the board styles the player owns: Classic, then each earned style.</summary>
    private void OnBoardStyleClicked(object? sender, EventArgs e)
    {
        var owned = new List<string> { CosmeticLoadout.DefaultId };
        owned.AddRange(CosmeticLoadout.Themes.Where(t => _loadout.IsAvailable(t.Id)).Select(t => t.Id));

        int at = owned.IndexOf(_loadout.SelectedBoard);
        _loadout.Select(owned[(at + 1) % owned.Count]);
        BoardStyleButton.Text = BoardName(_loadout.SelectedBoard).ToUpperInvariant();
    }

    private void OnEffectsToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.BlastEffects = e.Value;
    }

    private static string GuideName(int style) => style switch { 0 => "OFF", 2 => "BOLD", _ => "OUTLINE" };

    private void ShowGuideStyle()
    {
        GuideButton.Text = GuideName(_settings.LandingGuideStyle);
        SemanticProperties.SetDescription(GuideButton, $"Landing guide: {GuideName(_settings.LandingGuideStyle).ToLowerInvariant()}. Tap to change.");
    }

    private void OnGuideClicked(object? sender, EventArgs e)
    {
        _settings.LandingGuideStyle = (_settings.LandingGuideStyle + 1) % 3;
        ShowGuideStyle();
    }

    private void OnMotionToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ReducedMotion = e.Value;
    }

    private void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.SoundEffects = e.Value;
    }
}
