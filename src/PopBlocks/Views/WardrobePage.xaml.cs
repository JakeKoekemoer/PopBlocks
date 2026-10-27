using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

/// <summary>
/// The paint shelf: block skins, burst styles, boards, piece frames and Pip's accessories. A look is discovered through
/// the story or an achievement first, and only then can it be bought; rewards cost nothing. Locked looks show how to
/// find them, never a price. Everything here is cosmetic: it changes how the game looks and nothing else.
/// </summary>
public partial class WardrobePage : ContentPage
{
    private readonly CosmeticShop _shop;
    private readonly CosmeticLoadout _loadout;
    private readonly PlayerProgress _progress;
    private CosmeticSlot _slot = CosmeticSlot.Skin;

    public WardrobePage(CosmeticShop shop, CosmeticLoadout loadout, PlayerProgress progress)
    {
        InitializeComponent();
        _shop = shop;
        _loadout = loadout;
        _progress = progress;
        CoinView.Drawable = new GoldCoinDrawable();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private static string SlotName(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Skin => "BLOCKS",
        CosmeticSlot.Burst => "BURSTS",
        CosmeticSlot.Board => "BOARDS",
        CosmeticSlot.Frame => "FRAMES",
        _ => "FOR PIP",
    };

    private void Rebuild()
    {
        GoldLabel.Text = _progress.Gold.ToString("N0");

        TabsHost.Children.Clear();
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
        {
            bool selected = slot == _slot;
            var tab = new Button
            {
                Text = SlotName(slot),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                CornerRadius = 20,
                HeightRequest = 40,
                Padding = new Thickness(18, 0),
                BackgroundColor = selected ? Ui.Res("Accent", Colors.Cyan) : Color.FromArgb("#22FFFFFF"),
                TextColor = selected ? Ui.Res("AccentText", Colors.Black) : Ui.Res("TextSecondary", Colors.Gray),
            };
            var chosen = slot;
            tab.Clicked += (_, _) => { _slot = chosen; Rebuild(); };
            SemanticProperties.SetDescription(tab, selected ? $"{SlotName(slot)} tab, selected" : $"{SlotName(slot)} tab");
            TabsHost.Children.Add(tab);
        }

        Host.Children.Clear();
        foreach (var entry in _shop.Entries(_slot))
            Host.Children.Add(MakeCard(entry));

        Host.Children.Add(Ui.Body("Looks are found by restoring regions and earning achievements. Gold is only ever asked for " +
                                  "looks you have already found, and never for anything the story or settings need.", 12));
    }

    private View MakeCard(CosmeticEntry entry)
    {
        var item = entry.Item;
        var stack = new VerticalStackLayout { Spacing = 6 };

        // Locked looks keep their name and picture hidden behind the hint, so discovery stays a surprise.
        bool locked = entry.State == CosmeticState.Locked;
        stack.Children.Add(Ui.Heading(locked ? "Not found yet" : item.Name, 18));
        stack.Children.Add(Ui.Body(locked ? "" : item.Description, 13));
        if (stack.Children[^1] is Label empty && string.IsNullOrEmpty(empty.Text)) stack.Children.RemoveAt(stack.Children.Count - 1);

        if (!locked)
        {
            var preview = MakePreview(item);
            if (preview is not null) stack.Children.Add(preview);
        }

        switch (entry.State)
        {
            case CosmeticState.Locked:
                stack.Children.Add(new Label
                {
                    Text = $"🔒  {entry.Hint}",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Ui.Res("Warning", Color.FromArgb("#FBBF24")),
                    Margin = new Thickness(0, 4, 0, 0),
                });
                break;

            case CosmeticState.Owned:
                stack.Children.Add(OwnedRow(entry));
                break;

            default:
                stack.Children.Add(BuyButton(entry));
                break;
        }

        var card = Ui.Card(stack, new Thickness(18, 16));
        SemanticProperties.SetDescription(card, locked ? $"A look not found yet. {entry.Hint}" : $"{item.Name}. {item.Description}");
        return card;
    }

    private View OwnedRow(CosmeticEntry entry)
    {
        if (entry.Equipped)
        {
            return new Label
            {
                Text = "EQUIPPED  ✓",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                TextColor = Ui.Res("Accent", Colors.Cyan),
                Margin = new Thickness(0, 6, 0, 0),
            };
        }

        var button = SmallButton("EQUIP");
        string id = entry.Item.Id;
        button.Clicked += (_, _) =>
        {
            _shop.Equip(id);
            Rebuild();
        };
        SemanticProperties.SetDescription(button, $"Equip {entry.Item.Name}");
        return button;
    }

    private View BuyButton(CosmeticEntry entry)
    {
        bool canBuy = entry.State == CosmeticState.Buyable;
        var button = new Button
        {
            Text = $"BUY  ·  {entry.Item.Price:N0} GOLD",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1,
            CornerRadius = 24,
            HeightRequest = 48,
            Margin = new Thickness(0, 6, 0, 0),
            IsEnabled = canBuy,
        };
        if (canBuy)
        {
            button.Background = (Brush)Application.Current!.Resources["GoldGradient"];
            button.TextColor = Ui.Res("AccentText", Colors.Black);
        }
        else
        {
            button.BackgroundColor = Color.FromArgb("#22FFFFFF");
            button.TextColor = Ui.Res("TextSecondary", Colors.Gray);
        }

        string id = entry.Item.Id;
        button.Clicked += (_, _) =>
        {
            // Buying equips it too: nobody buys a look to leave it in a drawer.
            if (_shop.TryBuy(id)) _shop.Equip(id);
            Rebuild();
        };
        return button;
    }

    private static Button SmallButton(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        CharacterSpacing = 1.2,
        CornerRadius = 22,
        HeightRequest = 44,
        Margin = new Thickness(0, 6, 0, 0),
        HorizontalOptions = LayoutOptions.Start,
        MinimumWidthRequest = 120,
        BackgroundColor = Color.FromArgb("#22FFFFFF"),
        TextColor = Ui.Res("TextPrimary", Colors.White),
    };

    private static View? MakePreview(Cosmetic item)
    {
        switch (item.Slot)
        {
            case CosmeticSlot.Skin:
                return new GraphicsView { Drawable = new SkinPreviewDrawable { Skin = CosmeticCatalog.SkinOf(item.Id) }, HeightRequest = 52, Margin = new Thickness(0, 4, 0, 0) };

            case CosmeticSlot.Board:
                return new GraphicsView { Drawable = new BoardPreviewDrawable { BoardId = item.Id }, HeightRequest = 64, Margin = new Thickness(0, 4, 0, 0) };

            case CosmeticSlot.Accessory:
                return new GraphicsView
                {
                    Drawable = new PipDrawable { Brightness = 0.9f, Mood = PipMood.Happy, Accessory = CosmeticCatalog.AccessoryOf(item.Id) },
                    HeightRequest = 80,
                    WidthRequest = 80,
                    HorizontalOptions = LayoutOptions.Start,
                };

            default:
                return null;
        }
    }
}
