# PopBlocks economy

Gold is earned by playing and spent in Bram's Workshop. This page records where gold comes from, where it goes, and
the rules that keep it fair. The numbers are in code (`GoldRewards`, `ShopCatalog`, `AchievementCatalog`) and are
checked by tests (`P4ShopTests`, `P4GateTests`, `AchievementTests`); change them there and update this page.

## Rules

1. **First-clear gold is paid once per level.** Replaying a Story level, reloading a save or skipping a scene never
   pays it again. Since v1.1 popping blocks pays gold too, in Endless and in Story alike (see Income), in whole gold,
   once per banking event, with the leftover tenths carried in `classic_gold_carry`. Lessons and levels the player eased
   pay no pop gold, no first-clear gold and no consolation.
2. **Nothing needed to finish the game costs gold.** Every level 1-200 is winnable by the bot solver with zero
   purchases and zero abilities (`AllLevels_1_to_200_BotWinnable_WithNoUpgrades`). The ending does not depend on any
   optional collectible.
3. **Story and accessibility are never sold.** Archive, Journal, Achievements, settings (landing guide style, reduced
   motion, high contrast) and every scene are open from the first launch, whatever the gold or the workshop state.
4. **Cosmetics unlock through story or achievements first; gold comes second.** Gold cannot skip a story gate.
5. **Locked items show a hint, never a price.** A locked item says how to find it ("Restore the Crystal Caves").
6. **Saves are never rewritten.** A save from before this phase loads with the same gold, levels and upgrades. Region
   rewards and achievements are paid when they are earned in play, not when an old save is caught up.

## Income

Baseline income is level-clear gold only (`GoldRewards.For`), cumulative at the last level of each region pair:

| By level | 25 | 50 | 75 | 100 | 125 | 150 | 175 | 200 |
|---|---|---|---|---|---|---|---|---|
| Gold earned | 208 | 680 | 1,540 | 2,650 | 4,320 | 6,365 | 9,009 | 11,965 |

On top of that:

