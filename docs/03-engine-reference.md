# 3. Engine reference (`PopBlocks.Engine`)

Namespace layout mirrors the folders:

```
PopBlocks.Engine
  GameConfig.cs, StoryConfig.cs     constants
  Models/                           plain data: board, piece, colours, shapes, setup, state, results
  Logic/                            rules: collision, matching, gravity, scoring, generators, GameEngine
  Levels/                           Story: goals, level definitions, catalogue, solver, tutorials
  Services/                         saved data: progress, gold, shop, settings
```

Rule of thumb for where code goes: **Models** hold data and trivial helpers, **Logic** holds pure functions and the engine, **Levels** knows about Story, **Services** knows about saving. Models never call Logic; Logic may use Models; Levels may use both; Services use Levels.

## 3.1 Constants

### `GameConfig`
Single home for board size and gameplay tuning. Nothing else in the solution may hard-code these numbers. Groups: board (`Columns`, `VisibleRows`, `HiddenRows`, `TotalRows`), matching (`MinGroupSize`, `ColourFamilies`), Classic speed (`InitialFallIntervalMs`, `MinFallIntervalMs`, `FallIntervalStepMs`, `LevelScoreStep`), pop pacing fallback (`ResolveStepMs`), colours in play for Classic, and scoring values.

### `StoryConfig`
The Story level bands (`TutorialLastLevel`, `EasyLastLevel`, `MediumLastLevel`, `HardLastLevel`) and helpers: `TierFor(level)`, `FirstLevelOf(tier)`, `LengthOf(tier)`, `ProgressInTier(level)` (0 at the start of a band, 1 at the end; Extreme ramps over 100 levels then holds) and `Name(tier)`.

Every rule that depends on a level number reads from here. That is why the bands can be retuned in one place. Never write `if (level > 50)` anywhere else.

## 3.2 Models

| Type | What it is | Notes |
|---|---|---|
| `CellColour` | `Empty` plus the five colours, as a `byte` enum. | `Empty = 0` so a zeroed array is an empty board. |
| `BlockCell` | `(X, Y, Colour)` record struct. | Inside a piece, X/Y are offsets from the pivot; in results they are absolute board coordinates. Called `BlockCell`, not `Cell`, to avoid clashing with `Microsoft.Maui.Controls.Cell`. |
| `Board` | Fixed-size grid of `CellColour`. Origin top-left, Y grows down. | Indexer with bounds checks, `IsInside`, `IsDormant` / `SetDormant` (see `ColourMatcher`), `Clone`, `CopyFrom`, `Clear`, `CountOccupied`, `AnyInRows`, and text conversion (`FromRows`, `ToRows`, `FromChar`, `ToChar`). Rows 0..`HiddenRows-1` are the hidden spawn area. |
| `Piece` | Immutable falling cluster. | See 3.2.1. |
| `ShapeDef`, `ShapeCatalog` | Every shape in the game. | See 3.2.2. |
| `ColourGroup`, `CellMove`, `BurstWave` | Results of one burst wave. | `BurstWave` carries the chain index, multiplier, points, the groups, the cleared cells, and the cells that fell. It feeds scoring display, goal tracking and the visual effects. |
| `GameMode`, `Tier`, `GameSetup` | What the player chose. | `GameSetup` is a record: Classic, or Story level N. |
| `GameState`, `FailReason`, `GameEvent` | State machine, why a game was lost, and things that happened. | `GameEvent` is raised as things occur (moved, rotated, popped, penalty, level complete...) and drives tutorials. |

### 3.2.1 `Piece`

Immutable: `Moved` and `Rotated` return new pieces. Cells are offsets from a **pivot**; `(X, Y)` is the pivot's board position.

**Doubled pivots.** `Pivot2X`/`Pivot2Y` store the rotation centre in doubled units. A centre on a cell is an even number; a centre *between* cells is odd. This lets a 2x2 block rotate about its true centre (1,1)/2 = (0.5,0.5) using only integers, so rotation never drifts and a rotated block stays a block. A plain `(0,0)` means "rotate about the pivot cell".

`AtSpawn` centres the piece horizontally and puts its top row on the **last hidden row**, so it becomes visible after exactly one step down.

Colours travel with their cells through rotation. That is a core rule of the puzzle (see game design).

`WithColourOrder(colours)` deals the same cells new colours (geometry, position and pivot unchanged). `DistinctColourOrders` counts the different colourings the piece's colours allow, and `WithNextColourOrder()` moves to the next one in a fixed lexicographic cycle (null for a one-colour piece). Prism Shift is built on these.

