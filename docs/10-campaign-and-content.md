# 10. Campaign and content

How the 200-level campaign is put together, where its text lives, and how to change it. The rules for levels are in [Story mode](04-story-mode.md); this document is about the story around them.

## 10.1 The shape of the campaign

- **Eight regions of 25 levels** (`Story/RegionCatalog.cs`): the Forgotten Garden (1-25), the Crystal Caves, the Verdant Wilds, the Sunken City, the Clockwork Wastes, the Frozen Expanse, the Shattered Citadel and the Heart of the Spectrum (176-200). Levels above 200 are **Beyond the Spectrum**: endless, same rules as the last region, no scenes.
- **A region's state is a function of progress.** `RegionCatalog.Progress(region, highestCleared)` gives Faded, Recovering (from its middle level) or Restored (its last level). There is no second record of campaign progress to keep in step with `PlayerProgress`.
- **Everything else the story remembers is an id in `UnlockStore`**: scenes seen (`seen_*`), lore and memories (`lore_*`), Prism fragments (`frag_*`), region rewards paid (`gold_*`), achievements (`ach_*`), cosmetics owned (`cosmetic_*`).
- **One fixed ending.** Clearing level 200 plays one ending scene and asks for a purely cosmetic final choice (`StoryContent.FinalChoices`: three looks). Nothing in the text or the rules branches on it, and the choice can be changed later. The ending needs no purchase and no optional memory (`P7Tests`).
- **Finales** are the last level of each region (25, 50, ... 200). They are a little tougher, never flip or penalise, and have helpers switched off.
- **Memories** are optional: two per region, found by meeting a bonus objective on levels 11 and 17 of the region (`BonusPlanner`). Finding one in every region unlocks an optional epilogue.

## 10.2 Where the text lives

The story is **plain data in C#, not logic**. Canon is `docs/story/StoryText.md` (the story bible); the game text follows it, and where they differ the bible wins and the code is corrected.

| What | Where |
|---|---|
| Scenes, lore and memories for region N | `Story/Content/RegionN.cs` (`Scenes()` and `Lore()` iterators) |
| Short line builders (`Say.Pip(...)`, `Say.Warden(...)`) and the replay line | `Story/Content/Say.cs` |
| Who may speak, and from which region | `Story/StoryScene.cs` (`Cast`) |
| The scene and lore lists, the three final looks, the epilogue | `Story/StoryContent.cs` |
| Region names, descriptions of the faded and restored world | `Story/RegionCatalog.cs` |
| The cards shown on a region's first level and when a feature is introduced | `Story/RegionRules.cs` (`IntroCards`) |

A **scene** has an id, a region, a trigger (`RegionOpening`, `Milestone`, `MidDiscovery`, `RegionCompletion`, `Ending`, `Reaction`, `LevelIntro`), a title, the level that fires it, and its lines. Scenes fire on a first clear or when a level is first entered (`CampaignService.OnLevelCompleted`, `OnLevelEntered`); a replay never fires one. Every scene is skippable, and skipping it never loses a reward, because rewards are granted *before* the scene is shown.

### Editing a line

Change the text in the region file. That is all. Run the tests: `FullStoryTests` and `StoryLayerTests` check the things that are easy to break by editing text:

- nobody is named or speaks before the region where they are introduced, and the Warden does not speak directly before region 7;
- no text hints at a branching ending ("bad ending", "if you chose" ...);
- every scene is a sensible length and every line fits on screen;
- the story text is consistent with the mechanics (it never describes a rule the game does not have).

### Adding a scene or lore entry

1. Add a `StoryScene(...)` or `LoreEntry(...)` to the region's iterator with a **new, lowercase id** (`r3_something`). Ids are saved (`seen_<id>`, `lore_<id>`), so never rename or reuse one.
2. Give it the level that fires it (`UnlockLevel`) inside the region. Two scenes must not fire on the same level unless one is the ending pair.
3. Run the tests; then play it with the developer menu (jump to the level before it).

## 10.3 What the player gets, and when

| Moment | Reward | Paid |
|---|---|---|
| First clear of a level | Level gold; lore and scenes tied to that level | Once |
| Region's middle level | The region starts to recover (scene, picture) | Once |
| Region's last level | The region's Prism fragment, journal entry, a one-off gold reward, the looks it reveals (`CosmeticCatalog.FoundByRestoring`) | Once |
| A memory objective met | A memory in the Archive | Once; works on replays; no gold |
| An achievement | A small amount of gold and sometimes a look | Once |
| Level 200 | The ending, the final choice, three new boards | Once |

`CampaignOutcome` carries what a clear produced so the level-complete card can show it; the rewards are already granted when it is returned.

## 10.4 Rules to keep

1. Every level 1-200 is completable with zero purchases and zero helpers, **for every seed a player can be given** (the bot proves it when the level is generated, the gate test and the fuzz sample it; `P4GateTests`, `P7Tests`, `SeededCatalogueReplayTests`). No level needs gold to progress: the reroll, the only thing that spends gold on the way, is free from the tenth failure (`P7Tests`).
2. Gold is paid on first clear only (Classic pays by the block; see `docs/story/economy.md`). Replaying, skipping a scene or catching a save up (`Reconcile`) never pays again. A reroll never pays or refunds.
3. Story and accessibility are never sold. Cosmetics are discovered through the story or an achievement and then bought; locked items show a hint, never a price.
4. Never rename or reuse a save key or an unlock id. Add new ones.
5. Rules live in the engine. If an engine rule changes, change `LevelSolver` with it, and expect `LevelSnapshotTests` to show every level that moved.
6. Story text must match mechanics, and scenes must stay skippable.

## 10.5 Testing the campaign by hand

The developer menu (see [Build and release](08-build-and-release.md)) can jump to any level with the story flags rebuilt for that point, add gold, max the shop, fill the helpers, unlock every look, reset the story or reset everything. The checklists in `docs/story/` (`p3-playtest.md` to `p6-playtest.md`) list what needs a person on a device: how scenes and looks actually appear, frame time during a long chain, and playing a region start to finish. The economy and its reasoning are in `docs/story/economy.md`; the helper rules are in `docs/story/helpers.md`.
