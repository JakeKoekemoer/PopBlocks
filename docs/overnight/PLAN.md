# PopBlocks v1.1 — unattended build plan

Execute every phase in order without stopping. Repo `E:\PopBlocks`, branch `V1.0.2`.

## 0. Rules (re-read at the start of every phase and after any context compaction)

**Never stop or ask.** Missing detail → pick the option that keeps the build green, keeps old saves loading, and is easiest to change later; append one line to `docs/overnight/DECISIONS.md`. A feature that cannot be finished ships disabled behind its `GameRules`/flag with UI hidden, is noted in `docs/overnight/PROGRESS.md` under "Unfinished", and the run continues.

**Progress.** `docs/overnight/PROGRESS.md`: one row per phase (status, commit hash). Update after each commit. After compaction: read this section, then PROGRESS.md, resume at the first unfinished phase.

**Commits.** All on the current branch `V1.0.2`. One commit per phase (9a/9b/9c separate). Never push, rebase, amend or force. Never add, edit or delete `scripts/Build-Aab.ps1`.
```
git add -A -- . ':!scripts/Build-Aab.ps1'
GIT_COMMITTER_NAME="JakeKoekemoer" GIT_COMMITTER_EMAIL="jakekoekemoer@gmail.com" \
git commit --author="JakeKoekemoer <jakekoekemoer@gmail.com>" -F <msgfile>
```
(PowerShell: set `$env:GIT_COMMITTER_NAME/EMAIL` first.) Message format:
```
Phase <N>: <what changed, one line>

<Everything the phase did: why, then bullets per file group, rule changes,
new save keys, snapshot changes with level numbers affected, docs touched.>

Tests: <passed>/<total>   (or: not run, reason)

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Gate before every commit.** `dotnet build PopBlocks.sln` (if the Android workload is missing, build `src/PopBlocks.Engine` + `tests/PopBlocks.Tests` and state it in the message) and `dotnet test tests/PopBlocks.Tests` all green (baseline 965). Never commit a failing build or test. A frozen snapshot this plan changes on purpose is regenerated with `POPBLOCKS_WRITE_SNAPSHOT`; any other persistent failure → disable the feature as above. Every engine rule gets tests. Update affected docs (`docs/01`–`11`, `docs/story/economy.md`, `README.md`) in the same commit.

**Codebase invariants.**
- Engine (`PopBlocks.Engine`, pure net10.0) owns rules, money, stats, catalogues. App only binds and draws; graphics never decide rules.
- Save keys add-only: `SaveKeys.cs` + `SaveKeys.All` + `SaveCode` + migration default; extend the freeze tests, never loosen them.
- `LevelSolver` mirrors every rule that affects pops, gravity, spawning or moves (see commit 1323c71). Levels are rolled per player by `LevelRoller` through the bot gate, so any rule the solver mirrors is safe to use in Story; regenerate `LevelSnapshotData` when rules change and keep `SeededLevelStructureTests`' unverifiable-seed rate under 2%. The legacy-seed "byte-identical" guarantee is relaxed by this plan; update its tests to pin the new output.
- Determinism: no clock, no `Random.Shared` in the engine; new randomness uses dedicated seeded streams that never shift the piece stream.
- No INTERNET permission (`scripts/Check-Permissions.ps1`). Share via `Share.Default.RequestAsync`.
- Colour is never the only cue. Every new animation honours Reduced motion (static or short fade).
- Player-facing renames only: `GameMode.Classic`, `ClassicGold`, `ArchivePage`, save keys and achievement ids keep their names.
- The 60 fps animation timer runs only while an effect is alive; no per-frame allocations in draw paths.

## 1. Fixed decisions

- **D1 Pop gold in Story.** Story earns per-block gold at the Endless rate (`ClassicGoldTenths`/`ClassicGold`), banked on win in addition to the unchanged first-clear reward. Replays earn pop gold; first-clear still pays once.
- **D2 Loss consolation** = `floor(runPopGold/2) + floor(firstClearReward × goalProgress / 2)`; goalProgress 0–1 averaged over goals; the second term only while the level is uncleared, lifetime-capped per level at `firstClearReward` (key `consolation_paid`).
- **D3** Floating gold shows one decimal under 10 (`+0.6`), integers from 10.
- **D4 Continues.** +5 moves, only on `GameOver/OutOfMoves` in Story, not finales or eased levels. Costs 30 / 42 / 55; 3 per attempt; refill on retry, reroll, new level.
- **D5 Ease.** Offered at ≥15 total losses (not level 200/finales): reroll one `DifficultyBand` easier, move limit ×1.5 (ceil), through the bot gate. Eased clear pays nothing (no first-clear, pop gold, consolation or "win" achievements), advances story, marks level eased (key `eased_levels`, feather on Map). A later normal win removes the mark and pays first-clear.
- **D6 Counters.** New per-level `level_losses` never resets. Existing reroll failure count resets to 0 on reroll and on win.
- **D7 Custom rules.** Always apply to Endless; apply to Story only with "Use my rules in Story". Rules easier than default pay ×0.5 gold everywhere (incl. first-clear) and show a HUD chip "Custom rules · half gold". The 5-loss "solvable" note is hidden while custom rules apply to Story. Lessons and finales always use defaults.
- **D8 Mechanic introduction (Story).** `RegionRules` gains `FractureIntro = 36` and `OrbIntro = 40`. Fracture is active from 36 on; orbs from 40 on and appear in **every** level from 40 (see Phase 14). The Orb is the last new mechanic a player meets; later regions' flips/penalties/triple goals keep their levels. Both have intro cards via `RegionRules.IntroCards`. Endless: both on. Lessons: both off.
- **D9 Cosmetic gating.** Everything added tonight is `Requirement.None` with a price: buyable from day 1.
- **D10** Pure Solid skin hides symbols unless colour assist/High Contrast is on.
- **D11** Share link `https://play.google.com/store/apps/details?id=com.popblocks.game`.

