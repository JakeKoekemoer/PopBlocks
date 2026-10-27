using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>One region: its picture, how far it has recovered, what has been found there, and where to play next.</summary>
public partial class RegionPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly CampaignService _campaign;
    private int _number = 1;

    public RegionPage(IServiceProvider services, CampaignService campaign)
    {
        InitializeComponent();
        _services = services;
        _campaign = campaign;
    }

    /// <summary>Chooses the region (1-8). Call before the page is shown.</summary>
    public void Configure(int regionNumber) => _number = Math.Clamp(regionNumber, 1, RegionCatalog.All.Count);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void Rebuild()
    {
        var region = RegionCatalog.Get(_number);
        var p = _campaign.ProgressOf(region);

        TitleLabel.Text = region.Title;
        StateLabel.Text = $"{Ui.StateText(p.State)}   ·   {p.LevelsCleared} / {region.LevelCount}   ·   LEVELS {region.FirstLevel}–{region.LastLevel}";
        StateLabel.TextColor = Ui.StateColour(p.State);

        Host.Children.Clear();

        Host.Children.Add(new Border
        {
            Padding = 0,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(22) },
            Content = new GraphicsView
            {
                HeightRequest = 200,
                Drawable = new RegionSceneDrawable { Region = region, State = p.State, Locked = !p.IsUnlocked },
                InputTransparent = true,
            },
        });

        Host.Children.Add(new ProgressBar
        {
            Progress = p.Fraction,
            ProgressColor = Ui.StateColour(p.State),
            BackgroundColor = Color.FromArgb("#22FFFFFF"),
            HeightRequest = 5,
        });

        // What the region looks like now: the faded description until it is restored.
        var info = new VerticalStackLayout { Spacing = 6 };
        info.Children.Add(Ui.Caption(p.IsRestored ? "NOW" : "AS IT IS"));
        info.Children.Add(Ui.Body(p.IsRestored ? region.RestoredDescription : region.FadedDescription, 15));
        var restoredLore = StoryContent.LoreForRegion(region.Number).FirstOrDefault(l => l.Kind == LoreKind.RestoredWorld);
        if (p.IsRestored && restoredLore is not null && _campaign.HasLore(restoredLore))
            info.Children.Add(Ui.Body(restoredLore.Text, 14, Ui.Res("TextPrimary", Colors.White)));
        Host.Children.Add(Ui.Card(info));

        // Found here
        var found = new VerticalStackLayout { Spacing = 6 };
        found.Children.Add(Ui.Caption("FOUND HERE"));
        found.Children.Add(Ui.Heading(_campaign.HasFragment(region) ? "◆  Prism fragment" : "◇  Prism fragment: not yet found", 16));
        foreach (var journal in StoryContent.LoreForRegion(region.Number).Where(l => l.Kind == LoreKind.Journal))
        {
            found.Children.Add(_campaign.HasLore(journal)
                ? Ui.Heading($"◆  Journal: {journal.Title}", 16)
                : Ui.Body("◇  A journal entry waits here.", 14));
        }
        Host.Children.Add(Ui.Card(found));

        // Play
        int target = p.IsRestored ? region.FirstLevel : p.NextLevel;
        var play = new Button
        {
            Text = p.IsRestored ? "REPLAY THIS REGION" : p.LevelsCleared == 0 ? $"START LEVEL {target}" : $"CONTINUE · LEVEL {target}",
            Style = (Style)Application.Current!.Resources["PrimaryButton"],
            IsEnabled = p.IsUnlocked,
            HorizontalOptions = LayoutOptions.Fill,
        };
        play.Clicked += async (_, _) =>
        {
            var game = _services.GetRequiredService<GamePage>();
            game.Configure(GameSetup.Story(target));
            await Navigation.PushAsync(game);
        };
        Host.Children.Add(play);
    }
}
