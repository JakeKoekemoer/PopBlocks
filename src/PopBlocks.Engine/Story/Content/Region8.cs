namespace PopBlocks.Engine.Story.Content;

/// <summary>The Heart of Spectrum (levels 176-200). The fixed ending: power flows outward to every region.</summary>
internal static class Region8
{
    public const string EpilogueId = "r8_epilogue";

    public static IEnumerable<StoryScene> Scenes()
    {
        yield return new StoryScene("r8_open", 8, SceneTrigger.RegionOpening, "The start of a repair", 176, new[]
        {
            Say.Pip("We're here. I thought reaching the Heart would feel like the end of the journey."),
            Say.Bram("It looks more like the start of a difficult repair."),
            Say.Mara("We know what went wrong. We have to make sure we don't rebuild the same weakness."),
            Say.Warden("The fragments will fit together. That part is easy. What matters is how the power flows when they do."),
        });

        yield return new StoryScene("r8_prep", 8, SceneTrigger.Milestone, "Final preparation", 195, new[]
        {
            Say.Pip("Each region has its own light now. The garden, the caves, the city, the machines…"),
            Say.Mara("They no longer need the Heart to carry every connection."),
            Say.Warden("Then let the Heart guide the Spectrum, not command it."),
        });

        // Shown when the player starts the final puzzle.
        yield return new StoryScene("r8_final", 8, SceneTrigger.LevelIntro, "The final puzzle", 200, new[]
        {
            Say.Warden("Do not force every colour into one perfect shape. Let each connection support the next."),
            Say.Pip("We don't have to make the world exactly as it was."),
            Say.Mara("We have to make it able to stand on its own."),
        });

        yield return Say.Replay(8, "The Heart is humming. It sounds like it's waiting for us.");

        // The one ending. Pip's closing line comes last. Skippable; rewards are granted before it plays.
        yield return new StoryScene("r8_complete", 8, SceneTrigger.Ending, "The Spectrum restored", 200, new[]
        {
            Say.Note("The fragments join. The Prism Heart reforms, but it does not pull every colour inward."),
            Say.Note("Instead, streams of light move outward and travel along paths to the restored regions. Each region glows in its own way."),
            Say.Note("The Heart remains bright, but it is no longer the only source of light."),
            Say.Pip("It's working! The light is going out to the regions, not pulling them in."),
            Say.Bram("A system that shares the load. I like the sound of that."),
            Say.Mara("The old records will have to be rewritten."),
            Say.Warden("Not erased. Rewritten with the truth included."),
            Say.Pip("And what happens now?"),
            Say.Warden("Now we take care of what we restored."),
            Say.Pip("Good. I was hoping there'd be a tomorrow."),
            Say.Note("The Prism Heart was whole again, but the world was no longer bound to it."),
            Say.Note("The Forgotten Garden grew from its own roots. The Crystal Caves kept their memories. The Wilds found their rhythm. The Sunken City shone beneath the water."),
            Say.Note("The machines turned, the ice released its paths, and the Citadel opened its doors."),
            Say.Note("The Spectrum had not returned to the world as a single flame. It had become a thousand lights, connected and shared."),
            Say.Note("And for the first time since the fracture, the world did not merely shine."),
            Say.Note("It lived."),
            Say.Pip("Same colours. New connections. I think that's better."),
        });

        // Optional: only after a memory has been found in every region. Never fired by a level; see CampaignService.
        yield return new StoryScene(EpilogueId, 8, SceneTrigger.Ending, "Every last memory", -1, new[]
        {
            Say.Pip("We found every last memory."),
            Say.Mara("Then the story will not be told from one record alone."),
            Say.Warden("Let the world remember the warning, the mistake, and the recovery."),
            Say.Bram("And let it remember who fixed the workshop. That part is important."),
            Say.Pip("Bram!"),
            Say.Bram("What? History needs a little balance too."),
        });
    }

    public static IEnumerable<LoreEntry> Lore()
    {
        yield return new LoreEntry("r8_restored", 8, LoreKind.RestoredWorld, "The Spectrum Restored",
            "The Prism Heart has been reunited, and its power now flows outward through the regions instead of gathering in one place. The world is connected again—and more resilient than before.",
            200);

        yield return new LoreEntry("r8_journal", 8, LoreKind.Journal, "A Thousand Lights",
            "The Heart was never meant to be the only source of light. It was meant to help the lights find one another. The Restorer did not return the world to what it was; the Restorer helped it stand on its own.",
            200);

        yield return new LoreEntry("fragment_r8", 8, LoreKind.Fragment, "The Heart",
            "The last piece, and the first: the one that makes the others a whole, and lets them be more than one.",
            200);

        yield return new LoreEntry("mem_r8_1", 8, LoreKind.Memory, "The Heart's First Note",
            "Deep in the chamber, a single clear note rings whenever a fragment is held close. It is the sound Pip has always half remembered. It is not loud. It is the sound of many things agreeing. Will it sound different when the pieces are joined?",
            -1);

        yield return new LoreEntry("mem_r8_2", 8, LoreKind.Memory, "Names on the Floor",
            "Around the chamber floor, hundreds of tiny names have been scratched by the keepers who tended the Heart. The newest are barely legible. The Heart was never alone; many hands cared for it, one after another, for longer than anyone counted. Whose names will be added next?",
            -1);

        yield return new LoreEntry("epilogue_journal", 8, LoreKind.Journal, "Every Last Memory",
            "The story was never held in one record. It lived in letters, sketches, a lantern in the ice and names on a floor. Gathered together, they say what no single account could: the world fell, was saved, and was rebuilt by many hands.",
            -1);
    }
}
