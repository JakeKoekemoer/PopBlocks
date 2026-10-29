# 4. Story mode, levels, gold and shop

Code: `src/PopBlocks.Engine/Levels/` and `Services/`. Tests: `StoryTests.cs`.

## 4.1 What a level is

A level is one immutable `LevelDefinition` record. It is *data*; the engine interprets it. The important fields:

| Field | Meaning |
|---|---|
| `Number`, `Tier`, `Title`, `Tip` | Identity and the one-line hint on the level intro. |
| `IsTutorial` | A tutorial lesson (`LevelCatalog.Lesson`): coached, never paid, not part of the Story. |
| `StartRows` **or** `BoardSeed` + `Profile` | Either a hand-drawn pile (rows of letters `C R Y P L` and `.`) or the seed and profile that generate one. |
| `Goals` | One to three goals. |
| `MoveLimit` | Pieces allowed. 0 means unlimited (the earliest tutorials). |
| `FallIntervalMs`, `Colours`, `ExoticShare` | Pace, colour count, share of pieces from the exotic shape set. |
| `PieceSeed`, `Script` | Seed for the random piece sequence, and optionally a hand-written opening sequence (`"Pair:RC"`). |
| `FlipView`, `NoPopPenalty` | Extreme mechanics. |
| `IsFinale`, `AllowAbilities` | The last level of a region, and whether helpers (Second Chance, Prism Shift) may be used. Finales set `AllowAbilities` false. |
| `Bonus` | The optional memory objective, if the level carries one (see below). |
| `Steps` | Coaching cards shown during the level. |
| `ForceGhost` | Show the landing outline even if the player turned it off (used by the tutorial that explains it). |

`CreateBoard()` and `CreateGenerator()` turn the data into a starting board and a piece source.

### Why levels are data plus seeds

- **Determinism.** The same number *and seed* gives the same pile, pieces, goals and limit on every device and every run. Retrying a level is therefore fair (same puzzle), and a bug report "level 87 is impossible" is reproducible from the level number and the seed the levels page shows. Different players get different seeds, so they get different levels (see 4.8); the original number-based level is seed 0 (`LevelSeed.Legacy`).
- **Endless without storage.** Nothing is stored per level except the seed of the current level and of each cleared one. Level 5,000 exists as soon as someone asks for it.
- **Testable.** A level is checked by replaying a plan through the real engine (see 4.5).

## 4.2 Hand-made levels (the ten lessons, and Story level 11)

Authored in `LevelCatalog` as `Tutorial1()` to `Tutorial10()` (the **lessons**) and `NewShapesLevel()` (Story level 11). They have fixed piles, scripted pieces and coaching steps.

The lessons are **not Story levels**. They were Story levels 1 to 10 once, but that made new players sit through ten lessons before the game began, so the tutorial was split off: `LevelCatalog.Lesson(1..10)` serves them, `GameSetup.Lesson(n)` plays them, and Story levels 1 to 10 are now ordinary generated levels (`Generated(n)`, no coaching, still no gold). The lesson definitions did not change, and `LessonSnapshotData` freezes them.

| Lesson | Title | Teaches |
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

Lesson 10 is generated and solver-limited, but still `IsTutorial`. Story level 11 is hand-tuned (its first piece is scripted to be a Plus) but pays gold.

## 4.3 Generated levels (12 and up)

`LevelCatalog.Generated(n, seed)` builds a level in these steps. It is deterministic in `n` and the seed; with the legacy seed 0 it is exactly the level the game has always built for `n` (`LevelSnapshotTests` freezes that, and only that). A non-zero seed replaces `BoardSeed`, `PieceSeed` and the colour of a colour goal, and nothing else: every step below that depends on the level *number* (tier, region rule, special conditions, goal kinds) is unchanged by a seed.

