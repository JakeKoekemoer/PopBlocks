using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Layouts;
using PopBlocks.Engine;
using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The Story map: the eight regions of the campaign, and inside each region the levels that belong to it, so the player
/// can see at a glance which level lives in which area. "Beyond the Spectrum" (level 201 on) closes the list. One region
/// is open at a time (the one the next level is in, to begin with); tapping another region's header opens it. Tapping a
/// level shows its details at the top; tapping it again plays it.
///
/// Region state comes from the highest cleared level, so the map always agrees with the player's saved progress.
/// </summary>
public partial class WorldMapPage : ContentPage
{
    private const int BeyondKey = 9;        // the section key of the endless band; regions use their own number (1-8)
    private const int BeyondFirstWindow = 40;
    private const int Lookahead = 12;       // locked levels shown beyond the next playable one
    private const int MoreStep = 50;        // each "show more" press

    /// <summary>One collapsible part of the page: the region's header card, and its levels, which are built the first time it is opened.</summary>
    private sealed class Section
    {
        public required Border Card { get; init; }
        public required VerticalStackLayout BodyHost { get; init; }
        public required Label Chevron { get; init; }
        public required Func<View> BuildBody { get; init; }
        public required string Name { get; init; }
        public bool Built { get; set; }
    }

    private readonly IServiceProvider _services;
    private readonly CampaignService _campaign;
    private readonly PlayerProgress _progress;
    private readonly EndingService _ending;
    private readonly StoryLevels _levels;

    private readonly Dictionary<int, Section> _sections = new();
    private readonly Dictionary<int, Border> _tiles = new();
    private LevelTileGrid? _beyondWrap;
    private int _beyondLast;                // the last level of the endless band that has a tile
    private int _selected;                  // the level whose details are shown
    private int _open;                      // the section key that is open

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
        _open = KeyOf(_selected);
        _beyondLast = Math.Max(RegionCatalog.LastCampaignLevel + BeyondFirstWindow, _progress.NextLevel + Lookahead);
        Rebuild();
        ScrollToOpenSection();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    /// <summary>The section a level is listed in: its region, or the endless band.</summary>
    private static int KeyOf(int level) => RegionCatalog.For(level)?.Number ?? BeyondKey;

    // ------------------------------------------------------------------ building

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");
        SummaryLabel.Text = $"{_campaign.RestoredRegionCount} OF {RegionCatalog.All.Count} REGIONS RESTORED  ·  {_progress.HighestCleared} LEVEL{(_progress.HighestCleared == 1 ? "" : "S")} CLEARED";

        _sections.Clear();
        _tiles.Clear();
        _beyondWrap = null;
        RegionsHost.Children.Clear();

        if (_ending.CampaignComplete) RegionsHost.Children.Add(MakeFinalCard());
        foreach (var region in RegionCatalog.All)
            RegionsHost.Children.Add(MakeRegionSection(region));
        RegionsHost.Children.Add(MakeBeyondSection());

        // Open the right section now (building its levels), and show the selected level's details.
        OpenSection(_open, scroll: false);
        ShowDetail();
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

    private View MakeRegionSection(Region region)
    {
        var p = _campaign.ProgressOf(region);
        var picture = new GraphicsView
        {
            HeightRequest = 110,
            Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !p.IsUnlocked },
            InputTransparent = true,
        };

        string caption = p.IsUnlocked
            ? $"{Ui.StateText(p.State)}   ·   {p.LevelsCleared} / {region.LevelCount}   ·   LEVELS {region.FirstLevel}–{region.LastLevel}"
            : $"LOCKED   ·   LEVELS {region.FirstLevel}–{region.LastLevel}";
        string note = p.IsUnlocked
            ? (p.IsRestored ? region.RestoredDescription : region.FadedDescription)
            : $"Reach level {region.FirstLevel} to open this region.";

