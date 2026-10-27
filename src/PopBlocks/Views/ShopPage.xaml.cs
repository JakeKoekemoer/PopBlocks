using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The workbench: where gold from first-time level clears is spent. Items come from <see cref="ShopCatalog"/>;
/// this page only shows what <see cref="ShopService"/> reports. A locked item shows how to discover it, never a price.
/// Purchases are saved on the device.
/// </summary>
public partial class ShopPage : ContentPage
{
    private readonly ShopService _shop;
    private readonly PlayerProgress _progress;
    private readonly ConsumableInventory _stock;
    private ItemCategory _category = ItemCategory.Gameplay;

    public ShopPage(ShopService shop, PlayerProgress progress, ConsumableInventory stock)
    {
        InitializeComponent();
        _shop = shop;
        _progress = progress;
        _stock = stock;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    /// <summary>The tab to open on (set by the workshop before the page is shown).</summary>
    public ItemCategory StartCategory
    {
        get => _category;
        set => _category = value;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private static string CategoryName(ItemCategory c) => c switch
    {
        ItemCategory.Gameplay => "HELPERS",
        ItemCategory.Cosmetic => "LOOKS",
        _ => "SUPPLIES",
    };

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");

        // Tabs only for categories that have something on the shelf, and only when there is a choice.
        var present = ShopCatalog.Items.Select(i => i.Category).Distinct().ToList();
        if (!present.Contains(ItemCategory.Consumable)) present.Add(ItemCategory.Consumable);   // the helpers that are used up
        present.Sort();
        if (!present.Contains(_category) && present.Count > 0) _category = present[0];

        TabsHost.Children.Clear();
        TabsHost.IsVisible = present.Count > 1;
        foreach (var category in present)
        {
            bool selected = category == _category;
            var tab = new Button
            {
                Text = CategoryName(category),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                CornerRadius = 20,
                HeightRequest = 40,
                Padding = new Thickness(18, 0),
                BackgroundColor = selected ? Ui.Res("Accent", Colors.Cyan) : Color.FromArgb("#22FFFFFF"),
                TextColor = selected ? Ui.Res("AccentText", Colors.Black) : Ui.Res("TextSecondary", Colors.Gray),
            };
            var chosen = category;
            tab.Clicked += (_, _) => { _category = chosen; Rebuild(); };
            SemanticProperties.SetDescription(tab, selected ? $"{CategoryName(category)} tab, selected" : $"{CategoryName(category)} tab");
            TabsHost.Children.Add(tab);
        }

        ItemsHost.Children.Clear();
        if (_category == ItemCategory.Consumable)
        {
            foreach (var entry in _stock.Entries())
                ItemsHost.Children.Add(MakeSupplyCard(entry));
        }
        else
        {
            foreach (var entry in _shop.Entries(_category))
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
            buy.Clicked += (_, _) => { if (_stock.TryBuy(kind)) Rebuild(); };
            stack.Children.Add(buy);
        }

        return Ui.Card(stack, new Thickness(20, 18));
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
                if (_shop.TryBuy(id)) Rebuild();
            };
            stack.Children.Add(buy);
        }

        return Ui.Card(stack, new Thickness(20, 18));
    }
}