## Phases

### Phase 1 — Main menu
Files: `Views/MainPage.xaml(.cs)`, `App.xaml`, `Graphics/Scenes/*`, `Graphics/StoryPalette.cs`.
- Landing `MoreButton` text → `MENU`.
- `CLASSIC` → `ENDLESS`; every player-facing "Classic" → "Endless" (HUD, cards, reroll hint, achievement text, `classic_champion` name → "Endless Champion", README, `docs/store-listing/*`).
- `ARCHIVE` → `STORY BOOK`.
- Delete the HOW TO PLAY caption + label.
- TUTORIAL and ABOUT become `SecondaryButton` in the grid. Layout: `MAP | ENDLESS`, `WORKSHOP | STORY BOOK`, `TUTORIAL | ABOUT`, full-width `SETTINGS`, full-width `SHARE` (Phase 2).
- Story card: `PlayButton` text `CONTINUE`. Add a `GraphicsView` behind the card content drawing the next level's region scene in its current state (Faded/Recovering/Restored) via existing scene drawables; levels 201+ use the Heart scene. Static: render once to a cached image, redraw only on size or region/state change. Scrim: vertical gradient `#07101F` 0% → 70%. Button background = region accent from `StoryPalette`, text colour by contrast; card rim = accent at 60%.

### Phase 2 — Level-end cards
Files: `Views/GamePage.xaml(.cs)`, `ViewModels/GameViewModel.cs`, `Views/MainPage.*`, new engine `Services/ShareText.cs`.
- Titles: `LEVEL {n} COMPLETE`, `LEVEL {n} — OUT OF MOVES`, `LEVEL {n} — STACK REACHED THE TOP`. Lessons keep `LESSON n`.
- Complete card: secondary `SHOP` beside primary `NEXT LEVEL`; failed card: ghost `SHOP` under RETRY. Opens `WorkshopPage` on Upgrades; back returns to the Map.
- End beat: after the final resolution, hold 900 ms with the move counter at `0` (one pulse 1.0→1.15→1.0, red on loss) before fading the card in (250 ms). Overflow: 600 ms, top row flashes once. Reduced motion: 600 ms, no pulse. One-shot dispatcher timer; input blocked; tap skips.
- Share: `ShareText.Build(context)`; menu `SHARE` button, share icon on complete card and Endless game-over card. Texts:
  - Menu: "I've been playing PopBlocks: a calm, clever falling-block puzzle where you match colours to bring a faded world back to life. Help Pip restore eight regions, chase chains, and find the memories hidden in every level. No internet needed, just good puzzles. Give it a go: {link}"
  - Story win: "Just cleared level {n} of PopBlocks in {region} with {moves} moves to spare. Every level is solved by the computer before you see it, so there is always a way through. Think you can find it? {link}"
  - Endless: "{score:N0} points in PopBlocks Endless mode, with a best chain of {chain}. Match colours, set off chains, and see how long you can last. {link}"
  - Tests: link always present, ≤400 chars, thousands separators.

### Phase 3 — Rerolls
Files: `Services/StoryLevels.cs`, `Services/LifetimeStats.cs`, `Services/SaveKeys.cs`, `ViewModels/GameViewModel.cs`, `Views/JournalPanel.*`.
- Bug: goal bar keeps old goals after reroll. Route reroll and level start through one load method that rebuilds goals, trackers, bonus objective, move limit, piles and all HUD bindings.
- Failure count → 0 on successful reroll and on win (D6).
- `LifetimeStats.Rerolls` (key `stat_rerolls`), incremented per successful reroll; Journal row "Levels rerolled".