* **Region rewards** (`25 + 25 x region`, paid when the region's last level is cleared for the first time in play):
  50, 75, 100, 125, 150, 175, 200, 225. Total **1,100**.
* **Achievements** (13, one-off): total **910** (`AchievementCatalog.TotalGold`).
* "Beyond the Spectrum" levels above 200 keep paying level gold, so a finished game still earns.
* **Pop gold in Story** (v1.1): the same rate as Endless, by the score level the pop happens at, banked when the level is
  won (replays too), on top of the unchanged first-clear gold. A normal winning run of 60 to 100 blocks pays roughly
  8 to 20 gold. Pop gold floats up from each popped group as `+0.6` (one decimal under 10, whole numbers from 10), and
  the win card counts pop gold and first-clear gold up on two lines.
* **Loss consolation** (v1.1, `Consolation`): a lost level pays `floor(popGold / 2) + floor(firstClearReward x goalProgress / 2)`.
  The second term is paid only while the level is uncleared and is capped for good per level at that level's first-clear
  reward (`consolation_paid`), so losing can never out-earn winning. Eased levels and lessons pay none.
* **Classic** (`SeedingConfig.ClassicTenthsPerBlock`, `ClassicGold`), now called Endless in the app: tenths of a gold per block popped, by Classic level
  (`1 + score / 800`): 1 for levels 1-4, 2 for 5-8, 3 for 9-12, 4 for 13-16 and 5 from 17. Every wave counts, chain
  waves included, with no chain multiplier; drop points never count. A decent early run pays about 15 gold, roughly one
  early reroll. Paid when the game ends, when the player leaves Classic and when the app goes to the background; a
  run on a typed seed pays the same but never sets a best score. These numbers are starting values: tune them against
  real reroll prices in a playtest.

A player who finishes the campaign has earned about **14,000** gold from the Story alone; Classic is extra, and is
what a stuck player can turn to for the price of a reroll.

## Pip's Insight (v1.1)

Shop item `insight` (key `upgrade_insight`), Gameplay, no requirement: level 1 costs 250 (a button below the board, 3 uses per level), level 2 450 more (6 uses), level 3 800 more (unlimited, and an Always on switch, key `insight_always_on`, off by default): **1,500** in all. Like the new looks it is a long-term sink financed by pop gold and Endless income, so it is left out of the story-paced budget that `P7Tests` checks (level and region gold alone still buy every other upgrade and original look). It is off in lessons and on finales, and an Endless run that used it never sets the best score. It shows where the piece in hand does the most good and never plays the move.

## Easier rules pay half (v1.1)

`GameRules.GoldFactor` is 0.5 when any custom rule is easier than the normal one (slower falling, fewer colours or shapes, smaller bursts, no cracking, no Orbs, a taller board). It halves pop gold (per group, to the nearest tenth), first-clear gold (rounded down) and the consolation, on every level the rules apply to. The solver-guaranteed promise still only covers the normal rules.

## Looks sold from the first day (v1.1)

`CosmeticCatalog.NewPrice` (block sets, boards, frames and Pip looks added in v1.1) is separate from the story-paced budget (`OriginalPrice`, 2,960): those looks need no story step, cost 150 to 900 gold each, and are meant to be bought over time from Endless and Story pop gold. They are never required for anything.

## Sinks (continues)

| Phase | What | Gold |
|---|---|---|
| v1.1 | **Continue**: five more moves on a Story level lost by running out of moves (30, 42, 55; at most three per attempt; never on a finale or an eased level) | recurring, never needed to finish the game |

## Sinks

| Phase | What | Gold |
|---|---|---|
| Before | Lookout 1-2, Pop Flair 1-3, Spare Moves 1-3 (the original shop) | 3,050 |
| P4 | Lookout 3-4 (900, 1,500), Colour Lens (500, 1,100), Chain Sense (700) | 4,700 |
| P5 | Cosmetics: 7 skins (80-320), 6 bursts (60-260), 6 boards (50-150) | 2,960 (`CosmeticCatalog.TotalPrice`) |
| P6 | Prism Reserve (600 + 1,200) and the two helpers (Second Chance 150, Prism Shift 100 each, up to 3 held) | 1,800 for the upgrade, then recurring: at most 2 x 3 held = 750 gold of stock at a time |
| Seeded levels | **Reroll**: the level's first-clear gold × (10 − failures) / 10, rounded up, free from the 10th failure, always free on the opening levels and on replays of cleared levels (`RerollPricing`) | 11 to 114 for a first reroll on levels 12 to 180; **0** at 10 failures; recurring, but never needed |

Everything above adds up to roughly **12,500**, which leaves slack: a player who plays the whole campaign,
finds some memories and earns most achievements can afford the lot by the end, and a player who skips optional content
still buys the helpers they care about.

## Pacing

* The first purchase is affordable around level 15-20 (Pop Flair at 80 gold).
* The mid-game upgrades (Lookout 2, Spare Moves 2, Colour Lens) land when their region is restored, one every few
  hours of play.
* The new tiers are gated by region (`Lookout 3` needs region 3, `Lookout 4` needs region 6, `Colour Lens` needs
  regions 2 and 4, `Chain Sense` needs region 3) so a new player is not shown a wall of prices they cannot meet.

## What the helpers do (and do not do)

* **Lookout** shows 1 to 5 upcoming pieces. It changes what you can see, not what happens.
* **Colour Lens** outlines groups a block or two short of a burst (`ColourMatcher.FindNearGroups`). It only shows what
  is already on the board.
* **Chain Sense** glows the landing outline when dropping where the piece is now would start a chain
  (`PlacementPreview.CountWaves`, tested to equal a real drop). It never moves a piece or picks a move.
* **Spare Moves** adds moves on every non-tutorial Story level, finales included. Tutorials are untouched.

## Achievements

All local to the device and none needs grinding. Rules are pure functions over `AchievementFacts`, stored as
`ach_<id>` in the unlock store; gold is paid at the moment the id is first stored.

| Achievement | Rule | Gold |
|---|---|---|
| First Connection | Pop a group | 20 |
| Aftershock | A chain of 2 | 20 |
| A Bigger Picture | A group of 8 or more | 40 |
| Careful Planner | Win a Story level with 3+ moves spare | 40 |
| Restorer | Restore one region | 50 |
| Keeper of Memories | 20 Archive entries | 60 |
| Spectrum Seeker | A memory in every region | 150 |
| Balanced World | Finish the campaign | 150 |
| Classic Champion | 3,000 in Classic | 100 |
| Chain Master | A 5-wave chain | 80 |
| Clean Sweep | Win with an empty board | 80 |
| Perfectionist | Every memory of one region | 100 |
| Puzzle Scholar | Finish the ten tutorials | 20 |

*Perfectionist is read as "every memory in one region" rather than "every level perfect", because levels have no
star rating.* Achievements already deserved by an existing save are paid the next time a run ends, not at load.

## Changing a number

1. Change it in code. 2. Re-run the tests: the budget tests (`Total_achievement_gold_is_in_the_economy_budget`,
`Every_gameplay_item_is_optional_and_every_price_is_reachable`) fail if income and spend drift apart. 3. Update this
page. Never change a save key.

## Cosmetics

* A look is **discovered** through the story or an achievement and only then can it be **bought**. Locked looks show a
  hint ("Restore the Crystal Caves"), never a price.
* **Rewards cost nothing.** The Garden board (region 1), the Whole Spectrum skin (region 8), the three post-ending boards
  (campaign finished), the piece frames and Pip's accessories (achievements) and the High Contrast board (always) are
  yours as soon as their requirement is met. They are never sold.
* Region N reveals: 1 nature, 2 crystal, 3 leaf, 4 water, 5 clockwork, 6 ice, 7 citadel, 8 Spectrum.
* A skin may change shade, corner, border, highlight and texture, but never a colour's hue or its symbol. The tests check
  hue distance, perceived difference between the five colours, and symbol contrast for every skin.
* Cut from the plan for want of an on-device frame-time check: trail effects, chain celebration and victory effects.
  Nothing is for sale that the game does not draw.

## Helpers (P6)

* Second Chance (150 gold, opens when region 2 is restored) and Prism Shift (100 gold, region 4) are the only things in the
  game that are used up. A player holds at most **3** of each, and a level can use at most **one** of each (plus the
  free shifts of Prism Reserve), so stock cannot be hoarded into a way to play a level for you.
* They are off on tutorials, on the last level of every region, and in Classic. Level gold is paid on first clear whether
  or not a helper was used.
* Prism Reserve adds free Prism Shifts per level (1 at region 5, 2 at region 7). It spends no stock.
* No level needs a helper. The bot that proves each level winnable plays without them (see `HelperTests`).
* A level that is left without using a helper costs nothing; stock is taken only when a helper is used.
