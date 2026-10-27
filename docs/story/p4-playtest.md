# P4 playtest checklist (needs a device; nothing here could be run in the build sandbox)

The engine side of P4 (shop catalogue, achievements, region reward gold, accessibility settings, journal stats,
workshop stations, Colour Lens and Chain Sense logic) is covered by unit tests. The pages and drawing are not built
or run anywhere but your machine, so please check:

## Build
- [ ] The app builds. New files: `WorkshopPage`, `AchievementsPage`, `JournalPage` (xaml + cs), changed `ShopPage`,
      `MainPage`, `GamePage`, `GameDrawable`, `BlastEffects`, `GameViewModel`, `MauiProgram`.
- [ ] `GameViewModel` now takes an `AchievementService` (registered in `MauiProgram`).

## Main menu
- [ ] The old SHOP button now reads WORKSHOP and opens Bram's Workshop.
- [ ] Settings: "Landing guide" is a button cycling OUTLINE / BOLD / OFF; "Reduced motion" is a switch.
- [ ] An existing save that had the guide switched off still has it OFF; one that had it on has OUTLINE.

## Workshop
- [ ] A new save shows 1 of 7 lamps lit; the next corner is named with the region that lights it.
- [ ] Archive, Journal, Achievements and the workbench open whether or not their lamp is lit once you reach them
      (stations that are dark show "Not yet lit." but their OPEN buttons still work).
- [ ] The paint shelf shows no OPEN button yet (nothing for sale until P5).
- [ ] The workbench lists Lookout, Pop Flair, Spare Moves, Colour Lens and Chain Sense. Locked levels say
      "Restore ..." and show no price. Prices and effects match `docs/story/economy.md`.
- [ ] Lookout 3 and 4 show up to 5 upcoming pieces; the Next panel is wide enough.

## In a level
- [ ] Colour Lens level 1 outlines groups of 3 (solid line); level 2 also groups of 2 (dashed line). Nothing is
      outlined once a group is big enough to pop. Check on a flipped (Extreme) level: the outline is on the right sides.
- [ ] Chain Sense: the landing outline turns gold and dashed only when dropping would start a chain (spot-check by dropping).
- [ ] Bold landing guide: thicker outline, faint column of light, tinted landing cells.
- [ ] Reduced motion: no screen shake, no confetti or sparks, blocks do not slide, pending groups do not pulse.
      Pop scoring and timing are unchanged.

## Rewards
- [ ] Restoring a region the first time adds a "Region reward: +N gold" line (50, 75, ... 225). Replaying it adds nothing.
- [ ] Achievements appear as "Achievement: Name (+N gold)" on the level card once, and are marked EARNED on the
      Achievements page. Gold on the main menu matches.
- [ ] Journal page: bests update after a run; "levels played in each region" counts rise.