### Phase 4 — Loss support
Files: `Services/StoryLevels.cs`, `Levels/LevelRoller.cs`, `Levels/DifficultyBand.cs`, `Services/SaveKeys.cs`, `ViewModels/GameViewModel.cs`, `Views/GamePage.xaml`, `Views/WorldMapPage.xaml.cs`.
- `level_losses` (D6).
- ≥5 losses, default rules: note under the failed-card title: "**There is always a way through.** Pip's puzzle machine solved this exact level before you saw it, so it can be done. Try a different first move, or reroll for a fresh version."
- ≥15 losses: button `EASE THIS LEVEL` with "Want an easier version of this level? You'll still move on in the story, but this level won't pay any gold." → confirm sheet `EASE IT` / `KEEP TRYING` → D5.

### Phase 5 — Pop gold, floating gold, consolation
Files: `Services/ClassicGold.cs`, `Logic/ScoringSystem.cs`, `Logic/GameEngine.cs`, `Models/BurstResults.cs`, `ViewModels/GameViewModel.cs`, new `Graphics/FloatingText.cs`, `Views/GamePage.xaml`, `docs/story/economy.md`.
- `BurstResults` per group: cells, multiplier, gold tenths. Story run total per D1; consolation per D2.
- `FloatingText` (pooled, ≤12 alive, text pre-formatted at spawn): group centroid, `+0.6` + 12 px coin, 15 sp bold `#FFD54A`, 2 px outline `#3A2A00`; rises 36 dp over 900 ms ease-out, scale 0.8→1.0 in 120 ms, alpha held 500 ms then fades. Multiplier >1: second line `×N CHAIN`/`×N COMBO` (existing scoring wording) 12 sp `#FF7A45`, number pulses once; largest chain per move also gets a 1.2 s banner `CHAIN ×N  +x gold`. Half-gold rules show halved values. Reduced motion: fade in place 600 ms.
- Failed card gold row "Kept half your gold": count-up 0→value over 900 ms ease-out, coin tick SFX every ~120 ms (max 8), 6–10 coins fly to the gold chip which ticks up. Zero → "No gold this time", no animation. Win card uses the same count-up (pop gold and first-clear on two lines).
- Keep `P7Tests` true (ending reachable with no purchases; first-clear never paid twice). Update economy doc with new income and sinks.

### Phase 6 — Continues
Files: `Logic/GameEngine.cs`, new `Services/ContinuePricing.cs`, `Services/RunStats.cs`, `ViewModels/GameViewModel.cs`, `Views/GamePage.xaml`, heart drawable.
- `ContinuePricing.Price(n)` = 30, 42, 55; `MaxPerAttempt = 3` (D4).
- `GameEngine.Continue(5)`: only from `GameOver/OutOfMoves`; → `Playing`, move limit +5, next piece from the same stream, undo snapshot recorded.
- Failed card: primary `+5 MOVES · {price} GOLD`, below it 3 drawn hearts (22 dp; filled `#FF5C7A` with highlight = left, 2 dp outline at 60% = used), accessible text "{k} of 3 continues left". Unaffordable: disabled + "Earn gold in Endless mode". On use: chip counts down, heart cracks and empties (220 ms), card slides away, move counter flashes `+5`.
- `stat_continues`. Tests: ladder, cap, not offered for overflow/finale/eased, determinism after continue, single deduction.

