namespace PopBlocks.Engine.Story.Content;

/// <summary>The Sunken City (levels 76-100). The Warden's first warning appears here.</summary>
internal static class Region4
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r4_open", 4, SceneTrigger.RegionOpening, "Lights under the water", 76, new[]
        {
            Say.Pip("The city is under the water, but its lights are still there. I can see them blinking."),
            Say.Mara("The city's records were sealed when the Heart began to fail. If any warning survived, it may be here."),
        });

        yield return new StoryScene("r4_message", 4, SceneTrigger.Milestone, "A damaged message", 82, new[]
        {
            Say.Note("The Restorer restores a recording crystal."),
            Say.Voice("The outer channels are weakening. More power is being drawn toward the centre."),
            Say.Mara("This was recorded before the fracture."),
            Say.Pip("Then people knew the Heart was in trouble?"),
            Say.Mara("Some people did. We need to know what they tried to do about it."),
        });

        yield return new StoryScene("r4_warden", 4, SceneTrigger.Milestone, "A dark symbol", 91, new[]
        {
            Say.Note("A dark symbol appears on a wall, with a short message beneath it."),
            Say.WardenMessage("Do not mistake a recovered fragment for a recovered truth. The Heart was not broken without cause."),
            Say.Pip("That's the Warden. They're watching us."),
            Say.Mara("Or trying to stop us. We still don't know which."),
        });

        yield return Say.Replay(4, "The city lights are still blinking. I think they're waving.");

        yield return new StoryScene("r4_complete", 4, SceneTrigger.RegionCompletion, "The central beacon", 100, new[]
        {
            Say.Note("The city's central beacon glows, revealing a diagram of the Spectrum's old channels."),
            Say.Mara("The Heart drew the colours inward. The outer channels were failing because the flow was becoming unbalanced."),
            Say.Pip("Maybe the Warden tried to stop it."),
            Say.Mara("Maybe. We have evidence now. We still need the whole story."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r4_restored", 4, LoreKind.RestoredWorld, "Sunken City — Restored",
            "The city's records confirm that the Heart was unstable before it shattered. The Warden's warning suggests the fracture may have been an attempt to prevent something worse.",
            100);

        yield return new LoreEntry("r4_journal", 4, LoreKind.Journal, "Before the Fracture",
            "Warnings were recorded before the disaster. The Heart was drawing more and more of the Spectrum toward its centre, while the regions' own channels weakened.",
            100);

        yield return new LoreEntry("fragment_r4", 4, LoreKind.Fragment, "Fragment: the City",
            "A cool, bright piece that ripples like water. When Pip holds it, the city's lights flicker in time with it.",
            100);

        yield return new LoreEntry("mem_r4_1", 4, LoreKind.Memory, "Memory Crystal: The Drawn Light",
            "A crystal replays a harbour at dusk: lamps along the quay dimming one by one while the tallest tower burns brighter. Nobody on the quay seems alarmed. The tower was beautiful, and that may be why no one asked what it was costing the lamps. Who was watching the tower?",
            -1);

        yield return new LoreEntry("mem_r4_2", 4, LoreKind.Memory, "A Keeper's Last Page",
            "The final page of a keeper's log: ‘Messages sent to the Warden. No answer. Not ignored, I think—waiting.’ Whoever the Warden is, someone trusted them enough to keep writing, night after night, to a silence. What was the Warden waiting for?",
            -1);
    }
}
