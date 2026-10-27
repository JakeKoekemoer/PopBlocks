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
    private readonly PlayerProgress _progress;
    private readonly PlayerIdentity _identity;
    private readonly PersistentSettings _save;
    private bool _showingFullMenu;   // false: just PLAY and MORE. MORE shows the full menu.

    public MainPage(IServiceProvider services, HighScoreService highScores, PlayerProgress progress,
        PlayerIdentity identity, PersistentSettings save)
    {
        Diagnostics.Crumb("MainPage ctor: start");
        InitializeComponent();
        Diagnostics.Crumb("MainPage ctor: xaml loaded");
        _services = services;
        _highScores = highScores;
        _progress = progress;
        _identity = identity;
        _save = save;

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

        ShowMenu();
        Refresh();

        // Asked once, before the tutorial offer (which comes when level 1 starts): a new player on their first launch, and
        // a player from before names existed on their first launch after the update. Skipping is allowed.
        NamePrompt.IsVisible = _identity.NeedsPrompt && !_save.IsFrozen;

        ShowRestoreNotice();
    }

    /// <summary>
    /// A reinstall that found its save file says so once. A save file that was found but could not be used is reported too,
    /// so the player knows nothing has been written over it and where to look.
    /// </summary>
    private async void ShowRestoreNotice()
    {
        try
        {
            var notice = _save.TakeRestoreNotice();
            if (notice.Kind == RestoreKind.Restored)
            {
                var s = notice.Summary!;
                await DisplayAlert($"Welcome back, {_identity.Name}",
                    $"Your progress was restored from your save file: {s.HighestCleared} levels cleared and {s.Gold:N0} gold.",
                    "OK");
            }
            else if (notice.Kind == RestoreKind.Unreadable)
            {
                await DisplayAlert("Save file found",
                    $"PopBlocks found a save file in {_save.Location} but couldn't use it ({notice.Error}). It hasn't been changed, and nothing will be saved over it until you choose BACK UP NOW in Settings. You can also choose a file by hand there.",
                    "OK");
            }
        }
        catch (Exception e)
        {
            Diagnostics.Error("MainPage.ShowRestoreNotice", e);
        }
    }

    private async void OnNameFindSaveClicked(object? sender, EventArgs e)
    {
        var result = await SaveRestoreFlow.FindFileAsync(this, _save);
        if (result.Done)
        {
            NamePrompt.IsVisible = false;
            RestoredText.Text = result.Message;
            RestoredOverlay.IsVisible = true;
            SemanticScreenReader.Announce(result.Message);
        }
        else if (result.Message.Length > 0)
        {
            await DisplayAlert("Save file", result.Message, "OK");
        }
    }

    private void OnNameContinueClicked(object? sender, EventArgs e) => AnswerNamePrompt(NameEntry.Text);

    private void OnNameSkipClicked(object? sender, EventArgs e) => AnswerNamePrompt(null);

    private void AnswerNamePrompt(string? name)
    {
        _identity.Choose(name);   // an empty answer is a skip: "Traveller"
        NamePrompt.IsVisible = false;
        SemanticScreenReader.Announce($"Hello, {_identity.Name}");
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

        int best = _highScores.GetBest(GameSetup.Classic.BestScoreKey);
        BestLabel.Text = best > 0 ? $"Classic best  {best:N0}" : string.Empty;
        BestLabel.IsVisible = best > 0;
    }

    // ------------------------------------------------------------------ navigation

    private async void OnPlayClicked(object? sender, EventArgs e) =>
        await StartAsync(GameSetup.Story(_progress.NextLevel));

    private async void OnClassicClicked(object? sender, EventArgs e)
    {
        // An empty seed is a random one, chosen when the run starts. A typed seed fixes the pieces; such a run pays gold
        // as usual but never sets a best score.
        string typed = SeedEntry.Text?.Trim() ?? string.Empty;
        if (typed.Length == 0)
        {
            await StartAsync(GameSetup.Classic);
            return;
        }

        if (!int.TryParse(typed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seed))
        {
            SeedPanel.IsVisible = SeedNote.IsVisible = true;
            SeedNote.Text = "A seed is a whole number from 0 to 2,147,483,647.";
            SemanticScreenReader.Announce(SeedNote.Text);
            return;
        }

        await StartAsync(GameSetup.ClassicRun(seed, chosen: true));
    }

    private void OnSeedLinkClicked(object? sender, EventArgs e)
    {
        SeedPanel.IsVisible = !SeedPanel.IsVisible;
        SeedNote.IsVisible = SeedPanel.IsVisible;
        SeedNote.Text = "A run with a seed still pays gold, but it never sets your best score.";
    }

    private void OnSeedClearClicked(object? sender, EventArgs e)
    {
        SeedEntry.Text = string.Empty;
        SeedNote.Text = "A run with a seed still pays gold, but it never sets your best score.";
    }

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

    private async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<SettingsPage>());
}
