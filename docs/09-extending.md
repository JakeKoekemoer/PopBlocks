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
   - `GoalState.Fraction` and `ProgressText`: only if the goal does not read as "n / target".
2. **`PopBlocks.Engine/Levels/LevelSolver.cs`**
   - `Value`: add the goal to the progress switch. If you skip this, the bot has nothing guiding it toward the goal and will fail to win levels that use it, so the generator will keep easing the goal.
   - If the goal needs new state, add it to the solver's lightweight state and update it where the solver applies a wave. Keep it identical to the engine's counting; the replay test checks that.
3. **`PopBlocks.Engine/Levels/LevelCatalog.cs`**
   - `PickKinds`: make it available from a given level (the "introduced at" table: a goal appears only once introduced, and the introduction level always uses it).
   - `MakeGoal`: how to size the target from the tier's demand.
   - `Intros`: add a coaching card for the introduction level.
4. **App:** nothing for display. The goal bar is bound to `GoalState` and the text comes from `Describe()` and `ProgressText`.
5. **Tests:** add the new kind to the goal rule tests in `StoryRuleTests`; the catalogue replay will exercise it in generated levels.

---

## 9.3 Add or move a difficulty tier

Tier boundaries live in **`PopBlocks.Engine/StoryConfig.cs`** (`EasyLastLevel`, `MediumLastLevel`, `HardLastLevel`; Extreme is open-ended). Everything that depends on a level number reads these.

To **move a boundary**, change the constant. Then check:

- `GoldRewards` (in `Services/PlayerProgress.cs`) is by tier and offset into the tier, so payouts shift with the boundary.
- `LevelCatalogTests` has tests that pin the tier boundaries; update them deliberately.
- Several one-off level numbers are not tied to a tier: the extreme flags (151/156), variants at 41 to 50, goal introduction levels, and the "second goal from 32" and "third goal from 161" rules. They are written as literals in `LevelCatalog.cs`. Search that file for the numbers when you move a boundary.

To **add a tier**, add a `Tier` value (in `Models/GameSetup.cs`), extend every `switch` on `Tier` (search for `Tier.`; the project does not treat warnings as errors, so the compiler will not list them for you), add rows to the tuning tables (`ProfileFor`, `FallFor`, `DemandFor`, `SlackFor`, `ExoticShareFor`, `GoldRewards`), and give `LevelSelectPage` a section heading. Remember the About page and README describe the tiers.

To **tune difficulty** without changing structure, edit only the tables at the bottom of `LevelCatalog.cs`. Then run the tests: the replay test proves every level is still winnable.

---

## 9.4 Add or change an authored level or tutorial step

Hand-made levels are methods in `LevelCatalog.cs` (`Tutorial1` to `Tutorial10`, `NewShapesLevel`). Copy the closest one.

- **Pile:** `StartRows` is rows of letters from top to bottom: `C` cyan, `R` coral, `Y` yellow, `P` purple, `L` lime, `.` empty. The bottom row of the text is the floor. Keep the top of the visible board clear.
- **Script:** `"Pair:RC"` means a Pair with coral and cyan cells. The script plays before random pieces.
- **Coaching:** a list of `TutorialStep`s. Use `Tap` for a message, `Do` for something the player must do (give it the `GameEvent` that completes it; add `HintColumn` to point at a column), `Watch` for something that happens on its own.
- **If the level must be solver-limited** (like level 10), generate the pile with `BoardSeed` and a profile and let the generator set the limit.

**Test:** `TutorialLevelTests` plays tutorials 1 to 9 through the real engine following their own steps. Add your level to it if it is guided. A tutorial whose steps cannot be completed in the order given fails there.

To **add a new `GameEvent`** (so a step can wait for it): add it to the enum in `Models/GameState.cs`, raise it from `GameEngine` at the moment it happens, and forward it in `GameViewModel` (it already forwards every engine event to the tutorial controller).

---

## 9.5 Add a shop upgrade

1. **`PopBlocks.Engine/Services/Shop.cs`**
   - Add a value to `Upgrade`. **Its numeric value is the row index into `Costs`**, so keep the enum order and the `Costs` rows in step.
   - Add the price row to `Costs` (one entry per level; the number of entries is the maximum level).
   - Add an effects text array (what the player gets at each level; the array has one more entry than there are purchasable levels because level 0 is "nothing yet").
   - Add an `Items()` entry via `Describe(...)`.
   - Add a property that turns the level into the number the game uses (like `ExtraMoves => 2 * Level(...)`).
   - Ownership is saved automatically under `upgrade_<name>`; do not add storage code.
2. **Make the game read it** at the start of a game: for an engine effect, set it in `GameViewModel.NewGame()` next to `PreviewCount` and `ExtraMoves`; for a visual effect, set it in `GamePage.OnGameStarted`. The shop page needs no change; it lists whatever `Items()` returns.
3. **Do not make levels depend on upgrades.** Move limits are tuned for a player with none.
4. **Tests:** add the upgrade to `ProgressAndShopTests` (price, effect, saved across a new service instance on the same settings).

---

## 9.6 Add a persisted setting or value

1. Add a `const string ...Key` and a property to the right service (`GameSettings` for options, `PlayerProgress` for progress, `HighScoreService` for scores) in `PopBlocks.Engine/Services/`. Read the value in the constructor with a default, write it in the setter.
2. Keep keys lowercase with underscores, and **never rename or reuse an existing key**: an existing install would silently lose or misread the value.
3. A UI switch or button goes in the page and binds to the service; the service owns the saving.
4. Add the key to the table in [Architecture](02-architecture.md#26-persistence) and a round-trip test with `InMemorySettings` (construct a second service on the same settings object and read the value back).

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

## 9.10 Documentation checklist

When you change behaviour, update the doc that owns it:

| You changed | Update |
|---|---|
| A rule, score, goal, tier or control | [Game design](01-game-design.md), the matching engine or story doc |
| Project structure, DI, persistence keys | [Architecture](02-architecture.md) |
| A public engine class | [Engine reference](03-engine-reference.md) |
| Level generation, solver, shop numbers | [Story mode](04-story-mode.md) |
| Pages, view model, input | [App layer](05-app-layer.md) |
| Drawing, effects, palette | [Graphics](06-graphics.md) |
| Tests or test helpers | [Testing](07-testing.md) |
| Build steps, versions, tooling | [Build and release](08-build-and-release.md) |

Write what the thing *does* and *why it is where it is*, not the story of how it got there.
