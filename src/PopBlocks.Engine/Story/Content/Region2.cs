namespace PopBlocks.Engine.Story.Content;

/// <summary>The Crystal Caves (levels 26-50). Text follows the story bible, sections 6 and 10.</summary>
internal static class Region2
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r2_open", 2, SceneTrigger.RegionOpening, "The caves remember", 26, new[]
        {
            Say.Pip("The caves are full of old memories. You can almost hear them when the crystals catch the light."),
            Say.Pip("Mara lives somewhere ahead. She knows more about the Prism than I do."),
            Say.Note("The Restorer meets Mara beside a damaged archive."),
            Say.Mara("A Restorer. I wondered whether one would come."),
            Say.Mara("The records say the Heart was shattered by the Warden. Most people stopped reading there. It was easier to blame one name than understand the whole event."),
            Say.Pip("Was the Warden evil?"),
            Say.Mara("I don't know. And I won't pretend the records tell me more than they do."),
        });

        yield return new StoryScene("r2_record", 2, SceneTrigger.Milestone, "The first record", 31, new[]
        {
            Say.Mara("This record shows the Warden standing beside the Heart before it broke."),
            Say.Pip("So the Warden was there."),
            Say.Mara("Yes. Presence is evidence. Motive is a different question."),
        });

        yield return new StoryScene("r2_mid", 2, SceneTrigger.MidDiscovery, "The inscription", 38, new[]
        {
            Say.Note("An inscription is uncovered beneath a crystal shelf."),
            Say.Mara("The inscription reads: ‘When the centre carries all, the edges grow silent.’"),
            Say.Pip("That sounds like a warning."),
            Say.Mara("It sounds like a warning someone failed to heed."),
        });

        yield return Say.Replay(2, "The caves are still listening. Let's give them something worth remembering.");

        yield return new StoryScene("r2_complete", 2, SceneTrigger.RegionCompletion, "The second fragment", 50, new[]
        {
            Say.Note("A large crystal formation lights up. A second fragment is revealed inside it."),
            Say.Mara("The fragment responds to the first. The records were right about one thing: the pieces can recognise one another."),
            Say.Pip("Then we can put the Heart back together!"),
            Say.Mara("Perhaps. But before we decide what to rebuild, we should understand why it broke."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r2_restored", 2, LoreKind.RestoredWorld, "Crystal Caves — Restored",
            "The archive has reopened. Its records confirm that the Warden was present when the Prism Heart broke, but the cause remains uncertain.",
            50);

        yield return new LoreEntry("r2_journal", 2, LoreKind.Journal, "The Silent Edges",
            "An old inscription warns that the centre had begun to carry too much. The words may describe the Prism Heart itself—or the way the world had learned to depend on it.",
            50);

        yield return new LoreEntry("fragment_r2", 2, LoreKind.Fragment, "Fragment: the Caves",
            "A crystal sliver that hums when held near the first. Mara says a fragment remembers the shape of what it belonged to. This one remembers a curve.",
            50);

        yield return new LoreEntry("mem_r2_1", 2, LoreKind.Memory, "A Broken Slab",
            "A broken slab holds half a sentence: ‘…and the edges kept their own songs until…’ The rest is missing. The caves seem to have valued the edges as much as the centre, which the later records rarely mention. What did the edges stop singing?",
            -1);

        yield return new LoreEntry("mem_r2_2", 2, LoreKind.Memory, "A Keeper's Letter",
            "An unsigned letter, found between two records: ‘I have asked three times for the channels to be inspected. Each time I am told the Heart will provide.’ Someone noticed the strain and was not heard. Who was being asked, and why did they answer that way?",
            -1);
    }
}
