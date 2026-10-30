using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// The workbench, as two tabs of the workshop: UPGRADES (permanent upgrades) and SUPPLIES (helpers that are used up).
/// Where gold from first-time level clears is spent. Items come from <see cref="ShopCatalog"/> and
/// <see cref="ConsumableCatalog"/>; this panel only shows what <see cref="ShopService"/> and
/// <see cref="ConsumableInventory"/> report. What can be bought comes first and what is still locked comes after, and a
/// locked item shows how to discover it, never a price. Purchases are saved on the device.
/// </summary>
public partial class ShopPanel : ContentView, IWorkshopPanel
{
    private readonly ShopService _shop;
    private readonly ConsumableInventory _stock;

    public ShopPanel(ShopService shop, ConsumableInventory stock)
    {
        InitializeComponent();
        _shop = shop;
        _stock = stock;
    }

    /// <summary>What this panel sells: <see cref="ItemCategory.Gameplay"/> for upgrades, <see cref="ItemCategory.Consumable"/> for supplies.</summary>
    public ItemCategory Category { get; set; } = ItemCategory.Gameplay;

    public event Action? Changed;

    public void Refresh()
    {
        ItemsHost.Children.Clear();

        ItemsHost.Children.Add(ShopArt.Banner(Category == ItemCategory.Consumable ? "banner_supplies.png" : "banner_upgrades.png"));

        if (Category == ItemCategory.Consumable)
        {
            ItemsHost.Children.Add(Ui.Body($"Used up when you use them. Hold up to {ConsumableCatalog.Cap} of each.", 13));
            foreach (var entry in _stock.Entries().OrderBy(e => e.Locked))
                ItemsHost.Children.Add(MakeSupplyCard(entry));
        }
        else
        {
            ItemsHost.Children.Add(Ui.Body("Kept for good. They show you more; they never play for you.", 13));
            foreach (var entry in _shop.Entries(Category).OrderBy(e => e.Locked))
                ItemsHost.Children.Add(MakeCard(entry));
        }

        ItemsHost.Children.Add(new Label
        {
            Text = "Gold is earned by clearing Story levels for the first time, restoring regions and earning achievements. " +
                   "Nothing here is needed to finish the game. There is nothing to buy with real money, ever.",
            FontSize = 12,
            TextColor = Ui.Res("TextSecondary", Colors.Gray),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(12, 8, 12, 0),
        });
    }

    /// <summary>After a purchase: tell the workshop (the gold chip) and redraw, so prices and stock are current.</summary>
    private void Purchased()
    {
        Changed?.Invoke();
        Refresh();
    }

    /// <summary>A helper that is used up: how many are held, and a button to buy one more.</summary>
    private View MakeSupplyCard(ConsumableEntry entry)
    {
        var info = entry.Info;
        var text = Ui.Res("TextPrimary", Colors.White);
        var secondary = Ui.Res("TextSecondary", Colors.Gray);
        var accent = Ui.Res("Accent", Colors.Cyan);

        var pips = new HorizontalStackLayout { Spacing = 5, Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < ConsumableCatalog.Cap; i++)
            pips.Children.Add(new BoxView
            {
                WidthRequest = 22, HeightRequest = 6, CornerRadius = 3,
                Color = i < entry.Count ? accent : Color.FromArgb("#33FFFFFF"),
            });
        SemanticProperties.SetDescription(pips, $"{entry.Count} of {ConsumableCatalog.Cap} in stock");

        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label { Text = info.Title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = text });
        stack.Children.Add(new Label { Text = info.Description, FontSize = 13, TextColor = secondary });
        stack.Children.Add(pips);
        stack.Children.Add(new Label
        {
            Text = $"In stock: {entry.Count} of {ConsumableCatalog.Cap}",
            FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = text, Margin = new Thickness(0, 6, 0, 0),
        });
        stack.Children.Add(new Label { Text = info.Rules, FontSize = 12, TextColor = secondary });

