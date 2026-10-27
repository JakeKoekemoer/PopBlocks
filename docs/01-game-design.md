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
- **Exotic set:** Plus, Zig, Zag, Corner, Cup, Long (5 in a row), Stair, Diagonal (two cells touching only at a corner), Gap (two cells with a hole between). They appear in Story from level 11 on, at a share that grows with difficulty. Exotic shapes with 4+ cells are always two-coloured, because a single-colour piece of five would pop by itself the moment it landed.
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

### Story

Numbered levels. Each level has:

- a **starting pile** of blocks (hand-drawn for levels 1 to 11, generated afterwards),
- one to three **goals**,
- a **move limit**: the number of pieces you may place,
- a fall speed, a colour count, and a mix of shapes.

You win the moment all goals are met. You lose by running out of pieces, or by overflowing the stack. There is no timer.

Goal types (`Levels/Goal.cs`):

| Goal | Meaning |
|---|---|
| Score | Reach at least this score. |
| Pop blocks | Pop this many blocks in total. |
| Pop colour | Pop this many blocks of one colour. |
| Chain | Make a chain of this many waves from one piece. |
| Big group | Pop one group of at least this size. |
| Clear down | End with this many blocks or fewer on the board. Zero means "clear the board". |

Goals are checked against the **settled** board after a piece has fully resolved, never mid-chain, so a goal cannot be met by a state that is about to change.

Difficulty tiers (numbers adjustable in `StoryConfig.cs`; they are recommendations, not hard caps):

| Levels | Tier | Character |
|---|---|---|
| 1 to 10 | Tutorials | Hand-made, coached, no gold. |
| 11 to 50 | Easy | Four colours, low piles, generous moves. Level 11 introduces the exotic shapes. The last ten levels have variants (rainbow, tall pile, tight moves, shape-heavy, scattered). |
| 51 to 100 | Medium | Five colours, taller piles, fewer spare moves. |
| 101 to 150 | Hard | Dense scattered piles, faster pieces, often two goals. |
| 151 and up | Extreme | Upside-down boards, a points penalty for a piece that pops nothing, up to three goals, the fastest pieces. Endless. |

New goal types and mechanics are introduced one at a time with a short card (level 13 colour goals, 16 clear-down, 20 chains, 26 big groups, 32 double goals, 151 flipped board, 156 no-pop penalty, 161 triple goals). See [Story mode](04-story-mode.md).

### Extreme mechanics

- **Flipped board.** The picture is turned 180 degrees so pieces enter from the bottom of the screen. The rules are unchanged; only drawing and input are mirrored. See [Graphics](06-graphics.md#66-the-upside-down-view).
- **No-pop penalty.** When a piece locks and pops nothing, points are removed (never below zero). Because the penalty is about score, a penalty level always has a Score goal.

## 1.6 Gold and the shop

- Clearing a **non-tutorial** level for the **first time** pays gold. Harder tiers pay more (`Services/PlayerProgress.cs`, `GoldRewards`).
- Replaying a cleared level pays nothing. Tutorials pay nothing. Gold is paid exactly once per level, at the moment the level is completed.
- Gold buys permanent upgrades (`Services/Shop.cs`):

| Upgrade | Levels | Effect |
|---|---|---|
| Lookout | 2 | See the next 2, then 3 upcoming pieces. |
| Pop Flair | 3 | Richer pop effects: shockwave, sparks and screen shake, confetti. |
| Spare Moves | 3 | +2, +4, +6 pieces on every non-tutorial Story level. |

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
4. **Progress is local.** It lives in the app's private preferences; backups are disabled (`android:allowBackup="false"`).
5. **Respect the player's time.** No nag timers, no interruptions, no fake urgency.

The About page states these promises to the player. If one of them ever has to change, the About page and this document must change in the same commit.
