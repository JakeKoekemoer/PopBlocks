using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Views;

/// <summary>Builds and styles level tiles for a page and remembers which one is selected.</summary>
internal sealed class LevelTileSet
{
    private readonly PlayerProgress _progress;
    private readonly Action<int> _tapped;
    private readonly Dictionary<int, Border> _tiles = new();

    public int Selected { get; private set; }

    public LevelTileSet(PlayerProgress progress, int selected, Action<int> tapped)
    {
        _progress = progress;
        Selected = selected;
        _tapped = tapped;
    }

    public View Make(int level)
    {
        bool cleared = _progress.IsCleared(level);
        bool unlocked = _progress.IsUnlocked(level);
        bool current = level == _progress.NextLevel;
        bool eased = cleared && _progress.IsEased(level);

        var content = new Grid();
        content.Children.Add(new Label
        {
            Text = level.ToString(),
            FontSize = level >= 1000 ? 14 : level >= 100 ? 16 : 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = current ? Colors.White : unlocked ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#55FFFFFF"),
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
                TextColor = MapStyle.Lime,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 2, 4, 0),
            });
        }
        if (eased)
        {
            // Cleared in its eased form: a small feather. Winning the normal level later pays the gold and takes it off.
            content.Children.Add(new Label
            {
                Text = "\U0001FAB6",
                FontSize = 11,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 4, 2),
            });
        }

        var tile = new Border
        {
            Padding = new Thickness(2),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            Content = content,
        };
        string region = RegionCatalog.For(level)?.Title ?? RegionCatalog.BeyondTitle;
        SemanticProperties.SetDescription(tile, $"Level {level}, {region}"
            + (cleared ? (eased ? ", cleared in its eased form" : ", cleared") : current ? ", your next level" : unlocked ? "" : ", locked"));
        Style(tile, level);
        _tiles[level] = tile;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _tapped(level);
        tile.GestureRecognizers.Add(tap);
        return tile;
    }

    public void Select(int level)
    {
        int previous = Selected;
        Selected = level;
        Restyle(previous);
        Restyle(level);
    }

    private void Restyle(int level)
    {
        if (_tiles.TryGetValue(level, out var tile)) Style(tile, level);
    }

    private void Style(Border tile, int level)
    {
        bool cleared = _progress.IsCleared(level);
        bool unlocked = _progress.IsUnlocked(level);
        bool current = level == _progress.NextLevel;
        bool selected = level == Selected;

        tile.Shadow = null!;
        if (current)
        {
            tile.Background = new LinearGradientBrush(
                new GradientStopCollection { new GradientStop(Color.FromArgb("#0284C7"), 0f), new GradientStop(Color.FromArgb("#7C3AED"), 1f) },
                new Point(0, 0), new Point(1, 1));
            tile.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#38BDF8")), Radius = 14, Opacity = 0.6f, Offset = new Point(0, 0) };
        }
        else if (cleared) tile.Background = new SolidColorBrush(MapStyle.Lime.WithAlpha(0.16f));
        else if (unlocked) tile.Background = new SolidColorBrush(Color.FromArgb("#B3141B38"));
        else tile.Background = new SolidColorBrush(Color.FromArgb("#66141B38"));

        tile.Stroke = selected || current ? Colors.White
            : cleared ? MapStyle.Lime.WithAlpha(0.5f)
            : unlocked ? Color.FromArgb("#44FFFFFF")
            : Color.FromArgb("#18FFFFFF");
        tile.StrokeThickness = selected ? 3 : current ? 2 : 1;
    }
}