1. **Tier and progress.** `StoryConfig.TierFor(n)` and `ProgressInTier(n)` (0 to 1 through the band).
2. **Region rule.** `RegionRules.For(n)` (in `Story/RegionRules.cs`) is the one table that gives each region its character: goal-kind weights, the chance of two or three goals, a tighter or looser move limit, taller or more scattered piles, and a repeating **variant** cycle from a given level (0 rainbow, 1 tall pile, 2 tight moves, 3 shape-heavy, 4 scattered). The generator reads the rule and keeps no level numbers of its own.
3. **Starting pile.** `ProfileFor(tier, p, variant)` gives a `DifficultyProfile`; `BoardGenerator.Generate(n, profile)` builds the pile.
4. **Special conditions.** `RegionRules.Flips(n)` and `Penalises(n)`: a few listed levels in region 6 (128, 138, 146; 134, 141, 148), then from the Citadel (151) on a pattern (`n % 4 == 3` flips, `n % 3 == 0` penalises from 156). Finales never flip or penalise. Level 200 is built specially from the chain-set goal.
5. **Goals.** `PickKinds` chooses which goal kinds appear. A kind only becomes *available* once it has been introduced (colour goals from 13, clear-down from 16, chain from 20, big group from 26), and the introduction level always uses the new kind. From 32 on a second goal appears with a probability that grows by tier (always at 32 itself), and the region rule can raise it; from 161 a third can appear (always at 161). A penalty level always includes a Score goal. `MakeGoal` sizes each goal from the tier's *demand* (roughly how many good pieces the level asks for).
6. **Prove it.** The solver plays the draft level. If it wins, the move limit becomes `ceil(botMoves * slack) + add`, where slack shrinks as tiers get harder (`SlackFor`). If the bot fails, the goals are eased by 10% per attempt (up to 8 attempts) until it wins.
7. **Fallback.** If even the eased goals fail (should not happen) a plain generous level (pop 8 blocks, 40 moves) is returned so the player is never stuck. That level is **not bot-verified**; `BuiltLevel.Verified` says so, and a rolled seed never accepts one (4.8).
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

- A controller is created only for a level that has steps, **not yet cleared** (a lesson always coaches when entered fresh, because replaying the tutorial is its purpose), and not already shown in this page instance. So the intro cards never nag a player who has cleared the level, and a retry within one visit does not replay them.
- **Offering the tutorial.** A brand-new player (`HighestCleared == 0`, never asked, no lesson done: `TutorialProgress.ShouldOffer`) who starts Story level 1 is asked once whether they want it. The game is held still and the region's opening scene waits for the answer. *Yes* switches the same page to lesson 1; *No* restarts level 1 with its scene. The answer and each finished lesson are kept as ids in the `UnlockStore` (`tutorial_offered`, `tutorial_l1` to `tutorial_l10`), so they are in the save code and a dev reset clears them. The menu's TUTORIAL button always starts lesson 1.
- **Lessons stay outside the Story.** `GameViewModel` records only `TutorialProgress.Complete` when a lesson is won: no `PlayerProgress.Complete`, gold, scenes, region progress, lifetime stats or achievements. After the last lesson the button reads START PLAYING and goes to the player's next Story level.
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

**Region reward.** Restoring a region for the first time in play also pays `ShopCatalog.RegionClearGold(region)` (`25 + 25 * region`), recorded as the unlock id `gold_<region>` so it is paid once. `CampaignService.Reconcile` (a save caught up to a jump or an older build) never pays it.

`GameViewModel.HandleCompletion` calls `Complete` exactly once per completed game (guarded by a flag), so a repeated engine notification cannot double-pay. The result tells the overlay whether to show "+N gold" or an explanatory note ("Tutorial levels don't pay gold" / "Gold is only paid the first time a level is cleared").

## 4.7 The shop and the workshop

Every upgrade is a row in **`ShopCatalog`** (`Services/ShopCatalog.cs`): an id, a save key, a base effect, and tiers. A `ShopTier` has a price, an effect text and a `Requirement` (nothing, a region restored, an achievement earned, or the campaign finished). `ShopService` reads the catalogue generically (`Level`, `Entry`, `Entries(category)`, `TryBuy(id)`), keeps the three original `Upgrade` calls working, and saves the owned level under the item's own key. A locked tier exposes the requirement's `Describe()` text and no price.

| Item | Prices | What the game reads |
|---|---|---|
| Lookout | 120, 400, 900 (region 3), 1,500 (region 6) | `PreviewCount` = 1 + level (2 to 5 pieces shown). |
| Pop Flair | 80, 250, 600 | `FlairLevel` 0 to 3, passed to `BlastEffects.Flair`. |
| Spare Moves | 150, 450, 1,000 | `ExtraMoves` = 2 x level, added to non-tutorial Story move limits. |
| Colour Lens | 500 (region 2), 1,100 (region 4) | `LensLevel`: outlines groups one (or two) blocks short of a burst. |
| Chain Sense | 700 (region 3) | `ChainSenseOwned`: the landing outline glows when the drop would start a chain. |
| Prism Reserve | 600 (region 5), 1,200 (region 7) | `PrismFreeCharges`: free Prism Shifts per level. |

