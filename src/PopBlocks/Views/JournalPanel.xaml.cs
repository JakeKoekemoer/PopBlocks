using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Views;

/// <summary>
/// Personal bests and running totals, kept on the device (see <see cref="LifetimeStats"/>), shown inside the workshop's
/// TROPHIES tab. It only reports; nothing here changes a rule, a reward or a level.
/// </summary>
public partial class JournalPanel : ContentView, IWorkshopPanel
{
    private readonly LifetimeStats _stats;
    private readonly PlayerProgress _progress;
    private readonly CampaignService _campaign;
    private readonly HighScoreService _scores;

    public JournalPanel(LifetimeStats stats, PlayerProgress progress, CampaignService campaign, HighScoreService scores)
    {
        InitializeComponent();
        _stats = stats;
        _progress = progress;
        _campaign = campaign;
        _scores = scores;
    }

    /// <summary>Nothing here spends gold, so this never fires; it is part of what a workshop panel offers.</summary>
    public event Action? Changed { add { } remove { } }

    public void Refresh()
    {
        Host.Children.Clear();

        Section("THE CAMPAIGN", new (string, string)[]
        {
            ("Levels cleared", $"{_progress.HighestCleared}"),
            ("Regions restored", $"{_campaign.RestoredRegionCount} of {RegionCatalog.All.Count}"),
            ("Memories found", $"{_campaign.TotalMemoriesFound} of {_campaign.TotalMemories}"),
            ("Fragments found", $"{_campaign.FragmentCount} of {RegionCatalog.All.Count}"),
            ("Levels won (replays too)", $"{_stats.LevelsWon}"),
            ("Most goals on a level you cleared", _stats.MostGoalsOnClearedLevel > 0 ? $"{_stats.MostGoalsOnClearedLevel}" : "none yet"),
        });

        Section("YOUR BEST PLAY", new (string, string)[]
        {
            ("Biggest group", Show(_stats.LargestGroup)),
            ("Longest chain", Show(_stats.LongestChain)),
            ("Most blocks from one piece", Show(_stats.MostClearedByOnePiece)),
            ("Most moves left after a win", Show(_stats.MostMovesLeft)),
            ("Boards left empty", Show(_stats.BoardsCleared)),
            ("Endless best score", Show(_scores.Best)),
        });

        Section("TOTALS", new (string, string)[]
        {
            ("Blocks popped", $"{_stats.CellsPopped:N0}"),
            ("Helpers used", Show(_stats.AbilitiesUsed)),
        });

        var plays = new List<(string, string)>();
        foreach (var region in RegionCatalog.All)
            plays.Add((region.Title, $"{_stats.PlaysInRegion(region.Number)}"));
        Section("LEVELS PLAYED IN EACH REGION", plays);
    }

    private static string Show(int value) => value > 0 ? $"{value:N0}" : "none yet";

    private void Section(string title, IEnumerable<(string Label, string Value)> rows)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(Ui.Caption(title));
        foreach (var (label, value) in rows)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
            row.Add(Ui.Body(label, 14), 0);
            var v = Ui.Heading(value, 15);
            v.HorizontalTextAlignment = TextAlignment.End;
            row.Add(v, 1);
            SemanticProperties.SetDescription(row, $"{label}: {value}");
            stack.Children.Add(row);
        }
        Host.Children.Add(Ui.Card(stack, new Thickness(18, 16)));
    }
}
