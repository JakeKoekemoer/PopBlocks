using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// Bram's Workshop: a hub that grows lamp by lamp as regions are restored. Lighting a corner is the only thing the
/// restored regions change here. Every door (shop, archive, journal, achievements) is open from the start, so the
/// workshop never stands between the player and the story or settings. It costs no gold.
/// </summary>
public partial class WorkshopPage : ContentPage
{
    private readonly WorkshopService _workshop;
    private readonly PlayerProgress _progress;
    private readonly IServiceProvider _services;

    public WorkshopPage(WorkshopService workshop, PlayerProgress progress, IServiceProvider services)
    {
        InitializeComponent();
        _workshop = workshop;
        _progress = progress;
        _services = services;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");
        SummaryLabel.Text = $"{_workshop.LitCount} OF {WorkshopService.AllStations.Count} LAMPS LIT";
        Host.Children.Clear();

        foreach (var station in WorkshopService.AllStations)
            Host.Children.Add(MakeStation(station));

        if (_workshop.NextToLight is { } next)
            Host.Children.Add(Ui.Body($"Bram is still repairing the next corner. Restore {RegionCatalog.Get(next.RegionNeeded).TitleInSentence} to light it.", 13));
        else
            Host.Children.Add(Ui.Body("Every lamp is lit.", 13));
    }

    private View MakeStation(WorkshopStation station)
    {
        bool lit = _workshop.IsLit(station);
        var stack = new VerticalStackLayout { Spacing = 4 };

        // The state is written as well as drawn, so it never relies on colour alone.
        stack.Children.Add(Ui.Caption(lit ? "LIT  ✓" : "DARK", lit ? Color.FromArgb("#A3E635") : Color.FromArgb("#9AA6C8")));
        stack.Children.Add(Ui.Heading(station.Name));
        stack.Children.Add(Ui.Body(lit ? station.Description : "Not yet lit.", 13));

        var open = OpenAction(station);
        if (open is not null)
        {
            var button = new Button
            {
                Text = "OPEN",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                CornerRadius = 22,
                HeightRequest = 44,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalOptions = LayoutOptions.Start,
                MinimumWidthRequest = 120,
                BackgroundColor = Color.FromArgb("#22FFFFFF"),
                TextColor = Ui.Res("TextPrimary", Colors.White),
            };
            SemanticProperties.SetDescription(button, $"Open {station.Name}");
            button.Clicked += async (_, _) => await open();
            stack.Children.Add(button);
        }

        var card = Ui.Card(stack, new Thickness(20, 16));
        card.Opacity = lit ? 1.0 : 0.72;
        return card;
    }

    private Func<Task>? OpenAction(WorkshopStation station) => station.Target switch
    {
        WorkshopTarget.Shop => async () => await PushShop(ItemCategory.Gameplay),
        WorkshopTarget.Cosmetics => async () => await Navigation.PushAsync(_services.GetRequiredService<WardrobePage>()),
        WorkshopTarget.Archive => async () => await Navigation.PushAsync(_services.GetRequiredService<ArchivePage>()),
        WorkshopTarget.Achievements => async () => await Navigation.PushAsync(_services.GetRequiredService<AchievementsPage>()),
        WorkshopTarget.Journal => async () => await Navigation.PushAsync(_services.GetRequiredService<JournalPage>()),
        _ => null,
    };

    private async Task PushShop(ItemCategory category)
    {
        var shop = _services.GetRequiredService<ShopPage>();
        shop.StartCategory = category;
        await Navigation.PushAsync(shop);
    }
}