Two helpers are *used up* rather than owned: **Second Chance** (150) and **Prism Shift** (100), handled by `ConsumableInventory`. Looks are on the workshop's LOOKS tab (`CosmeticCatalog`). Prices, requirements and the reasoning for the numbers are in `docs/story/economy.md`; the helper rules are in `docs/story/helpers.md`.

The values are pushed into the engine and the effects at the **start of each game** (`GameViewModel.NewGame`, `GamePage.OnGameStarted`), so buying something mid-level never changes the level in progress.

The game's difficulty and the move limits are tuned for a player with **no** upgrades and no helpers. Everything here is a convenience, not a requirement. Keep it that way: if a level can only be cleared with a purchase, the generator's slack is wrong. `P4GateTests` and `P7Tests` prove every level winnable by the bot with nothing bought, that the ending is reachable with no purchase and no optional memory, and that everything for sale is affordable from the campaign's own income.

**The workshop** (`WorkshopService`, `WorkshopPage`) is Bram's one-screen hub with four tabs: UPGRADES, LOOKS, SUPPLIES and TROPHIES (achievements and the journal). Each tab has a lamp that lights as a region is restored (0, 3, 5 and 6); lighting a lamp is the only thing region progress changes there. Every tab is open from the start, so the workshop never stands between the player and the story. The Archive is deliberately not a workshop station: it opens from the main menu, and having two doors to it was the kind of repeat the workshop used to have (the workbench and the mechanic's bench were the same shop, and the paint shelf and the shop's LOOKS tab held the same cosmetics).

To add or change an upgrade see [Extending](09-extending.md).


## 4.8 Seeded levels, rerolls and Classic gold

Code: `Levels/LevelSeed.cs`, `LevelRoller.cs`, `DifficultyBand.cs` (generated), `GateTiming.cs`; `Services/PlayerIdentity.cs`, `StoryLevels.cs`, `SeedStore.cs`, `RerollPricing.cs`, `ClassicGold.cs`; `SeedingConfig.cs` (every number in one place). Tests: `SeedMixTests`, `SeededLevelStructureTests`, `SeededCatalogueReplayTests`, `RerollTests`, `ClassicGoldTests`, `SeedSaveTests`, `PlayerIdentityTests`, `LoadingRecapTests`, `P7Tests`. The sweep tool is `SeedSweep`. The spec this implements lives in `docs/seeded-levels-plan.json`.

### The seed

A player has a **salt**: `Hash32(utf8(name), installNonce)` (FNV-1a), computed once when they answer the name prompt and stored as `player_salt`. A Story level's seed is `Mix32(salt, level, reroll, attempt) & 0x3FFFFFFF` (never 0). `Mix32` is a splitmix-style mixer with golden-value tests; `string.GetHashCode()` is never used because it differs between runs. **Seed 0 is "legacy"**: the original number-based level, used for levels cleared before seeds existed, for every old test and for the frozen snapshot.

### The roll and the gate

`LevelCatalog.Roll(level, salt, reroll)` (`LevelRoller`) tries `attempt` = 0 to 23 (or two seconds, `SeedingConfig`), building each candidate with `LevelCatalog.Build(level, seed)` and keeping the first that passes:

1. **Hard requirements.** The full bot wins (`Verified`, not the plain fallback level); the move limit is at least 6; a memory level (offsets 11 and 17, never a finale) has its objective; and **the bot's plan can be played in the real engine** (`LevelSolver.PlanIsPlayable`). The last one matters: the bot's lightweight rules ignore that a tall pile can block a slide, so about one candidate in 1,500 has a plan a person could not carry out; "bot-verified" would not mean what it says.
2. **The band.** `hardness = min(2, weakNeed / fullNeed)`, where the weak bot (`SolveOptions.Weak`) takes the top-1 placement with no look-ahead. A board only the look-ahead bot can win is harder for a person; tightness of the move limit is constant by construction. `DifficultyBand` holds, per level, the 20th to 80th percentile of hardness over a sweep, smoothed over five levels each side inside the tier, and held to the median of the previous five levels at a tier start and for two levels after each new mechanic.
3. **Guard rails** (a preference, not a rule): a pile size or solve length outside the 10th to 90th percentile of the neighbouring levels is an outlier. A candidate inside them always beats one outside, but if every candidate is an outlier the closest is still used.

If nothing fits, the verified candidate closest to the band is used. An unverified level is never returned. A roll is deterministic for `(salt, level, reroll)` apart from the time budget.