### Phase 7 — Settings tabs and custom rules
Files: new `Models/GameRules.cs`, `Services/SettingsServices.cs`, `Services/SaveKeys.cs`, `Models/GameSetup.cs`, `Logic/GameEngine.cs`, `Logic/PieceGenerator.cs`, `Logic/ColourMatcher.cs`, `Views/SettingsPage.xaml(.cs)`, `ViewModels/GameViewModel.cs`.
- `GameRules` (immutable; `Default`; `IsEasierThanDefault`; `GoldFactor` 1.0/0.5): `FallSpeedMultiplier` 0.50–2.00 step 0.25 (interval = base ÷ mult, clamped `[MinFallIntervalMs/2, InitialFallIntervalMs×2]`, falling piece only); `ColourCount` 3–5 + `EnabledColours` (exactly ColourCount); `EnabledShapes` (≥3, ≥1 four-cell); `MinGroupSize` 3–6; `SolidPieceChance` Off/0.15/0.40; `BoardRows` (Phase 11); `Fracture` on/off (Phase 13); `Orbs` Auto/Always/Never + `OrbTimer` 6/8/10/12 (Phase 14); `ShowHanging`; `UseInStory`. Keys `rule_*`, all in `SaveCode`. Defaults reproduce current behaviour exactly (guard test: all existing tests pass under `Default`).
- `GameSetup` carries effective rules (D7). Generator respects colours/shapes; matcher uses `MinGroupSize`; Story goals naming a disabled colour become any-colour goals. Solver uses `Default`.
- Settings tabs (Workshop tab-bar style; remember tab, key `settings_tab`): **GAME** (status chip Default/Custom/Custom·half gold, fall-speed slider with "1.25× faster"/"0.75× slower" label written on release, colour count + chips with symbols, piece grid from `PreviewDrawables` tap-to-toggle, burst size, solid pieces, board height, fracture, orbs, orb timer, Use my rules in Story, `RESET TO DEFAULT`), **DISPLAY** (board style, landing guide, hanging drops, pop effects, Reduced motion, colour assist), **SOUND** (existing), **PLAYER** (name), **SAVE** (backup, find file, save code, delete).
- Warning sheet on first change of colours/shapes/burst size/board height and whenever Use in Story is turned on: "**Heads up: the story may stop making sense.** Story levels and their goals were built and solved for the normal rules. With your rules some goals may be impossible or too easy, and Pip's 'there is always a way through' promise no longer applies. Easier rules pay half gold. Reset to default any time." Buttons `USE MY RULES` / `CANCEL`.

### Phase 8 — Petals vs Shards
File: `Graphics/BlastEffects.cs`, wardrobe previews in `Graphics/PreviewDrawables.cs`. Reuse pooled particles.
- Petals: teardrop (two quadratics), 5/cell, 7–10 dp; face lightened 35% with centre→tip gradient, 1 in 5 `#FFE8F0`; launch 40–90 dp/s up/sideways; gravity 60 dp/s², sine sway 10 dp @1.4 Hz, tumble 90°/s, drag; life 1100 ms, fade last 500; one bloom ring per group (0→1.2 cells, alpha 0.35→0).
- Shards: thin triangles (seeded points), 4–6/cell, 4–14 dp; full-saturation face, white specular edge; radial 220–380 dp/s; gravity 900 dp/s², spin 540–900°/s, one floor bounce at 30%; life 480 ms, fade last 150; 60 ms white flash on popped cells + 5-line crack star 120 ms.
- Reduced motion: petals fade in place; shards single flash. Wardrobe tiles loop the real motion only while visible.

### Phase 9a — Block sets
Files: `Services/CosmeticCatalog.cs`, `Graphics/CellRenderer.cs`, `Graphics/PreviewDrawables.cs`, `Views/WardrobePanel.*`, `Services/SaveKeys.cs`.
- `SkinPattern` += `Stars, Waves, Rock, Flame, Flower`; `SkinSpec.HideSymbol` (D10). Hue-preservation test must pass (nudge hex if needed). Render each skin×colour×size once to an offscreen image and blit.
- Faces order Cyan, Coral, Yellow, Purple, Lime:

