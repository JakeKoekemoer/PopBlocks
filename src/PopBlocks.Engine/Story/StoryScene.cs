namespace PopBlocks.Engine.Story;

/// <summary>Who is speaking. Ids are stable keys; names are what the player reads.</summary>
public sealed record Character(string Id, string Name, int FirstRegion);

/// <summary>The people of the story. <see cref="Character.FirstRegion"/> is the region where they may first appear.</summary>
public static class Cast
{
    /// <summary>Stage directions, shown in italics without a name.</summary>
    public const string Narrator = "narrator";
    public const string Pip = "pip";
    public const string Mara = "mara";
    public const string Bram = "bram";
    public const string Warden = "warden";

    /// <summary>A recording found in the world (a memory crystal, a message on a wall). Not a person who is present.</summary>
    public const string Voice = "voice";
    public const string WardenRecording = "warden_recording";
    public const string WardenMessage = "warden_message";

    public static IReadOnlyList<Character> All { get; } = new[]
    {
        new Character(Narrator, "", 1),
        new Character(Pip, "Pip", 1),
        new Character(Mara, "Mara", 2),
        new Character(Bram, "Bram", 3),
        new Character(Warden, "The Warden", 7),
        new Character(Voice, "Recorded voice", 4),
        new Character(WardenRecording, "The Warden, recorded", 6),
        new Character(WardenMessage, "The Warden's message", 4),
    };

    public static Character? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}

/// <summary>One line of a scene. Narrator lines are stage directions.</summary>
public sealed record StoryLine(string SpeakerId, string Text)
{
    public bool IsNarration => SpeakerId == Cast.Narrator;
}

public enum SceneTrigger
{
    /// <summary>First time a region's first level is started.</summary>
    RegionOpening,

    /// <summary>A named story beat inside a region.</summary>
    Milestone,

    /// <summary>Halfway through a region: the region starts to recover.</summary>
    MidDiscovery,

    /// <summary>The region's last level was cleared for the first time.</summary>
    RegionCompletion,

    /// <summary>The ending of the whole campaign.</summary>
    Ending,

    /// <summary>A short remark from Pip, shown as a banner rather than a dialogue panel.</summary>
    Reaction,

    /// <summary>Shown when the player starts a particular level for the first time (for example the final puzzle).</summary>
    LevelIntro,
}

/// <summary>
/// A short piece of story: ordered lines, shown as a panel (or a banner for reactions).
/// <see cref="UnlockLevel"/> is the level that fires it: the level whose first clear shows it (or, for an opening or
/// level intro, the level whose first start shows it). The Archive lists it once seen or once the player is past it.
/// </summary>
public sealed record StoryScene(
    string Id,
    int Region,
    SceneTrigger Trigger,
    string Title,
    int UnlockLevel,
    IReadOnlyList<StoryLine> Lines)
{
    /// <summary>Panels can be replayed from the Archive; one-line banners are not listed.</summary>
    public bool CanReplay => Trigger != SceneTrigger.Reaction;
}

public enum LoreKind
{
    /// <summary>Overview of the world, always available.</summary>
    Overview,
    Character,

    /// <summary>A journal entry unlocked by finishing a region.</summary>
    Journal,

    /// <summary>The short "Restored" description of a region.</summary>
    RestoredWorld,

    /// <summary>The Prism fragment recovered from a region.</summary>
    Fragment,

    /// <summary>An optional memory earned by an optional objective.</summary>
    Memory,
}

/// <summary>
/// An Archive entry. <see cref="UnlockLevel"/>: 0 = available from the start; a level number = granted when that level
/// is first cleared (and to players already past it); -1 = granted only by an explicit event, such as finding a memory.
/// </summary>
public sealed record LoreEntry(string Id, int Region, LoreKind Kind, string Title, string Text, int UnlockLevel);
