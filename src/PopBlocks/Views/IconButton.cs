using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

public enum IconButtonKind { Secondary, Primary, Ghost }

/// <summary>
/// A button with a small drawn icon beside its text. It looks like the app's Secondary, Primary and Ghost buttons (same
/// shape, height and colours) and works from XAML like a Button: <c>Text</c>, <c>Icon</c>, <c>Kind</c>, <c>Clicked</c> and
/// <c>Command</c>. It is a plain container with a tap, so a finger that drags across it still scrolls the page.
/// </summary>
public sealed class IconButton : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(IconButton), string.Empty, propertyChanged: (b, _, _) => ((IconButton)b).Refresh());

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(IconKind), typeof(IconButton), IconKind.None, propertyChanged: (b, _, _) => ((IconButton)b).Refresh());

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconButtonKind), typeof(IconButton), IconButtonKind.Secondary, propertyChanged: (b, _, _) => ((IconButton)b).Refresh());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton), null);

    public event EventHandler? Clicked;

    private readonly Border _border;
    private readonly GraphicsView _iconView;
    private readonly IconDrawable _drawable = new();
    private readonly Label _label;
    private bool _autoDescription;

    public IconButton()
    {
        _drawable.Kind = IconKind.None;
        _iconView = new GraphicsView
        {
            WidthRequest = 22,
            HeightRequest = 22,
            InputTransparent = true,   // decoration only: the tap belongs to the container
            VerticalOptions = LayoutOptions.Center,
            Drawable = _drawable,
        };
        _label = new Label
        {
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1,
            LineBreakMode = LineBreakMode.NoWrap,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center,
            InputTransparent = true,
        };

        _border = new Border
        {
            Padding = new Thickness(10, 0),
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children = { _iconView, _label },
            },
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OnTappedAsync();
        _border.GestureRecognizers.Add(tap);

        Content = _border;
        Refresh();
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IconKind Icon { get => (IconKind)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IconButtonKind Kind { get => (IconButtonKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) Opacity = IsEnabled ? 1.0 : 0.5;
    }

    private async Task OnTappedAsync()
    {
        if (!IsEnabled) return;
        var command = Command;
        if (command is not null && !command.CanExecute(null)) return;

        var pressed = AnimatePressAsync();   // a quick dip, so the tap is felt; it never delays the action
        try
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            if (command is not null && command.CanExecute(null)) command.Execute(null);
        }
        catch (Exception e)
        {
            Diagnostics.Error("IconButton.Tapped", e);
        }
        await pressed;
    }

    private async Task AnimatePressAsync()
    {
        try
        {
            await _border.ScaleToAsync(0.96, 50, Easing.CubicOut);
            await _border.ScaleToAsync(1.0, 90, Easing.CubicIn);
        }
        catch
        {
            // The page may already be gone after a navigation. Nothing to do.
        }
    }

    private static Color Res(string key, Color fallback) => Ui.Res(key, fallback);

    private static Brush? ResBrush(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush ? brush : null;

    private void Refresh()
    {
        _label.Text = Text;
        _iconView.IsVisible = Icon != IconKind.None;
        _drawable.Kind = Icon;

        Color text;
        switch (Kind)
        {
            case IconButtonKind.Primary:
                text = Colors.White;
                _border.HeightRequest = 56;
                _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) };
                _border.StrokeThickness = 0;
                _border.Background = ResBrush("AccentGradient") ?? new SolidColorBrush(Res("Accent", Colors.Cyan));
                _label.FontSize = 16;
                break;

            case IconButtonKind.Ghost:
                text = Res("TextSecondary", Colors.Gray);
                _border.HeightRequest = 44;
                _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) };
                _border.StrokeThickness = 0;
                _border.Background = new SolidColorBrush(Colors.Transparent);
                _label.FontSize = 13;
                break;

            default:
                text = Res("TextPrimary", Colors.White);
                _border.HeightRequest = 52;
                _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) };
                _border.StrokeThickness = 1;
                _border.Stroke = new SolidColorBrush(Res("GlassStroke", Colors.White.WithAlpha(0.2f)));
                _border.Background = new SolidColorBrush(Res("GlassFill", Color.FromArgb("#B3141B38")));
                _label.FontSize = 14;
                break;
        }

        _label.TextColor = text;
        _drawable.Colour = text;
        _iconView.Invalidate();

        // Read out as one button with its own words, unless the XAML gave it a longer description.
        string current = SemanticProperties.GetDescription(this);
        if (string.IsNullOrEmpty(current) || _autoDescription)
        {
            SemanticProperties.SetDescription(this, Text);
            _autoDescription = true;
        }
    }
}
