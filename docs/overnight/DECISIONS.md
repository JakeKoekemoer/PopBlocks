# Decisions

One line per judgement call made while executing PLAN.md.

- P2: the end-beat move counter shows the real moves left (0 on an out-of-moves loss); the pulse is the cue, and lessons keep their old short card titles.
- P1: Story card scrim runs transparent (top) to 70% (bottom) and the scene image is cached via PlatformBitmapExportContext, with a live-draw fallback if it cannot be created.
- P3: the HUD blanks (no goals, moves or objective) while a level is being found, then everything rebinds through `LoadLevelState()`; this is how the stale goal bar after a reroll is fixed.
- P3: `stat_rerolls` counts every successful reroll, including the free ones on cleared levels; it is added to `SaveKeys.StatKeys` (so it is in `All`, the save code and the frozen list).
- P4: `level_losses` counts the same events as failures (a loss, or a restart after a piece was placed, on the current level only); it never resets. `eased_levels` is marked when the player eases (so the eased seed survives a restart), shown as a feather only once the level is cleared.
- P4: the eased band is [1.0, middle of the normal band]; levels whose normal band is already 1.0 get only the extra moves. Eased rolls use reroll number + 1,000,000 as the marker and their own seed stream (+5,000).
- P5: Story pop gold uses the Endless rate by score level (`ClassicTenthsPerBlock(Level)`); undo restores the gold counter so rewinding cannot farm it. Lessons, tutorial-tier levels (1-10) and eased levels earn none.
- P5: there is no "COMBO" wording in the scoring code, so floating gold shows only `×N CHAIN` (for chain waves); the largest-chain banner is the existing chain accent, now 1.2 s and with the chain's gold added.
- P7: ShowHanging lives in GameSettings (key show_hanging) and Colour assist in GameSettings (key colour_assist); both are new keys not in the plan appendix, recorded in SaveKeys/SaveCode/frozen list. Colour assist is used by Phase 9a.
- P7: IsEasierThanDefault is true when ANY dimension is easier (even if another is harder), so easy+hard pairs cannot earn full gold. A run on custom rules never sets the Endless best score.
- P7: board height, cracking and Orbs rules are stored and normalized but hidden/inert behind RuleFeatures until Phases 11, 13 and 14 switch them on. Shape lists are saved as letters (a = first catalogue shape) so they fit the 64-character text field of a save code.
- P8: the particles are stateless analytic functions of (cell, index, age) instead of pooled objects: nothing to pool, nothing allocated per frame, and the wardrobe tile can replay the identical motion.
- P9a: Starfield and Ember use dark symbols (the plan said light) because pale symbols on those bright faces fail the 3:1 symbol-contrast test; the Starfield darkening is confined to the lower part of the face for the same reason. Tests that budgeted ALL cosmetics (spend range, price-by-region, frames never sold, P7 income) now apply to the original looks only (OriginalPrice); NewPrice is a separate long-term sink.
- P9a: cosmetic_seen is stored as an id list in the existing Unlocks text format and does not count towards the save-code summary of unlocks.
- P9b: the static board layer is not rendered to an offscreen image (the gradient and the motifs are drawn each paint as before); the ambient movement is capped at 12 stateless particles at 15 steps a second instead. Revisit in Phase 16 if the draw budget is missed.
- P9c: Pip is not drawn on the main menu or the map, so Party Pip's confetti is available through PipDrawable.Confetti but is only shown where Pip has an animation clock (the story stage); the wardrobe tile is a still picture.
