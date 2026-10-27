using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// Testing shortcuts: gold, jump to any level, max the shop, reset. Reached from the main menu in development builds
/// only (see <see cref="DevMenu"/>). Everything it does edits saved data directly.
/// </summary>
public partial class DevMenuPage : ContentPage
{
    private readonly DevTools _dev;
    private Label? _status;
    private Entry? _levelEntry;

    public DevMenuPage(DevTools dev)
    {
        InitializeComponent();
        _dev = dev;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Build();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void Build()
    {
        Host.Children.Clear();

        _status = Ui.Body(_dev.Describe(), 14, Ui.Res("TextPrimary", Colors.White));
        Host.Children.Add(Ui.Card(Stack(Ui.Caption("CURRENT SAVE"), _status)));

        // ---- Gold
        var gold = Stack(Ui.Caption("GOLD"));
        gold.Children.Add(ButtonRow(
            ("+100", () => _dev.AddGold(100)),
            ("+1,000", () => _dev.AddGold(1000)),
            ("+10,000", () => _dev.AddGold(10000)),
            ("SET 0", () => _dev.SetGold(0))));
        Host.Children.Add(Ui.Card(gold));

        // ---- Jump to a level
        _levelEntry = new Entry
        {
            Placeholder = "level number",
            Keyboard = Keyboard.Numeric,
            TextColor = Ui.Res("TextPrimary", Colors.White),
            PlaceholderColor = Ui.Res("TextSecondary", Colors.Gray),
            HorizontalOptions = LayoutOptions.Fill,
        };
        var go = SmallButton("GO", () =>
        {
            if (int.TryParse(_levelEntry.Text, out int n) && n >= 1) _dev.SetNextLevel(n);
        });

        var jump = Stack(
            Ui.Caption("JUMP: MAKE THIS THE NEXT LEVEL"),
            Ui.Body("Levels before it count as cleared. Story flags are rebuilt for that point, so scenes and rewards you have not reached can be tested again.", 12));
        var entryRow = new Grid { ColumnSpacing = 8 };
        entryRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        entryRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        entryRow.Add(_levelEntry, 0, 0);
        entryRow.Add(go, 1, 0);
        jump.Children.Add(entryRow);

        jump.Children.Add(Ui.Caption("QUICK JUMPS"));
        jump.Children.Add(ButtonRow(
            ("1  fresh", () => _dev.SetNextLevel(1)),
            ("13  mid", () => _dev.SetNextLevel(13)),
            ("25  end of R1", () => _dev.SetNextLevel(25)),
            ("26  R2 start", () => _dev.SetNextLevel(26))));
        jump.Children.Add(ButtonRow(
            ("50", () => _dev.SetNextLevel(50)),
            ("100", () => _dev.SetNextLevel(100)),
            ("150", () => _dev.SetNextLevel(150)),
            ("200", () => _dev.SetNextLevel(200)),
            ("201+", () => _dev.SetNextLevel(201))));
        Host.Children.Add(Ui.Card(jump));

        // ---- Shop and story
        var other = Stack(Ui.Caption("SHOP AND STORY"));
        other.Children.Add(ButtonRow(
            ("MAX SHOP", () => _dev.MaxShop()),
            ("UNLOCK ALL LOOKS", () => _dev.UnlockAllLooks()),
            ("FILL HELPERS", () => _dev.FillConsumables()),
            ("RESET STORY FLAGS", () => _dev.ResetStoryFlags())));
        Host.Children.Add(Ui.Card(other));

        // ---- Danger
        var reset = SmallButton("RESET EVERYTHING (FRESH INSTALL)", null);
        reset.TextColor = Ui.Res("Danger", Colors.Red);
        reset.Clicked += async (_, _) =>
        {
            bool yes = await DisplayAlert("Reset everything?", "Level 1, no gold, no upgrades, no story. Settings are kept.", "RESET", "CANCEL");
            if (!yes) return;
            _dev.ResetEverything();
            Refresh();
        };
        Host.Children.Add(Ui.Card(Stack(Ui.Caption("DANGER"), reset)));

        Host.Children.Add(Ui.Body("In a game, pause (or fail) and use DEV: WIN LEVEL to finish the level through the normal completion path.", 12));
    }

    private void Refresh()
    {
        if (_status != null) _status.Text = _dev.Describe();
    }

    private static VerticalStackLayout Stack(params View[] children)
    {
        var s = new VerticalStackLayout { Spacing = 8 };
        foreach (var c in children) s.Children.Add(c);
        return s;
    }

    private Button SmallButton(string text, Action? action)
    {
        var b = new Button
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 18,
            HeightRequest = 40,
            Padding = new Thickness(12, 0),
            TextColor = Ui.Res("TextPrimary", Colors.White),
            BackgroundColor = Ui.Res("Panel", Colors.DimGray),
        };
        if (action != null)
            b.Clicked += (_, _) =>
            {
                action();
                Refresh();
            };
        return b;
    }

    /// <summary>Buttons side by side that wrap onto new lines on a narrow screen.</summary>
    private View ButtonRow(params (string Text, Action Action)[] items)
    {
        var wrap = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
        foreach (var (text, action) in items)
        {
            var b = SmallButton(text, action);
            b.Margin = new Thickness(0, 0, 8, 8);
            wrap.Children.Add(b);
        }
        return wrap;
    }
}
