using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>Where gold from first-time level clears is spent. Upgrades are saved on the device.</summary>
public partial class ShopPage : ContentPage
{
    private readonly ShopService _shop;
    private readonly PlayerProgress _progress;

    public ShopPage(ShopService shop, PlayerProgress progress)
    {
        InitializeComponent();
        _shop = shop;
        _progress = progress;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private static Color Resource(string key, Color fallback)
    {
        var resources = Application.Current?.Resources;
        return resources is not null && resources.TryGetValue(key, out var value) && value is Color c ? c : fallback;
    }

    private static Style? GlassStyle()
    {
        var resources = Application.Current?.Resources;
        if (resources is not null && resources.TryGetValue("Glass", out var value))
            return value as Style;
        return null;
    }

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");
        ItemsHost.Children.Clear();

        foreach (var item in _shop.Items())
            ItemsHost.Children.Add(MakeCard(item));

        ItemsHost.Children.Add(new Label
        {
            Text = "Gold is earned by clearing Story levels for the first time. Harder levels pay more. " +
                   "There is nothing to buy with real money, ever.",
            FontSize = 12,
            TextColor = Resource("TextSecondary", Colors.Gray),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(12, 8, 12, 0),
        });
    }

    private View MakeCard(UpgradeInfo item)
    {
        var text = Resource("TextPrimary", Colors.White);
        var secondary = Resource("TextSecondary", Colors.Gray);
        var accent = Resource("Accent", Colors.Cyan);
        var gold = Resource("GoldColor", Colors.Gold);

        // Level pips: filled for what is owned.
        var pips = new HorizontalStackLayout { Spacing = 5, Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < item.MaxLevel; i++)
        {
            pips.Children.Add(new BoxView
            {
                WidthRequest = 22,
                HeightRequest = 6,
                CornerRadius = 3,
                Color = i < item.Level ? accent : Color.FromArgb("#33FFFFFF"),
            });
        }

        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label { Text = item.Title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = text });
        stack.Children.Add(new Label { Text = item.Description, FontSize = 13, TextColor = secondary });
        stack.Children.Add(pips);
        stack.Children.Add(new Label
        {
            Text = $"Now: {item.CurrentEffect}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = text,
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (item.IsMaxed)
        {
            stack.Children.Add(new Label
            {
                Text = "FULLY UPGRADED",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                TextColor = accent,
                Margin = new Thickness(0, 6, 0, 0),
            });
        }
        else
        {
            stack.Children.Add(new Label { Text = $"Next: {item.NextEffect}", FontSize = 13, TextColor = secondary });

            int cost = item.NextCost ?? 0;
            bool canAfford = _progress.Gold >= cost;
            var buy = new Button
            {
                Text = $"BUY  ·  {cost:N0} GOLD",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1,
                CornerRadius = 24,
                HeightRequest = 48,
                Margin = new Thickness(0, 8, 0, 0),
                IsEnabled = canAfford,
            };
            if (canAfford)
            {
                buy.Background = (Brush)Application.Current!.Resources["GoldGradient"];
                buy.TextColor = Resource("AccentText", Colors.Black);
            }
            else
            {
                buy.BackgroundColor = Color.FromArgb("#22FFFFFF");
                buy.TextColor = secondary;
            }

            var kind = item.Kind;
            buy.Clicked += (_, _) =>
            {
                if (_shop.TryBuy(kind)) Rebuild();
            };
            stack.Children.Add(buy);
        }

        var card = new Border { Padding = new Thickness(20, 18), Content = stack };
        if (GlassStyle() is { } style) card.Style = style;
        return card;
    }
}
