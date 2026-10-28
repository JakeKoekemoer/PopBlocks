using System.Text;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using PopBlocks.Graphics;
using Region = PopBlocks.Engine.Story.Region;

namespace PopBlocks.Views;

/// <summary>
/// Everything the player has discovered, read offline from bundled text. Entries unlock with progress; locked
/// ones are never described, only a line such as "More will be discovered in the Crystal Caves."
/// </summary>
public partial class ArchivePage : ContentPage
{
    private static readonly string[] TabNames = { "STORY", "MEMORIES", "PEOPLE", "WORLD" };
    private const string EmptyText = "Nothing here yet. Keep playing to fill this page.";

    private readonly CampaignService _campaign;
    private readonly EndingService _ending;
    private readonly ILocalSettings _local;
    private int _tab;

    public ArchivePage(CampaignService campaign, EndingService ending, ILocalSettings local)
    {
        InitializeComponent();
        _campaign = campaign;
        _ending = ending;
        _local = local;
        _tab = Math.Clamp(local.GetInt(SaveKeys.StoryBookTab, 0), 0, TabNames.Length - 1);
        foreach (var _ in TabNames) TabBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Rebuild();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    /// <summary>The tab bar, in the workshop's style. The chosen tab is remembered (a small number in the save).</summary>
    private void BuildTabs()
    {
        TabBar.Children.Clear();
        for (int i = 0; i < TabNames.Length; i++)
        {
            bool selected = i == _tab;
            var tab = new Button
            {
                Text = TabNames[i],
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 0.7,
                CornerRadius = 22,
                HeightRequest = 44,
                Padding = new Thickness(2, 0),
                MinimumWidthRequest = 0,
                BackgroundColor = selected ? Ui.Res("Accent", Colors.Cyan) : Colors.Transparent,
                TextColor = selected ? Ui.Res("AccentText", Colors.Black) : Ui.Res("TextSecondary", Colors.Gray),
            };
            SemanticProperties.SetDescription(tab, $"{TabNames[i]} tab{(selected ? ", selected" : "")}");
            int chosen = i;
            tab.Clicked += (_, _) =>
            {
                _tab = chosen;
                _local.SetInt(SaveKeys.StoryBookTab, chosen);
                Rebuild();
            };
            TabBar.Add(tab, i, 0);
        }
    }

    private void Rebuild()
    {
        BuildTabs();
        Host.Children.Clear();

        var lore = _campaign.UnlockedLore().ToList();
        var scenes = StoryContent.Scenes.Where(_campaign.IsSceneAvailable).ToList();
        SummaryLabel.Text = $"{lore.Count + scenes.Count} ENTRIES  ·  FRAGMENTS {_campaign.FragmentCount} / {RegionCatalog.All.Count}";

        switch (_tab)
        {
            case 0: BuildStory(lore, scenes); break;
            case 1: BuildMemories(); break;
            case 2: BuildPeople(lore); break;
            default: BuildWorld(); break;
        }
    }

    /// <summary>A tab with nothing unlocked yet says so in one line, rather than listing locked placeholders.</summary>
    private void Empty() => Host.Children.Add(Ui.Body(EmptyText, 14));

    // ---- STORY: the overview, the journal and every scene seen so far
    private void BuildStory(List<LoreEntry> lore, List<StoryScene> scenes)
    {
        if (!lore.Any(l => l.Kind is LoreKind.Overview or LoreKind.Journal) && scenes.Count == 0) { Empty(); return; }

        Section("THE STORY");
        foreach (var entry in lore.Where(l => l.Kind is LoreKind.Overview or LoreKind.Journal))
            Host.Children.Add(Expandable(entry.Title, entry.Text, entry.Kind == LoreKind.Journal ? "Journal" : null));
        foreach (var scene in scenes)
            Host.Children.Add(Expandable(scene.Title, SceneText(scene), RegionCatalog.Get(scene.Region).Title));
        AddNextPlaceholder();
    }

    // ---- PEOPLE: Pip, Mara, Bram and the Warden, as they are met
    private void BuildPeople(List<LoreEntry> lore)
    {
        var people = lore.Where(l => l.Kind == LoreKind.Character).ToList();
        if (people.Count == 0) { Empty(); return; }

        Section("PEOPLE");
        foreach (var entry in people)
            Host.Children.Add(CharacterCard(entry));
        Host.Children.Add(Ui.Body("More characters will be met along the way.", 13));
    }

    // ---- WORLD: the fragments and the restored world
    private void BuildWorld()
    {
        bool anyRestored = RegionCatalog.All.Any(r =>
            StoryContent.LoreForRegion(r.Number).FirstOrDefault(l => l.Kind == LoreKind.RestoredWorld) is { } e && _campaign.HasLore(e));
        if (_campaign.FragmentCount == 0 && !anyRestored) { Empty(); return; }

        Section($"FRAGMENTS  ·  {_campaign.FragmentCount} / {RegionCatalog.All.Count}");
        foreach (var region in RegionCatalog.All)
        {
            bool has = _campaign.HasFragment(region);
            var entry = StoryContent.FindLore(region.FragmentId);
            if (has && entry != null)
                Host.Children.Add(Expandable("◆  " + entry.Title, entry.Text, null));
            else
                Host.Children.Add(Ui.Card(Ui.Body($"◇  Not yet found  ·  {region.Title}", 14), new Thickness(16, 12)));
        }

        Section("THE RESTORED WORLD");
        foreach (var region in RegionCatalog.All)
        {
            var entry = StoryContent.LoreForRegion(region.Number).FirstOrDefault(l => l.Kind == LoreKind.RestoredWorld);
            if (entry != null && _campaign.HasLore(entry))
                Host.Children.Add(Expandable(entry.Title, entry.Text, null));
            else
                Host.Children.Add(Ui.Card(Ui.Body(StoryContent.LockedPlaceholder(region), 14), new Thickness(16, 12)));
        }
    }

    // ---- MEMORIES: found by region (optional objectives on a few levels) and, after the ending, how the world looks
    private void BuildMemories()
    {
        if (_campaign.TotalMemoriesFound == 0 && !_ending.CampaignComplete) { Empty(); return; }

        AddLookPicker();

        Section($"MEMORIES  ·  {_campaign.TotalMemoriesFound} / {_campaign.TotalMemories}");
        foreach (var region in RegionCatalog.All)
            foreach (var memory in StoryContent.MemoriesOf(region.Number))
            {
                if (_campaign.HasLore(memory))
                    Host.Children.Add(Expandable("◈  " + memory.Title, memory.Text, region.Title));
                else
                    Host.Children.Add(Ui.Card(Ui.Body($"◇  Not yet found  ·  {region.Title}", 14), new Thickness(16, 12)));
            }
        Host.Children.Add(Ui.Body("Memories are optional. Look for the gold ◆ line under the goals on some levels.", 13));
    }

    /// <summary>After the ending: pick (or change) how the world looks. Purely cosmetic, and every look stays available.</summary>
    private void AddLookPicker()
    {
        if (!_ending.CampaignComplete) return;

        Section("HOW THE WORLD LOOKS");
        Host.Children.Add(Ui.Body("This only changes how things are drawn. There is one ending.", 13));
        foreach (var look in _ending.Available)
        {
            bool chosen = _ending.Choice?.Id == look.Id;
            var button = new Button
            {
                Text = (chosen ? "✓  " : "") + look.Name,
                Style = (Style)Application.Current!.Resources[chosen ? "PrimaryButton" : "SecondaryButton"],
            };
            string id = look.Id;
            button.Clicked += (_, _) =>
            {
                _ending.Choose(id);
                Rebuild();
            };
            Host.Children.Add(button);
            Host.Children.Add(Ui.Body(look.Description, 13));
        }
    }

    private void Section(string title)
    {
        var label = Ui.Caption(title, Ui.Res("Accent", Colors.Cyan));
        label.Margin = new Thickness(2, 12, 0, 0);
        Host.Children.Add(label);
    }

    /// <summary>One line for the first region that has no story yet, so the player knows more is coming without a spoiler.</summary>
    private void AddNextPlaceholder()
    {
        Region? next = RegionCatalog.All.FirstOrDefault(r => !StoryContent.Scenes.Any(s => s.Region == r.Number));
        if (next != null) Host.Children.Add(Ui.Body(StoryContent.LockedPlaceholder(next), 13));
    }

    private static string SceneText(StoryScene scene)
    {
        var sb = new StringBuilder();
        foreach (var line in scene.Lines)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            if (line.IsNarration) sb.Append('(').Append(line.Text).Append(')');
            else sb.Append(Cast.Find(line.SpeakerId)?.Name).Append(": ").Append(line.Text);
        }
        return sb.ToString();
    }

