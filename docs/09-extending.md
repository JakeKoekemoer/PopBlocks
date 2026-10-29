# 9. Extending the game: checklists

Each section lists, in order, every place that has to change for a common kind of addition, and which test will complain if you miss one. Paths are relative to `src/` unless they start with `tests/`.

General rule: **make the rule change in the engine first, with a test, then wire the app to it.** The engine is where mistakes are cheap to find.

After any change in `PopBlocks.Engine`, run `dotnet test tests/PopBlocks.Tests`. After any change in `PopBlocks/`, run the app and play (see [Testing](07-testing.md#74-what-is-not-tested-automatically)).

---

## 9.1 Add a piece shape

1. **`PopBlocks.Engine/Models/ShapeCatalog.cs`**: add a `ShapeDef` to `Classic` or `Exotic`.
   - `Name`: unique; it is used by tutorial scripts (`"Plus:RRRRR"`) and `ShapeCatalog.Find`.
   - `Weight`: how often it is drawn relative to the others in its set. Weight 0 means "never drawn at random" (the `Dot` shape uses this).
   - `Offsets`: cell offsets in spawn orientation. Row 0 is the top row of the piece.
   - `Pivot2X`/`Pivot2Y`: the rotation centre **doubled**. A pivot on a cell is even; a pivot in the middle of four cells is odd (the 2x2 `Block` uses 1,1). Pick the pivot so that four rotations return the piece exactly where it started and no rotation moves it by half a cell.
2. **Classic or Exotic?** `Classic` is used everywhere. `Exotic` appears in Story from level 11 on, in the share given by `ExoticShareFor` in `LevelCatalog.cs`. Classic mode never uses Exotic shapes. Adding to `Classic` changes Classic mode and every Story level's piece sequence; adding to `Exotic` only changes Story levels that use exotic pieces. Either way, **every generated level's piece sequence changes**, because the random draw lands on different shapes. That is safe (the solver re-proves every level), but any saved expectations about a particular level will be different.
3. **Width.** The board is 8 wide and a piece spawns centred. A shape wider than about 5 cells will have trouble spawning; `ShapeTests` checks that every shape fits at spawn.
4. **Nothing else.** The renderer draws any cell list, the solver reads pieces from the generator and tries every rotation, and the preview draws whatever pieces it is given.
5. **Tests that cover you:** `ShapeTests` (fits at spawn, four rotations return to start, no cell lost or merged), the catalogue replay test (every level 1 to 220 still winnable), and `StoryFuzzTests`.

---

## 9.2 Add a goal type

A goal is `GoalKind` plus a target. Everything that understands goals:

1. **`PopBlocks.Engine/Levels/Goal.cs`**
   - Add the value to `GoalKind`.
   - `Goal.Describe()`: the player-facing sentence.
   - `GoalTracker`: record whatever the goal needs while waves are applied (see how `BestChain` or `_poppedByColour` is tracked) and add the case to the `current` switch in the evaluation. Decide whether "done" means `current >= Target` (default) or the opposite (as `ClearDown` does).
   - `GoalState.Fraction` and `ProgressText`: only if the goal does not read as "n / target". (`ChainSet` shows "1 / 3 chains".)
   - A goal that needs more than a target can add an optional field to `Goal`: `ChainSet` ("make N separate chains of at least M waves") uses `MinWaves`, and counts a chain as closed when the next wave with `ChainIndex == 1` arrives.
2. **`PopBlocks.Engine/Levels/LevelSolver.cs`**
   - `Value`: add the goal to the progress switch. If you skip this, the bot has nothing guiding it toward the goal and will fail to win levels that use it, so the generator will keep easing the goal.
   - If the goal needs new state, add it to the solver's lightweight state and update it where the solver applies a wave. Keep it identical to the engine's counting; the replay test checks that.
3. **`PopBlocks.Engine/Levels/LevelCatalog.cs`**
   - `PickKinds`: make it available from a given level (the "introduced at" table: a goal appears only once introduced, and the introduction level always uses it).
   - `MakeGoal`: how to size the target from the tier's demand.
   - `Story/RegionRules.cs`: the introduction level constant and its coaching card (`IntroCards`). A goal that should only appear on a special level (like `ChainSet` on level 200) is set directly in `Generated` and is not in the random pool.
4. **App:** nothing for display. The goal bar is bound to `GoalState` and the text comes from `Describe()` and `ProgressText`.
5. **Tests:** add the new kind to the goal rule tests in `StoryRuleTests`; the catalogue replay will exercise it in generated levels.

---

## 9.3 Add or move a difficulty tier

Tier boundaries live in **`PopBlocks.Engine/StoryConfig.cs`** (`EasyLastLevel`, `MediumLastLevel`, `HardLastLevel`; Extreme is open-ended). Everything that depends on a level number reads these.

To **move a boundary**, change the constant. Then check:

- `GoldRewards` (in `Services/PlayerProgress.cs`) is by tier and offset into the tier, so payouts shift with the boundary.
- `LevelCatalogTests` has tests that pin the tier boundaries; update them deliberately.
- Several one-off level numbers are not tied to a tier: flipped boards and pop penalties, layout variants, goal introduction levels, and the "second goal from 32" and "third goal from 161" rules. They live in **`PopBlocks.Engine/Story/RegionRules.cs`** (one rule per region plus named constants). Regions are 25 levels each, so a region boundary is also a tier boundary for the shipped campaign (two regions per tier).

To **add a tier**, add a `Tier` value (in `Models/GameSetup.cs`), extend every `switch` on `Tier` (search for `Tier.`; the project does not treat warnings as errors, so the compiler will not list them for you), add rows to the tuning tables (`ProfileFor`, `FallFor`, `DemandFor`, `SlackFor`, `ExoticShareFor`, `GoldRewards`), and give `LevelSelectPage` a section heading. Remember the About page and README describe the tiers.

To **tune difficulty** without changing structure, edit only the tables at the bottom of `LevelCatalog.cs` and the region rules in `RegionRules.cs`. Then run the tests: the replay test proves every level is still winnable, and `LevelSnapshotTests` shows exactly which levels changed (see 9.11).

---

## 9.4 Add or change an authored level or tutorial step

Hand-made levels are methods in `LevelCatalog.cs`: the tutorial lessons (`Tutorial1` to `Tutorial10`, served by `LevelCatalog.Lesson`) and Story level 11 (`NewShapesLevel`). Copy the closest one.

- **Pile:** `StartRows` is rows of letters from top to bottom: `C` cyan, `R` coral, `Y` yellow, `P` purple, `L` lime, `.` empty. The bottom row of the text is the floor. Keep the top of the visible board clear.
- **Script:** `"Pair:RC"` means a Pair with coral and cyan cells. The script plays before random pieces.
- **Coaching:** a list of `TutorialStep`s. Use `Tap` for a message, `Do` for something the player must do (give it the `GameEvent` that completes it; add `HintColumn` to point at a column), `Watch` for something that happens on its own.
- **If the level must be solver-limited** (like level 10), generate the pile with `BoardSeed` and a profile and let the generator set the limit.

**Test:** `TutorialLevelTests` plays lessons 1 to 9 through the real engine following their own steps. The lessons are frozen by `LessonSnapshotData`; change that file by hand only when you change a lesson on purpose. Add your level to it if it is guided. A tutorial whose steps cannot be completed in the order given fails there.

To **add a new `GameEvent`** (so a step can wait for it): add it to the enum in `Models/GameState.cs`, raise it from `GameEngine` at the moment it happens, and forward it in `GameViewModel` (it already forwards every engine event to the tutorial controller).

---

## 9.5 Add a shop upgrade

Upgrades are data. Everything about one is a `ShopItem` in **`Services/ShopCatalog.cs`**.

1. Add an id constant and a `ShopItem` to `ShopCatalog.Items`: title, description, `ItemCategory`, base effect, and one `ShopTier` per purchasable level (price, effect text, and a `Requirement`: `None`, `RegionRestored(n)` or `Achievement(id)`). Prices must rise with each tier.
2. Add a save key constant to `SaveKeys` (**a new key; never reuse one**), put it in `SaveKeys.All`, and use it as the item's `SaveKey`. Then add it to the frozen list in `P7Tests.FrozenKeys` and decide whether the save code carries it (`SaveCode` picks up every catalogue item automatically).
3. Add a property to `ShopService` that turns the level into the number the game uses (like `LensLevel` or `PrismFreeCharges`).
4. **Make the game read it** at the start of a game: for an engine effect, set it in `GameViewModel.NewGame()` next to `PreviewCount` and `ExtraMoves`; for a visual effect, set it in `GamePage.OnGameStarted`. `ShopPanel` (the workshop's UPGRADES tab) and `DevTools.MaxShop` list whatever the catalogue contains.
5. **Do not make levels depend on it.** Move limits are tuned for a player with nothing.
6. **Tests:** price, effect and persistence in `P4ShopTests`; `P7Tests` checks the new tiers are affordable from the campaign's own income. Update the economy table in `docs/story/economy.md`.

---

## 9.6 Add a persisted setting or value

1. Add the key to **`Services/SaveKeys.cs`** (and to `SaveKeys.All`), then a property to the right service (`GameSettings` for options, `PlayerProgress` for progress, `HighScoreService` for scores) in `PopBlocks.Engine/Services/`. Read the value in the constructor with a default, write it in the setter.
2. Keep keys lowercase with underscores, and **never rename or reuse an existing key**: an existing install would silently lose or misread the value.
3. A UI switch or button goes in the page and binds to the service; the service owns the saving.
4. Add the key to the table in [Architecture](02-architecture.md#26-persistence), to `P7Tests.FrozenKeys`, and decide whether the save code should carry it (`SaveCode.Keys`; `SaveCodeTests` fails until you have decided). Add a round-trip test with `InMemorySettings` (construct a second service on the same settings object and read the value back).

---

## 9.7 Add a colour

This touches a lot because five colours are baked into several tables, and it is the one change that is **not** cheap.

- `Models/CellColour.cs` and `Board.FromChar` / `Board.ToChar` (the letter used in level text and tests).
- Colour selection by count in `PieceGenerator` and `BoardGenerator` (`Colours` on the level selects the first N).
- `GameConfig.ColourFamilies` and `StartingColours`, and `ScoringSystem.ColourCountForLevel` (Classic's colours-by-level curve).
- Graphics: palette and neon variant in `VisualDesign.cs`, a symbol in `CellRenderer.DrawSymbol`, and the XAML palette if it is used in UI.
- `Goal.ColourName`.
- `PopColour` goals and the solver's per-colour counters (arrays sized by the colour count).
- Tests that assume five colours.

More colours also makes matches rarer, so retune the tiers (`ProfileFor`, `SlackFor`) and re-run the replay test.

---

## 9.8 Change the board size or hidden rows

`GameConfig.cs` holds `Columns`, `VisibleRows` and `HiddenRows`. Most code reads these, but two places have the geometry baked in and must be checked by hand:

- **The solver** (`LevelSolver.cs`) sizes its flat arrays from `GameConfig`, so it follows the constants, but its heuristic has tuned numbers (for example the "stack taller than 9 rows" penalty) that assume the current height.
- **Hand-drawn levels and tests** that contain rows of letters assume 8 columns.

The layout and the touch thresholds are in cell sizes, so they adapt. Re-run all tests and look at every tutorial in the app.

---

## 9.9 Change a rule (scoring, matching, gravity, spawn)

1. Change it in the engine.
2. **Mirror it in `LevelSolver`'s lightweight rules.** The solver calls `ScoringSystem` for the numbers, so a pure scoring change needs no solver edit, but a change to matching, gravity, locking or spawn position does.
3. Run the tests. If the catalogue replay fails on many levels, the solver and engine disagree; fix the solver (or the rule) until they agree.
4. If the rule changes what players see or are told, update the how-to text on `MainPage`, tutorial card text, the About page, [Game design](01-game-design.md) and the README.

---

## 9.10 Change how a region plays

Each region has a `RegionRule` in **`Story/RegionRules.cs`**: goal-kind weights, the chance of two or three goals, how tight the move limit is, taller or more scattered piles, a repeating layout variant, and the card shown on its first level. `LevelCatalog.Generated` reads the rule for the level's region, so changing a rule never means touching the generator.

1. Edit the rule. Levels 1 to 11 never change (a test freezes them).
2. Run the tests. `LevelSnapshotTests.Levels_match_the_approved_snapshot` fails and names the first level that changed. A change here is deliberate, so review it, then regenerate the snapshot: run the tests once with `POPBLOCKS_WRITE_SNAPSHOT` set to the full path of `tests/PopBlocks.Tests/LevelSnapshotData.cs`, read the diff in git, and commit it.
3. `RegionRulesTests` checks that a region's favoured goal kind is clearly more common there than elsewhere, that finales are clean, and that nothing appears before its introduction.
4. Flipped boards and pop penalties are listed by level number in `RegionRules` (`FlipLevelsInRegion6`, `PenaltyLevelsInRegion6`) and follow a pattern from the Citadel on. Each one needs a card in `IntroCards`.

**Finales** are the last level of a region (`RegionRules.IsFinale`). They ask for a little more, leave a little less spare, never flip or penalise, and carry `AllowAbilities = false` so shop helpers and consumables stay off on them. Level 200 is built specially from the chain-set goal.

---

## 9.11 Add or change an optional memory objective

A few levels (`BonusPlanner.Offsets` into each region) carry an optional objective that uncovers one of the region's memories. It never blocks progress.

- `BonusPlanner.Choose` tries the objective kinds in turn and keeps the first that is **possible**: the bot finds a plan with the objective included, then the real `GameEngine` plays that plan and the objective must actually be earned. The check has to use the real engine because a level ends the moment its main goals are met; an objective that could only be met afterwards is impossible.
- To add a kind, add it to `BonusKind`, give it a description, an `IsEarned` rule (from `RunStats`), an `Evaluate` for the on-screen row, a `ProgressFraction` for the solver, and a candidate in `BonusPlanner`.
- A memory is granted by `CampaignService.OnBonusEarned`, on a first clear or a replay, and pays no gold.
- Tests: `BonusObjectiveTests` replays every shipped objective through the real engine.

---

## 9.12 Documentation checklist

When you change behaviour, update the doc that owns it:

| You changed | Update |
|---|---|
| A rule, score, goal, tier or control | [Game design](01-game-design.md), the matching engine or story doc |
| Project structure, DI, persistence keys | [Architecture](02-architecture.md) |
| A public engine class | [Engine reference](03-engine-reference.md) |
| Level generation, seeding, the difficulty band, rerolls, Classic gold, solver, shop numbers | [Story mode](04-story-mode.md) (4.8 for seeds, rerolls and Classic gold), and `docs/story/economy.md` for any price |
| Pages, view model, input | [App layer](05-app-layer.md) |
| Drawing, effects, palette | [Graphics](06-graphics.md) |
| Tests or test helpers | [Testing](07-testing.md) |
| Build steps, versions, tooling | [Build and release](08-build-and-release.md) |
| Regions, story text, scenes, rewards, the ending, looks | [Campaign and content](10-campaign-and-content.md), and `docs/story/economy.md` for any price |

Write what the thing *does* and *why it is where it is*, not the story of how it got there.

---

## 9.13 Add a cosmetic (skin, burst, board, frame, Pip accessory)

1. Add a `Cosmetic` to `CosmeticCatalog` (id, name, slot, description, how it is discovered, price; price 0 makes it a reward that is owned the moment its requirement is met). Skins and boards carry a `SkinSpec` or `BoardSpec`; bursts, frames and accessories name an existing style enum value. A new *style* (a new way to draw) is a case in the matching drawing code (`CellRenderer`, `BlastEffects.DrawSpark`, `GameDrawable.DrawFrame`, `PipDrawable.DrawAccessory`).
2. A look is **discovered** through the story (`RegionRestored`) or an achievement, then bought. Never gate the story or accessibility behind gold, and never show a price on a locked look.
3. `CosmeticTests` then checks it for you: colours defined as hex, hue near classic, the five colours far enough apart, symbols readable, and every skin legible on every board. Fix the data, not the test.
4. Ownership is the unlock id `cosmetic_<id>`; equipping uses the existing `cos_*` keys, so no new key is needed.
5. If the look draws something new, run it on a device at Pop Flair 3 during a long chain (see `docs/story/p5-playtest.md`).

## 9.14 Add or change a helper

Helpers are the only used-up items. Read `docs/story/helpers.md` first.

1. The rule belongs in `GameEngine` (like `UseSecondChance` and `ShiftNextPiece`), gated by `HelpersAllowed` and a per-level charge copied in at game start. It must refuse in `Paused`, `ResolvingBursts` and after the level ends, raise only its own `GameEvent`, and leave the generator's sequence alone.
2. Anything it changes that `RunStats` watches must roll back with an undo (`RunStats.Checkpoint`).
3. Stock, price and requirement are a row in `ConsumableCatalog`; the stock key is a new `SaveKeys` entry (frozen list, save code).
4. Never make a level need it. `LevelSolver` plays without helpers and the fairness tests keep it that way. If the helper changes what the solver models, change `LevelSolver` too.
5. UI: a button in `GamePage.xaml` bound to a `GameViewModel` command, shown only when allowed and held, disabled with a reason, with a spoken description.

---

## 9.15 Retune the difficulty band

Every level number has a band of acceptable *hardness* (`min(2, weakNeed / fullNeed)`, see [Story mode](04-story-mode.md#48-seeded-levels-rerolls-and-classic-gold)) in `Levels/DifficultyBand.cs`. That file is **generated**; never edit it by hand. Retuning means measuring again:

1. **Run the sweep** (about two minutes on a desktop):

   ```
   set POPBLOCKS_SWEEP_CSV=E:\PopBlocks\docs\story\seed-sweep.csv
   dotnet test tests/PopBlocks.Tests --filter "FullyQualifiedName~Run_the_sweep_when_asked"
   ```

   It builds `POPBLOCKS_SWEEP_SEEDS` (default 50) candidates for every level 1 to 220 and writes `level, seed, verified, memoryOk, fullNeed, weakNeed, hardness, startBlocks, buildMs, weakMs`. `verified` means the bot wins *and* a person could play its plan in the real engine.
2. **Write the band** from the CSV:

   ```
   set POPBLOCKS_BAND_FROM_CSV=E:\PopBlocks\docs\story\seed-sweep.csv
   set POPBLOCKS_BAND_TO=E:\PopBlocks\src\PopBlocks.Engine\Levels\DifficultyBand.cs
   dotnet test tests/PopBlocks.Tests --filter "FullyQualifiedName~Write_the_band_table_when_asked"
   ```

   The percentiles (20th to 80th for the band, 10th to 90th for the guard rails), the smoothing window (5 levels), the soft-start length (5 at a tier start, 2 after each introduction level) and the introduction levels (read from the `RegionRules` constants) are constants at the top of `SeedSweep.cs`.
3. **Run the whole suite and the fuzz** (7.6). Then commit the CSV and the generated file together.
4. **Playtest the edges**: five seeds at each band edge on three levels per tier, against the notes in `docs/story/p3-playtest.md` to `p6-playtest.md`. Hardness is a proxy for how hard a person finds a board. If an edge feels wrong, change the percentiles and repeat from step 2.

Other numbers live in `SeedingConfig.cs`: the number of attempts (24) and time budget (2 s) of a roll, the loading card thresholds, the free-reroll failure count (10), the Classic gold rate, the name length. Check roll time on a low-end phone with the dev menu's **TIME THE GATE ON THIS PHONE**; if the 95th percentile is above about a second, lower the attempt count or drop the weak-bot run for levels where it is not needed.

A band change affects only levels rolled *after* it. A level already assigned keeps its stored seed, so retuning cannot change a level a player is on. The flip side: a level found to be broken after release cannot be fixed by retuning alone.

If you change a rule the solver mirrors (9.9), also rerun the sweep: `weakNeed` and `fullNeed` come from the solver.

---

## 9.16 Add a `GameRules` field

`GameRules` (`Models/GameRules.cs`) is the player's own rule set. Every field has a default that equals the normal game, so a save with no rule keys plays exactly as before.

1. Add the field to the record with its default, and clamp it in `GameRules.Normalized()` (the engine never trusts a stored value).
2. Add a `rule_*` key to `SaveKeys` (and to `SaveKeys.All` and `SaveCode.Keys`; both lists are add-only and the frozen-key test in `P7Tests` gets the new name), and read and write it in `GameRulesStore`.
3. Decide whether it is "easier than normal" (`GameRules.IsEasierThanDefault`, which halves gold through `GoldFactor`) and whether it can apply in the Story (`RuleFeatures`, `GameSetup.Rules`). The Story's promise that a level was solved applies to the normal rules only.
4. If the rule changes how the board plays, **the solver must mirror it** (see 9.18), or the "proven winnable" claim and the snapshot set stop meaning anything.
5. UI: a row in `GameRulesPanel`; a chip in `RuleChipDrawables` if the HUD should show it.

## 9.17 Tall boards and the camera

The board is `BoardRows` rows of play plus the hidden rows above them. When `BoardRows` is more than fits on the screen, the engine keeps a camera (`GameEngine.CameraBottom`, the row at the bottom of the view) that follows the stack; the drawing converts rows with `BoardSpace` (`ViewTop`, `AboveView`, `BelowView`) and draws only the rows on screen plus one either side. Rules: the camera moves only between pieces; it eases over 300 ms and repaints only while it moves; the depth gauge shows how many rows of stack are below the view. A new feature that draws on the board must go through `BoardSpace` and skip rows out of view.

## 9.18 Cracks and Orbs: what the solver must mirror

`LevelSolver` has its own copy of the board rules (`State`, `Settle`), checked against the real engine by the tests that replay every bot plan, so a rule that changes the board must be written twice. A rule that lives only in the app layer (drawing, sounds, floating gold) is free:

- **Fracture.** `Board` keeps a crack level (0, 1, 2; `Board.Broken` is 3) per cell, and it moves with the block in gravity (`GravityResolver`) and is copied by `Board.Clone`; the solver keeps the same array in `State.Fracture`. `GameEngine.ApplyWave` cracks the neighbours of a burst; a block that reaches `Board.Broken` is removed and counts for goals.
- **Orbs.** `OrbSystem` owns where an Orb can appear, how blocks are pushed aside, what it clears and what happens when its timer runs out. The Story spawns Orbs on its own from level 40 (`RuleFeatures.OrbsInStory`), so `LevelSolver` carries them in its `State` (`Placed`, `OrbPos`, `OrbTimer`, `PiecesSinceOrb`, `OrbRolls`, `OrbEver`): the timer and corruption at the lock, the break inside the wave (touched by a burst or a broken block, cross cleared, counted for goals, no points), rigid gravity (`Gravity`, `DropOrb`), and `MaybeSpawnOrb`. The window test and the 35% roll run on the arrays; the placing itself calls `OrbSystem` and `GravityResolver` on a `Board`, so the engine and the bot cannot disagree about where an Orb goes. `SolverOrbMirrorTests` plays plans through the engine and compares the two boards after every move. The solver board is the 24-row default, so the camera that picks the Orb's window is the same.
- After a deliberate change to a rule, regenerate the frozen level snapshots with `POPBLOCKS_WRITE_SNAPSHOT=1` and read the diff; a snapshot that changes without a rule change is a bug.

## 9.19 Development tools

Debug builds (and builds with `DEVMENU`) have a Dev Menu (`DevMenuPage`, backed by `Services/DevTools`) and, on the pause card, board tools (`GameViewModel.ShowDevBoardTools`): spawn an Orb now, crack every block (`GameEngine.DevCrackAll`), fill the board to 12 or 20 rows (`DevFillRows`). The Dev Menu sets the failures and the losses on the current level, plays a level with a typed seed, times the level gate and unlocks every look. `DevMenu.Enabled` is false in Release, the buttons are hidden, and nothing in a Release build can call these methods from the UI. Put new development helpers behind the same switch.
