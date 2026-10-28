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
