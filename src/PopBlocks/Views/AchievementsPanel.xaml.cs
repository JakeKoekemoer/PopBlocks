using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// Small proofs of things the player has done, shown inside the workshop's TROPHIES tab. Every achievement is shown with
/// how to earn it (none is hidden or needs grinding), and its gold is paid once, when it is earned. What is still to earn
/// comes first, then what is earned. Everything stays on the device.
/// </summary>
public partial class AchievementsPanel : ContentView, IWorkshopPanel
{
    private readonly AchievementService _achievements;

    public AchievementsPanel(AchievementService achievements)
    {
        InitializeComponent();
        _achievements = achievements;
    }

    /// <summary>Nothing here spends gold, so this never fires; it is part of what a workshop panel offers.</summary>
    public event Action? Changed { add { } remove { } }

    public void Refresh()
    {
        Host.Children.Clear();

        var earned = AchievementCatalog.All.Where(a => _achievements.IsEarned(a.Id)).ToList();
        var toEarn = AchievementCatalog.All.Where(a => !_achievements.IsEarned(a.Id)).ToList();
        int earnedGold = earned.Sum(a => a.Gold);

        Host.Children.Add(Ui.Body($"{_achievements.EarnedCount} OF {_achievements.TotalCount} EARNED  ·  {earnedGold:N0} OF {AchievementCatalog.TotalGold:N0} GOLD", 13));

        if (toEarn.Count > 0)
        {
            Host.Children.Add(Section("TO EARN"));
            foreach (var a in toEarn) Host.Children.Add(MakeCard(a, false));
        }

        if (earned.Count > 0)
        {
            Host.Children.Add(Section("EARNED"));
            foreach (var a in earned) Host.Children.Add(MakeCard(a, true));
        }
    }

    private static View Section(string title)
    {
        var caption = Ui.Caption(title, Ui.Res("Accent", Colors.Cyan));
        caption.Margin = new Thickness(2, 8, 0, 0);
        return caption;
    }

    private static View MakeCard(Achievement a, bool earned)
    {
        var stack = new VerticalStackLayout { Spacing = 3 };
        stack.Children.Add(Ui.Caption(earned ? "EARNED  ✓" : $"+{a.Gold} GOLD WHEN EARNED",
            earned ? Color.FromArgb("#A3E635") : Ui.Res("TextSecondary", Colors.Gray)));
        stack.Children.Add(Ui.Heading(a.Name, 18));
        stack.Children.Add(Ui.Body(a.Description, 13));

        var card = Ui.Card(stack, new Thickness(18, 14));
        card.Opacity = earned ? 1.0 : 0.9;
        SemanticProperties.SetDescription(card, $"{a.Name}. {a.Description} {(earned ? "Earned." : "Not yet earned.")}");
        return card;
    }
}
