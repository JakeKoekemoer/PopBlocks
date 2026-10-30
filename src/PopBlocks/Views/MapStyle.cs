using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Region = PopBlocks.Engine.Story.Region;

namespace PopBlocks.Views;

/// <summary>Colours and small pieces shared by the map and region pages.</summary>
internal static class MapStyle
{
    public static readonly Color Lime = Color.FromArgb("#A3E635");

    public static Color TierColour(Tier tier) => tier switch
    {
        Tier.Easy => Color.FromArgb("#38BDF8"),
        Tier.Medium => Color.FromArgb("#A3E635"),
        Tier.Hard => Color.FromArgb("#FBBF24"),
        _ => Color.FromArgb("#E879F9"),
    };

    public static Style GlassStyle => (Style)Application.Current!.Resources["Glass"];

    /// <summary>A small rounded label: filled when found, outlined when not yet.</summary>
    public static View Chip(string text, bool found)
    {
        var chip = new Border
        {
            Padding = new Thickness(10, 5),
            Margin = new Thickness(0, 0, 6, 6),
            StrokeThickness = 1,
            Stroke = found ? Color.FromArgb("#66A3E635") : Color.FromArgb("#33FFFFFF"),
            Background = new SolidColorBrush(found ? Color.FromArgb("#1FA3E635") : Color.FromArgb("#0DFFFFFF")),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = new Label
            {
                Text = text,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = found ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#9AA6C8"),
            },
        };
        return chip;
    }

    /// <summary>What has been found in a region: its Prism fragment and its journal entries.</summary>
    public static View FoundHere(Region region, CampaignService campaign)
    {
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
        bool fragment = campaign.HasFragment(region);
        flex.Children.Add(Chip(fragment ? "◆ Prism fragment" : "◇ Prism fragment", fragment));
        foreach (var journal in StoryContent.LoreForRegion(region.Number).Where(l => l.Kind == LoreKind.Journal))
        {
            bool has = campaign.HasLore(journal);
            flex.Children.Add(Chip(has ? "◆ Journal: " + journal.Title : "◇ Journal entry", has));
        }
        return flex;
    }
}