### 3.2.2 `ShapeCatalog`

Three arrays: `Classic`, `Exotic`, and the single `Dot`. A `ShapeDef` is a name, a random-draw weight, cell offsets and a pivot. `Build` colours a shape; `Parse("Pair:RC")` builds a piece from a short text spec (shape name, then one colour letter per cell). The text form exists so hand-made tutorial levels can script their piece sequence readably and tests can set up exact situations.

## 3.3 Logic

All of these are pure (no hidden state) except `GameEngine` and the generators.

### `CollisionDetector`
`IsValid` (inside the board and on empty cells), `CanMove`, `TryRotate`, `HardDropDistance`. All gameplay collision lives here, never in drawing code.

`TryRotate` tries the rotated piece at horizontal offsets `0, -1, +1, -2, +2` (a simple "wall kick") so a rotation near a wall or pile usually succeeds by nudging sideways instead of failing silently. Only horizontal kicks are tried: vertical kicks would let a piece climb over blocks.

### `ColourMatcher`
`FindGroups(board, minSize = 4)` returns all connected same-colour groups using **orthogonal** adjacency only (iterative flood fill with an explicit stack, so a big group cannot overflow the call stack). It is read-only: it never changes the board, so every group in a wave comes from one consistent snapshot. That matters because two groups that touch the same falling column must both be found before either is removed.

**Dormant blocks.** A piece that is a burst-sized group of one colour by itself (a *solid* piece, `Piece.IsSolid`) must not pop just by landing. Right after a lock the engine calls `MarkDormant(board, placedCells)`, which puts any burst-sized group made *only* of the piece's own cells to sleep (`Board.SetDormant`). `FindGroups` then skips a group unless at least one of its cells is awake, so a sleeping group waits until another block of its colour touches it; at that point the group has an awake cell and everything in it bursts together, sleepers included. The flags live on the `Board`: writing a cell wakes it, and `Clone`, `CopyFrom` (undo) and `GravityResolver.Apply` (falling) carry them along. `PlacementPreview` applies the same rule so its glow matches a real drop.

### `GravityResolver`
`Clear` empties cells; `Apply` compacts each column downward, preserving order, and returns the list of `CellMove`s (which cells fell from where to where). The move list exists purely so the app can animate falling; the engine itself does not need it.

### `ScoringSystem`
`Multiplier`, `GroupPoints`, `WavePoints`, plus the Classic difficulty curve (`LevelForScore`, `FallIntervalMs`, `ColourCountForLevel`). Uses `checked` arithmetic so an overflow would throw in tests instead of silently wrapping.

### `PieceGenerator`, `IPieceGenerator` and `RewindableGenerator`
`IPieceGenerator.Next(colourCount)` returns a piece already at the spawn position. `PieceGenerator` draws random pieces from the weighted Classic set, or from the Exotic set for a configurable share. It is seeded with a `Random` instance so levels are deterministic. A piece that would be solid is recoloured unless a `solidChance` roll (default `GameConfig.SolidPieceChance`, 0.15, about 1 piece in 60 overall) lets it stay; the recolouring draws no random numbers and the roll is drawn only for those pieces. Prism Shift skips colourings that would make an ordinary piece solid. `ScriptedPieceGenerator` plays a fixed list of pieces first (hand-made tutorials) and then hands over to another generator.

The interface exists so the engine can be fed any sequence: random for Classic, seeded for Story, scripted for tutorials, and fixed lists in tests.

`RewindableGenerator` wraps any generator and remembers every piece it hands out. `System.Random` cannot be wound back, so undo goes back by *replaying the remembered pieces*; the wrapped generator is only asked for something new once the log is exhausted. The engine wraps its generator in one at every game start and trims the log behind the oldest snapshot it can still return to.

### `BoardGenerator`
Builds the starting pile of a generated Story level from `(seed, DifficultyProfile)`. Guarantees:

- cells rest on the floor (no floating blocks);
- no group of four or more exists (nothing pops before the player acts);
- the top of the board, including the spawn rows, is empty.

It fills bottom row first, left to right, and checks the whole board after each candidate. Because only the left and lower neighbours exist when a cell is placed, that check is enough to guarantee the property for the finished board. `DifficultyProfile` has: colours, base height, height variance, max height, **clump** (chance a cell copies a neighbour's colour: bigger groups, easier) and **scatter** (chance a cell avoids both neighbours' colours: checkerboard-like, harder). `PieceSeed(seed)` derives a separate seed for the piece sequence so changing board generation cannot change the pieces.

