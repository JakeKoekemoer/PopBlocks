# P3 playtest checklist: region rules, finales, memories and the ending

Build with the dev menu on (`-p:DevMenu=true`, or a Debug build). Use **Dev menu → Set next level** to jump.

## Region rules (levels 26-200)

| Jump to | What to look for |
|---|---|
| 26 | Crystal Caves card, then the big-group card. Goals lean on "Pop a group of N". |
| 51 | Verdant Wilds card, then the Medium card. Many "Pop N <colour> blocks" goals. |
| 76 | Sunken City card. Move counts feel tight. |
| 101 | Clockwork Wastes card, then the Hard card. Chain goals and scattered piles. |
| 126 | Frozen Expanse card. Denser piles. |
| 128 | First **flipped board**, with its own card explaining the controls. |
| 134 | First **pop penalty** level, with its own card ("a piece that pops nothing costs points"). |
| 151 | Shattered Citadel card. Two goals at once; three from 161. |
| 176 | Heart of Spectrum card. Everything mixed. |

Flipped boards and penalties never appear before level 126, and never on a finale.

## Finales (25, 50, 75 ... 200)

- [ ] The tip names the region ("The last level of the Crystal Caves ...").
- [ ] Each finale is a little tougher than the level before it, but you can win it with no shop upgrades.
- [ ] Level 200: the goal reads "Make 3 separate chains of x3 or more", the tip is "Do not force every colour into one perfect shape. Let each connection support the next.", and the card explains that each chain counts once.

## Memories (optional)

Memory levels: 12, 18, 37, 43, 62, 68, 87, 93, 112, 118, 137, 143, 162, 168, 187, 193.

- [ ] A gold ◆ "Optional: ..." line sits under the goals, with its own progress.
- [ ] Meeting the optional objective *and* winning shows "Memory found: <title>" on the complete card; the Archive's MEMORIES section lists it.
- [ ] Winning without it shows "Optional memory not found: ..." and nothing else changes (gold, progress and rewards are the same).
- [ ] Replaying the level and meeting it later still finds the memory, and pays no gold.
- [ ] A memory in every region unlocks the journal entry "Every Last Memory" and, after the ending, the optional epilogue.

Note: the level ends the moment the main goals are met, so the optional objective has to be met by then.

## Ending

- [ ] Clearing 200 plays "r8_complete", then asks "How should the world look now?" with three looks.
- [ ] Picking a look shows Pip's line for it. DECIDE LATER leaves it open.
- [ ] The Archive shows "HOW THE WORLD LOOKS" and lets you change it at any time.
- [ ] The map shows "The Spectrum, restored" at the top once 200 is cleared.
- [ ] Levels 201 and above carry on with no story panels.
- [ ] If you have a memory from every region and have picked a look, the epilogue plays once, then stays readable in the Archive.

## Not checked by tests

How the new region levels *feel*: whether a region's theme comes across, and whether the finales are fun rather than just harder.
