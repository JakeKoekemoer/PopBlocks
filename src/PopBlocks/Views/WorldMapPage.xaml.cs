using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine;
using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The Story map. It leads with where the player is: a journey strip of the eight regions, the region the next level is
/// in (with its next few levels, one tap from playing), the regions already restored folded into one row, and the regions
/// still ahead, locked. Opening a region shows all of its levels on the region page. The selected level and its PLAY
/// button are pinned to the bottom.
///
/// Region state comes from the highest cleared level, so the map always agrees with the player's saved progress.
/// </summary>
public partial class WorldMapPage : ContentPage
{
    private const int UpNext = 5;           // levels in the "up next" strip

    private readonly IServiceProvider _services;
    private readonly CampaignService _campaign;
    private readonly PlayerProgress _progress;
    private readonly EndingService _ending;
    private readonly StoryLevels _levels;

    private LevelTileSet? _tiles;
    private LevelPlayBar? _bar;
    private int _selected;

    public WorldMapPage(IServiceProvider services, CampaignService campaign, PlayerProgress progress, EndingService ending, StoryLevels levels)
    {
        InitializeComponent();
        _services = services;
        _campaign = campaign;
        _progress = progress;
        _ending = ending;
        _levels = levels;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _services.GetService<GameAudio>()?.ShowMenu();   // the map shares the menu's quiet theme
        _selected = _progress.NextLevel;
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    // ------------------------------------------------------------------ building

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");
        SummaryLabel.Text = $"{_campaign.RestoredRegionCount} OF {RegionCatalog.All.Count} REGIONS RESTORED  ·  {_progress.HighestCleared} LEVEL{(_progress.HighestCleared == 1 ? "" : "S")} CLEARED";

        _tiles = new LevelTileSet(_progress, _selected, OnTileTapped);
        _bar = new LevelPlayBar(_levels, _progress);
        _bar.PlayRequested += async (_, level) => await PlayAsync(level);
        _bar.Show(_selected);
        BarHost.Content = _bar;

        BuildJourney();

        RegionsHost.Children.Clear();
        if (_ending.CampaignComplete) RegionsHost.Children.Add(MakeFinalCard());

        var current = RegionCatalog.For(_progress.NextLevel);
        var restored = RegionCatalog.All.Where(r => _campaign.ProgressOf(r).IsRestored).ToList();
        if (restored.Count > 0) RegionsHost.Children.Add(MakeRestoredGroup(restored));

        foreach (var region in RegionCatalog.All)
        {
            if (restored.Contains(region)) continue;
            RegionsHost.Children.Add(region == current ? MakeFeaturedCard(region) : MakeRegionRow(region));
        }

        RegionsHost.Children.Add(MakeBeyondRow());
    }

    /// <summary>Eight pips, one per region: restored, the one in progress, or still ahead.</summary>
    private void BuildJourney()
    {
        JourneyHost.Children.Clear();
        JourneyHost.ColumnDefinitions.Clear();
        var current = RegionCatalog.For(_progress.NextLevel);
        int index = 0;
        foreach (var region in RegionCatalog.All)
        {
            var p = _campaign.ProgressOf(region);
            JourneyHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var colour = p.IsRestored ? MapStyle.Lime
                : region == current ? Color.FromArgb("#38BDF8")
                : Color.FromArgb("#26FFFFFF");
            JourneyHost.Add(new BoxView { Color = colour, HeightRequest = 6, CornerRadius = 3 }, index++, 0);
        }
        SemanticProperties.SetDescription(JourneyHost, $"{_campaign.RestoredRegionCount} of {RegionCatalog.All.Count} regions restored");
    }

