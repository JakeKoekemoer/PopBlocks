using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Layouts;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>Every level the player can reach, grouped by difficulty. Levels unlock in order.</summary>
public partial class LevelSelectPage : ContentPage
{
    private const int FirstWindow = 40;     // levels always shown, at least
    private const int Lookahead = 12;       // locked levels shown beyond the next playable one
    private const int MoreStep = 50;        // each "show more" press

    private readonly IServiceProvider _services;
    private readonly PlayerProgress _progress;
    private int _shownUpTo;

    public LevelSelectPage(IServiceProvider services, PlayerProgress progress)
    {
        InitializeComponent();
        _services = services;
        _progress = progress;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _shownUpTo = Math.Max(_shownUpTo, Math.Max(FirstWindow, _progress.NextLevel + Lookahead));
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnShowMoreClicked(object? sender, EventArgs e)
    {
        _shownUpTo += MoreStep;
        Rebuild();
    }

    private static Color TierColour(Tier tier) => tier switch
    {
        Tier.Easy => Color.FromArgb("#38BDF8"),
        Tier.Medium => Color.FromArgb("#A3E635"),
        Tier.Hard => Color.FromArgb("#FBBF24"),
        _ => Color.FromArgb("#E879F9"),
    };

    private static Color Resource(string key, Color fallback)
    {
        var resources = Application.Current?.Resources;
        return resources is not null && resources.TryGetValue(key, out var value) && value is Color c ? c : fallback;
    }

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");
        ProgressLabel.Text = _progress.HighestCleared == 0
            ? "NOT STARTED"
            : $"{_progress.HighestCleared} LEVEL{(_progress.HighestCleared == 1 ? "" : "S")} CLEARED";

        LevelsHost.Children.Clear();

        foreach (var tier in new[] { Tier.Easy, Tier.Medium, Tier.Hard, Tier.Extreme })
        {
            int first = StoryConfig.FirstLevelOf(tier);
            if (first > _shownUpTo) break;

            int last = tier == Tier.Extreme ? _shownUpTo : Math.Min(_shownUpTo, first + StoryConfig.LengthOf(tier) - 1);
            AddSection(tier, first, last);
        }

        var more = new Button
        {
            Text = "SHOW MORE LEVELS",
            Style = (Style)Application.Current!.Resources["GhostButton"],
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 6, 0, 0),
        };
        more.Clicked += OnShowMoreClicked;
        LevelsHost.Children.Add(more);
    }

    private void AddSection(Tier tier, int first, int last)
    {
        var colour = TierColour(tier);
        string range = tier == Tier.Extreme ? $"{first}+" : $"{first}–{first + StoryConfig.LengthOf(tier) - 1}";

        LevelsHost.Children.Add(new Label
        {
            Text = $"{StoryConfig.Name(tier).ToUpperInvariant()}   ·   {range}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.5,
            TextColor = colour,
            Margin = new Thickness(2, 10, 0, 0),
        });

        var wrap = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
        };

        for (int n = first; n <= last; n++)
            wrap.Children.Add(MakeTile(n, tier, colour));

        LevelsHost.Children.Add(wrap);
    }

    private View MakeTile(int level, Tier tier, Color colour)
    {
        bool cleared = _progress.IsCleared(level);
        bool unlocked = _progress.IsUnlocked(level);
        bool current = level == _progress.NextLevel;

        var fill = cleared ? colour.WithAlpha(0.22f)
            : current ? Resource("Panel", Colors.DimGray)
            : unlocked ? Resource("GlassFill", Colors.DimGray)
            : Color.FromArgb("#66141B38");
        var stroke = current ? colour
            : cleared ? colour.WithAlpha(0.6f)
            : unlocked ? Color.FromArgb("#44FFFFFF")
            : Color.FromArgb("#18FFFFFF");

        var number = new Label
        {
            Text = level.ToString(),
            FontSize = level >= 1000 ? 13 : level >= 100 ? 15 : 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = unlocked ? Resource("TextPrimary", Colors.White) : Color.FromArgb("#55FFFFFF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        var content = new Grid();
        content.Children.Add(number);
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
        }
        else if (level <= StoryConfig.TutorialLastLevel)
        {
            content.Children.Add(new Label
            {
                Text = "LESSON",
                FontSize = 7,
                FontAttributes = FontAttributes.Bold,
                TextColor = colour,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, -4),
            });
        }

        var tile = new Border
        {
            WidthRequest = 54,
            HeightRequest = 54,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(2),
            BackgroundColor = fill,
            Stroke = stroke,
            StrokeThickness = current ? 2.5 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            Content = content,
        };

        if (unlocked)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await PlayAsync(level);
            tile.GestureRecognizers.Add(tap);
        }

        return tile;
    }

    private async Task PlayAsync(int level)
    {
        var game = _services.GetRequiredService<GamePage>();
        game.Configure(GameSetup.Story(level));
        await Navigation.PushAsync(game);
    }
}
