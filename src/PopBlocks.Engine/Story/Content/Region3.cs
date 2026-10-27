namespace PopBlocks.Engine.Story.Content;

/// <summary>The Verdant Wilds (levels 51-75). Bram and the Spectrum Workshop arrive here.</summary>
internal static class Region3
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r3_open", 3, SceneTrigger.RegionOpening, "Everything holding still", 51, new[]
        {
            Say.Pip("The Wilds should be full of birds and little lights. Instead, everything is holding still."),
            Say.Note("Bram is trying to repair a small machine that refuses to start."),
            Say.Bram("If this gear turns, that wheel turns. If that wheel turns, the water pump works. If the pump works—well, you get the idea."),
            Say.Pip("And if one gear is missing?"),
            Say.Bram("Then the whole contraption has a very quiet day."),
            Say.Bram("You're helping the regions reconnect? Excellent. I can help you repair things that still have moving parts."),
        });

        yield return new StoryScene("r3_workshop", 3, SceneTrigger.Milestone, "The old workshop", 54, new[]
        {
            Say.Bram("This old workshop used to maintain tools for the whole region. It's a mess, but a useful mess."),
            Say.Bram("Bring back a little colour and I'll get the benches working again. I can make tools, polish your effects, and preserve the things you discover."),
            Say.Pip("Can you fix the Prism?"),
            Say.Bram("I can fix a hinge. The Prism is a much bigger hinge."),
        });

        yield return new StoryScene("r3_mid", 3, SceneTrigger.MidDiscovery, "The pump", 63, new[]
        {
            Say.Note("The Restorer restores a pump that feeds a dry grove."),
            Say.Bram("There. The pump isn't pulling power from the Heart. It's using the water already here."),
            Say.Pip("It works without the great light?"),
            Say.Bram("Of course. The Heart helped everything work together. It was never meant to do every job itself."),
        });

        yield return Say.Replay(3, "I can hear Bram's tools ticking away somewhere. That's a good sound.");

        yield return new StoryScene("r3_complete", 3, SceneTrigger.RegionCompletion, "Lamps in the grove", 75, new[]
        {
            Say.Note("The grove comes alive and the workshop's lamps switch on."),
            Say.Bram("Look at that. The Wilds are thriving, and the workshop has power again."),
            Say.Pip("You sound surprised."),
            Say.Bram("I'm a professional. I prefer to call it pleasantly impressed."),
            Say.Bram("Take this. Every good Restorer deserves a little colour of their own."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r3_restored", 3, LoreKind.RestoredWorld, "Verdant Wilds — Restored",
            "The Wilds are growing again, and Bram has reopened the Spectrum Workshop. His repairs show that the world can thrive through local connections, not just power from the Heart.",
            75);

        yield return new LoreEntry("r3_journal", 3, LoreKind.Journal, "A Thousand Small Things",
            "Some systems can work when their parts support one another. The Heart once helped the world connect, but over time the world began asking it to do too much.",
            75);

        yield return new LoreEntry("fragment_r3", 3, LoreKind.Fragment, "Fragment: the Wilds",
            "A green-gold shard with a vein of root-like light running through it. Holding it feels like the moment just before something grows.",
            75);

        yield return new LoreEntry("mem_r3_1", 3, LoreKind.Memory, "A Sketch of the Channels",
            "A sketch pinned inside the workshop shows small channels linking one grove to the next, each with its own little pump. It looks homemade and hopeful. Before everything flowed to the centre, the regions seem to have shared with their neighbours. When did that stop?",
            -1);

        yield return new LoreEntry("mem_r3_2", 3, LoreKind.Memory, "A Tinkerer's Note",
            "A note in Bram's handwriting, older than the workshop's dust: ‘Tried to build a bigger pump today. Worked for a week. Then everything downstream got thirsty.’ Bigger is not always better. Did anyone else learn that lesson, or only Bram?",
            -1);
    }
}
