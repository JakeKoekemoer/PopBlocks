using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// Small proofs of things the player has done. Every achievement is shown with how to earn it (none is hidden or
/// needs grinding), and its gold is paid once, when it is earned. Everything stays on the device.
/// </summary>
public partial class AchievementsPage : ContentPage
{
    private readonly AchievementService _achievements;

    public AchievementsPage(AchievementService achievements)
    {
        InitializeComponent();
        _achievements = achievements;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void Rebuild()
    {
        SummaryLabel.Text = $"{_achievements.EarnedCount} OF {_achievements.TotalCount} EARNED";
        Host.Children.Clear();

        // Earned first, then the rest in catalogue order.
        var ordered = AchievementCatalog.All
            .OrderByDescending(a => _achievements.IsEarned(a.Id))
            .ToList();

        foreach (var a in ordered)
        {
            bool earned = _achievements.IsEarned(a.Id);
            var stack = new VerticalStackLayout { Spacing = 3 };
            stack.Children.Add(Ui.Caption(earned ? "EARNED  ✓" : $"+{a.Gold} GOLD WHEN EARNED",
                earned ? Color.FromArgb("#A3E635") : Ui.Res("TextSecondary", Colors.Gray)));
            stack.Children.Add(Ui.Heading(a.Name, 18));
            stack.Children.Add(Ui.Body(a.Description, 13));

            var card = Ui.Card(stack, new Thickness(18, 14));
            card.Opacity = earned ? 1.0 : 0.8;
            SemanticProperties.SetDescription(card, $"{a.Name}. {a.Description} {(earned ? "Earned." : "Not yet earned.")}");
            Host.Children.Add(card);
        }
    }
}