    /// <summary>The finished map: the Heart with power flowing outward to every region.</summary>
    private View MakeFinalCard()
    {
        var picture = new GraphicsView
        {
            HeightRequest = 150,
            Drawable = new RegionSceneDrawable { Region = RegionCatalog.Get(RegionCatalog.All.Count), State = RegionState.Restored },
            InputTransparent = true,
        };

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(picture);
        stack.Children.Add(new Label
        {
            Text = "The Spectrum, restored",
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ui.Res("TextPrimary", Colors.White),
            Margin = new Thickness(0, 6, 0, 0),
        });
        string look = _ending.Choice is { } c ? $"  ·  {c.Name}" : string.Empty;
        stack.Children.Add(Ui.Caption("EVERY REGION LIT" + look.ToUpperInvariant(), Ui.Res("GoldColor", Colors.Gold)));
        stack.Children.Add(Ui.Body("The light no longer gathers in one place. It flows outward from the Heart to every region.", 13));
        return Ui.Card(stack, new Thickness(14, 14));
    }

    /// <summary>The region the next level is in: art, progress, what has been found, the next few levels and a way to see them all.</summary>
    private View MakeFeaturedCard(Region region)
    {
        var p = _campaign.ProgressOf(region);
        var colour = Ui.StateColour(p.State);

        var art = new GraphicsView
        {
            WidthRequest = 112,
            HeightRequest = 112,
            Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !p.IsUnlocked },
            InputTransparent = true,
            VerticalOptions = LayoutOptions.Start,
        };