### Failures, reroll and the first clear

`StoryLevels` (one object, `GameViewModel` talks to it) keeps the rules of this section.

| Situation | Seed used |
|---|---|
| Current level (HighestCleared + 1), first entry | `Roll(level, salt, 0)`, stored as `current_level_seed` |
| Current level, later entries | The stored seed |
| Cleared level with a recorded first-clear seed | The recorded seed |
| Cleared before seeds existed, no record | Legacy seed 0 (the player replays the level they beat) |
| Reroll on a cleared level | `Roll(level, salt, 1000 + k)`, `k` counted for this session. Free. Never stored. |

- **Failures** (`RecordFailure`): a loss, or a restart after at least one piece was placed, on the current level only. Saved on every increment, back to 0 by a successful reroll (v1.1; the lifetime reroll count is the `stat_rerolls` stat), reset when the level is cleared. Leaving to the menu, replays and rerolling away from a run do not count.
- **Reroll** (`TryReroll`): offered from the first failure, and always on a cleared level. `RerollPricing.Price = (reward * (10 - min(failures, 10)) + 9) / 10`: 12, 11, 6, 2, 0 gold on level 12 at 0, 1, 5, 9 and 10 failures. Not enough gold changes nothing. On the current level the new reroll count and the cleared seed are saved first and the gold is taken second, so a crash in between gives a free reroll rather than a lost payment; `Roll` is deterministic for a reroll count, so a restart gives the same new level.
- **Losses and easing** (v1.1): every `RecordFailure` also adds to `level_losses` (`level:count`, never resets, not even on a win). From `SeedingConfig.LossesForSolvableNote` (5) losses the failed card says "There is always a way through" (not on an eased level). From `LossesForEase` (15) the current level (never a region finale) offers EASE THIS LEVEL: `StoryLevels.TryEase` marks it in `eased_levels`, starts the failure count again and asks for the eased roll (`LevelRoller.Roll` with a reroll number at or above `EasedRerollOffset`: the same bot gate, but `DifficultyBand.EasedFor` -- from 1.0 to the middle of the normal band -- and its own seed stream). The level is played as `LevelDefinition.ToEased()` (moves x `EasedMoveFactor` 1.5, rounded up; `GameSetup.Eased` carries the flag through restarts). An eased clear advances the story, records the seed, pays nothing (`RunStats.Completed` stays false, so no win counts); the Map shows a feather. A later normal win of that level pays the first-clear gold once and removes the mark; a replay of an eased level is rolled fresh rather than replaying the eased seed, and a paid reroll turns an eased level back into a normal one.
- **Continues** (v1.1): a normal Story level lost by running out of moves can be continued: `GameEngine.CanContinue` / `Continue(5)` put the game back to playing with five more moves, the next piece coming from the same stream and an undo snapshot taken. `ContinuePricing`: 30, 42, 55 gold, at most three per attempt (a retry, reroll or new level starts the count again). Never after an overflow, on a region's finale, on an eased level or in a lesson. The view model takes the gold exactly once (`BuyContinue`). A loss that is continued is not paid twice: a second loss pays only the difference (`Consolation.Pay(..., alreadyPaid)`), and a win after a continue credits back the pop gold already paid. `RunStats.Continues` feeds `stat_continues`. The run is added to the lifetime bests when the attempt really ends, not at the first game over.
- **First clear**: `PlayerProgress.Complete` writes `level:seed` to `first_clear_seeds` in the same place gold is paid (never overwriting, never recording the legacy seed), then resets the current-level state.
- **Prefetch**: the next level is rolled when a level begins; after a failure the next reroll is rolled too; both are cancelled when the player leaves the level.

### The screens

The name prompt (main menu, once, before the tutorial offer), the REROLL button and "?" hint on the failed and pause panels (words only: the price, "Free in N more tries", "Fresh level, free"), the loading card, the seed on the Map page (tap a level to see its details and its region; tap the seed to copy it), the Rename card in Settings, the Classic SEED link, and the dev menu (play a level with a typed seed, set failures, time the gate on the phone).

### Classic gold

`GameEngine.ClassicGoldTenths` counts tenths per block popped at the Classic level of the pop; `ClassicGold.Bank` pays whole gold and carries the rest, exactly once per banking event. A Classic run can start from a seed (`GameSetup.ClassicRun`, `SeededPieceGenerator`: piece `i` from `Random(Mix32(seed, i))`); a typed seed never sets `best_score`.
