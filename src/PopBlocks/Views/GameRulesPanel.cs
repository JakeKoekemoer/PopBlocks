using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The GAME tab of the settings page: the player's own rules. It is built in code, like the workshop panels. Every control
/// writes through <see cref="GameRulesStore"/> as soon as it is let go of (a slider on release), so nothing is lost and the
/// debounced store writes once. Changes that could make the Story stop making sense ask first.
/// </summary>
public sealed class GameRulesPanel : ContentView
{
    private const string WarningTitle = "Heads up: the story may stop making sense.";
    private const string WarningBody =
        "Story levels and their goals were built and solved for the normal rules. With your rules some goals may be impossible "
        + "or too easy, and Pip's 'there is always a way through' promise no longer applies. Easier rules pay half gold. "
        + "Reset to default any time.";

    private static bool _warned;   // asked once per run of the app

    private readonly GameRulesStore _store;

    public GameRulesPanel(GameRulesStore store)
    {
        _store = store;
        Build();
    }

    public void Refresh() => Build();

    private GameRules Rules => _store.Rules;

    // ------------------------------------------------------------------ building the panel

    private void Build()
    {
        var rules = Rules;
        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(0, 0, 0, 12) };

        stack.Children.Add(StatusCard(rules));
        stack.Children.Add(SpeedCard(rules));
        stack.Children.Add(ColourCard(rules));
        stack.Children.Add(ShapeCard(rules));
        stack.Children.Add(ChoiceCard("Burst size", "How many blocks of one colour pop together.",
            new[] { 3, 4, 5, 6 }.Select(n => (n.ToString(), n == rules.MinGroupSize, (Action)(() => Change(r => r with { MinGroupSize = n }, warn: true)))).ToList()));
        stack.Children.Add(ChoiceCard("Solid pieces", "Pieces that are one colour all the way through. They wait where they land until something joins them.",
            new[] { ("OFF", 0.0), ("SOMETIMES", 0.15), ("OFTEN", 0.40) }
                .Select(c => (c.Item1, Math.Abs(c.Item2 - rules.SolidPieceChance) < 0.001, (Action)(() => Change(r => r with { SolidPieceChance = c.Item2 })))).ToList()));

        if (RuleFeatures.TallBoards)
            stack.Children.Add(ChoiceCard("Board height", "Rows from floor to ceiling. Taller boards scroll to follow the stack.",
                GameRules.BoardRowChoices.Select(n => (n.ToString(), n == rules.BoardRows, (Action)(() => Change(r => r with { BoardRows = n }, warn: true)))).ToList()));
        if (RuleFeatures.Fracture)
            stack.Children.Add(SwitchCard("Cracking blocks", "Blocks crack when a pop hits them from two sides.", rules.Fracture,
                on => Change(r => r with { Fracture = on })));
        if (RuleFeatures.Orbs)
        {
            stack.Children.Add(ChoiceCard("Orbs", "Automatic follows the story. Always puts one in every level. Never keeps them out.",
                new[] { ("AUTO", OrbMode.Auto), ("ALWAYS", OrbMode.Always), ("NEVER", OrbMode.Never) }
                    .Select(c => (c.Item1, c.Item2 == rules.Orbs, (Action)(() => Change(r => r with { Orbs = c.Item2 })))).ToList()));
            stack.Children.Add(ChoiceCard("Orb timer", "Pieces an Orb waits before it cracks the blocks around it.",
                GameRules.OrbTimerChoices.Select(n => (n.ToString(), n == rules.OrbTimer, (Action)(() => Change(r => r with { OrbTimer = n })))).ToList()));
        }

        stack.Children.Add(SwitchCard("Use my rules in Story", "Off: your rules apply to Endless only. Lessons and each region's finale always use the normal rules.",
            rules.UseInStory, on => Change(r => r with { UseInStory = on }, warn: on)));

        var reset = new Button
        {
            Text = "RESET TO DEFAULT",
            Style = Application.Current?.Resources.TryGetValue("GhostButton", out var s) == true ? s as Style : null,
            IsEnabled = !rules.IsDefault || rules.UseInStory,
        };
        reset.Clicked += (_, _) =>
        {
            _store.Reset();
            Build();
            SemanticScreenReader.Announce("Rules reset to the normal ones");
        };
        stack.Children.Add(reset);