        if (entry.Locked)
        {
            // A hint to discover, never a price.
            stack.Children.Add(new Label
            {
                Text = $"🔒  {entry.LockHint}",
                FontSize = 13, FontAttributes = FontAttributes.Bold,
                TextColor = Ui.Res("Warning", Color.FromArgb("#FBBF24")), Margin = new Thickness(0, 6, 0, 0),
            });
        }
        else if (entry.IsFull)
        {
            stack.Children.Add(new Label
            {
                Text = "STOCK FULL", FontSize = 12, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.2,
                TextColor = accent, Margin = new Thickness(0, 6, 0, 0),
            });
        }
        else
        {
            bool canAfford = entry.CanAfford;
            var buy = new Button
            {
                Text = $"BUY ONE  ·  {info.Price:N0} GOLD",
                FontSize = 14, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1,
                CornerRadius = 24, HeightRequest = 48, Margin = new Thickness(0, 8, 0, 0), IsEnabled = canAfford,
            };
            if (canAfford)
            {
                buy.Background = (Brush)Application.Current!.Resources["GoldGradient"];
                buy.TextColor = Ui.Res("AccentText", Colors.Black);
            }
            else
            {
                buy.BackgroundColor = Color.FromArgb("#22FFFFFF");
                buy.TextColor = secondary;
            }
            var kind = info.Kind;
            buy.Clicked += (_, _) => { if (_stock.TryBuy(kind)) Purchased(); };
            stack.Children.Add(buy);
        }

        return Ui.Card(ShopArt.Beside(stack, ShopArt.ForSupply(info.Kind), entry.Locked), new Thickness(20, 18));
    }

    private View MakeCard(ShopEntry entry)
    {
        var item = entry.Item;
        var text = Ui.Res("TextPrimary", Colors.White);
        var secondary = Ui.Res("TextSecondary", Colors.Gray);
        var accent = Ui.Res("Accent", Colors.Cyan);

        // Level pips: filled for what is owned.
        var pips = new HorizontalStackLayout { Spacing = 5, Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < item.MaxLevel; i++)
        {
            pips.Children.Add(new BoxView
            {
                WidthRequest = item.MaxLevel > 4 ? 16 : 22,
                HeightRequest = 6,
                CornerRadius = 3,
                Color = i < entry.Level ? accent : Color.FromArgb("#33FFFFFF"),
            });
        }
        SemanticProperties.SetDescription(pips, $"{entry.Level} of {item.MaxLevel} levels owned");

        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label { Text = item.Title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = text });
        stack.Children.Add(new Label { Text = item.Description, FontSize = 13, TextColor = secondary });
        stack.Children.Add(pips);
        stack.Children.Add(new Label
        {
            Text = $"Now: {entry.CurrentEffect}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = text,
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (entry.IsMaxed)
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
        else if (entry.Locked)
        {
            // A hint to discover, never a price: the lock is written out as well as shown, so colour is not the only cue.
            stack.Children.Add(new Label { Text = $"Next: {entry.NextEffect}", FontSize = 13, TextColor = secondary });
            stack.Children.Add(new Label
            {
                Text = $"🔒  {entry.LockHint}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Ui.Res("Warning", Color.FromArgb("#FBBF24")),
                Margin = new Thickness(0, 6, 0, 0),
            });
        }
        else
        {
            stack.Children.Add(new Label { Text = $"Next: {entry.NextEffect}", FontSize = 13, TextColor = secondary });

            int cost = entry.NextPrice ?? 0;
            bool canAfford = entry.CanAfford;
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
                buy.TextColor = Ui.Res("AccentText", Colors.Black);
            }
            else
            {
                buy.BackgroundColor = Color.FromArgb("#22FFFFFF");
                buy.TextColor = secondary;
            }

            string id = item.Id;
            buy.Clicked += (_, _) =>
            {
                if (_shop.TryBuy(id)) Purchased();
            };
            stack.Children.Add(buy);
        }

        View body = ShopArt.HeroForUpgrade(item.Id) is { } hero
            ? ShopArt.Above(stack, hero)
            : ShopArt.Beside(stack, ShopArt.ForUpgrade(item.Id), entry.Locked);
        return Ui.Card(body, new Thickness(20, 18));
    }
}
