using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

public partial class MainPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly HighScoreService _highScores;
    private readonly GameSettings _settings;
    private readonly PlayerProgress _progress;
    private bool _loadingSettings;

    public MainPage(IServiceProvider services, HighScoreService highScores, GameSettings settings, PlayerProgress progress)
    {
        Diagnostics.Crumb("MainPage ctor: start");
        InitializeComponent();
        Diagnostics.Crumb("MainPage ctor: xaml loaded");
        _services = services;
        _highScores = highScores;
        _settings = settings;
        _progress = progress;

        LogoView.Drawable = new LogoDrawable();
        CoinView.Drawable = new GoldCoinDrawable();
        Diagnostics.Crumb("MainPage ctor: done");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Diagnostics.Crumb("MainPage.OnAppearing");

        _loadingSettings = true;
        EffectsSwitch.IsToggled = _settings.BlastEffects;
        GhostSwitch.IsToggled = _settings.ShowGhost;
        _loadingSettings = false;

        Refresh();
    }

    /// <summary>Repaints everything that changes as the player progresses.</summary>
    private void Refresh()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");

        int next = _progress.NextLevel;
        var tier = StoryConfig.TierFor(next);
        NextLevelLabel.Text = _progress.HighestCleared == 0 ? "Level 1" : $"Level {next}";
        NextLevelDetail.Text = _progress.HighestCleared == 0
            ? "Start here. A few quick lessons first."
            : $"{StoryConfig.Name(tier)} · {_progress.HighestCleared} cleared";
        PlayButton.Text = _progress.HighestCleared == 0 ? "START" : "CONTINUE";

        int best = _highScores.GetBest(GameSetup.Classic.BestScoreKey);
        BestLabel.Text = best > 0 ? $"Classic best  {best:N0}" : string.Empty;
        BestLabel.IsVisible = best > 0;
    }

    // ------------------------------------------------------------------ navigation

    private async void OnPlayClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Story(_progress.NextLevel));

    private async void OnClassicClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Classic);

    private async Task StartAsync(GameSetup setup)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(setup);
        await Navigation.PushAsync(game);
    }

    private async void OnLevelsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<LevelSelectPage>());

    private async void OnShopClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ShopPage>());

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<AboutPage>());

    // ------------------------------------------------------------------ settings

    private void OnEffectsToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.BlastEffects = e.Value;
    }

    private void OnGhostToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ShowGhost = e.Value;
    }
}
