# Helpers: Second Chance and Prism Shift (P6)

This is the definition that `PopBlocks_spec.md` section 5.9 asks for before Prism Shift is built ("activation cost,
limits and user interface must be defined before implementation"). `PopBlocks_spec.md` has uncommitted edits of yours, so
it was left alone; fold the text below into 5.9 when convenient.

## Where they work
Story levels that are not tutorials and not the last level of a region (`LevelDefinition.AllowAbilities`, finales set it
false). Never in Classic. Neither is ever needed: the level bot plays without them.

## Second Chance (undo)
* Takes back the last placement: the board, score, goals, moves left and the pieces coming return to how they were
  when that piece appeared. The same pieces come again; the level is never rerolled.
* One step back only. Once per level, however many are held. Only while a piece is in play (not while blocks pop, not
  paused, not after the level ends). The move is not refunded twice.
* Raises no sound or tutorial event of its own except `SecondChanceUsed`.
* Run statistics are rolled back with it, so popping a big group and then taking it back does not earn a memory.
* It does not give back a Prism Shift that was used after the placement being taken back.

## Prism Shift
* Deals the colours of the NEXT piece out again in the next different order. The shape, position and the rest of the
  queue do not change. Each use moves to the next different colouring, so a larger piece can be cycled by spending more.
* A piece of one colour cannot be shifted; the button says so.
* One paid shift per level plus the free ones from the Prism Reserve upgrade (1 at region 5, 2 at region 7). Free ones are
  used first and take nothing out of stock.

## Cost and limits
Second Chance 150 gold (region 2 restored), Prism Shift 100 gold (region 4 restored). At most 3 of each held.
Stock is copied into the game when it starts and one is taken off when it is used, so quitting a level unused costs
nothing and buying mid-level changes nothing.

## Interface
Two buttons above the movement controls, shown only when the level allows helpers and the player holds one:
`UNDO x1` and `SHIFT x1` (the number is what is left this level). A disabled button says why in the line below it.
Both buttons carry a spoken description. Buying is on the workshop's SUPPLIES tab; a locked helper shows how to find it,
never a price.

The original plan asked for a tap on the NEXT box to cycle the shift; a button is used instead because the NEXT box is
a drawing and a button is reachable with a screen reader.

## Optional objectives
From region 5, the second memory of each region asks for its Chain or Big Group objective "without helpers". The bot
proves it can be met without them.
