using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine;
using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;
using Region = PopBlocks.Engine.Story.Region;

namespace PopBlocks.Views;

/// <summary>
/// One region of the Story map: its picture and progress, what has been found there, and all of its levels. The same page
/// shows the endless "Beyond the Spectrum" band (key 9), with SHOW MORE. Tap a level to select it, tap it again to play.
/// </summary>
public sealed class RegionPage : ContentPage
{
    public const int BeyondKey = 9;
    private const int BeyondFirstWindow = 40;
    private const int Lookahead = 12;
    private const int MoreStep = 50;

    private readonly IServiceProvider _services;
    private readonly CampaignService _campaign;
    private readonly PlayerProgress _progress;
    private readonly StoryLevels _levels;

    private int _key = 1;
    private int _beyondLast;
    private LevelTileSet? _tiles;
    private LevelTileGrid? _grid;
    private LevelPlayBar? _bar;

    public RegionPage(IServiceProvider services, CampaignService campaign, PlayerProgress progress, StoryLevels levels)
    {
        _services = services;
        _campaign = campaign;
        _progress = progress;
        _levels = levels;
        NavigationPage.SetHasNavigationBar(this, false);
        Background = (Brush)Application.Current!.Resources["PageGradient"];
    }

    /// <summary>Which part of the map to show: a region number (1-8) or <see cref="BeyondKey"/>.</summary>
    public void Configure(int key) => _key = key;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _services.GetService<GameAudio>()?.ShowMenu();
        Build();
    }

    private void Build()
    {
        bool beyond = _key == BeyondKey;
        Region? region = beyond ? null : RegionCatalog.Get(_key);
        int first = beyond ? RegionCatalog.LastCampaignLevel + 1 : region!.FirstLevel;
        int last = beyond ? Math.Max(RegionCatalog.LastCampaignLevel + BeyondFirstWindow, _progress.NextLevel + Lookahead) : region!.LastLevel;
        _beyondLast = last;

        int selected = Math.Clamp(_progress.NextLevel, first, last);
        if (!beyond && _progress.NextLevel > last) selected = first;
        _tiles = new LevelTileSet(_progress, selected, OnTileTapped);
        _bar = new LevelPlayBar(_levels, _progress);
        _bar.PlayRequested += async (_, level) => await PlayAsync(level);
        _bar.Show(selected);

        // Header
        var back = new Button { Text = "←", Style = (Style)Application.Current!.Resources["RoundButton"], FontSize = 18 };
        back.Clicked += async (_, _) => await Navigation.PopAsync();
        SemanticProperties.SetDescription(back, "Back to the map");
        var heading = new VerticalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = beyond ? RegionCatalog.BeyondTitle : $"{region!.Number}. {region.Title}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#F8FAFC") },
                Ui.Caption(beyond ? $"LEVELS {first}+" : $"LEVELS {first}–{last}"),
            },
        };
        var header = new Grid { ColumnSpacing = 12, Margin = new Thickness(20, 16, 20, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        header.Add(back, 0, 0);
        header.Add(heading, 1, 0);

        // Body
        var body = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(20, 0, 20, 24) };
        body.Children.Add(beyond ? BeyondSummary(first) : RegionSummary(region!));

        var legend = new Grid();
        legend.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        legend.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        legend.Add(Ui.Caption("LEVELS"), 0, 0);
        legend.Add(new Label
        {
            Text = "✓ Cleared   ● Next",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#9AA6C8"),
            VerticalOptions = LayoutOptions.Center,
        }, 1, 0);
        body.Children.Add(legend);

        _grid = new LevelTileGrid();
        for (int n = first; n <= last; n++) _grid.AddTile(_tiles.Make(n));
        body.Children.Add(_grid);

        if (beyond)
        {
            var more = new Button
            {
                Text = "SHOW MORE LEVELS",
                Style = (Style)Application.Current!.Resources["GhostButton"],
                HorizontalOptions = LayoutOptions.Center,
            };
            more.Clicked += (_, _) =>
            {
                if (_grid is null || _tiles is null) return;
                int from = _beyondLast + 1;
                _beyondLast += MoreStep;
                for (int n = from; n <= _beyondLast; n++) _grid.AddTile(_tiles.Make(n));
            };
            body.Children.Add(more);
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Add(header, 0, 0);
        root.Add(new ScrollView { Content = body }, 0, 1);
        root.Add(_bar, 0, 2);
        Content = root;
    }

    private View RegionSummary(Region region)
    {
        var p = _campaign.ProgressOf(region);
        var colour = Ui.StateColour(p.State);

        var art = new GraphicsView
        {
            WidthRequest = 104,
            HeightRequest = 104,
            Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !p.IsUnlocked },
            InputTransparent = true,
            VerticalOptions = LayoutOptions.Start,
        };
        var text = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                Ui.Caption(Ui.StateText(p.State), colour),
                new Label { Text = $"{p.LevelsCleared} / {region.LevelCount}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#F8FAFC") },
                Ui.Body(p.IsRestored ? region.RestoredDescription : region.FadedDescription, 13),
                new ProgressBar { Progress = p.Fraction, ProgressColor = colour, BackgroundColor = Color.FromArgb("#22FFFFFF"), HeightRequest = 4, Margin = new Thickness(0, 4, 0, 0) },
            },
        };
        var top = new Grid { ColumnSpacing = 14 };
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        top.Add(art, 0, 0);
        top.Add(text, 1, 0);

        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(top);
        stack.Children.Add(Ui.Caption("FOUND HERE"));
        stack.Children.Add(MapStyle.FoundHere(region, _campaign));
        var restoredLore = StoryContent.LoreForRegion(region.Number).FirstOrDefault(l => l.Kind == LoreKind.RestoredWorld);
        if (p.IsRestored && restoredLore is not null && _campaign.HasLore(restoredLore))
            stack.Children.Add(Ui.Body(restoredLore.Text, 13, Color.FromArgb("#F8FAFC")));
        return Ui.Card(stack, new Thickness(14, 14));
    }

    private View BeyondSummary(int first)
    {
        var colour = MapStyle.TierColour(Tier.Extreme);
        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Ui.Caption($"LEVELS {first}+   ·   {Math.Max(0, _progress.HighestCleared - first + 1)} CLEARED", colour),
                Ui.Body("The light goes on, past every map. These levels never end.", 13),
            },
        };
        return Ui.Card(stack, new Thickness(14, 14));
    }

    private async void OnTileTapped(int level)
    {
        if (_tiles is null || _bar is null) return;
        bool unlocked = _progress.IsUnlocked(level);
        _services.GetService<GameAudio>()?.Ui(unlocked ? UiSound.Tap : UiSound.Locked);
        if (_tiles.Selected == level && unlocked)
        {
            await PlayAsync(level);
            return;
        }
        _tiles.Select(level);
        _bar.Show(level);
    }

    private async Task PlayAsync(int level)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(GameSetup.Story(level));
        await Navigation.PushAsync(game);
    }
}
