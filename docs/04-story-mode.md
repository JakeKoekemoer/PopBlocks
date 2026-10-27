# 4. Story mode, levels, gold and shop

Code: `src/PopBlocks.Engine/Levels/` and `Services/`. Tests: `StoryTests.cs`.

## 4.1 What a level is

A level is one immutable `LevelDefinition` record. It is *data*; the engine interprets it. The important fields:

| Field | Meaning |
|---|---|
| `Number`, `Tier`, `Title`, `Tip` | Identity and the one-line hint on the level intro. |
| `IsTutorial` | Guided level (1 to 10): no gold. |
| `StartRows` **or** `BoardSeed` + `Profile` | Either a hand-drawn pile (rows of letters `C R Y P L` and `.`) or the seed and profile that generate one. |
| `Goals` | One to three goals. |
| `MoveLimit` | Pieces allowed. 0 means unlimited (the earliest tutorials). |
| `FallIntervalMs`, `Colours`, `ExoticShare` | Pace, colour count, share of pieces from the exotic shape set. |
| `PieceSeed`, `Script` | Seed for the random piece sequence, and optionally a hand-written opening sequence (`"Pair:RC"`). |
| `FlipView`, `NoPopPenalty` | Extreme mechanics. |
| `Steps` | Coaching cards shown during the level. |
| `ForceGhost` | Show the landing outline even if the player turned it off (used by the tutorial that explains it). |

`CreateBoard()` and `CreateGenerator()` turn the data into a starting board and a piece source.

### Why levels are data plus seeds

- **Determinism.** The same number gives the same pile, pieces, goals and limit on every device and every run. Retrying a level is therefore fair (same puzzle), and a bug report "level 87 is impossible" is reproducible.
- **Endless without storage.** Nothing is stored per level. Level 5,000 exists as soon as someone asks for it.
- **Testable.** A level is checked by replaying a plan through the real engine (see 4.5).

## 4.2 Hand-made levels (1 to 11)

Authored in `LevelCatalog` as `Tutorial1()` to `Tutorial10()` and `NewShapesLevel()`. They have fixed piles, scripted pieces and coaching steps.

| Level | Title | Teaches |
|---|---|---|
| 1 | Pop it! | Match four, move a piece, drop it. |
| 2 | Turn it around | Rotating to fit a gap. |
| 3 | Chain reaction | Chains: blocks that fall after a pop can line up and pop too, multiplying points. |
| 4 | Count your moves | The move limit. |
| 5 | Bigger is better | Score goals, the bonus for large groups, and the landing outline (`ForceGhost` is on). Its pile starts with no ready-made group of four, so the player has to make one. |
| 6 | Speed it up | Soft drop (slow drag or DOWN) and hard drop (flick or DROP). |
| 7 | Two colours | Pieces with more than one colour; each block keeps its own. |
| 8 | Plan ahead | The NEXT box, with a clear-the-board goal. |
| 9 | Big group | The big-group goal: fill a gap to join everything into one group. |
| 10 | Final exam | Two goals at once, with a generated pile and a solver-set limit. |
| 11 | New shapes | The exotic shapes. Not a tutorial: it pays gold. |

Level 10 is generated and solver-limited, but still `IsTutorial`. Level 11 is hand-tuned (its first piece is scripted to be a Plus) but pays gold.

## 4.3 Generated levels (12 and up)

`LevelCatalog.Generated(n)` builds a level in these steps. It is deterministic in `n`.

1. **Tier and progress.** `StoryConfig.TierFor(n)` and `ProgressInTier(n)` (0 to 1 through the band).
2. **Variant.** In the last ten Easy levels (41 to 50) `variant = (n - 41) % 5` selects: 0 rainbow (five colours), 1 tall pile, 2 tight moves, 3 shape-heavy, 4 scattered pile.
3. **Starting pile.** `ProfileFor(tier, p, variant)` gives a `DifficultyProfile`; `BoardGenerator.Generate(n, profile)` builds the pile.
4. **Extreme flags.** `flip` is on at 151 and then on levels where `n % 4 == 3`; `penalty` is on at 156 and then where `n % 3 == 0`.
5. **Goals.** `PickKinds` chooses which goal kinds appear. A kind only becomes *available* once it has been introduced (colour goals from 13, clear-down from 16, chain from 20, big group from 26), and the introduction level always uses the new kind. From 32 on a second goal appears with a probability that grows by tier (always at 32 itself); from 161 a third can appear (always at 161). A penalty level always includes a Score goal. `MakeGoal` sizes each goal from the tier's *demand* (roughly how many good pieces the level asks for).
6. **Prove it.** The solver plays the draft level. If it wins, the move limit becomes `ceil(botMoves * slack) + add`, where slack shrinks as tiers get harder (`SlackFor`). If the bot fails, the goals are eased by 10% per attempt (up to 8 attempts) until it wins.
7. **Fallback.** If even the eased goals fail (should not happen) a plain generous level (pop 8 blocks, 40 moves) is returned so the player is never stuck.
8. **Intro card.** Levels in the `Intros` table get a coaching card describing what is new.

All tuning tables are at the bottom of `LevelCatalog.cs`: `ProfileFor`, `ExoticShareFor`, `FallFor`, `DemandFor`, `SlackFor`, `MakeGoal`. To make levels harder or easier, change those, not the algorithm.

### Why prove levels winnable

A generated puzzle with a hard move limit can easily be impossible: random piles and random pieces do not care about your goals. Rather than hope, the generator asks a bot to win each draft first. Every generated level is therefore *known* to be winnable, and the limit is based on how many pieces a competent (but not perfect) player needs. Players can still lose, because the bot is only a baseline, not the best possible play.

