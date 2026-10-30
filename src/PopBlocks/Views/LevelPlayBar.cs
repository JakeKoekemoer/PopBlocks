using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Views;

/// <summary>
/// The bar pinned to the bottom of the map and region pages: which level is selected, which region and tier it is in,
/// its seed (tap to copy, for bug reports) and PLAY / REPLAY.
/// </summary>
internal sealed class LevelPlayBar : Border
{
    private readonly StoryLevels _levels;
    private readonly PlayerProgress _progress;
    private readonly Label _title;
    private readonly Label _region;
    private readonly Label _seed;
    private readonly Button _play;
    private int _level;

    public event EventHandler<int>? PlayRequested;

    public LevelPlayBar(StoryLevels levels, PlayerProgress progress)
    {
        _levels = levels;
        _progress = progress;

        BackgroundColor = Color.FromArgb("#F2141B38");
        Stroke = Color.FromArgb("#33FFFFFF");
        StrokeThickness = 1;
        Padding = new Thickness(20, 12, 20, 14);
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(22, 22, 0, 0) };

        _title = new Label { FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#F8FAFC") };
        _region = new Label { FontSize = 12, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.2 };
        _seed = new Label { FontSize = 12, FontAttributes = FontAttributes.Bold, CharacterSpacing = 0.6, TextColor = Color.FromArgb("#9AA6C8") };
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnSeedTapped;
        _seed.GestureRecognizers.Add(tap);

        _play = new Button
        {
            Style = (Style)Application.Current!.Resources["PrimaryButton"],
            HeightRequest = 48,
            MinimumWidthRequest = 110,
            CornerRadius = 24,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
        };
        _play.Clicked += (_, _) =>
        {
            if (_progress.IsUnlocked(_level)) PlayRequested?.Invoke(this, _level);
        };

        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _region, _seed } };
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Add(text, 0, 0);
        grid.Add(_play, 1, 0);
        Content = grid;
    }

    public void Show(int level)
    {
        _level = level;
        bool unlocked = _progress.IsUnlocked(level);
        bool cleared = _progress.IsCleared(level);
        var tier = StoryConfig.TierFor(level);
        var region = RegionCatalog.For(level);

        _title.Text = $"Level {level}  ·  {StoryConfig.Name(tier)}";
        _region.Text = (region is null ? RegionCatalog.BeyondTitle : $"{region.Number}. {region.Title}").ToUpperInvariant();
        _region.TextColor = MapStyle.TierColour(tier);

        string seed = _levels.SeedText(level);
        bool copyable = _levels.SeedNumber(level) is not null;
        _seed.Text = copyable ? seed + "  ⧉" : seed;
        _seed.IsVisible = seed.Length > 0;
        SemanticProperties.SetDescription(_seed, seed.Length == 0 ? string.Empty : copyable ? $"{seed}. Tap to copy the number." : seed);

        _play.IsEnabled = unlocked;
        _play.Text = cleared ? "REPLAY" : unlocked ? "PLAY" : "LOCKED";
        SemanticProperties.SetDescription(_play, unlocked ? $"{_play.Text.ToLowerInvariant()} level {level}" : $"Level {level} is locked");
    }

    private async void OnSeedTapped(object? sender, TappedEventArgs e)
    {
        int level = _level;
        if (_levels.SeedNumber(level) is not { } seed) return;
        try
        {
            await Clipboard.Default.SetTextAsync(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _seed.Text = "Seed copied  ✓";
            SemanticScreenReader.Announce("Seed copied");
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1600), () =>
            {
                if (_level == level) Show(level);
            });
        }
        catch (Exception)
        {
            // Copying is a convenience; the number is on screen.
        }
    }
}
