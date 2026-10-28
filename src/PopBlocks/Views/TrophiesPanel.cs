namespace PopBlocks.Views;

/// <summary>
/// The workshop's TROPHIES tab (once the display case and the map table): ACHIEVEMENTS and JOURNAL behind one row of
/// chips. Both only report what the player has done; nothing here costs gold.
/// </summary>
public sealed class TrophiesPanel : ContentView, IWorkshopPanel
{
    private readonly AchievementsPanel _achievements;
    private readonly JournalPanel _journal;
    private readonly HorizontalStackLayout _chips = new() { Spacing = 6 };
    private readonly ContentView _body = new();
    private bool _showJournal;

    public TrophiesPanel(AchievementsPanel achievements, JournalPanel journal)
    {
        _achievements = achievements;
        _journal = journal;

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)),
            RowSpacing = 10,
        };
        grid.Add(_chips, 0, 0);
        grid.Add(_body, 0, 1);
        Content = grid;
    }

    /// <summary>Nothing here spends gold, so this never fires; it is part of what a workshop panel offers.</summary>
    public event Action? Changed { add { } remove { } }

    public void Refresh()
    {
        _chips.Children.Clear();
        _chips.Children.Add(MakeChip("ACHIEVEMENTS", !_showJournal, showJournal: false));
        _chips.Children.Add(MakeChip("JOURNAL", _showJournal, showJournal: true));

        if (_showJournal)
        {
            _journal.Refresh();
            _body.Content = _journal;
        }
        else
        {
            _achievements.Refresh();
            _body.Content = _achievements;
        }
    }

    private Button MakeChip(string text, bool selected, bool showJournal)
    {
        var chip = Ui.Chip(text, selected);
        SemanticProperties.SetDescription(chip, selected ? $"{text} tab, selected" : $"{text} tab");
        chip.Clicked += (_, _) =>
        {
            _showJournal = showJournal;
            Refresh();
        };
        return chip;
    }
}
