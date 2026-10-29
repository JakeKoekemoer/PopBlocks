using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// Bram's Workshop: one screen with four tabs (UPGRADES, LOOKS, SUPPLIES, TROPHIES), each with a lamp that lights as
/// regions are restored. Lighting a lamp is the only thing the restored regions change here. Every tab is open from the
/// start, so the workshop never stands between the player and the story or settings. It costs no gold itself; the tabs
/// that sell things are <see cref="ShopPanel"/> and <see cref="WardrobePanel"/>. The Archive is not part of the
/// workshop: it opens from the main menu.
/// </summary>
public partial class WorkshopPage : ContentPage
{
    private readonly WorkshopService _workshop;
    private readonly PlayerProgress _progress;
    private readonly IServiceProvider _services;
    private readonly Dictionary<WorkshopTab, View> _panels = new();
    private WorkshopTab _tab = WorkshopTab.Upgrades;

    public WorkshopPage(WorkshopService workshop, PlayerProgress progress, IServiceProvider services)
    {
        InitializeComponent();
        _workshop = workshop;
        _progress = progress;
        _services = services;
        CoinView.Drawable = new GoldCoinDrawable();

        foreach (var _ in WorkshopService.AllStations)
            TabBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    }

    /// <summary>The tab to open on (set before the page is shown).</summary>
    public WorkshopTab StartTab
    {
        get => _tab;
        set => _tab = value;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void UpdateGold() => GoldLabel.Text = _progress.Gold.ToString("N0");

    private void Rebuild()
    {
        UpdateGold();
        SummaryLabel.Text = _workshop.FullyRestored
            ? "EVERY LAMP LIT"
            : $"{_workshop.LitCount} OF {WorkshopService.AllStations.Count} LAMPS LIT";

        BuildTabs();
        ShowLamp();

        var panel = PanelFor(_tab);
        PanelHost.Content = panel;
        if (panel is IWorkshopPanel workshopPanel) workshopPanel.Refresh();
    }

    private void BuildTabs()
    {
        TabBar.Children.Clear();
        var stations = WorkshopService.AllStations;
        for (int i = 0; i < stations.Count; i++)
        {
            var station = stations[i];
            bool lit = _workshop.IsLit(station);
            bool selected = station.Tab == _tab;

            var tab = Ui.TabButton(station.Name.ToUpperInvariant(), selected);
            var chosen = station.Tab;
            tab.Clicked += (_, _) =>
            {
                _tab = chosen;
                Rebuild();
            };
            SemanticProperties.SetDescription(tab, $"{station.Name} tab{(selected ? ", selected" : "")}, lamp {(lit ? "lit" : "dark")}");

            TabBar.Add(tab, i, 0);
            TabBar.Add(LampDot(lit, selected), i, 0);
        }
    }

    /// <summary>A small lamp in the corner of a tab: filled and glowing when lit, an empty ring when dark.</summary>
    private static View LampDot(bool lit, bool selected)
    {
        var ink = selected ? Ui.Res("AccentText", Colors.Black) : Ui.Res("TextSecondary", Colors.Gray);
        var fill = lit ? (selected ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FDE047")) : Colors.Transparent;
        return new Border
        {
            WidthRequest = 9,
            HeightRequest = 9,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 7, 9, 0),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(5) },
            StrokeThickness = lit && !selected ? 0 : 1.5,
            Stroke = new SolidColorBrush(ink),
            Background = new SolidColorBrush(fill),
        };
    }

    /// <summary>The state of the selected lamp, written out as well as drawn so it never relies on colour alone.</summary>
    private void ShowLamp()
    {
        var station = WorkshopService.AllStations.First(s => s.Tab == _tab);
        if (_workshop.IsLit(station))
        {
            LampStateLabel.Text = "LAMP LIT  ✓";
            LampStateLabel.TextColor = Color.FromArgb("#A3E635");
            LampHintLabel.Text = "";
        }
        else
        {
            LampStateLabel.Text = "LAMP DARK";
            LampStateLabel.TextColor = Ui.Res("TextSecondary", Colors.Gray);
            LampHintLabel.Text = $"·  Restore {RegionCatalog.Get(station.RegionNeeded).TitleInSentence} to light it";
        }
    }

    /// <summary>Each tab's panel is built the first time it is opened and kept, so switching back is instant.</summary>
    private View PanelFor(WorkshopTab tab)
    {
        if (_panels.TryGetValue(tab, out var existing)) return existing;

        View panel = tab switch
        {
            WorkshopTab.Upgrades => ShopPanelFor(ItemCategory.Gameplay),
            WorkshopTab.Supplies => ShopPanelFor(ItemCategory.Consumable),
            WorkshopTab.Looks => _services.GetRequiredService<WardrobePanel>(),
            _ => _services.GetRequiredService<TrophiesPanel>(),
        };

        // A purchase changes the gold, and the gold chip lives in this page's header.
        if (panel is IWorkshopPanel workshopPanel) workshopPanel.Changed += UpdateGold;
        _panels[tab] = panel;
        return panel;
    }

    private ShopPanel ShopPanelFor(ItemCategory category)
    {
        var shop = _services.GetRequiredService<ShopPanel>();
        shop.Category = category;
        return shop;
    }
}
