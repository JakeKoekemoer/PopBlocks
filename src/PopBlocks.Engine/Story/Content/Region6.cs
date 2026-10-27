namespace PopBlocks.Engine.Story.Content;

/// <summary>The Frozen Expanse (levels 126-150). The Warden's fear becomes understandable.</summary>
internal static class Region6
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r6_open", 6, SceneTrigger.RegionOpening, "So quiet", 126, new[]
        {
            Say.Pip("It's so quiet here. Even the lights look as if they're afraid to shine."),
            Say.Mara("The cold preserved this place. It also preserved the damage."),
        });

        yield return new StoryScene("r6_ice", 6, SceneTrigger.Milestone, "A memory in the ice", 132, new[]
        {
            Say.Note("A memory crystal becomes visible inside a frozen wall."),
            Say.WardenRecorded("If the flow continues, the Heart will collapse. If I interrupt it, the regions will suffer—but they may survive."),
            Say.Pip("They knew the regions would suffer."),
            Say.Mara("Yes. The question is whether there was another choice."),
        });

        yield return new StoryScene("r6_mid", 6, SceneTrigger.MidDiscovery, "Putting everything back", 138, new[]
        {
            Say.Pip("I keep thinking that if we put everything back, the world will be safe again."),
            Say.Mara("The old world was beautiful. That does not mean every part of it was wise."),
            Say.Pip("I don't want to find out the Heart was a mistake."),
            Say.Mara("It wasn't a mistake to connect the world. It was a mistake to make every connection depend on one point."),
        });

        yield return Say.Replay(6, "Quiet again. But it feels like a quiet that's thinking, not afraid.");

        yield return new StoryScene("r6_complete", 6, SceneTrigger.RegionCompletion, "The path opens", 150, new[]
        {
            Say.Note("The ice clears around a path leading toward the Shattered Citadel."),
            Say.Pip("I understand why the Warden was afraid now."),
            Say.Mara("Understanding is not the same as agreeing."),
            Say.Pip("No. But maybe we can finish what they couldn't."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r6_restored", 6, LoreKind.RestoredWorld, "Frozen Expanse — Restored",
            "The frozen memory reveals the Warden's fear of a catastrophic collapse. The Warden knew the regions would suffer after the shutdown, but believed the alternative was worse.",
            150);

        yield return new LoreEntry("r6_journal", 6, LoreKind.Journal, "The Cost of Safety",
            "The Warden chose to interrupt the Heart's flow. The choice may have saved the world from immediate collapse, but the Warden then left the regions without a path to recovery.",
            150);

        yield return new LoreEntry("fragment_r6", 6, LoreKind.Fragment, "Fragment: the Expanse",
            "A clear piece, cold at the edges and warm at the centre. Inside it the light moves slowly, as if remembering how.",
            150);

        yield return new LoreEntry("mem_r6_1", 6, LoreKind.Memory, "The Frozen Lantern",
            "Inside the ice, a lantern is still lit, tiny and stubborn. Someone left it burning on purpose, so the way back would not be lost. The cold kept the light safe but also kept it small. Is a light that is only protected really alive?",
            -1);

        yield return new LoreEntry("mem_r6_2", 6, LoreKind.Memory, "A Drawing in the Ice",
            "A drawing frozen in the wall shows the Heart with many thin lines reaching outward, not one thick line pulling in. Whoever drew it imagined a different shape for the world, quietly, where few would see. Was it a plan, or only a wish?",
            -1);
    }
}