        Content = stack;
    }

    private static string StatusText(GameRules rules) =>
        rules.IsDefault ? "Default" : rules.IsEasierThanDefault ? "Custom · half gold" : "Custom";

    private View StatusCard(GameRules rules)
    {
        var chip = new Label
        {
            Text = StatusText(rules),
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = rules.IsDefault ? Ui.Res("TextPrimary", Colors.White) : Color.FromArgb("#FFD54A"),
        };
        SemanticProperties.SetDescription(chip, $"Rules: {StatusText(rules)}");
        var body = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Ui.Caption("YOUR RULES"),
                chip,
                Ui.Body(rules.IsDefault
                    ? "The game as it was made. Change anything below to play your own way."
                    : rules.IsEasierThanDefault
                        ? "Some rules are easier than normal, so gold is paid at half the rate everywhere."
                        : "Your own rules are on. Gold is paid at the normal rate.", 13),
            },
        };
        return Ui.Card(body);
    }

    // ---- fall speed

    private View SpeedCard(GameRules rules)
    {
        var value = new Label { FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White), FontSize = 14 };
        void Show(double v) => value.Text = SpeedLabel(v);
        Show(rules.FallSpeedMultiplier);

        var slider = new Slider
        {
            Minimum = GameRules.MinFallSpeed,
            Maximum = GameRules.MaxFallSpeed,
            Value = rules.FallSpeedMultiplier,
            MinimumTrackColor = Ui.Res("Accent", Colors.Cyan),
            ThumbColor = Ui.Res("Accent", Colors.Cyan),
        };
        SemanticProperties.SetDescription(slider, "Fall speed");
        slider.ValueChanged += (_, e) => Show(Snap(e.NewValue));
        // The value is written when the slider is let go, so a drag costs one write.
        slider.DragCompleted += (_, _) =>
        {
            double snapped = Snap(slider.Value);
            slider.Value = snapped;
            Change(r => r with { FallSpeedMultiplier = snapped });
        };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        head.Add(new Label { Text = "Fall speed", FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) }, 0);
        head.Add(value, 1);
        return Ui.Card(new VerticalStackLayout { Spacing = 4, Children = { head, Ui.Body("How fast the piece in your hand falls. Pops and chains keep their pace.", 12), slider } });
    }

    private static double Snap(double v) =>
        Math.Clamp(Math.Round(v / GameRules.FallSpeedStep) * GameRules.FallSpeedStep, GameRules.MinFallSpeed, GameRules.MaxFallSpeed);

    private static string SpeedLabel(double v)
    {
        double s = Snap(v);
        string n = s.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return s > 1.0 ? $"{n}× faster" : s < 1.0 ? $"{n}× slower" : "Normal speed";
    }

    // ---- colours

    private View ColourCard(GameRules rules)
    {
        // Five equal columns, so all five colours always fit whatever the phone's width. (A row of fixed 54-unit chips ran off the
        // edge of the card on narrower phones, and the last colours could not be reached.) The chips are tapped through
        // Ui.TapTarget so that a drag that starts on one still scrolls the page.
        var row = new Grid { ColumnSpacing = 6, HorizontalOptions = LayoutOptions.Fill };
        var colours = new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };
        for (int i = 0; i < colours.Length; i++) row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < colours.Length; i++)
        {
            var colour = colours[i];
            bool on = rules.Allows(colour);
            var chip = new GraphicsView { HeightRequest = 56, Drawable = new ColourChipDrawable { Colour = colour, On = on } };
            var target = Ui.TapTarget(chip, () =>
            {
                var next = rules.EnabledColours.ToList();
                if (on) next.Remove(colour); else next.Add(colour);
                if (next.Count < GameRules.MinColourCount) { SemanticScreenReader.Announce("At least three colours must stay on"); return; }
                Change(r => r with { EnabledColours = next }, warn: true);
            }, $"{Engine_ColourName(colour)} blocks, {(on ? "on" : "off")}. Tap to switch.");
            row.Add(target, i, 0);
        }
        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = $"Colours: {rules.ColourCount} of 5", FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) },
                Ui.Body("Tap a colour to switch it on or off (three at least). Each one has its own symbol.", 13),
                row,
            },
        });
    }

    private static string Engine_ColourName(CellColour c) => c switch
    {
        CellColour.Cyan => "Blue",
        CellColour.Coral => "Red",
        CellColour.Yellow => "Yellow",
        CellColour.Purple => "Purple",
        _ => "Green",
    };

    // ---- shapes

    private View ShapeCard(GameRules rules)
    {
        var wrap = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
        foreach (var shape in ShapeCatalog.Classic.Concat(ShapeCatalog.Exotic))
        {
            bool on = rules.AllowsShape(shape.Name);
            var chip = new GraphicsView { WidthRequest = 54, HeightRequest = 54, Drawable = new ShapeChipDrawable { Shape = shape, On = on } };
            var target = Ui.TapTarget(chip, () =>
            {
                var next = rules.EnabledShapes.ToList();
                if (on) next.Remove(shape.Name); else next.Add(shape.Name);
                bool big = next.Any(n => ShapeCatalog.Find(n).CellCount >= 4);
                if (next.Count < GameRules.MinShapeCount || !big)
                {
                    SemanticScreenReader.Announce("Keep at least three pieces on, and one with four or more blocks");
                    return;
                }
                Change(r => r with { EnabledShapes = next }, warn: true);
            }, $"{shape.Name} piece, {(on ? "on" : "off")}. Tap to switch.");
            target.WidthRequest = 54;
            target.HeightRequest = 54;
            target.Margin = new Thickness(0, 0, 8, 8);
            wrap.Children.Add(target);
        }
        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = $"Pieces: {rules.EnabledShapes.Count} of {ShapeCatalog.Classic.Length + ShapeCatalog.Exotic.Length}", FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) },
                Ui.Body("Tap a shape to switch it on or off. Keep three at least, and one with four blocks or more.", 13),
                wrap,
            },
        });
    }

    // ---- generic cards

    private View ChoiceCard(string title, string help, IReadOnlyList<(string Text, bool Selected, Action Pick)> choices)
    {
        // The chips wrap onto a second line when they do not fit. (A horizontal scroll inside the page's own scroll made the page hard
        // to scroll whenever a finger started on a row of chips.)
        var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
        foreach (var (text, selected, pick) in choices)
        {
            var chip = Ui.Chip(text, selected);
            chip.Margin = new Thickness(0, 0, 8, 8);
            SemanticProperties.SetDescription(chip, $"{title}: {text}{(selected ? ", selected" : "")}");
            chip.Clicked += (_, _) => pick();
            row.Children.Add(chip);
        }
        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = title, FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) },
                Ui.Body(help, 13),
                row,
            },
        });
    }

    private View SwitchCard(string title, string help, bool isOn, Action<bool> toggled)
    {
        var sw = new Switch { IsToggled = isOn, OnColor = Ui.Res("Accent", Colors.Cyan), VerticalOptions = LayoutOptions.Center };
        SemanticProperties.SetDescription(sw, title);
        sw.Toggled += (_, e) => toggled(e.Value);

        var texts = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children = { new Label { Text = title, FontAttributes = FontAttributes.Bold, TextColor = Ui.Res("TextPrimary", Colors.White) }, Ui.Body(help, 13) },
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10 };
        grid.Add(texts, 0);
        grid.Add(sw, 1);
        return Ui.Card(grid);
    }

    // ------------------------------------------------------------------ changing a rule

    /// <summary>Applies a change. A change that can make the Story stop making sense asks once first; refusing puts everything back.</summary>
    private async void Change(Func<GameRules, GameRules> change, bool warn = false)
    {
        var next = change(Rules);
        if (warn && !_warned && !next.IsDefault)
        {
            var page = FindPage();
            if (page is not null)
            {
                bool go = await page.DisplayAlertAsync(WarningTitle, WarningBody, "USE MY RULES", "CANCEL");
                if (!go) { Build(); return; }
            }
            _warned = true;
        }
        _store.Set(next);
        Build();
    }

    private Page? FindPage()
    {
        Element? e = this;
        while (e is not null and not Page) e = e.Parent;
        return e as Page;
    }
}
