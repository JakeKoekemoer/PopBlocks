using System.Windows.Input;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// A key with a drawn icon and no words: the controls under the board and the ability keys beside it. The real
/// <see cref="Button"/> is underneath (so Pressed / Released work for keys that repeat), and the icon and the little count badge
/// sit on top of it without taking the touch.
/// </summary>
public sealed class IconKey : Grid
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(IconKind), typeof(IconKey), IconKind.None, propertyChanged: (b, _, _) => ((IconKey)b).Refresh());

    public static readonly BindableProperty BadgeProperty = BindableProperty.Create(
        nameof(Badge), typeof(string), typeof(IconKey), string.Empty, propertyChanged: (b, _, _) => ((IconKey)b).Refresh());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconKey), null, propertyChanged: (b, _, n) => ((IconKey)b).Pad.Command = n as ICommand);

    /// <summary>Lit up: the main key of a row, or a switch that is on (Insight "always on").</summary>
    public static readonly BindableProperty AccentProperty = BindableProperty.Create(
        nameof(Accent), typeof(bool), typeof(IconKey), false, propertyChanged: (b, _, _) => ((IconKey)b).Refresh());

    private readonly GraphicsView _iconView;
    private readonly IconDrawable _drawable = new();
    private readonly Label _badge;
    private readonly Border _badgeHost;

    /// <summary>The button underneath. The page wires its Pressed / Released for the control keys.</summary>
    public Button Pad { get; }

    public IconKey()
    {
        Pad = new Button
        {
            Text = string.Empty,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ClassId = "NoTap",   // the controls play their own sounds
        };
        if (Application.Current?.Resources.TryGetValue("ControlButton", out var style) == true && style is Style controlStyle)
            Pad.Style = controlStyle;

        _iconView = new GraphicsView
        {
            Drawable = _drawable,
            InputTransparent = true,
            WidthRequest = 26,
            HeightRequest = 26,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        _badge = new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            InputTransparent = true,
        };
        _badgeHost = new Border
        {
            Content = _badge,
            Padding = new Thickness(5, 0),
            MinimumWidthRequest = 18,
            HeightRequest = 18,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#E6141B38"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(9) },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 3, 3),
            InputTransparent = true,
            IsVisible = false,
        };

        Children.Add(Pad);
        Children.Add(_iconView);
        Children.Add(_badgeHost);
        Refresh();
    }

    public IconKind Icon { get => (IconKind)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Badge { get => (string)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public bool Accent { get => (bool)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled) && Pad is not null)
        {
            Pad.IsEnabled = IsEnabled;
            _iconView.Opacity = IsEnabled ? 1.0 : 0.35;
        }
    }

    private void Refresh()
    {
        if (_iconView is null) return;   // (a property set while the constructor is still running)
        _drawable.Kind = Icon;
        _drawable.Colour = Accent ? Colors.White : Ui.Res("TextPrimary", Colors.White);
        _iconView.Invalidate();
        _badge.Text = Badge;
        _badgeHost.IsVisible = !string.IsNullOrEmpty(Badge);

        if (Accent && Application.Current?.Resources.TryGetValue("AccentGradient", out var brush) == true && brush is Brush accent)
            Pad.Background = accent;
        else
            Pad.BackgroundColor = Ui.Res("GlassFill", Color.FromArgb("#B3141B38"));
    }
}
