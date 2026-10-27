# 7. Testing

Project: `tests/PopBlocks.Tests` (xUnit, `net10.0`). It references only `PopBlocks.Engine`, so it runs with no emulator and no Android SDK, in a few seconds.

```
dotnet test tests/PopBlocks.Tests
```

or use **Test Explorer** in Visual Studio. There are roughly 560 test cases.

## 7.1 What is tested, and where

| File | Covers |
|---|---|
| `CollisionDetectorTests.cs` | Bounds, occupancy, wall kicks, hard-drop distance. |
| `ColourMatcherTests.cs` | Group finding: orthogonal only, sizes, multiple groups, no mutation. |
| `GravityResolverTests.cs` | Compaction and the reported moves. |
| `PieceTests.cs` | Spawn position, movement, rotation (including the doubled-pivot 2x2 block) and colour travel. |
| `ScoringSystemTests.cs` | Group points, chain multiplier cap, Classic speed and colour curves. |
| `GameEngineTests.cs` | The core engine: spawning, falling, moving, locking, bursts, simultaneous groups, chains and multipliers, hard and soft drop scoring, pause and resume (including during a chain), restart, game over (hidden-row lock, blocked spawn), Classic level speed-up, events. |
| `ShapeCatalogTests.cs` | Every shape (and every random piece) is edge-connected, in all four rotations. Guards against gaps and corner-only pieces. |
| `ToneSynthTests.cs` | Sound effects are short, audible, unclipped, start and end in silence, and wrap into a valid WAV; the sound setting defaults on and persists. |
| `ServicesAndBoardTests.cs` | `HighScoreService`, `GameSettings`, `Board` (text conversion, clone, counts). |
| `StoryTests.cs` | Everything in Story, in these classes: |

The campaign and economy have their own files:

| File | Covers |
|---|---|
| `LevelSnapshotTests.cs`, `LevelSnapshotData.cs` | A frozen description of levels 1 to 220: any change to level generation shows up as a diff to review. Levels 1 to 11 may never change. |
| `RegionRulesTests.cs` | Each region leans on its favoured goal, finales are clean (no flip, no penalty, no helpers), nothing appears before its introduction. |
| `StoryLayerTests.cs`, `FullStoryTests.cs`, `EndingTests.cs` | Scenes, lore, characters, replay rules, region rewards, the ending and its choice, and checks on the story text itself (nobody is named early, no text promises a branch, scenes fit on screen). |
| `RunStatsTests.cs`, `BonusObjectiveTests.cs` | The record of a run and the optional objectives, replayed through the real engine. |
| `P4ShopTests.cs`, `P4HelperTests.cs`, `P4SettingsTests.cs`, `P4GateTests.cs`, `WorkshopTests.cs` | The shop catalogue, Colour Lens and Chain Sense, accessibility settings, achievements, the baseline-save gate, the workshop. |
| `CosmeticTests.cs` | Skin rules (hue, separation, symbol contrast), board legibility, ownership, buying, equipping, older saves. |
| `UndoTests.cs` | Undo restores every field; random play with random undos never diverges from replaying only the kept moves; refusals; the rewindable generator; colour-order cycling. |
| `HelperTests.cs` | Prism Shift, helper stock, caps, locks, prices, Prism Reserve, the "without helpers" objective. |
| `SaveInfrastructureTests.cs`, `SaveCodeTests.cs`, `P7Tests.cs` | Save keys (frozen), the migrator, the unlock store, the save code (round trip, tampering, bombs, clamping), and the fairness rules: every level winnable with nothing bought, the ending reachable with no purchase or memory, replays never pay twice, everything for sale affordable. |
| `DevToolsTests.cs` | The testing shortcuts. |

`StoryTests.cs` is split into classes by topic:

