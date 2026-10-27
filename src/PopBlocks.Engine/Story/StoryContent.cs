using PopBlocks.Engine.Story.Content;

namespace PopBlocks.Engine.Story;

/// <summary>One option for "how should the world look going forward?" after the ending. Cosmetic only.</summary>
public sealed record FinalChoice(string Id, string Name, string Description, string CosmeticName, string EpilogueLine);

/// <summary>
/// All bundled story text. Content is data: adding or editing a scene never touches game rules or drawing.
/// Canon is docs/story/StoryText.md; the files under Content/ hold one region each.
/// </summary>
public static class StoryContent
{
    public const string ReactionFirstBurst = "r1_first_burst";
    public const string ReactionFirstChain = "r1_first_chain";

    /// <summary>The optional epilogue scene and journal entry, opened by finding a memory in every region.</summary>
    public const string EpilogueSceneId = Region8.EpilogueId;
    public const string EpilogueJournalId = "epilogue_journal";

    public static IReadOnlyList<StoryScene> Scenes { get; } = BuildScenes();

    public static IReadOnlyList<LoreEntry> Lore { get; } = BuildLore();

    /// <summary>
    /// The three looks offered after the ending. All are the same ending: the choice changes how the world is
    /// drawn from then on, never what happened.
    /// </summary>
    public static IReadOnlyList<FinalChoice> FinalChoices { get; } = new[]
    {
        new FinalChoice("lanterns", "Many Lanterns",
            "Warm gold light from many small lamps, each region lit in its own way.",
            "Lantern Dawn board",
            "Look at all the lamps. Every region lit its own."),
        new FinalChoice("aurora", "Aurora Tide",
            "Cool teal and violet light that drifts like water across the sky.",
            "Aurora Tide board",
            "It looks like the sky decided to join in."),
        new FinalChoice("meadow", "Open Meadow",
            "Soft greens and wide skies, as if the garden grew all the way here.",
            "Open Meadow board",
            "It looks like the garden grew all the way to the Heart."),
    };

    public static FinalChoice? FindFinalChoice(string id) => FinalChoices.FirstOrDefault(c => c.Id == id);

    private static IReadOnlyList<StoryScene> BuildScenes()
    {
        var all = new List<StoryScene>();
        all.AddRange(Region1.Scenes());
        all.AddRange(Region2.Scenes());
        all.AddRange(Region3.Scenes());
        all.AddRange(Region4.Scenes());
        all.AddRange(Region5.Scenes());
        all.AddRange(Region6.Scenes());
        all.AddRange(Region7.Scenes());
        all.AddRange(Region8.Scenes());
        return all;
    }

    private static IReadOnlyList<LoreEntry> BuildLore()
    {
        var list = new List<LoreEntry>
        {
            new("lore_spectrum", 0, LoreKind.Overview, "The Spectrum",
                "The Spectrum is the connection between the world's colours, energy, and life. For generations, the Prism Heart helped guide its flow. When the Heart fractured, the regions began to lose their colour and balance. The reason for the fracture is not yet known.",
                0),
            new("char_pip", 0, LoreKind.Character, "Pip",
                "A small Spark who survived the fracture. Pip remembers the old world in impressions and feelings rather than complete events. Curious and loyal, Pip travels with the Restorer in the hope that the world's colours can be brought together again.",
                0),

            // Characters unlock when the player has met them.
            new("char_mara", 2, LoreKind.Character, "Mara",
                "The Crystal Keeper. Mara has spent years preserving records in the Crystal Caves, many of them damaged or incomplete since the fracture. She is careful about what a record proves and what it only suggests, and she is willing to change her mind when new evidence appears.",
                26),
            new("char_bram", 3, LoreKind.Character, "Bram",
                "A Tinkerer who repairs what the fracture left behind: lanterns, small machines, map tools, and eventually the Spectrum Workshop. Bram is not a magical expert. He understands how parts fit together, and he is honest when one of his repairs goes wrong.",
                51),
            new("char_warden", 4, LoreKind.Character, "The Warden",
                "A guardian of the Prism Heart, known so far only by a dark mark on a wall and a warning. Who the Warden is, and what they want, is not yet known. Pip says they feel watched, though not unkindly.",
                91),
            new("char_warden_truth", 7, LoreKind.Character, "The Warden, Known",
                "Once the guardian of the Prism Heart. The Warden shattered it to stop a catastrophic overload, then, afraid of repeating the mistake, left the regions to recover alone. They have admitted that this was fear as much as protection, and have offered to help.",
                175),
        };

        list.AddRange(Region1.Lore());
        list.AddRange(Region2.Lore());
        list.AddRange(Region3.Lore());
        list.AddRange(Region4.Lore());
        list.AddRange(Region5.Lore());
        list.AddRange(Region6.Lore());
        list.AddRange(Region7.Lore());
        list.AddRange(Region8.Lore());
        return list;
    }

    public static StoryScene? FindScene(string id) => Scenes.FirstOrDefault(s => s.Id == id);

    public static LoreEntry? FindLore(string id) => Lore.FirstOrDefault(l => l.Id == id);

    public static StoryScene? SceneFor(Region region, SceneTrigger trigger) =>
        Scenes.FirstOrDefault(s => s.Region == region.Number && s.Trigger == trigger);

    public static IEnumerable<LoreEntry> LoreForRegion(int regionNumber) => Lore.Where(l => l.Region == regionNumber);

    /// <summary>The optional memories of a region (two each), in order.</summary>
    public static IEnumerable<LoreEntry> MemoriesOf(int regionNumber) =>
        Lore.Where(l => l.Region == regionNumber && l.Kind == LoreKind.Memory);

    /// <summary>Text shown in the Archive for a region that has no content yet. Never reveals what is in it.</summary>
    public static string LockedPlaceholder(Region region) => $"More will be discovered in {region.TitleInSentence}.";
}