## 4.4 The solver

`LevelSolver.Solve(level, maxMoves)` returns whether the bot won, in how many pieces, and the plan (for each piece: rotations, and the column of its left edge).

How it works:

- **Lightweight copy of the rules.** It uses flat byte arrays and reimplements locking, group finding, gravity and scoring (it calls `ScoringSystem` for the numbers so the scoring cannot drift). This avoids allocating a full `GameEngine` per candidate; it evaluates thousands of positions per level.
- **Hard drops only.** Every candidate is "rotate k times, put the left edge in column c, hard drop". It tries all four rotations (skipping rotations that give an identical shape) and every column, and discards placements that would end up in the hidden rows (game over).
- **Two-piece look-ahead.** For each candidate placement it keeps the best six by a heuristic value, then looks at the *next* piece's best reply and combines the two (30% now, 70% after). The piece sequence is read ahead from the level's generator, the same sequence the player will get.
- **Heuristic value** (`Value`): goal progress dominates (x1000), plus a bonus for pairs and triples of one colour that are close to popping, minus penalties for the number of blocks, for holes under blocks, and for a stack taller than 9 rows. Completing every goal adds a huge bonus.
- **No-pop penalty is simulated**, so penalty levels are solved honestly.
- **Deliberately conservative.** It never counts drop-bonus points and it only hard-drops, so it needs *at least* as many pieces as a skilled human would and the move limit has slack on top.

### Keeping the copy honest

A second implementation of the rules is a liability: if it drifts from the real engine, levels would be set from wrong numbers. The test suite therefore replays the bot's plans for **levels 1 to 220** through the real `GameEngine` and asserts that each plan actually wins in the number of moves the solver claimed. If you change a rule in the engine (scoring, matching, gravity, spawning) and forget the solver, this test fails.

## 4.5 Tutorials and coaching

`TutorialStep` is a title, text, a mode, and optionally the engine event that completes it and a board column to highlight. `StepMode`:

| Mode | Behaviour |
|---|---|
| `Tap` | A card with a button. The game is frozen until the player taps it. Input to the board is blocked meanwhile. |
| `Do` | Asks for an action ("move the piece", "drop it"). The piece stops falling until the engine raises the step's `Advance` event. A column can be highlighted as a hint. |
| `Watch` | Plays out on its own and completes when its event happens (for example the pop). The game keeps running. |

`TutorialController` is **pure logic**: it holds the steps and an index, advances on `Notify(GameEvent)` or `Dismiss()`, can `Skip()` everything, and exposes `FreezesGame` and `Current`. It has no UI, so it is unit-tested (and tutorials 1 to 9 are replayed end to end by tests that follow the coaching instructions).

How the app uses it (`GameViewModel`):

- A controller is created only for a level that has steps, **not yet cleared**, and not already shown in this page instance. So the lessons never nag a player who has done them, and a retry within one visit does not replay them, while the first play of any level always coaches.
- The game timer skips ticks while the controller `FreezesGame`, **except** while the piece is still in the hidden spawn rows. It is allowed to step into view first so the player can see the piece they are being told to move.
- Engine events are forwarded to the controller, which is how "drop it" completes when the piece is dropped.

Why engine events rather than polling the state: the tutorial reacts to *what the player did*. An event is exactly that, and it is the same mechanism used for feedback such as the "No pop!" toast.

## 4.6 Progress and gold

`PlayerProgress` (saved through `ILocalSettings`):

- `HighestCleared` and `Gold`.
- `IsUnlocked(level)`: levels `1..HighestCleared+1`. `IsCleared(level)`: `1..HighestCleared`. `NextLevel`: `HighestCleared + 1`.
- `Complete(level)`: if the level is the next uncleared one, record it and pay `GoldRewards.For(...)`; otherwise do nothing. Returns `CompletionResult(FirstClear, GoldAwarded)`.
- `TrySpend(cost)` for purchases.

`GoldRewards.For(level, isTutorial)`: 0 for tutorials and levels up to the last tutorial; otherwise by tier, growing slowly inside the tier (`Easy 10 + into/4`, `Medium 30 + into*2/5`, `Hard 60 + into*3/5`, `Extreme 100 + min(200, into/2)`, where `into` is the level's offset within its tier).

`GameViewModel.HandleCompletion` calls `Complete` exactly once per completed game (guarded by a flag), so a repeated engine notification cannot double-pay. The result tells the overlay whether to show "+N gold" or an explanatory note ("Tutorial levels don't pay gold" / "Gold is only paid the first time a level is cleared").

## 4.7 The shop

`ShopService` has three upgrades. Each has a price per level (`Costs` table), and the owned level is saved under `upgrade_<name>`.

| Upgrade | Prices | What the game reads |
|---|---|---|
| Lookout (`Preview`) | 120, 400 | `PreviewCount` = 1 + level, passed to `GameEngine.PreviewCount`. The queue shown grows to 2 then 3 pieces. |
| Pop Flair (`PopFlair`) | 80, 250, 600 | `FlairLevel` 0 to 3, passed to `BlastEffects.Flair`. |
| Spare Moves (`ExtraMoves`) | 150, 450, 1000 | `ExtraMoves` = 2 x level (+2, +4, +6), added to non-tutorial Story move limits. |

The values are pushed into the engine and the effects at the **start of each game** (`GameViewModel.NewGame`, `GamePage.OnGameStarted`), so buying something mid-level never changes the level in progress.

The game's difficulty and the move limits are tuned for a player with **no** upgrades. Spare Moves is a convenience, not a requirement. Keep it that way: if a level can only be cleared with upgrades, the generator's slack is wrong.

To add or change an upgrade see [Extending](09-extending.md).