        var pill = new Border
        {
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            StrokeThickness = 0,
            Background = new SolidColorBrush(Color.FromArgb("#2638BDF8")),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
            Content = new Label { Text = "YOU ARE HERE", FontSize = 10, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.2, TextColor = Color.FromArgb("#38BDF8") },
        };
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        titleRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        titleRow.Add(new Label { Text = $"{region.Number}. {region.Title}", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) }, 0, 0);
        titleRow.Add(new Label { Text = $"{p.LevelsCleared} / {region.LevelCount}", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = colour, VerticalOptions = LayoutOptions.Center }, 1, 0);

        var text = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                pill,
                titleRow,
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

        int next = _progress.NextLevel;
        int last = Math.Min(region.LastLevel, next + UpNext - 1);
        stack.Children.Add(Ui.Caption("UP NEXT"));
        var grid = new LevelTileGrid();
        for (int n = next; n <= last; n++) grid.AddTile(_tiles!.Make(n));
        stack.Children.Add(grid);

        var all = new Button
        {
            Text = $"All {region.LevelCount} levels  ›",
            Style = (Style)Application.Current!.Resources["SecondaryButton"],
            HeightRequest = 44,
            CornerRadius = 22,
            FontSize = 13,
            MinimumWidthRequest = 0,
        };
        all.Clicked += async (_, _) =>
        {
            _services.GetService<GameAudio>()?.Ui(UiSound.Tap);
            await OpenRegionAsync(region.Number);
        };
        stack.Children.Add(all);

        return Ui.Card(stack, new Thickness(14, 14));
    }

    /// <summary>A compact row for a region: small picture, name, state and a way in. Locked regions are dashed and dimmed.</summary>
    private View MakeRegionRow(Region region)
    {
        var p = _campaign.ProgressOf(region);
        bool unlocked = p.IsUnlocked;

        string caption = unlocked
            ? $"{Ui.StateText(p.State)}   ·   {p.LevelsCleared} / {region.LevelCount}"
            : $"LOCKED   ·   REACH LEVEL {region.FirstLevel}";

        return MakeRow(
            new GraphicsView
            {
                WidthRequest = 56,
                HeightRequest = 56,
                Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !unlocked },
                InputTransparent = true,
            },
            $"{region.Number}. {region.Title}", caption,
            unlocked ? Ui.StateColour(p.State) : Ui.Res("TextSecondary", Colors.Gray),
            unlocked, region.Number);
    }

    private View MakeBeyondRow()
    {
        int first = RegionCatalog.LastCampaignLevel + 1;
        bool unlocked = _progress.IsUnlocked(first);
        var colour = MapStyle.TierColour(Tier.Extreme);
        string caption = unlocked
            ? $"LEVELS {first}+   ·   {Math.Max(0, _progress.HighestCleared - first + 1)} CLEARED"
            : $"LOCKED   ·   REACH LEVEL {first}";

        var icon = new Label
        {
            Text = "∞",
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            TextColor = unlocked ? colour : Ui.Res("TextSecondary", Colors.Gray),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        return MakeRow(icon, RegionCatalog.BeyondTitle, caption, unlocked ? colour : Ui.Res("TextSecondary", Colors.Gray), unlocked, RegionPage.BeyondKey);
    }

    private View MakeRow(View leading, string title, string caption, Color captionColour, bool unlocked, int key)
    {
        var text = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) },
                Ui.Caption(caption, captionColour),
            },
        };
        var trailing = new Label
        {
            Text = unlocked ? "›" : "\U0001F512",
            FontSize = unlocked ? 24 : 16,
            TextColor = Ui.Res("TextSecondary", Colors.Gray),
            VerticalOptions = LayoutOptions.Center,
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Add(leading, 0, 0);
        grid.Add(text, 1, 0);
        grid.Add(trailing, 2, 0);

        var row = new Border { Style = MapStyle.GlassStyle, Padding = new Thickness(12), Content = grid };
        row.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(18) };
        if (!unlocked)
        {
            row.Opacity = 0.6;
            row.StrokeDashArray = new DoubleCollection { 4, 3 };
        }
        SemanticProperties.SetDescription(row, unlocked ? $"{title}. {caption}. Tap to show its levels." : $"{title}. {caption}");

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _services.GetService<GameAudio>()?.Ui(unlocked ? UiSound.Tap : UiSound.Locked);
            if (unlocked) await OpenRegionAsync(key);
        };
        row.GestureRecognizers.Add(tap);
        return row;
    }

    /// <summary>The restored regions folded into one row; tap to list them.</summary>
    private View MakeRestoredGroup(List<Region> restored)
    {
        var host = new VerticalStackLayout { Spacing = 8, IsVisible = false, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var region in restored) host.Children.Add(MakeRegionRow(region));

        var chevron = new Label { Text = "▸", FontSize = 18, TextColor = Ui.Res("TextSecondary", Colors.Gray), VerticalOptions = LayoutOptions.Center };
        string title = $"{restored.Count} region{(restored.Count == 1 ? "" : "s")} restored";

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.Add(new Label { Text = "✓", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = MapStyle.Lime, VerticalOptions = LayoutOptions.Center }, 0, 0);
        header.Add(new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White), VerticalOptions = LayoutOptions.Center }, 1, 0);
        header.Add(chevron, 2, 0);
        SemanticProperties.SetDescription(header, title + ". Tap to show or hide them.");

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _services.GetService<GameAudio>()?.Ui(UiSound.Tap);
            host.IsVisible = !host.IsVisible;
            chevron.Text = host.IsVisible ? "▾" : "▸";
        };
        header.GestureRecognizers.Add(tap);

        var stack = new VerticalStackLayout { Children = { header, host } };
        return Ui.Card(stack, new Thickness(14, 12));
    }

    // ------------------------------------------------------------------ selecting and playing

    private async void OnTileTapped(int level)
    {
        if (_tiles is null || _bar is null) return;
        bool unlocked = _progress.IsUnlocked(level);
        _services.GetService<GameAudio>()?.Ui(unlocked ? UiSound.Tap : UiSound.Locked);
        // Tap a level to select it; tap the one already selected to play it. (The next level starts selected, so the
        // button is one tap away, and tapping it twice plays it straight away.)
        if (_tiles.Selected == level && unlocked)
        {
            await PlayAsync(level);
            return;
        }
        _selected = level;
        _tiles.Select(level);
        _bar.Show(level);
    }

    private async Task OpenRegionAsync(int key)
    {
        var page = _services.GetRequiredService<RegionPage>();
        page.Configure(key);
        await Navigation.PushAsync(page);
    }

    private async Task PlayAsync(int level)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(GameSetup.Story(level));
        await Navigation.PushAsync(game);
    }
}
