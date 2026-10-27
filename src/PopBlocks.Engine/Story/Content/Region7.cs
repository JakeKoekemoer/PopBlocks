namespace PopBlocks.Engine.Story.Content;

/// <summary>The Shattered Citadel (levels 151-175). The Warden is met and the truth is told.</summary>
internal static class Region7
{
    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r7_open", 7, SceneTrigger.RegionOpening, "Pieces of a thing", 151, new[]
        {
            Say.Warden("You have crossed half the world carrying pieces of a thing you do not understand."),
            Say.Pip("We know the Heart was failing. We know you broke it."),
            Say.Warden("Then you know more than most. But do you know what would have happened if I had done nothing?"),
            Say.Note("The Restorer says nothing. The challenge begins."),
        });

        yield return new StoryScene("r7_confession", 7, SceneTrigger.Milestone, "The full confession", 170, new[]
        {
            Say.Warden("The Heart was built to guide the Spectrum. Over time, the world asked it to carry every burden. The regions stopped trusting their own channels. Everything flowed inward."),
            Say.Warden("The Heart began to fail. I could see the overload spreading. I tried to warn the keepers, but they believed the system could be repaired without changing it."),
            Say.Mara("And so you shattered it."),
            Say.Warden("I interrupted the flow. I saved the world from collapsing in that moment."),
            Say.Pip("But you left it broken."),
            Say.Warden("Yes."),
            Say.Note("A pause."),
            Say.Warden("I told myself that no one should ever rebuild the Heart. I called that protection. It was also fear. I could prevent the old disaster, but I could not imagine a better future."),
            Say.Mara("You could have helped us."),
            Say.Warden("I should have."),
        });

        yield return new StoryScene("r7_plan", 7, SceneTrigger.Milestone, "The new plan", 172, new[]
        {
            Say.Bram("We don't need to restore the old channels exactly as they were. We can reconnect the regions to one another."),
            Say.Mara("The fragments can guide the flow without drawing everything into a single point."),
            Say.Pip("Then the Heart can be part of the world, instead of the thing the whole world depends on."),
            Say.Warden("If you can make that work, I will help you."),
        });

        yield return Say.Replay(7, "Back at the Citadel. Let's go carefully.");

        yield return new StoryScene("r7_complete", 7, SceneTrigger.RegionCompletion, "Let the result speak", 175, new[]
        {
            Say.Warden("The final fragments are within reach. I will not ask you to trust me. Let the result speak."),
            Say.Pip("That's fair. Let's finish this together—carefully."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r7_restored", 7, LoreKind.RestoredWorld, "Shattered Citadel — Restored",
            "The Warden has confessed the truth: the Prism Heart was shattered to stop a catastrophic overload. The final task is not to restore the old system, but to create a balance that can endure.",
            175);

        yield return new LoreEntry("r7_journal", 7, LoreKind.Journal, "The Warden's Choice",
            "The Warden stopped the old system from collapsing, then allowed fear to prevent a new system from being built. The Restorer's task is to preserve the lesson without preserving the damage.",
            175);

        yield return new LoreEntry("fragment_r7", 7, LoreKind.Fragment, "Fragment: the Citadel",
            "A heavy, dark-edged shard that the Warden did not want to touch. It glows a little brighter when it is handed over.",
            175);

        yield return new LoreEntry("mem_r7_1", 7, LoreKind.Memory, "The Warden's Seal",
            "The seal carved above the citadel doors has been scratched through, then carefully smoothed again, as if its owner could neither keep it nor destroy it. It says something about the weight of the choice. What did the Warden hope to hear when the doors were first opened?",
            -1);

        yield return new LoreEntry("mem_r7_2", 7, LoreKind.Memory, "A Plan for the Bridges",
            "Pinned beneath a stone is a plan for the citadel's bridges, drawn in two hands: one cautious, one bold. The margins argue politely. The two seem to have respected each other. Who were they, and did they ever finish the argument?",
            -1);
    }
}
