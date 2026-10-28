# Decisions

One line per judgement call made while executing PLAN.md.

- P2: the end-beat move counter shows the real moves left (0 on an out-of-moves loss); the pulse is the cue, and lessons keep their old short card titles.
- P1: Story card scrim runs transparent (top) to 70% (bottom) and the scene image is cached via PlatformBitmapExportContext, with a live-draw fallback if it cannot be created.
- P3: the HUD blanks (no goals, moves or objective) while a level is being found, then everything rebinds through `LoadLevelState()`; this is how the stale goal bar after a reroll is fixed.
- P3: `stat_rerolls` counts every successful reroll, including the free ones on cleared levels; it is added to `SaveKeys.StatKeys` (so it is in `All`, the save code and the frozen list).
