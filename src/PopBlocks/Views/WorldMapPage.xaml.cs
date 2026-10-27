using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The eight regions of the campaign and how restored each one is. Region state comes from the highest cleared
/// level, so the map always agrees with the player's saved progress.
/// </summary>
public partial class WorldMapPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly CampaignService _campaign;
    private readonly PlayerProgress _progress;
    private readonly EndingService _ending;

    public WorldMapPage(IServiceProvider services, CampaignService campaign, PlayerProgress progress, EndingService ending)
    {
        InitializeComponent();
        _services = services;
        _campaign = campaign;
        _progress = progress;
        _ending = ending;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void Rebuild()
    {
        int restored = _campaign.RestoredRegionCount;
        SummaryLabel.Text = $"{restored} OF {RegionCatalog.All.Count} REGIONS RESTORED  ·  FRAGMENTS {_campaign.FragmentCount} / {RegionCatalog.All.Count}";

        RegionsHost.Children.Clear();
        if (_ending.CampaignComplete) RegionsHost.Children.Add(MakeFinalCard());
        foreach (var region in RegionCatalog.All)
            RegionsHost.Children.Add(MakeRegionCard(region));

        var all = new Button
        {
            Text = "ALL LEVELS",
            Style = (Style)Application.Current!.Resources["GhostButton"],
            HorizontalOptions = LayoutOptions.Center,
        };
        all.Clicked += async (_, _) => await Navigation.PushAsync(_services.GetRequiredService<LevelSelectPage>());
        RegionsHost.Children.Add(all);
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

    private View MakeRegionCard(Region region)
    {
        var p = _campaign.ProgressOf(region);

        var picture = new GraphicsView
        {
            HeightRequest = 120,
            Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !p.IsUnlocked },
            InputTransparent = true,
        };

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(picture);
        stack.Children.Add(new Label
        {
            Text = $"{region.Number}.  {region.Title}",
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ui.Res("TextPrimary", Colors.White),
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (!p.IsUnlocked)
        {
            stack.Children.Add(Ui.Caption("LOCKED"));
            stack.Children.Add(Ui.Body($"Reach level {region.FirstLevel} to open this region.", 13));
        }
        else
        {
            stack.Children.Add(Ui.Caption($"{Ui.StateText(p.State)}   ·   {p.LevelsCleared} / {region.LevelCount}", Ui.StateColour(p.State)));
            stack.Children.Add(Ui.Body(p.IsRestored ? region.RestoredDescription : region.FadedDescription, 13));
            stack.Children.Add(new ProgressBar
            {
                Progress = p.Fraction,
                ProgressColor = Ui.StateColour(p.State),
                BackgroundColor = Color.FromArgb("#22FFFFFF"),
                HeightRequest = 4,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var card = Ui.Card(stack, new Thickness(14, 14));
        card.Opacity = p.IsUnlocked ? 1.0 : 0.65;

        if (p.IsUnlocked)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                var page = _services.GetRequiredService<RegionPage>();
                page.Configure(region.Number);
                await Navigation.PushAsync(page);
            };
            card.GestureRecognizers.Add(tap);
        }
        return card;
    }
}