| Id | Name | Price | Corner/Border | Highlight | Pattern | Faces |
|---|---|---|---|---|---|---|
| skin_solid | Pure Solid | 350 | 0.8/0.5 | Soft | none, HideSymbol, 1 px inner bevel | #1FB6F0 #F2577A #F7B500 #9566F2 #86D11F |
| skin_starfield | Starfield | 450 | 1.0/0.9 | Gloss | Stars: face darkened 35% at bottom → face at top; 5–7 white dots 1–1.6 dp α0.5–0.9 + one 4-point sparkle, seeded per colour; light symbol | #3A8FE0 #E0507F #E6B83A #8C5CE6 #6FC23A |
| skin_tidepool | Tidepool | 450 | 1.35/0.8 | Gloss | Waves: two sine bands lower half (lighter 20%/35%), curved glass highlight top-left | #1BB0E3 #F0708C #F5CB52 #8F82F0 #79D46A |
| skin_granite | Granite | 450 | 0.45/1.5 | Soft | Rock: 20–30 speckles (−25%/+15%), one hairline crack, two-tone chiselled border | #4A97B3 #C4677A #C49E48 #8775B3 #86A656 |
| skin_ember | Ember | 500 | 0.9/1.1 | Soft | Flame: hot core +40% bottom-centre, 3 flame tongues from bottom edge, dark char ring; light symbol | #2FC0F0 #FF5458 #FFAE1A #AE58FF #96E02A |
| skin_blossom | Blossom | 450 | 1.35/0.9 | Soft | Flower: 5-petal flower (petals +45%, centre #FFF2B3) top-right at 30% cell | #6CCDF0 #FF8DAA #FFD66B #C19EFF #A6E278 |
| skin_patchwork | Patchwork | 900 | per source | per source | each colour drawn as its chosen owned skin | — |

- Patchwork: after purchase, wardrobe shows 5 colour rows each with a strip of owned skins. Keys `cosmetic_mix_cyan|coral|yellow|purple|lime` (default `skin_classic`; unowned → Classic). Resolve to a lookup table on loadout change.
- "NEW" ribbon on tonight's items until viewed (key `cosmetic_seen`).

### Phase 9b — Boards and frames
Files: `Services/CosmeticCatalog.cs`, `Graphics/GameDrawable.cs`, `Graphics/CellRenderer.cs`, `Graphics/PreviewDrawables.cs`.
- `BoardMotif` += `Stars, Nebula, Waves, Strata, Embers, Petals, Patchwork`. Static layer cached; motion ≤12 particles at ≤15 fps, stopped when paused, idle 10 s, or Reduced motion.

| Id | Name | Price | Top | Bottom | Rim | Grid | Grid α | Motif α |
|---|---|---|---|---|---|---|---|---|
| board_nightsky | Night Sky | 300 | #0E1433 | #03050F | #9DB4FF | #24305E | 0.30 | Stars 0.14 |
| board_nebula | Nebula Drift | 400 | #2A1446 | #070B1F | #E08CFF | #3D2A66 | 0.30 | Nebula 0.16 |
| board_tidepool | Tide Pool | 300 | #0D4257 | #041A26 | #62E3F0 | #1F6178 | 0.32 | Waves 0.12 |
| board_canyon | Canyon Strata | 300 | #4A2A1C | #1E100A | #E8A26A | #6E4330 | 0.36 | Strata 0.12 |
| board_forge | Ember Forge | 400 | #3A1410 | #120504 | #FF8A3D | #5E2418 | 0.34 | Embers 0.16 |
| board_blossom | Blossom Grove | 350 | #3B2236 | #160C16 | #FFB3CF | #5E3955 | 0.32 | Petals 0.12 |
| board_quilt | Patchwork Quilt | 500 | #242036 | #100E1C | #F5D07A | #3E3858 | 0.34 | Patchwork 0.10 |

- `FrameStyle` += 6 sold frames (outline the moving piece, 2–3 dp; animate only while a piece falls; static under Reduced motion):
  - frame_starlight Starlight 220: `#BFD3FF` line, 4 sparkles orbiting slowly.
  - frame_ripple Ripple 220: `#7EE7F5` line wobbling ±1 dp.
  - frame_stone Stone Edge 200: 3 dp bevel `#B8B2A8`/`#6B655C`.
  - frame_flame Flame Edge 260: `#FF6A2B`→`#FFD23F`, flickering tips on top edge.
  - frame_vine Vine 240: `#5FBF4A` line, a small leaf on 3 corners.
  - frame_neon Neon Pulse 300: `#FF4FD8`, pre-blurred cached 4 dp glow, alpha 0.6↔1.0 @0.8 Hz.

### Phase 9c — Pip looks
Files: `Services/CosmeticCatalog.cs` (`AccessoryStyle` += `Daisy, NerdGlasses, Sunglasses, Sporty, Shy, Party`), `Graphics/StoryDrawables.cs`, `Graphics/WardrobeDrawables.cs`, `Graphics/PreviewDrawables.cs`. Layer at the existing accessory anchors. Add `PipMood` override (Normal/Shy/Party); scene-requested expressions win during story scenes.
- pip_daisy Daisy 150: white 8-petal daisy, centre `#FFC93C`, right of head, 15° tilt.
- pip_nerd Nerdy Glasses 180: round `#1B1B1F` 2.5 dp frames, white tape on bridge, faint lens glint.
- pip_shades Sunglasses 200: wide `#14161C` lenses with diagonal shine, thin `#E6B84A` arms.
- pip_sporty Sporty Pip 220: `#E8434F` sweatband with white stripe, whistle on cord, motion swoosh by cheek.
- pip_shy Shy Pip 220: mood Shy — eyes down/side, small wavy mouth, `#FF9EB5` blush ovals α0.7, hand-blob at cheek, peeks up every ~6 s (static under Reduced motion).
- pip_party Party Pip 260: striped cone hat `#FF5C7A`/`#FFD54A`, pom-pom `#7CF5C8`, party horn, 6 drifting confetti specks on menu/map only.

### Phase 10 — Story Book
Files: `Views/ArchivePage.xaml(.cs)` (class name unchanged).
- Title and all player-facing "Archive" → "Story Book" (incl. `keeper_of_memories` text).
- Workshop-style tab bar: **STORY** (scenes), **MEMORIES** (by region + ending look picker), **PEOPLE** (Pip, Mara, Bram, Warden), **WORLD** (lore, fragments). Map existing sections onto these. Empty tab shows "Nothing here yet. Keep playing to fill this page." Remember tab (key `storybook_tab`).

### Phase 11 — Tall scrolling board
Files: `GameConfig.cs`, `Models/Board.cs`, every user of `VisibleRows/TotalRows/HiddenRows` (~36 references across engine, app, tests), `Logic/GameEngine.cs`, `Logic/CollisionDetector.cs`, `Logic/GravityResolver.cs`, `Logic/BoardGenerator.cs`, `Levels/LevelSolver.cs`, `Levels/LevelRoller.cs`, `Story/RegionRules.cs`, `Models/EngineSnapshot.cs`, `Graphics/GameDrawable.cs`, `Graphics/BoardSpace.cs`, `Views/GamePage.*`, `Models/GameRules.cs`.
- Board height becomes per-board: `Board.Rows` (instance). `GameConfig`: `ViewportRows = 16`, `DefaultBoardRows = 24`, `MinBoardRows = 16`, `MaxBoardRows = 32`, `HiddenRows = 2` (spawn buffer above the viewport). Lessons use 16 (no scrolling). Story/Endless use `GameRules.BoardRows` (default 24; custom 16/20/24/28/32).
- Camera (engine-owned, deterministic, mirrored in solver): `stackTop` = height of the highest block. `CameraBottom = clamp(stackTop − ViewportRows/2, 0, Rows − ViewportRows)`. The view shows rows `[CameraBottom, CameraBottom + ViewportRows)`, so a tall stack shows only its upper half with free space above; as the stack is cleared the camera moves down until it reaches row 0. Recomputed only when the board settles, before the next spawn — never while a piece falls.
- Spawn at the top of the viewport (`CameraBottom + ViewportRows`, using the hidden buffer), not the board top. Overflow = a locked cell at or above `Rows`, or a spawn that collides.
- Goals referring to rows (ClearDown, hanging cues, piles) use absolute board rows; piles may fill up to `Rows − ViewportRows/2`. `RegionRules` may give later regions deeper piles; keep existing pile tuning unless the bot gate fails.
- Drawing: cell size from `ViewportRows`; render only visible rows +1; camera moves animate 300 ms ease-in-out (instant under Reduced motion). When rows are hidden below: bottom-edge fade and label `▼ {n} rows below`. Right-edge depth gauge (4 dp): full board height, bracket for the viewport, filled to `stackTop`.
- Solver, `PlacementPreview`, hanging cues, undo snapshots handle variable rows and the camera. Regenerate snapshots. Tests: camera math, spawn row, overflow at board top, no scroll on 16-row boards, solver/engine agreement on tall boards.

### Phase 12 — Pip's Insight (best placement)
Files: `Services/ShopCatalog.cs`, `Services/SaveKeys.cs`, new `Logic/PlacementAdvisor.cs`, `Logic/PlacementPreview.cs`, `Levels/LevelSolver.cs`, `ViewModels/GameViewModel.cs`, `Graphics/GameDrawable.cs`.
- Shop item `insight` (key `upgrade_insight`), Gameplay, no requirement: L1 250 (button below board, 3 uses/level), L2 +450 (6 uses), L3 +800 (unlimited + "Always on" toggle, key `insight_always_on`, default off). Off in lessons and finales. An Endless run using it never sets best score (as seeded runs).
- `PlacementAdvisor.Best(snapshot, piece, rules)`: all rotations × columns, real collision, score with solver evaluation (goal progress, then cells popped/chain depth, orb breaks, then lower stack and fewer holes). One-piece lookahead, ≤4 ms, on a background task, cached by (piece id, board version).
- Draw: dashed 2 dp outline in target spot (piece colours α0.7) pulsing, arrow above column, rotate glyph with turn count; hides when reached or after 4 s. Reduced motion: no pulse.
- Tests: obvious win picked, no colliding suggestion, deterministic, respects rules.

### Phase 13 — Fracture
Files: `Models/BlockCell.cs` (`Fracture` 0–2), `Models/Board.cs`, `Logic/ColourMatcher.cs`, `Logic/GameEngine.cs`, `Logic/GravityResolver.cs`, `Logic/PlacementPreview.cs`, `Logic/ScoringSystem.cs`, `Levels/LevelSolver.cs`, `Story/RegionRules.cs`, `Graphics/CellRenderer.cs`, `Graphics/BlastEffects.cs`, `Models/GameRules.cs`.
- Active per D8 and `GameRules.Fracture`.
- After each wave's groups are removed: each surviving block whose orthogonal neighbours popped in that wave on ≥2 distinct sides gets `Fracture += 1` (max once per wave; diagonals ignored). A block at 2 that fractures again breaks: removed in the next step of the same resolution, counts for its colour's pop-N goals (not burst-group goals), feeds gravity and chains.
- Fracture travels with falling blocks, survives undo/clone/copy. Dormant blocks can fracture.
- Gold factor: ×1.0 intact, ×0.75 one crack, ×0.5 two cracks, ×0.5 when broken by fracture.
- Solver mirrors exactly; regenerate snapshots; list changed levels in the commit.
- Draw (cached per skin×colour×level): crack 1 = 3-segment `#000000` α0.45 line from a corner + 1 px light edge; crack 2 = second crack from the opposite side meeting it, face −10%. New crack: 120 ms white tick + 2 dust specks. Break: block splits into 3 grey-tinted chunks, 400 ms.
- Intro card at level 36: "Cracked blocks! When a pop hits a block from two sides, it cracks. A third crack breaks it. Cracked blocks pay a little less gold."
- Tests: two-side rule, one side/diagonal no-op, break on third, chains after break, gold factors, undo, falling, solver/engine agreement on fixed-seed random boards.

### Phase 14 — The Orb
Files: new `Models/Orb.cs`, new `Logic/OrbSystem.cs`, `Logic/GameEngine.cs`, `Logic/GravityResolver.cs`, `Models/Board.cs`, `Models/EngineSnapshot.cs`, `Logic/ScoringSystem.cs`, `Levels/LevelSolver.cs`, `Levels/LevelRoller.cs`, `Story/RegionRules.cs`, `Models/GameRules.cs`, new `Graphics/OrbDrawable.cs`, `Graphics/GameDrawable.cs`, `Graphics/BlastEffects.cs`, `Audio/SfxLibrary.cs`, `Audio/Cues.cs`.
- 2×2 unit in the board; not a colour; never joins groups. Active per D8 and `GameRules.Orbs`. Fully deterministic from its own seeded stream; **solver mirrors spawn, timer, break and corruption**, so the bot gate accounts for orbs.
- Spawn check, once per settle before the next spawn:
  1. No orb on the board; ≥4 pieces locked since the last orb left (or since level start).
  2. Candidate windows: 4×4 windows inside the visible viewport with ≥10 blocks, ≥4 colours, ≥6 blocks with no same-colour orthogonal neighbour. Running-sum scan, reusable arrays.
  3. Roll 35% (Always → 100%). **Guarantee (every Story level ≥40, and Endless):** if no orb has appeared by the 8th piece, the next settle spawns one at the best available window even if the window thresholds are not met (fallback: the 2×2 with the most distinct colours touching it).
  4. Place the 2×2 at the centre of the best window (most lonely blocks; ties lower, then left). Blocks in the footprint are pushed up their columns in order. Reject any spot where a pushed block would reach `Rows` (Phase 11 board top); try the next; none → no spawn.
- Timer = `GameRules.OrbTimer` (8), −1 per locked piece.
- Break: any orthogonally adjacent block popping (group, fracture break, another effect) breaks it in the same resolution → pops every block in the orb's 2 rows and 2 columns across the whole board (cross 2 cells thick). Those count for pop-N goals and pay pop gold (fracture factors apply) plus **orb bonus 25 gold** (×GoldFactor), floating `+25 ORB!`. Gravity and chains follow. No new orb in that resolution.
- Corruption at 0: the 12 cells ringing the orb each get +1 fracture (breaks at 3); orb removed; gravity runs; no gold. If fracture is disabled by rules: those blocks only darken for one move.
- Orb falls as a rigid 2×2 when both columns below are empty; blocks rest on it; pieces collide with it; undo includes it; Prism Shift ignores it; Insight values breaking it.
- Draw: sphere filling 2×2 minus 3 dp, radial `#F4F7FF`→`#8C7CFF`→`#2B1E6E`, inner conic swirl of the five block colours α0.35 rotating once per 6 s (≤30 fps, only while playing; cached still otherwise), white specular ellipse. Timer ring 4 dp emptying, white → `#FFB020` at 3 → `#FF4D4D` at 1, centred number 20 sp bold outlined. ≤2: wobble + dark veins. Spawn: blocks slide up 180 ms, orb scales 0.6→1 + fade 300 ms, chime. Break: white flash, beams sweep rows/columns 250 ms popping centre-out. Corrupt: cracks, turns `#3A0F4A`, implodes, violet shock ring, new cracks appear. Reduced motion: no swirl/wobble; break = single flash.
- Intro card at level 40 (and first Endless orb, key `seen_orb_intro`): "An Orb! Pop a block touching it to break it and clear its rows and columns for a big gold bonus. If its timer runs out, it cracks the blocks around it."
- Regenerate snapshots; verify every rolled level ≥40 in the snapshot set contains an orb in the bot's winning plan. Tests: conditions, cooldown, max one, guarantee by piece 8, push never overflows, determinism, break cross + gold, corruption, falling unit, undo, solver/engine agreement, disabled → none.

### Phase 15 — Achievements
Files: `Services/AchievementCatalog.cs`, `Services/AchievementService.cs`, `Services/RunStats.cs`, `Services/LifetimeStats.cs`, `Views/AchievementsPanel.*`. Eased clears never satisfy "win". New counters: `stat_orbs_broken`, `stat_fracture_breaks`, `stat_shares`.

| Id | Name | Condition | Gold |
|---|---|---|---|
| orb_breaker | Orb Breaker | Break your first Orb | 30 |
| orb_collector | Crystal Collector | Break 25 Orbs | 120 |
| close_call | Close Call | Break an Orb with 1 move on its timer | 60 |
| hairline | Hairline | Crack a block | 20 |
| shatterer | Shatterer | Break 100 blocks by fracture | 100 |
| fault_lines | Fault Lines | Break 5 blocks by fracture with one piece | 80 |
| nine_lives | Not Done Yet | Win a level after buying a continue | 40 |
| full_hearts | Full of Heart | Win 10 Story levels without a continue | 50 |
| roll_with_it | Roll With It | Reroll 10 levels | 40 |
| stubborn | Stubborn | Win a level lost 10+ times, without easing | 80 |
| gold_rush | Gold Rush | Earn 10 pop gold in one Story level | 60 |
| deep_dive | Deep Dive | Clear a board back down to its bottom row after it scrolled | 60 |
| endless_night | Endless Night | Last 200 pieces in one Endless run | 100 |
| rule_maker | Rule Maker | Play an Endless run with your own rules | 20 |
| dressed_up | Dressed Up | Equip a bought skin, board, frame and Pip look together | 50 |
| spread_the_word | Spread the Word | Open the share sheet | 20 |

Update economy totals and `P7Tests`.

### Phase 16 — Performance
Record findings and before/after numbers in `docs/overnight/PERFORMANCE.md`; fix anything over budget.
- Budgets: draw ≤8 ms while animating; zero per-frame allocations in `GameDrawable.Draw` and all effects; settle (fracture + orb + camera) ≤2 ms; `PlacementAdvisor` ≤4 ms off the UI thread; idle board and menus run no repaint timer.
- Add an engine benchmark test (skipped by default): 10,000 random moves with default rules, then 24-row board + fracture + orbs. Hot paths: no LINQ, no new collections; reuse arrays; window scan by running sums.
- Audit every new per-frame path (floating text, bursts, orb, frames, motifs, hearts, camera tween, depth gauge): pooling, cached paints/paths, pre-formatted strings, glow from cached pre-blurred paths, scrims drawn once per frame.
- Caches: skin×colour×fracture cells, motif static layers, menu scene, Patchwork table; invalidate only on change.
- Timer discipline: animation timer stops when the last effect ends, on pause, background and menus; ambient motion stops after 10 s idle. Add a Debug-only idle frames-per-minute counter in `Diagnostics.cs`.
- Rendering only visible rows on tall boards.
- Audio: coin ticks capped, existing voice pool, no new threads. Settings sliders persist on release via the debounced store. Catalogues are static arrays; nothing new on the start-up path.
- Re-run all tests; run `scripts/Check-Permissions.ps1` on a Release build if the Android SDK is present.

### Phase 17 — Wrap-up
- `PopBlocks.csproj`: `ApplicationDisplayVersion` 1.1.0, `ApplicationVersion` +1.
- Update `README.md`, `docs/09-extending.md` (adding skins/boards/frames/Pip looks, adding a `GameRules` field, board camera, fracture and orb rules the solver must mirror), `docs/store-listing/full-description.txt` (Endless, tall boards, Orbs, cracks, Story Book, new looks).
- Debug-only Dev Menu tools: set current level losses, spawn orb now, crack all blocks, fill board to N rows, grant all new cosmetics.
- Finalise `PROGRESS.md` (every phase, hash, unfinished items) and `DECISIONS.md`.

## Appendix — new save keys (add-only, in `SaveKeys.All` and `SaveCode`; match existing naming style and record final names in DECISIONS.md)
`stat_rerolls stat_continues stat_orbs_broken stat_fracture_breaks stat_shares level_losses eased_levels consolation_paid rule_fall_speed rule_colour_count rule_colours rule_shapes rule_min_group rule_solid_chance rule_board_rows rule_fracture rule_orbs rule_orb_timer rule_use_in_story settings_tab storybook_tab upgrade_insight insight_always_on cosmetic_mix_cyan cosmetic_mix_coral cosmetic_mix_yellow cosmetic_mix_purple cosmetic_mix_lime cosmetic_seen seen_orb_intro`
