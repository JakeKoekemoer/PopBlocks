# Seed sweep notes

What the difficulty band was measured from, what was chosen, and what is still to be checked on a phone. How to rerun it: [Extending](../09-extending.md#915-retune-the-difficulty-band).

## The sweep

`docs/story/seed-sweep.csv`: 11,000 rows, 50 candidate seeds for each of levels 1 to 220 (`Mix32(20260101, level, 0, i)`), written by `SeedSweep.Run_the_sweep_when_asked`. Columns: `level, seed, verified, memoryOk, fullNeed, weakNeed, hardness, startBlocks, buildMs, weakMs`.

- `verified` = the full bot wins the level **and** a person could play its plan in the real engine (`LevelSolver.PlanIsPlayable`). 10,991 of 11,000 candidates pass; 9 do not (2 fell back to the unverified plain level, 7 had a plan the real engine cannot carry out because a tall pile blocks a slide). Roughly one candidate in 1,200 is rejected, so a roll almost never needs more than a few attempts.
- `hardness = min(2, weakNeed / fullNeed)`. The weak bot takes the single best placement with no look-ahead. About 11% of candidates have hardness below 1 (the weak bot happened to do better than the full bot); they sit under every band and are rejected as "too easy".

| Tier | Hardness 20th / 50th / 80th percentile |
|---|---|
| Easy (1-50) | 1.00 / 1.00 / 1.25 |
| Medium (51-100) | 1.00 / 1.14 / 1.67 |
| Hard (101-150) | 1.00 / 1.25 / 1.95 |
| Extreme (151-220) | 1.03 / 1.37 / 2.00 (capped) |

## What was chosen

| Parameter | Value | Where |
|---|---|---|
| Band | 20th to 80th percentile of hardness, pooled over the level and five levels each side inside its tier | `SeedSweep.BandLow/BandHigh/SmoothingWindow` |
| Soft starts | Upper edge held to the median of the previous five levels, for the first five levels of the Medium, Hard and Extreme tiers (51, 101, 151) and for each introduction level plus the two after it (11, 13, 16, 20, 26, 32, 128, 134, 151, 161); if that would empty the band, accept up to its lower edge | `SeedSweep.ComputeBand` |
| Guard rails | 10th to 90th percentile of pile size and solve length over the same window. A **preference**: inside the rails always beats outside, but a roll never fails because every candidate is an outlier | `LevelRoller` |
| Search | 24 attempts or 2 seconds, then the verified candidate closest to the band | `SeedingConfig` |

One change from the first reading of the spec: the guard rails are per level (a window of neighbouring levels), not per tier. A tier ramps up from its first level to its last, so a whole-tier 10th-to-90th rail would reject every candidate at the end of the Easy tier (levels 42 to 50 need more pieces than the tier's 90th percentile).

## Timing

Measured on the desktop that ran the sweep, with all cores busy, so the numbers are pessimistic for one roll:

| Tier | Build + weak bot per candidate: median / 95th / slowest |
|---|---|
| Easy | 4 / 19 / 53 ms |
| Medium | 16 / 55 / 633 ms |
| Hard | 39 / 143 / 876 ms |
| Extreme | 52 / 182 / 1,109 ms |

A roll is usually one to three candidates.

The full fuzz (`SeedFuzzTests`, 5,000 rolls per tier, all gate assertions passing) timed whole rolls on the same machine, again while other test runs shared the CPU (`docs/story/seed-fuzz-stats.txt`):

| Tier | Whole roll: median / 95th / slowest |
|---|---|
| Easy | 8 / 79 / 898 ms |
| Medium | 34 / 210 / 1,258 ms |
| Hard | 87 / 1,173 / 2,932 ms |
| Extreme | 108 / 810 / 2,150 ms |

The Hard tier's 95th percentile is already over a second on a desktop under load, so expect more on a low-end phone: the loading card is what covers that wait, and the attempt count is the lever if the phone timing is too slow.

**Not yet done: the same measurement on a low-end Android phone.** The dev menu has **TIME THE GATE ON THIS PHONE** (`GateTiming`), which rolls eight levels per tier and reports the median, the 95th percentile and the slowest. If the 95th percentile is above about one second on the target phone, lower `SeedingConfig.RollAttempts` (24) or drop the weak-bot run for the levels that do not need it, then rerun the gate tests. Until then the 2-second search budget is the safety net: a roll that runs out of time takes the closest verified candidate it has, and the loading card covers the wait.

## Still to do by hand

- The playtest of the band edges: five seeds at each edge on three levels per tier, against `p3-playtest.md` to `p6-playtest.md`. Hardness is a proxy for how hard a person finds a board.
- Tuning the Classic gold rate against real reroll prices (the target is that a decent early run pays about 15 gold, roughly one early reroll).
