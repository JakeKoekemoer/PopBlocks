# 7. Testing

Project: `tests/PopBlocks.Tests` (xUnit, `net10.0`). It references only `PopBlocks.Engine`, so it runs with no emulator and no Android SDK, in a few seconds.

```
dotnet test tests/PopBlocks.Tests
```

or use **Test Explorer** in Visual Studio. There are roughly 930 test cases.

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
| `AudioMusicTests.cs`, `AudioSystemTests.cs` | The procedural music and sound: scores are in key and in tempo range, every cue is finite, quiet enough and starts and ends in silence, the three restoration states differ and share one piece, the settings and their save keys, instant click-free mute, the adaptive states, lifecycle and interruption handling, and the song catalogue, how songs unlock, and that a chosen song plays in games, survives the pause, and is saved; and that the audio code has no network or MAUI references. They cannot judge how it sounds. |
| `ServicesAndBoardTests.cs` | `HighScoreService`, `GameSettings`, `Board` (text conversion, clone, counts). |
| `StoryTests.cs` | Everything in Story, in these classes: |

The campaign and economy have their own files:

| File | Covers |
|---|---|
| `LevelSnapshotTests.cs`, `LevelSnapshotData.cs` | A frozen description of levels 1 to 220 **built from the legacy seed** (the original, number-based levels): any change to level generation shows up as a diff to review. Level 11 may never change. Seeded levels are covered by the tests below, not by this snapshot, and the snapshot is never regenerated for a retune. |
| `RegionRulesTests.cs` | Each region leans on its favoured goal, finales are clean (no flip, no penalty, no helpers), nothing appears before its introduction. |
| `StoryLayerTests.cs`, `FullStoryTests.cs`, `EndingTests.cs` | Scenes, lore, characters, replay rules, region rewards, the ending and its choice, and checks on the story text itself (nobody is named early, no text promises a branch, scenes fit on screen). |
| `RunStatsTests.cs`, `BonusObjectiveTests.cs` | The record of a run and the optional objectives, replayed through the real engine. |
| `P4ShopTests.cs`, `P4HelperTests.cs`, `P4SettingsTests.cs`, `P4GateTests.cs`, `WorkshopTests.cs` | The shop catalogue, Colour Lens and Chain Sense, accessibility settings, achievements, the baseline-save gate, the workshop. |
| `CosmeticTests.cs` | Skin rules (hue, separation, symbol contrast), board legibility, ownership, buying, equipping, older saves. |
| `UndoTests.cs` | Undo restores every field; random play with random undos never diverges from replaying only the kept moves; refusals; the rewindable generator; colour-order cycling. |
| `HelperTests.cs` | Prism Shift, helper stock, caps, locks, prices, Prism Reserve, the "without helpers" objective. |
| `PersistentSettingsTests.cs` | The save file mirror with an in-memory file store: debounced writes, restore on a fresh install only, a file that cannot be read is never overwritten, delete / back up / erase, freezing, and a full reinstall round trip. |
| `SaveInfrastructureTests.cs`, `SaveCodeTests.cs`, `P7Tests.cs` | Save keys (frozen), the migrator, the unlock store, the save code (round trip, tampering, bombs, clamping), and the fairness rules: every level winnable with nothing bought, the ending reachable with no purchase or memory, replays never pay twice, everything for sale affordable. |
| `DevToolsTests.cs`, `SeededDevAndPageTests.cs` | The testing shortcuts (gold, jumps, failures, reset), the seed text on the Map page, and the gate timing report. |
| `SeedMixTests.cs`, `SeededLevelStructureTests.cs` | Golden values for `Mix32`, `Hash32` and seed derivation; the same seed gives an identical level; different salts give different piles; a seed never changes a level's structure (goal kinds, flip, penalty, finale, colours, speed) for any level 1 to 220; the bounded, single-flight cache; the solver options. |
| `SeededCatalogueReplayTests.cs` | The **gate**: for a fixed salt, levels 1 to 220 at rerolls 0 to 2 are bot-winnable, the bot's plan wins through the real `GameEngine`, the start is fair, memory levels have their objective, the unverified fallback is never used, and the hardness is in the band or the closest-candidate rule applied. Also `SeedFuzzTests` (see 7.6). |
| `LossSupportTests.cs` | v1.1: the never-resetting loss count, the solvable note threshold, easing (15 losses, never a finale, eased roll, 1.5x moves, restarts keep it, pays nothing, later normal win pays once and removes the mark, replays roll fresh), save-code round trip of `level_losses` and `eased_levels`. |
| `ContinueTests.cs` | v1.1: the 30/42/55 ladder and cap, five more moves, same piece stream as a level with more moves, undo snapshot, three per attempt, a retry refills, never after an overflow, on a finale, eased or lesson, run stats. |
| `ConsolationTests.cs` | v1.1: the two halves of a loss payout, the per-level lifetime cap, cleared/eased/opening levels, a second loss after a continue. |
| `GameRulesTests.cs` | v1.1: the default is the old game, what is easier, half gold, fall-speed limits, normalizing, saving (restart and save code, broken values), where rules apply, the engine playing by smaller and bigger bursts, enabled colours and shapes, disabled-colour goals, halved pop gold / first clear / consolation, and no best score on custom rules. |
| `AchievementV11Tests.cs` | v1.1: the sixteen achievements (names, gold), total-based and run-based rules, an eased clear is never a win, Stubborn, cracks / breaks / Orb close calls counted in a run, Endless-only rules, the saved counters, and the service paying Dressed Up and Spread the Word once. |
| `OrbTests.cs` | v1.1: spots and thresholds, placing and pushing, the board-top refusal, the Orb is not a colour, the rigid 2x2 fall, the cross, break and bonus gold, corruption, the eighth-piece guarantee and cooldown, determinism, the switches, undo. |
| `FractureTests.cs` | v1.1: the two-sided crack, one side and corners do nothing, a third crack breaks, goals, gravity carry, undo, gold weights, the switches and plans replaying on levels with cracks. |
| `RerollTests.cs` | Table-driven reroll prices, failures (current level only, saved on every increment, survive a restart, reset to 0 by a successful reroll, reset by a clear), the reroll action (atomic, free at 10, never needs gold), which seed a level plays, first-clear recording, prefetch and cancellation, single-flight rolls. |
| `SeedSaveTests.cs`, `PlayerIdentityTests.cs` | The eight new save keys round-trip through a save code, an old save loads, malformed or oversized seed lists are rejected, a stale `current_level_for` is ignored; the name and salt rules. |
| `ClassicGoldTests.cs` | The Classic rate table, accrual in the engine, banking (once per event, carry kept), seeded piece streams, a typed seed never sets a best score, and that Classic reads no Story state. |
| `LoadingRecapTests.cs` | The loading card thresholds (300 ms, 600 ms) and that the recap shows only story the player has seen. |

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

## 7.6 The seeded-level gate, sweep and fuzz

The gate test (`SeededCatalogueReplayTests`) runs on every build: 660 rolls in about half a minute on a desktop, in parallel. It is the seeded twin of the catalogue replay test in 7.3, which keeps running on the legacy levels unchanged.

Two things are switched on by environment variable because they take minutes:

| Variable | What it does |
|---|---|
| `POPBLOCKS_FUZZ=1` (`POPBLOCKS_FUZZ_SEEDS`, default 5000, `POPBLOCKS_FUZZ_STATS` = a file to write) | `SeedFuzzTests`: 5,000 random rolls per tier with the same assertions as the gate, plus roll-time statistics (median, 95th percentile, slowest per tier). |
| `POPBLOCKS_SWEEP_CSV=path` (`POPBLOCKS_SWEEP_SEEDS`, default 50) and `POPBLOCKS_BAND_FROM_CSV=path` with `POPBLOCKS_BAND_TO=path` | `SeedSweep`: the measurement behind `DifficultyBand`. See [Extending](09-extending.md#915-retune-the-difficulty-band). |

Run a switched-on test from a copy of the build output (`dotnet test <copy>/PopBlocks.Tests.dll`) if you want to keep editing and building while it runs.
