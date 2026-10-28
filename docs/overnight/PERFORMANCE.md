# Performance findings (v1.1 build)

Measured on the build machine (a desktop CPU, not a phone), with the engine benchmark that is skipped by default:

```
POPBLOCKS_BENCH=1 dotnet test tests/PopBlocks.Tests --filter EngineBenchmark --logger "console;verbosity=detailed"
```

Each figure is one hard drop plus everything that settles after it (every wave, gravity, cracks, the Orb, the camera), over
10,000 random moves. Times are microseconds.

| Run | Mean | p99 | Max | Budget |
|---|---|---|---|---|
| Before v1.1's rules: no cracks, no Orbs (`Fracture = false`, `Orbs = Never`) | 33.7 | 121.7 | 3,704 (JIT / GC) | 2,000 p99 |
| Default rules (cracks on, Orbs on) | 46.8 | 144.7 | 615 | 2,000 p99 |
| 24 rows + cracks + Orbs always | 59.3 | 197.1 | 1,079 | 2,000 p99 |
| Pip's Insight (`PlacementAdvisor.Best`), 600 pieces | 756 | 2,141 | 5,251 | 4,000 p99, off the UI thread |

A settle with cracks, an Orb and the camera costs about a hundred microseconds more than the old settle at the mean and stays
two orders of magnitude inside the 2 ms budget at p99. The advisor stays inside its 4 ms at p99 and runs on a background task,
so a slow piece never holds up a frame.

## What was checked, and what was changed

| Area | Finding | Action |
|---|---|---|
| `GameDrawable.Draw` | The rounded clip outline of the board was rebuilt every frame (a `PathF` allocation). | Cached by size; rebuilt only when the board's size changes. |
| Burst particles (`BurstParticles`) | `foreach` over `IReadOnlyList` boxes an enumerator on every call. | Index loops. The two outlines (petal, shard) are built once. Particles are stateless functions of (cell, number, age), so there is no pool to grow. |
| Cracks / breaks (`BlastEffects.DrawCracking`) | Same enumerator boxing over the wave lists. | Index loops; no allocation. |
| Pip's Insight outline (`GameDrawable.DrawInsight`) | Enumerator boxing; dash array. | Index loop; the dash array is static. |
| Floating gold (`FloatingText`) | Text built per frame would allocate. | Strings are formatted when a group pops and stored in the slot; twelve slots, reused. |
| Orb (`OrbDrawable`) | Number text. | Pre-made digit strings; no allocation. |
| Board ambience (`BoardAmbient`), animated frames, camera, depth gauge | Per-frame math only; "n rows below" text. | Particle count capped at 12 and time rounded to 15 steps a second; the label string is rebuilt only when the number changes. |
| Block patterns | The detailed Starfield / Tidepool / Granite / Ember / Blossom faces have many small parts. | Painted once per set, colour and size into an image (`PatternCache`, at most 48 entries) and blitted. |
| Menu scene | The Story card's region picture. | Rendered once to an image (`CachedSceneDrawable`) and redrawn only when the size, region or its state changes. |
| Repaint timers | The 60 fps timer runs only while an effect is alive. The board's slow movement (ambience, animated frames, Orb) repaints at about 15 fps and only while a game is playing: never when paused, never after 10 seconds without input, never under Reduced motion. Wardrobe burst tiles repaint only while on screen. | A debug-only counter (`Diagnostics.CountFrame` / `FramesPerMinute`) shows the board's frames per minute, so an idle board that keeps painting is easy to spot. |
| Tall boards | Only the rows on (and one row either side of) the screen are drawn; the 300 ms camera ease repaints only while it moves. | Done in Phase 11. |
| Audio | Coin ticks are capped at 8 per count-up (about 120 ms apart) and use the existing voice pool; no new thread. | None needed. |
| Settings | The fall-speed slider writes when it is let go, not on every move. | Done in Phase 7. |
| Catalogues | The looks, achievements and shop items are static arrays built once. | None needed. |
| Permissions | The manifest still has no `<uses-permission>` entry, and no package was added. `Share.Default.RequestAsync` needs none. | `scripts/Check-Permissions.ps1` was not run: it needs a signed Release APK and none was built overnight. |

## Not done (and why)

* **The board surface is not cached as an image.** The gradient and the motifs are redrawn on each paint, as they always were.
  The new ambient layer is cheap (at most 12 stateless shapes), and nothing measured over budget on the engine side; a phone
  draw-time measurement is still wanted before deciding. If the 8 ms draw budget is missed, cache `DrawBoardSurface` with
  `PatternCache`-style keys (board id, size).
* **Cracked blocks are drawn straight onto the canvas** (a few short lines) rather than cached per set x colour x level.
* **Draw time on a phone was not measured** (there is no device here); the figures above are engine times.
* The engine's wave code still uses LINQ for the per-wave lists (groups, cleared cells). It runs once per wave, not per frame, and
  the settle numbers above include it.
* One test in the suite failed once in each of two full runs and passed every time it was re-run (it is not timing-free); the one
  suspect, the Insight speed check, now takes the best of twenty runs instead of an average.