- **`StoryRuleTests`**: move limits, winning on the last move, stack overflow, each goal kind, the no-pop penalty, timed pop phases (highlight, pop, settle), preview queue length, shop move bonus, event order, restart determinism.
- **`TutorialSeparationTests`, `TutorialProgressTests`**: the tutorial is its own sequence of ten lessons, Story levels 1 to 10 are ordinary and uncoached, and the "want the tutorial?" state is remembered once and travels in the save code.
- **`TutorialLevelTests`**: lessons 1 to 9 are *played through the real engine by following their own coaching steps*. If a tutorial's instructions stop being completable, this fails.
- **`TutorialControllerTests`**: the pure coaching state machine (Tap, Do, Watch, skip, freeze).
- **`ProgressAndShopTests`**: unlock order, gold paid once, tutorials pay zero, spending, upgrade prices and effects, persistence round trips.
- **`ShapeTests`**: every shape fits at spawn, four rotations return a shape to where it started, rotation never loses or merges cells, shapes can be found and parsed by name (bad specs are rejected), Classic generation never makes exotic pieces, and a script plays before random pieces.
- **`BoardGeneratorTests`**: the same seed gives the same board, different seeds differ, generated piles are settled (nothing floating) with nothing to pop and a clear top, only the requested colours are used, and taller profiles make bigger piles.
- **`LevelCatalogueTests`**: tier boundaries, goal introductions, flipped and penalty levels, and the big one: **the solver's plan for every level 1 to 220 is replayed through the real engine and must win** (see below).
- **`StoryFuzzTests`**: random play on many levels must never throw or leave the board invalid.

## 7.2 Test support code

- **`ScriptedGenerator`** (in `TestHelpers.cs`): hands out a fixed list of pieces so an engine test is fully deterministic.
- **`InMemorySettings`**: an `ILocalSettings` backed by a dictionary. This is why save logic is testable.
- **`Pieces`**: builders for common pieces (`Pair`, `Bar`, `Square`, `At`).
- **`Story`** (in `StoryHelpers.cs`): builds a small hand-made level (`Story.Level(rows, script, goals, ...)`), starts an engine on it, and has helpers to settle a chain, move a piece to a column, rotate, and **replay a solver plan**.

A typical Story rule test is a few lines: draw a pile as rows of letters, script the pieces, set a goal, move and drop, assert the state. Keep new tests in that shape: the situation should be readable from the test itself.

## 7.3 The catalogue replay test (important)

`LevelSolver` contains its own lightweight copy of the rules so it can evaluate thousands of positions quickly. A copy can drift. The replay test is what keeps it honest: for each of levels 1 to 220 it takes the solver's plan (for each piece: rotations and left column), plays it through the real `GameEngine` with hard drops, and asserts the level is won. If you change scoring, matching, gravity, spawn position or goal logic in the engine and forget to mirror it in the solver, this test will fail, usually on many levels at once.

The test also protects generated levels from regressions in tuning: if you change a tuning table so a level cannot be won in its move limit, the replay shows it.

Generating 220 levels takes a few seconds because each one runs the solver. That is the main cost in the suite.

## 7.4 What is *not* tested automatically

The MAUI layer has no automated tests: pages, the view model's bindings, gestures, drawing and animation. Reasons: they need a device or emulator, and almost all of the risk (rules, saving, level validity) is in the engine, which is covered. The consequences:

- After changing anything under `src/PopBlocks/`, **run the app** and play: a tutorial level, a pop, the shop, About, the level list, and a Classic game. See [Build, run and release](08-build-and-release.md).
- Keep logic out of the app layer so it stays testable: if you are writing an `if` that decides something about the game in a page or the view model, ask whether it belongs in the engine.
- `GameDrawable.Clock` is replaceable so a particular animation moment can be rendered deterministically if you want to build visual checks.

## 7.5 Writing a new test

1. Find the closest existing class (or add a class to the right file).
2. For a rule: `Story.Level(...)` with an exact pile, exact script and goals; `Story.Start(level)`; drive with `MoveTo`, `Rotate`, `HardDrop`, `Settle`; assert `engine.State`, `engine.Score`, `engine.Goals` or `engine.Board`.
3. For saving: construct the service with `new InMemorySettings()`, perform the action, then construct a **second** service on the same settings object and assert it reads the saved value (that proves the data really persisted).
4. Prefer asserting on behaviour the player sees (state, score, goals, board) over internal fields.
5. Tests must be deterministic. Use seeded `Random` or scripted generators; never `new Random()` without a seed.
6. When fixing a bug, add the test that would have caught it first.