    /// <summary>A card that opens and closes when tapped. Closed cards show only the title.</summary>
    private static View Expandable(string title, string text, string? caption)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        if (caption != null) stack.Children.Add(Ui.Caption(caption.ToUpperInvariant()));

        var heading = Ui.Heading("▸  " + title, 17);
        var body = Ui.Body(text, 14, Ui.Res("TextPrimary", Colors.White));
        body.IsVisible = false;
        stack.Children.Add(heading);
        stack.Children.Add(body);

        var card = Ui.Card(stack, new Thickness(16, 14));
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            body.IsVisible = !body.IsVisible;
            heading.Text = (body.IsVisible ? "▾  " : "▸  ") + title;
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private View CharacterCard(LoreEntry entry)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        if (entry.Id == "char_pip")
        {
            float brightness = 0.35f + 0.65f * _campaign.RestoredRegionCount / RegionCatalog.All.Count;
            var portrait = new GraphicsView
            {
                WidthRequest = 64,
                HeightRequest = 64,
                VerticalOptions = LayoutOptions.Start,
                Drawable = new PipDrawable { Brightness = brightness, Mood = PipMood.Calm },
            };
            grid.Add(portrait, 0, 0);
        }

        var text = new VerticalStackLayout { Spacing = 4 };
        text.Children.Add(Ui.Heading(entry.Title, 18));
        text.Children.Add(Ui.Body(entry.Text, 14, Ui.Res("TextPrimary", Colors.White)));
        grid.Add(text, 1, 0);

        return Ui.Card(grid, new Thickness(16, 14));
    }
}