### `GameEngine`
The heart of the game. It owns the board, the current piece, the upcoming queue, score, moves, goals and state. See [Architecture](02-architecture.md#24-state-machine) for the state machine.

**Public surface (grouped):**

- *Lifecycle:* `Start()`, `Start(GameSetup)`, `StartLevel(LevelDefinition)`, `Restart()`, `Pause()`, `Resume()`.
- *Time:* `Tick()` and `TimerIntervalMs`.
- *Input:* `MoveLeft`, `MoveRight`, `SoftDrop`, `Rotate`, `HardDrop`, and `LandingDistance` (for the ghost outline).
- *State:* `Board`, `State`, `FailReason`, `CurrentPiece`, `NextPiece`, `Upcoming`, `Score`, `PiecesPlaced`, `Chain`, `LastChain`, `LastWave`, `LastPenalty`, `PendingGroups`, `Goals`, `MoveLimit`, `MovesLeft`, `Story`, `Setup`.
- *Configuration the app sets before a game:* `PreviewCount` (1 to 5), `ExtraMoves` (shop bonus), `Timing` (`PopTiming`), and the helper allowance: `SecondChanceStock`, `PrismShiftStock`, `PrismFreeCharges`.
- *Helpers:* `UseSecondChance()`, `ShiftNextPiece()`, `SecondChanceStatus` / `PrismShiftStatus` (an `AbilityStatus` saying why not), `SecondChancesLeft`, `PrismShiftsLeft`, `SecondChanceUsed`, `PrismShiftsUsed`, `LastShiftWasPaid`, `HelpersAllowed`; and the mechanism underneath undo, `CanRewind` / `Rewind()`.
- *Events:* `Changed` (anything a view might redraw), `WaveResolved` (once per burst wave, for effects), `EventRaised` (`GameEvent`s, for tutorials and feedback).

**Locking and resolving a piece** (`LockPiece`):

1. Write the piece's cells into the board, count it as placed, reset chain counters.
2. Find groups. If there are none, apply the level's no-pop penalty (if any) and finish.
3. With `PopTiming.Instant`: resolve the first wave immediately; any further waves (a chain) are paced one per `Tick`. With timed pops: enter the **Highlight** phase, which sets `PendingGroups` and leaves the board unchanged so the app can light the match up.
4. `AdvanceResolution` (called by `Tick`) goes Highlight, then `ApplyWave` (clear, gravity, score, goal tracking, raise `WaveResolved`), then **Settle** (a pause while blocks fall), then looks for the next wave or finishes.
5. `FinishResolution` decides: goals met, overflow, out of moves, or spawn the next piece.

**Move accounting.** `MoveLimit` is the level's limit plus `ExtraMoves` (the shop bonus) for non-tutorial levels. `MovesLeft = MoveLimit - PiecesPlaced`. The out-of-moves check runs after the last piece has fully resolved and the goals have been tested, so winning on the last piece works.

**Helpers.** Both are *copied in at game start* (like `ExtraMoves`), so buying one mid-level changes nothing, and both are refused unless the level allows them: `HelpersAllowed` is true only for a Story level that is not a tutorial and has `AllowAbilities` (finales set it false). Classic never has them. A level has at most one Second Chance and one paid Prism Shift however many are in stock, plus `PrismFreeCharges` free shifts (spent first).

- **Second Chance (undo).** When a piece appears the engine takes an `EngineSnapshot` (board clone, the piece, the queue, score, pieces placed, blocks popped, last chain and penalty, last wave, a `GoalTracker.Clone()` and the generator position). It keeps the newest and the one before. `Rewind()` restores the older one, rewinds the generator to its position, and keeps nothing older, so it goes **one step** back and a second use needs another placement first. It works only in `Playing` (never while blocks pop, paused, or after the level ends), raises only `GameEvent.SecondChanceUsed` (no sound or tutorial event fires again), and does not refund the move twice. It does not give back a Prism Shift spent after the placement it undoes.
- **Prism Shift.** Replaces the first upcoming piece with `Piece.WithNextColourOrder()`: the same colours dealt out in the next different order, same shape and position. A one-colour piece has nothing to rearrange (`AbilityStatus.SingleColour`). The generator is not touched.
- `RunStats` listens for these events: it rolls back what an undone placement did (so a big group popped and then taken back does not satisfy an objective) and counts helpers used.
- `ConsumableInventory.Attach(engine)` takes one out of stock when the engine reports a *paid* use, so leaving a level without using a helper costs nothing.

**Why `PendingGroups` is separate from the board.** During the highlight phase the board is deliberately unchanged. The drawing code lights up the pending cells in place and the engine pops them later. If the engine mutated the board first, there would be nothing left to light up.

## 3.4 Levels

See [Story mode](04-story-mode.md) for the full picture. In brief:

| Type | Role |
|---|---|
| `Goal`, `GoalKind`, `GoalState`, `GoalTracker` | A goal, its progress, and the tracker the engine feeds one wave at a time. |
| `LevelDefinition` | Everything that defines a level (immutable record). |
| `LevelCatalog` | Hand-made levels 1 to 11, generated levels from 12 on, and the cache. |
| `LevelSolver` | A bot that plays a level to prove it is winnable and to measure the pieces it needs. |
| `TutorialStep`, `StepMode`, `TutorialController` | Coaching cards and the pure logic that advances them. |

## 3.5 Services

| Type | Role |
|---|---|
| `ILocalSettings` | Key/value store abstraction (int, bool, string). Implemented by the app with `Preferences`, by tests in memory. |
| `HighScoreService` | Classic personal best; saved the moment it is beaten. |
| `SaveKeys`, `SaveMigrator`, `SaveCode` | Every saved key, the format version, and the copy-and-paste save code. |
| `UnlockStore` | The set of unlock ids (scenes seen, lore, fragments, achievements, cosmetics). `Unlock` returns true only the first time. |
| `GameSettings` | Pop effects, landing guide, collapsed controls. Each property saves itself on set. |
| `PlayerProgress` | Highest Story level cleared and gold. `Complete(level)` pays only the first clear. `TrySpend` for purchases. Raises `Changed`. |
| `GoldRewards` | The gold formula by tier. |
| `ShopCatalog`, `ShopItem`, `ShopTier`, `Requirement` | Every upgrade as data: id, save key, tiers with price, effect text and what must be restored or earned first. |
| `ShopService` | Upgrade levels, locks, purchasing, and the effects (`PreviewCount`, `FlairLevel`, `ExtraMoves`, `LensLevel`, `ChainSenseOwned`, `PrismFreeCharges`). Generic API by item id (`Entry`, `Entries`, `TryBuy`); the original `Upgrade` API is kept. |
| `ConsumableCatalog`, `ConsumableInventory` | The two helpers that are used up: price, requirement, stock (cap 3), buying, taking one when used, copying stock into an engine. |
| `CosmeticCatalog`, `CosmeticShop`, `ColourMath` | Every skin, burst, board, frame and Pip accessory as data; owning, discovering, buying and equipping them; colour maths for the skin rules. |
| `AchievementCatalog`, `AchievementService`, `LifetimeStats`, `RunStats` | Achievements, personal bests, and the record of one run that feeds them and the optional objectives. |
| `WorkshopService` | Which of Bram's lamps are lit (a function of regions restored). |
| `DevTools` | Testing shortcuts (gold, jump to a level, max the shop, fill helpers, unlock all looks, reset). The app shows them only in development builds. |

**Why `PlayerProgress.Complete` checks `level.Number == HighestCleared + 1`.** Levels unlock in order, and gold is paid once per level. Requiring the level to be the *next* one means replaying a cleared level, or any out-of-order completion, pays nothing and moves nothing, with no extra bookkeeping. If you ever allow skipping levels, this rule must be replaced with an explicit per-level "paid" record.

## 3.6 Contracts worth knowing

- `Tick()` returns false and does nothing when the game is paused, ready, over or complete.
- `HardDrop` scores 2 per row travelled and locks immediately; `SoftDrop` moves one row for 1 point and never locks (the next tick does).
- Input methods return false (and do nothing) when the game is not in `Playing` or there is no current piece. They are safe to call at any time.
- `Restart()` on the same level gives exactly the same pile and piece order again (levels are deterministic).
- The `Goals` property can be read at any time (the HUD does), but whether a level is *won* is only decided in `FinishResolution`, on the settled board after a piece's whole chain has played out. A transient mid-chain state never wins a level.
- `LevelCatalog.Get` is thread-safe; nothing else in the engine is.
