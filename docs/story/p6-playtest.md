# P6 playtest checklist (needs a device; the pages and view model are only syntax-checked here)

Covered by unit tests: undo restores every field and replays the same pieces (including random play with random undos),
refusals (paused, popping, over, tutorial, finale, Classic), Prism Shift rules and cycling, stock/caps/prices/locks,
charges taken from stock only when used, run statistics rolled back, the "without helpers" objective, saves.
**Not** covered: how the buttons look and feel, or the timing of the fall timer after an undo.

## Build
- [ ] The app builds. Changed: `GameViewModel` (new ctor argument `ConsumableInventory`, helper commands),
      `GamePage.xaml` (helper row), `ShopPage` (SUPPLIES tab), `DevMenuPage`, `MauiProgram` (registers `ConsumableInventory`).

## Dev menu
- [ ] MAX SHOP now also fills both helpers to 3 and buys Prism Reserve. FILL HELPERS fills only the helpers.

## Workbench (SUPPLIES tab)
- [ ] Both helpers listed; locked ones show a hint ("Restore ...") and no price; unlocked ones show BUY ONE with the price.
- [ ] Buying spends gold and fills a pip; at 3 the card says STOCK FULL.
- [ ] HELPERS tab has Prism Reserve (locked until region 5 / 7).

## In a level (region 2 or later, with a helper held)
- [ ] The UNDO / SHIFT buttons appear only when a helper is held; they are hidden on tutorials, the last level of a region,
      and in Classic.
- [ ] UNDO is greyed with "Place a block first..." before the first placement. After a placement it takes the block back,
      the board returns, the same piece comes again, moves left goes back up by one, and the button then reads x0.
- [ ] UNDO is greyed while blocks are popping and when paused.
- [ ] SHIFT swaps the colours in the NEXT box; the shape does not change; the badge counts down; a single-colour next
      piece shows "nothing to rearrange".
- [ ] After using a helper and leaving the level, the stock on the workbench is one lower. Leaving a level without using
      one costs nothing.
- [ ] A screen reader reads each button with its remaining count and why it is disabled.
- [ ] No level feels like it needs a helper (play a few with none held).

## Memory objectives
- [ ] The second memory of regions 5-8 reads "... without helpers" for Chain / Big Group objectives, and using a helper
      turns the progress line to "a helper was used".
