# 1. Game design

This document describes the game as the player experiences it, and the product rules behind it. All numbers quoted here live in one place in code; the file is named next to each group so you can change them without hunting.

## 1.1 The core loop

1. A piece (2 to 5 coloured cells) appears at the top of an 8-column, 16-row board and falls.
2. The player moves it sideways, rotates it, and drops it.
3. When it lands it locks into the board.
4. Every group of **four or more** cells of the same colour that touch **sideways or up/down** (never diagonally) bursts.
5. Whatever is above a burst falls into the gap. If that creates new groups they burst too. Each repeat is one **wave**; the number of waves in a row is the **chain**.
6. The next piece appears. The game ends when the stack reaches the top (or, in Story, when the pieces run out).

Pieces are not single-colour tetrominoes. A piece can have one or two colours, and its cells keep their colours when it rotates. That is what makes the puzzle: you are lining up colours, not filling lines. There is no "line clear" in the Tetris sense.

Constants: `GameConfig.cs` (`Columns` 8, `VisibleRows` 16, `HiddenRows` 2, `MinGroupSize` 4, `ColourFamilies` 5).

## 1.2 Board and spawn area

The engine's board is 18 rows tall: 2 hidden rows on top plus 16 visible. New pieces spawn in the hidden rows and become visible after one step down. If anything is still resting in the hidden rows after a lock has fully resolved, the stack has overflowed and the game is lost. If a new piece cannot be placed at all, that is also an overflow.

Why hidden rows: it gives pieces room to spawn without overlapping the visible board, and it gives "game over" a precise, testable meaning (something is in rows 0 and 1) rather than a vague "the stack looks too high".

## 1.3 Colours and shapes

Five colours: Cyan, Coral, Yellow, Purple, Lime. Each also has a **symbol** drawn on the block (diamond, circle, star, plus, triangle) so colour is never the only signal. This is deliberate accessibility: the game must be playable by someone who cannot tell two of the colours apart. Do not remove symbols to make blocks "cleaner".

Piece shapes live in `Models/ShapeCatalog.cs`:

- **Classic set:** Pair, Bar (3), Elbow, Block (2x2), Hook, Tee. These appear everywhere.
- **Exotic set:** Plus, Zig, Zag, Corner, Cup, Long (5 in a row), Stair. They appear in Story from level 11 on, at a share that grows with difficulty. Exotic shapes with 4+ cells are always two-coloured, because a single-colour piece of five would pop by itself the moment it landed.
- **Every shape is one solid piece:** each cell touches another cell sideways or up/down, in every rotation. There are no lines with a missing middle and no cells that touch only at a corner. Alpha 0.0.1 had a "Gap" and a "Diagonal" piece; playtesters could not read or place them, so they were removed. `ShapeCatalogTests` fails if a shape like that is ever added again.
- **Dot:** a single cell. Never drawn at random; only hand-made tutorials use it.

A random piece is one-coloured 40% of the time and two-coloured otherwise.

## 1.4 Scoring

All in `Logic/ScoringSystem.cs`, integer arithmetic only.

- A group scores 10 points per cell, plus 10 for every cell beyond the first four.
- A wave's points are multiplied by the chain position: x1 for the first wave, x2 for the second, up to a cap of x5.
- Soft drop gives 1 point per row, hard drop 2 points per row.

Why integers: scores are compared against goal targets and saved; floating point rounding would make "score 1000" occasionally mean 999.9999.

## 1.5 Game modes

### Classic

Starts from an empty board and never ends until the stack overflows. Difficulty comes from the score: the "level" is `1 + score / 800`, the fall interval shrinks 40 ms per level from 800 ms down to a floor of 140 ms, and the number of colours rises from 4 to 5 over the first couple of levels. A personal best is saved the instant it is beaten (not at game over), so a crash or a swipe-away cannot lose it.

