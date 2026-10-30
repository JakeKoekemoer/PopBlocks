# Achievement icon art: prompts

The Trophies tab shows an icon beside each achievement. The app picks up an icon automatically when a file named
`ach_<id>.png` is in `src/PopBlocks/Resources/Images/` (for example `ach_first_connection.png`). An achievement with no
file just shows its text, so the icons can be added in any order. Each icon is shown about 84 dp square, so square
tiles of 340 px or more are plenty.

## Style (paste at the start of every batch)

> A sheet of mobile puzzle-game achievement icons, in the same style as the PopBlocks shop icons: glossy, painterly 3D
> game art with vivid neon glow and soft rim light, rich jewel colours (cyan, coral, amber, violet, lime) on a deep navy
> blue background, each icon on its own square dark-navy tile with rounded corners and a faint glowing edge. Friendly and
> a little magical, not realistic. Pip is a small round blue cat-like creature with big eyes. No text, numbers or letters
> inside any tile. Keep every subject centred with space around it so it can be cropped square. Lay the tiles out as a
> grid of 4 columns by 2 rows with a gap between them, and put the achievement's name in small clean white text under each tile.

## Batch 1: first steps (8)

| File | Name | Picture |
|---|---|---|
| ach_first_connection | First Connection | Two glowing blocks joining with a bright spark between them |
| ach_aftershock | Aftershock | A block bursting and setting off a second burst, with ripple rings |
| ach_bigger_picture | A Bigger Picture | A big cluster of same-coloured blocks merging into one large burst |
| ach_careful_planner | Careful Planner | A blueprint of blocks with a pencil and ruler, a small thinking cap on top |
| ach_restorer | Restorer | A dark lamp relighting, with a green sprout growing from a restored tile |
| ach_keeper_of_memories | Keeper of Memories | An open storybook with a glowing memory lantern rising from its pages |
| ach_spectrum_seeker | Spectrum Seeker | A rainbow prism with eight small coloured lights circling it |
| ach_balanced_world | Balanced World | A pair of golden scales balancing a small glowing globe |

## Batch 2: skill (8)

| File | Name | Picture |
|---|---|---|
| ach_classic_champion | Endless Champion | A golden trophy with a glowing infinity loop around it |
| ach_chain_master | Chain Master | A chain of five glowing links crackling with sparks |
| ach_clean_sweep | Clean Sweep | A broom sweeping an empty board, leaving a trail of sparkles |
| ach_perfectionist | Perfectionist | A gold medal with a flawless cut gem in its centre |
| ach_puzzle_scholar | Puzzle Scholar | A graduation cap resting on a stack of puzzle blocks |
| ach_orb_breaker | Orb Breaker | A glowing orb cracking open with light pouring out |
| ach_orb_collector | Crystal Collector | A wooden bowl heaped with glowing orbs and crystals |
| ach_close_call | Close Call | A cracking orb beside an hourglass with one last grain of sand |

## Batch 3: cracks and comebacks (8)

| File | Name | Picture |
|---|---|---|
| ach_hairline | Hairline | A single block with one fine glowing crack across it |
| ach_shatterer | Shatterer | A block exploding into many bright fragments, a small hammer beside it |
| ach_fault_lines | Fault Lines | A block with branching glowing cracks and a small tremor arc |
| ach_nine_lives | Not Done Yet | A heart glowing as it rises back up from a small flame |
| ach_full_hearts | Full of Heart | A row of three full, glowing hearts |
| ach_roll_with_it | Roll With It | A block circled by two curved reroll arrows, with a pair of dice |
| ach_stubborn | Stubborn | A scuffed, determined stone block with a small flag planted on top |
| ach_gold_rush | Gold Rush | A heap of gold coins and nuggets with sparkles |

## Batch 4: the rest (5)

Ask for a 4 by 2 grid again and leave the last three tiles empty, or ask for 5 tiles in one row.

| File | Name | Picture |
|---|---|---|
| ach_deep_dive | Deep Dive | A tall board with a downward arrow and rising bubbles |
| ach_endless_night | Endless Night | A crescent moon over a field of stars, with a small infinity sign |
| ach_rule_maker | Rule Maker | A scroll of rules with a pen and three slider knobs |
| ach_dressed_up | Dressed Up | Pip wearing sunglasses and a striped party hat |
| ach_spread_the_word | Spread the Word | A paper plane leaving a trail of sparkles |

## After generating

Send the sheets back and ask for them to be sliced and added. Each tile is cropped, saved under the file name in the
tables above, and dropped into `Resources/Images`. Rebuild and the icons appear on the Trophies tab.
