using PopBlocks.Engine.Story;

namespace PopBlocks.Views;

/// <summary>Small helpers shared by the pages and workshop panels that are built in code (map, region, archive, workshop).</summary>
internal static class Ui
{
    public static Color Res(string key, Color fallback)
    {
        var resources = Application.Current?.Resources;
        return resources is not null && resources.TryGetValue(key, out var value) && value is Color c ? c : fallback;
    }

    public static Border Card(View content, Thickness? padding = null)
    {
        var card = new Border { Padding = padding ?? new Thickness(18, 16), Content = content };
        var resources = Application.Current?.Resources;
        if (resources is not null && resources.TryGetValue("Glass", out var style) && style is Style glass)
            card.Style = glass;
        return card;
    }

    public static Label Caption(string text, Color? colour = null) => new()
    {
        Text = text,
        FontSize = 11,
        FontAttributes = FontAttributes.Bold,
        CharacterSpacing = 1.2,
        TextColor = colour ?? Res("TextSecondary", Colors.Gray),
    };

    public static Label Heading(string text, double size = 20) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = Res("TextPrimary", Colors.White),
    };

    public static Label Body(string text, double size = 14, Color? colour = null) => new()
    {
        Text = text,
        FontSize = size,
        LineHeight = 1.25,
        TextColor = colour ?? Res("TextSecondary", Colors.Gray),
    };

    /// <summary>A small pill for a second row of tabs (looks by kind, achievements or journal). Touch height is 44.</summary>
    public static Button Chip(string text, bool selected) => new()
    {
        Text = text,
        FontSize = 11,
        FontAttributes = FontAttributes.Bold,
        CharacterSpacing = 1,
        CornerRadius = 22,
        HeightRequest = 44,
        Padding = new Thickness(14, 0),
        MinimumWidthRequest = 0,
        BackgroundColor = selected ? Color.FromArgb("#3338BDF8") : Color.FromArgb("#14FFFFFF"),
        TextColor = selected ? Res("TextPrimary", Colors.White) : Res("TextSecondary", Colors.Gray),
        BorderColor = selected ? Res("Accent", Colors.Cyan) : Colors.Transparent,
        BorderWidth = 1,
    };

    /// <summary>State is always written out as well as drawn, so it never depends on colour alone.</summary>
    public static string StateText(RegionState state) => state switch
    {
        RegionState.Restored => "RESTORED  ✓",
        RegionState.Recovering => "RECOVERING",
        _ => "FADED",
    };

    public static Color StateColour(RegionState state) => state switch
    {
        RegionState.Restored => Color.FromArgb("#A3E635"),
        RegionState.Recovering => Color.FromArgb("#FBBF24"),
        _ => Color.FromArgb("#9AA6C8"),
    };
}
