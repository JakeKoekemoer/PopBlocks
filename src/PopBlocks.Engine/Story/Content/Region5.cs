namespace PopBlocks.Engine.Story.Content;

/// <summary>The Clockwork Wastes (levels 101-125). Evidence that the Warden shut the flow down.</summary>
internal static class Region5
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r5_open", 5, SceneTrigger.RegionOpening, "Too clever", 101, new[]
        {
            Say.Bram("The old machines were clever. Too clever, sometimes. Everything was linked to the Heart, even things that could have managed on their own."),
            Say.Pip("Why build it that way?"),
            Say.Bram("Because it worked. Then it worked better. Then everyone forgot there was another way."),
        });

        yield return new StoryScene("r5_machine", 5, SceneTrigger.Milestone, "The central machine", 107, new[]
        {
            Say.Note("The Restorer restores a machine with several disconnected gears."),
            Say.Bram("This whole station waited for a signal from the centre. Even the local gears couldn't turn without it."),
            Say.Pip("So when the Heart failed, everything stopped?"),
            Say.Bram("Not everything. But too much did."),
        });

        yield return new StoryScene("r5_discovery", 5, SceneTrigger.Milestone, "The shutdown control", 116, new[]
        {
            Say.Note("An old control panel shows the flow being interrupted."),
            Say.Mara("The Warden's mark is on the shutdown control."),
            Say.Pip("They really did it."),
            Say.Bram("Yes. But look at the readings before the shutdown. The system was close to overloading."),
            Say.Mara("The Warden may have stopped the flow to prevent a collapse."),
            Say.Pip("Then why didn't they tell anyone?"),
            Say.Bram("Perhaps there was no time. Perhaps they believed nobody would listen. Both can be true—and both can still leave a terrible mess."),
        });

        yield return Say.Replay(5, "Listen to those gears. Somebody built all this to last.");

        yield return new StoryScene("r5_complete", 5, SceneTrigger.RegionCompletion, "Local power", 125, new[]
        {
            Say.Note("The machines restart, now drawing from local energy channels."),
            Say.Bram("I've rerouted this station so it doesn't depend on one signal. It's slower to set up, but if one part fails, the others can keep working."),
            Say.Pip("Like the colours in a chain?"),
            Say.Bram("Exactly. A connection is stronger when it doesn't have to carry the whole world alone."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r5_restored", 5, LoreKind.RestoredWorld, "Clockwork Wastes — Restored",
            "The old machines reveal that the Warden interrupted the Heart's central flow. The shutdown was deliberate, but the evidence suggests it may have prevented a catastrophic overload.",
            125);

        yield return new LoreEntry("r5_journal", 5, LoreKind.Journal, "The Shutdown",
            "The Warden's mark appears on the shutdown controls. The decision was deliberate. Whether it was justified—and why the Warden abandoned the regions afterward—remains unresolved.",
            125);

        yield return new LoreEntry("fragment_r5", 5, LoreKind.Fragment, "Fragment: the Wastes",
            "A shard with a tiny gear-like pattern etched through it. It ticks, very softly, whenever nobody is talking.",
            125);

        yield return new LoreEntry("mem_r5_1", 5, LoreKind.Memory, "Warden Record: An Earlier Attempt",
            "A worn record describes a smaller shutdown, tried once and abandoned: ‘Flow reduced for one hour. Three regions dimmed. Council voted to restore it.’ The Warden may have tried a gentler fix first and been overruled. What would a longer attempt have needed?",
            -1);

        yield return new LoreEntry("mem_r5_2", 5, LoreKind.Memory, "A Gear Left Oiled",
            "One gear in the central machine has been oiled recently, long after the shutdown. Someone still tends this place and leaves no name. The machines were not abandoned entirely, only left to run alone. Who comes here, and why do they never stay?",
            -1);
    }
}