        return MakeSection(region.Number, $"{region.Number}.  {region.Title}", picture, caption,
            p.IsUnlocked ? Ui.StateColour(p.State) : Ui.Res("TextSecondary", Colors.Gray), note,
            p.IsUnlocked ? p.Fraction : null, Ui.StateColour(p.State), p.IsUnlocked, () => BuildRegionBody(region, p));
    }

    private View MakeBeyondSection()
    {
        int first = RegionCatalog.LastCampaignLevel + 1;
        bool unlocked = _progress.IsUnlocked(first);
        var colour = TierColour(Tier.Extreme);

        string caption = unlocked
            ? $"LEVELS {first}+   ·   {Math.Max(0, _progress.HighestCleared - first + 1)} CLEARED"
            : $"LOCKED   ·   LEVELS {first}+";
        string note = unlocked
            ? "The light goes on, past every map. These levels never end."
            : $"Reach level {first} to go beyond.";

        return MakeSection(BeyondKey, RegionCatalog.BeyondTitle, null, caption, unlocked ? colour : Ui.Res("TextSecondary", Colors.Gray),
            note, null, colour, unlocked, BuildBeyondBody);
    }

    /// <summary>A header card (picture, title, state, note, progress) that opens to show the section's levels.</summary>
    private View MakeSection(int key, string title, View? picture, string caption, Color captionColour, string note,
        double? fraction, Color barColour, bool unlocked, Func<View> buildBody)
    {
        var chevron = new Label
        {
            Text = "▸",
            FontSize = 18,
            TextColor = Ui.Res("TextSecondary", Colors.Gray),
            VerticalOptions = LayoutOptions.Center,
            IsVisible = unlocked,
        };

        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Margin = new Thickness(0, 6, 0, 0) };
        titleRow.Add(new Label
        {
            Text = title,
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ui.Res("TextPrimary", Colors.White),
        }, 0, 0);
        titleRow.Add(chevron, 1, 0);

        var header = new VerticalStackLayout { Spacing = 4 };
        if (picture is not null) header.Children.Add(picture);
        header.Children.Add(titleRow);
        header.Children.Add(Ui.Caption(caption, captionColour));
        header.Children.Add(Ui.Body(note, 13));
        if (fraction is { } f)
        {
            header.Children.Add(new ProgressBar
            {
                Progress = f,
                ProgressColor = barColour,
                BackgroundColor = Color.FromArgb("#22FFFFFF"),
                HeightRequest = 4,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var bodyHost = new VerticalStackLayout { IsVisible = false };
        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Children.Add(header);
        stack.Children.Add(bodyHost);

        var card = Ui.Card(stack, new Thickness(14, 14));
        card.Opacity = unlocked ? 1.0 : 0.65;

        var section = new Section { Card = card, BodyHost = bodyHost, Chevron = chevron, BuildBody = buildBody, Name = title };
        _sections[key] = section;

        SemanticProperties.SetDescription(header, unlocked ? $"{title}. {caption}. Tap to show its levels." : $"{title}. {caption}");

        if (unlocked)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _services.GetService<GameAudio>()?.Ui(UiSound.Tap);
                // Tapping the open region closes it; tapping another opens that one instead.
                if (_open == key) CloseSection(key);
                else OpenSection(key, scroll: true);
            };
            header.GestureRecognizers.Add(tap);
        }
        return card;
    }

    private void OpenSection(int key, bool scroll)
    {
        if (_open != key && _sections.TryGetValue(_open, out var previous)) Collapse(previous);
        _open = key;
        if (!_sections.TryGetValue(key, out var section)) return;

        if (!section.Built)
        {
            section.BodyHost.Children.Add(section.BuildBody());
            section.Built = true;
        }
        section.BodyHost.IsVisible = true;
        section.Chevron.Text = "▾";
        if (scroll) ScrollToOpenSection();
    }

    private void CloseSection(int key)
    {
        if (_sections.TryGetValue(key, out var section)) Collapse(section);
        _open = 0;
    }

    private static void Collapse(Section section)
    {
        section.BodyHost.IsVisible = false;
        section.Chevron.Text = "▸";
    }

    private void ScrollToOpenSection()
    {
        // Wait for the layout to settle, then bring the open region's card to the top.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), async () =>
        {
            try
            {
                if (_sections.TryGetValue(_open, out var section))
                    await Scroller.ScrollToAsync(section.Card, ScrollToPosition.Start, false);
            }
            catch (Exception)
            {
                // Scrolling is a convenience; the page works without it.
            }
        });
    }

    // ------------------------------------------------------------------ a region's levels

    /// <summary>What is inside an open region: what it looks like now, what has been found there, and its levels.</summary>
    private View BuildRegionBody(Region region, RegionProgress p)
    {
        var body = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };

        // What has been found here.
        var found = new VerticalStackLayout { Spacing = 4 };
        found.Children.Add(Ui.Caption("FOUND HERE"));
        found.Children.Add(Ui.Body(_campaign.HasFragment(region) ? "◆  Prism fragment" : "◇  Prism fragment: not yet found", 14,
            _campaign.HasFragment(region) ? Ui.Res("TextPrimary", Colors.White) : null));
        foreach (var journal in StoryContent.LoreForRegion(region.Number).Where(l => l.Kind == LoreKind.Journal))
        {
            found.Children.Add(_campaign.HasLore(journal)
                ? Ui.Body($"◆  Journal: {journal.Title}", 14, Ui.Res("TextPrimary", Colors.White))
                : Ui.Body("◇  A journal entry waits here.", 14));
        }
        var restoredLore = StoryContent.LoreForRegion(region.Number).FirstOrDefault(l => l.Kind == LoreKind.RestoredWorld);
        if (p.IsRestored && restoredLore is not null && _campaign.HasLore(restoredLore))
            found.Children.Add(Ui.Body(restoredLore.Text, 13, Ui.Res("TextPrimary", Colors.White)));
        body.Children.Add(found);

        body.Children.Add(Ui.Caption("LEVELS"));

        var wrap = NewWrap();
        for (int n = region.FirstLevel; n <= region.LastLevel; n++)
            wrap.AddTile(MakeTile(n, TierColour(StoryConfig.TierFor(n))));
        body.Children.Add(wrap);
        return body;
    }

    /// <summary>The endless band: tiles from level 201, with SHOW MORE to list further.</summary>
    private View BuildBeyondBody()
    {
        var body = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
        body.Children.Add(Ui.Caption("LEVELS"));

        _beyondWrap = NewWrap();
        var colour = TierColour(Tier.Extreme);
        for (int n = RegionCatalog.LastCampaignLevel + 1; n <= _beyondLast; n++)
            _beyondWrap.AddTile(MakeTile(n, colour));
        body.Children.Add(_beyondWrap);

        var more = new Button
        {
            Text = "SHOW MORE LEVELS",
            Style = (Style)Application.Current!.Resources["GhostButton"],
            HorizontalOptions = LayoutOptions.Center,
        };
        more.Clicked += (_, _) =>
        {
            if (_beyondWrap is null) return;
            int from = _beyondLast + 1;
            _beyondLast += MoreStep;
            var colour2 = TierColour(Tier.Extreme);
            for (int n = from; n <= _beyondLast; n++)
                _beyondWrap.AddTile(MakeTile(n, colour2));
        };
        body.Children.Add(more);
        return body;
    }

    private static LevelTileGrid NewWrap() => new();

    private static Color TierColour(Tier tier) => tier switch
    {
        Tier.Easy => Color.FromArgb("#38BDF8"),
        Tier.Medium => Color.FromArgb("#A3E635"),
        Tier.Hard => Color.FromArgb("#FBBF24"),
        _ => Color.FromArgb("#E879F9"),
    };

    // ------------------------------------------------------------------ level tiles

    private View MakeTile(int level, Color colour)
    {
        bool cleared = _progress.IsCleared(level);
        bool unlocked = _progress.IsUnlocked(level);
        bool current = level == _progress.NextLevel;

        var content = new Grid();
        content.Children.Add(new Label
        {
            Text = level.ToString(),
            FontSize = level >= 1000 ? 13 : level >= 100 ? 15 : 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = unlocked ? Ui.Res("TextPrimary", Colors.White) : Color.FromArgb("#55FFFFFF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        });
        if (cleared)
        {
            content.Children.Add(new Label
            {
                Text = "✓",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = colour,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, -2, 0, 0),
            });
            if (_progress.IsEased(level))
            {
                // Cleared in its eased form: a small feather. Winning the normal level later pays the gold and takes it off.
                content.Children.Add(new Label
                {
                    Text = "ðŸª¶",
                    FontSize = 11,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(0, 0, 0, -2),
                });
            }
        }

        var tile = new Border
        {
            WidthRequest = LevelTileGrid.TileSize,
            HeightRequest = LevelTileGrid.TileSize,
            Padding = new Thickness(2),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            Content = content,
        };
        string region = RegionCatalog.For(level)?.Title ?? RegionCatalog.BeyondTitle;
        SemanticProperties.SetDescription(tile, $"Level {level}, {region}" + (cleared ? (_progress.IsEased(level) ? ", cleared in its eased form" : ", cleared") : current ? ", your next level" : unlocked ? "" : ", locked"));
        StyleTile(tile, level, colour);
        _tiles[level] = tile;

        // Tap a level to see its details; tap the one already shown to play it. (The next level starts selected, so the
        // button is one tap away, and tapping it twice plays it straight away.)
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _services.GetService<GameAudio>()?.Ui(unlocked ? UiSound.Tap : UiSound.Locked);
            if (_selected == level && unlocked)
            {
                await PlayAsync(level);
                return;
            }
            Select(level);
        };
        tile.GestureRecognizers.Add(tap);
        return tile;
    }

    /// <summary>Fill and outline for a tile, from whether it is cleared, next, open or locked, and whether it is the one selected.</summary>
    private void StyleTile(Border tile, int level, Color colour)
    {
        bool cleared = _progress.IsCleared(level);
        bool unlocked = _progress.IsUnlocked(level);
        bool current = level == _progress.NextLevel;
        bool selected = level == _selected;

        tile.BackgroundColor = cleared ? colour.WithAlpha(0.22f)
            : current ? Ui.Res("Panel", Colors.DimGray)
            : unlocked ? Ui.Res("GlassFill", Colors.DimGray)
            : Color.FromArgb("#66141B38");
        tile.Stroke = selected ? Ui.Res("TextPrimary", Colors.White)
            : current ? colour
            : cleared ? colour.WithAlpha(0.6f)
            : unlocked ? Color.FromArgb("#44FFFFFF")
            : Color.FromArgb("#18FFFFFF");
        tile.StrokeThickness = selected || current ? 2.5 : 1;
    }

    private void Select(int level)
    {
        int previous = _selected;
        _selected = level;
        RestyleTile(previous);
        RestyleTile(level);
        ShowDetail();
    }

    private void RestyleTile(int level)
    {
        if (_tiles.TryGetValue(level, out var tile))
            StyleTile(tile, level, TierColour(StoryConfig.TierFor(level)));
    }

    // ------------------------------------------------------------------ the selected level

    /// <summary>Shows the selected level, the region it belongs to, its seed (tap to copy) and a PLAY button.</summary>
    private void ShowDetail()
    {
        int level = _selected;
        bool unlocked = _progress.IsUnlocked(level);
        bool cleared = _progress.IsCleared(level);
        var tier = StoryConfig.TierFor(level);
        var region = RegionCatalog.For(level);

        DetailTitle.Text = $"Level {level}  ·  {StoryConfig.Name(tier)}";
        DetailRegion.Text = (region is null ? RegionCatalog.BeyondTitle : $"{region.Number}. {region.Title}").ToUpperInvariant();
        DetailRegion.TextColor = TierColour(tier);

        string seed = _levels.SeedText(level);
        DetailSeed.Text = seed.Length > 0 && _levels.SeedNumber(level) is not null ? seed + "  ·  tap to copy" : seed;
        DetailSeed.IsVisible = seed.Length > 0;
        SemanticProperties.SetDescription(DetailSeed, seed.Length == 0 ? string.Empty
            : _levels.SeedNumber(level) is not null ? $"{seed}. Tap to copy the number." : seed);

        DetailPlay.IsEnabled = unlocked;
        DetailPlay.Text = cleared ? "REPLAY" : unlocked ? "PLAY" : "LOCKED";
        SemanticProperties.SetDescription(DetailPlay, unlocked ? $"{DetailPlay.Text.ToLowerInvariant()} level {level}" : $"Level {level} is locked");

        DetailHint.IsVisible = false;
    }

    private async void OnSeedTapped(object? sender, TappedEventArgs e)
    {
        if (_levels.SeedNumber(_selected) is not { } seed) return;
        try
        {
            await Clipboard.Default.SetTextAsync(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            DetailHint.Text = "Seed copied.";
            DetailHint.IsVisible = true;
            SemanticScreenReader.Announce("Seed copied");
        }
        catch (Exception)
        {
            // Copying is a convenience; the number is on screen.
        }
    }

    private async void OnDetailPlayClicked(object? sender, EventArgs e)
    {
        if (_progress.IsUnlocked(_selected)) await PlayAsync(_selected);
    }

    private async Task PlayAsync(int level)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(GameSetup.Story(level));
        await Navigation.PushAsync(game);
    }
}
