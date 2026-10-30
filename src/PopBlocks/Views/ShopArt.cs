using Microsoft.Maui.Controls.Shapes;
using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// The pictures in the workshop (Resources/Images). They are decoration only: every card still says what the item is
/// and does in words, so a picture is never the only thing that tells one item from another, and screen readers skip them.
/// The engine knows nothing about these files; this is the one place that matches an item to its picture.
/// </summary>
internal static class ShopArt
{
    private static readonly Color Backdrop = Color.FromArgb("#0B1432");
    private static readonly Dictionary<string, bool> Packaged = new();

    /// <summary>
    /// Is this picture in the app? A picture that is not shows nothing rather than an empty box, so artwork can be added
    /// one piece at a time (an achievement's icon is just a file named ach_&lt;id&gt;.png in Resources/Images).
    /// </summary>
    public static bool Exists(string file)
    {
        if (Packaged.TryGetValue(file, out var known)) return known;
        bool found = true;
#if ANDROID
        var context = Android.App.Application.Context;
        found = context.Resources?.GetIdentifier(System.IO.Path.GetFileNameWithoutExtension(file), "drawable", context.PackageName) is > 0;
#endif
        Packaged[file] = found;
        return found;
    }

    /// <summary>An achievement's icon, if its picture has been added.</summary>
    public static string? ForAchievement(string id)
    {
        var file = $"ach_{id}.png";
        return Exists(file) ? file : null;
    }

    public static string? ForUpgrade(string id) => id switch
    {
        ShopCatalog.Preview => "shop_lookout.png",
        ShopCatalog.PopFlair => "shop_popflair.png",
        ShopCatalog.ExtraMoves => "shop_extramoves.png",
        ShopCatalog.ColourLens => "shop_colourlens.png",
        ShopCatalog.ChainSense => "shop_chainsense.png",
        ShopCatalog.Insight => "shop_insight.png",
        _ => null,
    };

    /// <summary>Prism Reserve is shown with a wide picture across the top of its card instead of a small one.</summary>
    public static string? HeroForUpgrade(string id) => id == ShopCatalog.PrismReserve ? "banner_prismreserve.png" : null;

    public static string? ForSupply(Consumable kind) => kind switch
    {
        Consumable.SecondChance => "shop_secondchance.png",
        Consumable.PrismShift => "shop_prismshift.png",
        _ => null,
    };

    public static string? ForBurst(string cosmeticId) => cosmeticId switch
    {
        "burst_petals" => "shop_burst_petals.png",
        "burst_shards" => "shop_burst_shards.png",
        "burst_bubbles" => "shop_burst_bubbles.png",
        "burst_cogs" => "shop_burst_cogs.png",
        "burst_frost" => "shop_burst_frost.png",
        "burst_starburst" => "shop_burst_starburst.png",
        _ => null,
    };

    /// <summary>A wide picture for the top of a tab. It keeps its own shape, so nothing in it is cropped.</summary>
    public static View Banner(string file) => !Exists(file) ? new ContentView { HeightRequest = 0 } : Rounded(new Image { Source = file, Aspect = Aspect.AspectFit }, 20, null);

    /// <summary>
    /// Puts a small picture beside a card's text. Without a picture the text is returned as it is. A picture for
    /// something still locked is dimmed.
    /// </summary>
    public static View Beside(View content, string? file, bool dimmed = false)
    {
        if (file is null || !Exists(file)) return content;

        var thumb = Rounded(new Image { Source = file, Aspect = Aspect.AspectFit }, 16, Backdrop);
        thumb.WidthRequest = 84;
        thumb.HeightRequest = 84;
        thumb.VerticalOptions = LayoutOptions.Start;
        if (dimmed) thumb.Opacity = 0.45;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 14,
        };
        grid.Add(thumb, 0, 0);
        grid.Add(content, 1, 0);
        return grid;
    }

    /// <summary>A picture across the top of a card, with the card's text under it.</summary>
    public static View Above(View content, string file) =>
        Exists(file) ? new VerticalStackLayout { Spacing = 12, Children = { Banner(file), content } } : content;

    private static Border Rounded(Image image, double radius, Color? background)
    {
        AutomationProperties.SetIsInAccessibleTree(image, false);
        var border = new Border
        {
            Padding = new Thickness(0),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Content = image,
            InputTransparent = true,   // decoration: a drag that starts on it must still scroll the page
        };
        if (background is not null) border.BackgroundColor = background;
        AutomationProperties.SetIsInAccessibleTree(border, false);
        return border;
    }
}
