# P5 playtest checklist (needs a device; the drawing code is only compiled against stand-ins here)

Covered by unit tests: the catalogue, ownership, buying, equipping, the skin rules (hue, separation, symbol contrast),
board legibility, saves from before P5. **Not** covered: how anything actually looks, or how fast it draws.

## Build
- [ ] The app builds. New: `WardrobePage`, `Graphics/WardrobeDrawables.cs`. Changed: `VisualDesign`, `CellRenderer`,
      `BlastEffects`, `GameDrawable` (BoardPalette is now built from the catalogue), `StoryDrawables` (Pip),
      `GamePage`, `GameViewModel`, `MauiProgram`, `WorkshopPage`, `DevMenuPage`.
- [ ] `GamePage` now takes a `CosmeticLoadout`; `CosmeticShop` is registered.

## Dev menu
- [ ] DEV MENU > UNLOCK ALL LOOKS gives every look so each can be tried without playing for it.

## Paint shelf (Workshop > The paint shelf; it opens even before its lamp is lit)
- [ ] Tabs: BLOCKS, BURSTS, BOARDS, FRAMES, FOR PIP. Owned looks first, then buyable, then "Not found yet" cards that show
      only a hint ("Restore ...").
- [ ] Buying spends gold once and equips. A reward (frames, Pip's things, Spectrum skin, post-ending boards) has no price.
- [ ] The skin preview shows all five blocks with their symbols (diamond, circle, star, plus, triangle) for every skin.

## In a level (equip, then start a level; looks apply at the start of a level, never mid-level)
- [ ] **All five symbols are visible under every skin**, on every colour, also in the NEXT panel.
- [ ] Skins keep their colour families: cyan stays blue, coral pink, yellow gold, purple violet, lime green.
- [ ] Facet skins (Crystal, Frost, Whole Spectrum) show two corner facets and do not clash with the symbol. Textured
      skins (veins, bubbles, grain) keep texture away from the centre.
- [ ] A match about to pop still glows in its colour under every skin.
- [ ] Bursts: Petals, Shards, Bubbles, Cogs, Frost, Starburst each scatter their shape; Pop Flair still decides how many.
      With pop effects off, or Reduced motion on, nothing flies.
- [ ] Boards: each board's decoration is faint and never makes a block or grid line hard to read; High Contrast is black
      with a white grid. After the ending, the chosen look is worn automatically and the other two stay available.
- [ ] Piece frames outline the moving piece only. On a flipped (Extreme) level they sit on the right cells.
- [ ] **Frame time stays smooth during a 5-wave chain** with the richest burst (Pop Flair 3) and a textured skin. If it
      does not, say which look and I will simplify it.

## Pip
- [ ] Accessories (bow, cap, antenna, lantern) show on Pip in scenes and in the banner, once earned and equipped.
- [ ] Pip shows one small orbiting light per restored region.

## Rewards card
- [ ] Restoring a region adds "New in the workshop: ..." lines.
