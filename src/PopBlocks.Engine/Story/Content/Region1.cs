namespace PopBlocks.Engine.Story.Content;

/// <summary>The Forgotten Garden (levels 1-25). Text follows the story bible, section 6 and 10.</summary>
internal static class Region1
{
    private static StoryLine Pip(string text) => new(Cast.Pip, text);
    private static StoryLine Note(string text) => new(Cast.Narrator, text);

    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r1_open", 1, SceneTrigger.RegionOpening, "Hello?", 1, new[]
        {
            Pip("Hello? Can you hear me?"),
            Note("A small light floats out from behind a cracked stone."),
            Pip("Oh! You can see me. That's a good start."),
            Pip("Everything used to glow here. Not all the time—just enough to make you feel the world was awake."),
            Pip("Then the great light broke. Since then, the colours have been slipping away."),
            Pip("I don't know how you can see the connections. But if you can, maybe we can help this place."),
            Pip("Will you try?"),
        });

        yield return new StoryScene("r1_first_burst", 1, SceneTrigger.Reaction, "First burst", 0, new[]
        {
            Pip("Did you see that? The colours found each other!"),
            Pip("It's a tiny change, I know. But the garden noticed."),
        });

        yield return new StoryScene("r1_first_chain", 1, SceneTrigger.Reaction, "First chain", 0, new[]
        {
            Pip("One burst, then another! The connections are still there—they just needed a little help."),
        });

        yield return new StoryScene("r1_mid", 1, SceneTrigger.MidDiscovery, "A mark beneath the roots", 13, new[]
        {
            Note("The Restorer finds a faintly glowing mark beneath the roots of an old tree."),
            Pip("That symbol… I've seen it in my dreams. It was carved around the Prism Heart."),
            Pip("Something from the Heart reached this garden. Maybe a piece of it is still here."),
        });

        yield return Say.Replay(1, "The garden remembers you. Back again\u2014good.");

        yield return new StoryScene("r1_complete", 1, SceneTrigger.RegionCompletion, "The garden breathes", 25, new[]
        {
            Note("The garden regains its colour. Flowers open in a wave, and a Prism fragment appears in the roots."),
            Pip("Look! It's a piece of the Prism."),
            Pip("I thought finding a fragment would make everything feel fixed. It doesn't. But the garden is breathing again."),
            Pip("One region restored. Seven to go."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r1_restored", 1, LoreKind.RestoredWorld, "Forgotten Garden — Restored",
            "The garden's colours have returned, and the first Prism fragment has been recovered. Pip believes the other regions may still hold pieces of the Heart.",
            25);

        yield return new LoreEntry("r1_journal", 1, LoreKind.Journal, "The First Light",
            "The fracture did not erase every connection. Some survived in roots, stones, and the small patterns that still joined the colours. The Restorer's gift is not to create those connections, but to help them find one another again.",
            25);

        yield return new LoreEntry("mem_r1_1", 1, LoreKind.Memory, "A Gardener's Tag",
            "A small clay tag is tied to a rosebush stem: \u2018Water at dusk. The light likes company.\u2019 Someone cared for these plants one by one, not all at once. The tag is faded, but the knot is still tight. Who was the gardener, and did they stay after the colours began to slip?",
            -1);

        yield return new LoreEntry("mem_r1_2", 1, LoreKind.Memory, "Seeds in the Wall",
            "Tucked in a crack in the garden wall is a pouch of seeds, each labelled in careful handwriting. Some are for flowers that no longer grow here. It suggests the garden was meant to be replanted, not simply admired. Where were the seeds meant to go?",
            -1);

        yield return new LoreEntry("fragment_r1", 1, LoreKind.Fragment, "Fragment: the Garden",
            "A piece of the Prism, found in the roots of the Forgotten Garden. “It's warm. Not hot—just… familiar.”",
            25);
    }
}