Classic **earns gold**: every block popped pays a tenth of a gold at Classic levels 1 to 4, rising to half a gold at level 17 and above (`SeedingConfig.ClassicTenthsPerBlock`), counting every wave including chain waves, with no chain multiplier and nothing for drop points. The gold is paid ("banked") exactly once at each of three moments: game over, the player leaving Classic, and the app losing focus or going to the background; the leftover tenths are kept between runs (`classic_gold_carry`). A run can also be started from an optional **seed** (typed on the start screen under a small SEED link); the seed fixes the piece stream, a seeded run pays gold at the normal rate but never sets the personal best, and Classic never reads anything from the Story, so a Classic seed cannot affect it. See [Seeded levels, rerolls and Classic gold](04-story-mode.md#48-seeded-levels-rerolls-and-classic-gold).

### Story

The main mode, and a campaign: **200 levels in eight regions of 25** (the Forgotten Garden, the Crystal Caves, the Verdant Wilds, the Sunken City, the Clockwork Wastes, the Frozen Expanse, the Shattered Citadel and the Heart of the Spectrum), followed by an endless band, **Beyond the Spectrum**, that plays like the last region and has no scenes. The story (Pip, the Prism Heart, the Warden) is told in short skippable scenes between levels; see [Campaign and content](10-campaign-and-content.md).

Each level has:

- a **starting pile** of blocks, **generated per player** from their own seed (level 11 keeps its scripted opening piece and card),
- one to three **goals**,
- a **move limit**: the number of pieces you may place,
- a fall speed, a colour count, and a mix of shapes.

You win the moment all goals are met. You lose by running out of pieces, or by overflowing the stack. There is no timer.

**Levels are generated per player.** On first launch the player picks an in-game name (or skips; "Traveller"). The name plus a hidden random number is hashed once into a *salt*, and a level's seed comes from that salt, the level number and a reroll count, so two players get different level 40s. The seed changes the pile, the pieces and the colour of a colour goal, and nothing else: goal kinds, goal counts, flips, penalties, finales, speed and colours still come from the level number alone. A generated level is shown only if the bot can win it, a person could play the bot's plan in the real engine, and its difficulty sits in a band for that level number (gentler where a tier or a new mechanic starts). Identical levels on every device and every app version are **not** a goal. The levels of players who finished levels before this existed replay the original, number-based level (the *legacy* seed 0).

**Failing and rerolling.** A *failure* is a loss, or a manual restart after at least one piece was placed, on the player's current level only (leaving to the menu does not count, and replays never do). After the first failure the player can **reroll** the level: swap it for a new seed. A reroll costs the level's first-clear gold × (10 − failures) / 10, rounded up, so it gets cheaper with each failure and is **free from the tenth** (and always free on the opening levels and on replays of cleared levels). The failure count survives an app kill, is not reset by a reroll, and is reset when the level is cleared. Retrying the same seed is always free.

**Seeds and replays.** The seed a level is first cleared with is recorded once and shown on the levels page (tap to copy; a level cleared before this shows "Original"). Replays use it; any other seed, including one rolled on a replay, is thrown away.

Goal types (`Levels/Goal.cs`):

| Goal | Meaning |
|---|---|
| Score | Reach at least this score. |
| Pop blocks | Pop this many blocks in total. |
| Pop colour | Pop this many blocks of one colour. |
| Chain | Make a chain of this many waves from one piece. |
| Big group | Pop one group of at least this size. |
| Clear down | End with this many blocks or fewer on the board. Zero means "clear the board". |
| Chain set | Make several *separate* chains, each of at least a given length (used by the final puzzle, level 200). |

Goals are checked against the **settled** board after a piece has fully resolved, never mid-chain, so a goal cannot be met by a state that is about to change.

Difficulty tiers (numbers adjustable in `StoryConfig.cs`; they are recommendations, not hard caps) and the region that each tier sits in:

| Levels | Tier | Character |
|---|---|---|
| 1 to 10 | Opening levels | Generated Easy levels with no coaching, no gold. (The hand-made, coached tutorial is a separate ten-lesson sequence; see [Story mode](04-story-mode.md) 4.5.) |
| 11 to 50 | Easy | Four colours, low piles, generous moves. Level 11 introduces the exotic shapes. Regions 1 and 2. |
| 51 to 100 | Medium | Five colours, taller piles, fewer spare moves. Regions 3 and 4. |
| 101 to 150 | Hard | Dense scattered piles, faster pieces, often two goals. Regions 5 and 6. |
| 151 to 200 | Extreme | Upside-down boards, a points penalty for a piece that pops nothing, up to three goals, the fastest pieces. Regions 7 and 8. |
| 201 and up | Extreme, endless | Beyond the Spectrum: more of the same, no story scenes. |

Each region also leans on one idea (big groups, colour goals, tight moves, chains, unusual conditions, several goals, everything combined) through its `RegionRule`. The last level of every region is a **finale**: a little tougher and played without helpers. New goal types and mechanics are introduced one at a time with a short card (level 13 colour goals, 16 clear-down, 20 chains, 26 big groups, 32 double goals, 128 flipped board, 134 no-pop penalty, 151 extreme levels, 161 triple goals, 200 the chain-set puzzle). See [Story mode](04-story-mode.md).

### Optional memories

Two levels in each region (offsets 11 and 17 into it, never a finale) carry an **optional objective**, such as "pop a group of 8" or "finish with 2 moves to spare". Meeting it as well as the main goals uncovers one of the region's *memories* in the Archive. A memory pays no gold and is never needed to finish a level or the game. From region 5 the second memory's Chain and Big Group objectives also ask for it "without helpers".

### Extreme mechanics

- **Flipped board.** The picture is turned 180 degrees so pieces enter from the bottom of the screen. The rules are unchanged; only drawing and input are mirrored. See [Graphics](06-graphics.md#66-the-upside-down-view).
- **No-pop penalty.** When a piece locks and pops nothing, points are removed (never below zero). Because the penalty is about score, a penalty level always has a Score goal.

## 1.6 Gold and the shop

Gold comes only from playing and is spent only on things that make the game easier or prettier. It is never needed to finish the game.

**Where gold comes from** (about 14,000 over the whole campaign; see `docs/story/economy.md`):

- Clearing a **non-tutorial** level for the **first time** pays gold; harder tiers pay more (`Services/PlayerProgress.cs`, `GoldRewards`). Replaying a cleared level pays nothing, and gold is paid exactly once per level.
- Restoring a region in play pays a one-off reward (`ShopCatalog.RegionClearGold`: 50, 75, ... 225). Catching a save up quietly (`CampaignService.Reconcile`) pays nothing retroactively.
- Achievements pay small one-off amounts (`AchievementCatalog`).
- **Classic** pays by the block popped (see [Game modes](#15-game-modes)); a decent early run is about 15 gold, roughly one early reroll.

**What gold buys** (all data in `Services/ShopCatalog.cs`, `ConsumableInventory.cs`, `CosmeticCatalog.cs`):

| Where | Item | Effect |
|---|---|---|
| Workbench, HELPERS | Lookout (4 levels) | See the next 2 to 5 pieces. |
| | Pop Flair (3) | Richer pop effects. |
| | Spare Moves (3) | +2, +4, +6 pieces on every non-tutorial Story level. |
| | Colour Lens (2) | Outlines groups that are one or two blocks short of a burst. |
| | Chain Sense (1) | The landing outline glows when dropping there would start a chain. |
| | Prism Reserve (2) | Free Prism Shifts on every level that allows helpers. |
| Workbench, SUPPLIES | Second Chance | Take back the last placement. Used up. |
| | Prism Shift | Rearrange the colours of the NEXT piece. Used up. |
| Paint shelf | Skins, burst styles, boards | How blocks, pops and the board look. Never change a rule. |
| Failed or pause panel | **Reroll** | A new level when stuck. Costs less after each failure; free from the tenth. Never needed to progress. |

Some items open only after a region is restored or an achievement is earned. A locked item shows **how to find it, never its price.** Looks that are rewards (the Garden board, piece frames, Pip's accessories, the post-ending boards) cost nothing at all.

**Helpers** (Second Chance, Prism Shift) are the only things that are used up. At most 3 of each are held, a level can use at most one of each (plus Prism Reserve's free shifts), and they are off on tutorials, finales and Classic. See `docs/story/helpers.md` for the exact rules.

There is nothing to buy with real money. Gold can only be earned by playing. That is a product promise (it is also stated on the About page), not just a current limitation.

## 1.7 Controls

| Action | Touch | Button |
|---|---|---|
| Rotate | Tap the board | ROTATE |
| Move | Drag sideways | LEFT / RIGHT (hold to repeat) |
| Soft drop | Slow drag down | DOWN (hold to repeat) |
| Hard drop | Quick downward flick (at least 2.5 cells within 260 ms) | DROP |
| Pause | Pause button, or the Android Back button | |

The button row can be folded away ("HIDE CONTROLS"); the choice is remembered, and gestures always work. On a flipped board the controls follow the picture: "down" is toward the top of the screen and the DOWN button is labelled UP.

The game pauses by itself when the app loses focus or goes to the background, and nothing accumulates while paused (see [Architecture](02-architecture.md#25-time-and-the-game-loop)).

## 1.8 Product promises

These are constraints on every future change, not features:

1. **Offline.** The Android manifest requests no permissions. Release builds must not contain `INTERNET` (`scripts/Check-Permissions.ps1` checks this).
2. **No ads, no tracking, no accounts, no analytics, no crash-reporting SDK.** The only diagnostics write to the device's own log (`adb logcat -s PopBlocks`).
3. **No pay-to-win and no real-money purchases.** Gold is earned only by playing.
4. **Progress is local.** It lives in the app's private preferences; backups are disabled (`android:allowBackup="false"`). A player who changes phone moves their progress by hand with a **save code** (copied from, and pasted into, the About page; see [Architecture](02-architecture.md#26-persistence)).
5. **Respect the player's time.** No nag timers, no interruptions, no fake urgency. Every story scene can be skipped.
6. **Every level can be won with no purchases and no helpers.** Move limits are tuned for a player with nothing. Each level is verified by a bot **when it is generated** (and a person could play its plan in the real engine), a level the bot cannot win is never offered, and the tests sample thousands of generated levels against the same rules.
7. **Gold never gates the story or accessibility.** Cosmetics are discovered through the story or an achievement first, then bought; a locked item shows a hint, never a price. The one thing that spends gold on the way, the reroll, is free from the tenth failure, so a player with no gold is never stuck; its hint ("Short on gold? Pop blocks in Classic mode to earn some.") is never the only way forward.

The About page states these promises to the player. If one of them ever has to change, the About page and this document must change in the same commit.
