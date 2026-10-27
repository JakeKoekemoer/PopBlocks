# Region 1 vertical slice: what to check on the phone

Branch: `feature/shattered-spectrum`. This is the P1 slice from `docs/shattered-spectrum-plan.json`:
the Forgotten Garden (levels 1-25) with Pip, one fragment, one journal entry and one board style.
Nothing after Region 1 is built until this slice is approved.

**The app screens in this slice were written without a MAUI build available.** The engine layer
(regions, story text, campaign rewards, save handling) has unit tests and passes. The pages, the
story panel and the drawings have never been compiled or run, so expect a first build to need small fixes.

## Build and run

1. Build and install on the S24 as usual.
2. Test once with a fresh install, and once over an existing save (see "Existing saves" below).

## Dev menu (for testing)

Debug builds have a red **DEV MENU** button at the bottom of the main menu. To get it in a Release build, build
with `-p:DevMenu=true`. A normal Release build hides it completely.

- **Gold:** +100 / +1,000 / +10,000 / set 0.
- **Jump:** type a level, or use a quick jump (1 fresh, 13, 25 = last level of Region 1, 26, 50, 100, 150, 200, 201+).
  That level becomes the next one to play. Story flags are rebuilt for that point, so jumping to 25 lets you play the
  region ending, and jumping to 1 replays the opening.
- **Shop and story:** max all upgrades; reset story flags (keeps levels and gold).
- **Reset everything:** back to a fresh install, after a confirmation.
- **In a level:** pause (or fail) and use **DEV: WIN LEVEL** or **DEV: +1,000 GOLD**. Winning runs the normal
  completion path, so gold, the story panel and the rewards all behave as in real play.

## Fresh install checklist (story bible section 13)

- [ ] Starting level 1 shows Pip's opening panel before the tutorial card. SKIP works. NEXT steps through the lines.
- [ ] While a panel is open, the piece does not fall and touching the board does nothing.
- [ ] Back (the Android back button) while a panel is open skips it instead of leaving the level.
- [ ] Restart the level: the opening does not come back.
- [ ] Your first pop shows a small Pip banner (not a panel). Your first chain shows the chain line once. Neither repeats.
- [ ] Clearing level 13 shows the "mark beneath the roots" panel. The map card changes to RECOVERING.
- [ ] Clearing level 25 shows the completion panel, then the card lists: Prism fragment, journal entry "The First Light", new board style Garden (applied).
- [ ] Skip the level 25 panel, then open the Archive: the fragment, journal entry and "restored" text are all there.
- [ ] Replay level 25: no panel, no second fragment, no gold.
- [ ] Close and reopen the app: map state, fragment count and board style match.
- [ ] Map: Region 1 shows FADED, RECOVERING, RESTORED with different pictures (drooping stems and closed buds, then half-open, then open flowers and lights). The word is always written, not just the colour.
- [ ] Regions 2-8 show as locked or faded placeholders, and the Archive says "More will be discovered in the Crystal Caves." and shows no spoilers.
- [ ] Main menu: MAP, CLASSIC, SHOP, ARCHIVE; LEVELS and ABOUT are still reachable. Board style row appears only after the Garden is earned.
- [ ] Classic mode and the shop behave as before.
- [ ] Airplane mode: everything still works (the app has no network permission).

## Existing saves

A save that already cleared 25 or more levels should, on first launch, show Region 1 as restored with the
fragment, journal entry and Garden board style already granted (quietly, with no panels), and the story panels
still readable from the Archive.

## What is deliberately not here yet

Regions 2-8 text, Mara/Bram/the Warden, region rules, finales, the ending, optional memories, the workshop.
Those start only after you approve this slice.

## Questions for you after playing

1. Does Pip's opening feel the right length? Are the panels too slow or too frequent?
2. Is the Garden picture good enough as a base for the other seven, or does the drawing style need to change first?
3. Are the reward lines on the level-complete card enough, or do you want a bigger moment at the end of the region?
