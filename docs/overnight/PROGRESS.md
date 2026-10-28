# Progress

| Phase | Status | Commit |
|---|---|---|
| 1 Main menu | done | b403232 |
| 2 Level-end cards | done | c533cc3 |
| 3 Rerolls | done | 19f59e1 |
| 4 Loss support | done | c3eccf8 |
| 5 Pop gold, floating gold, consolation | done | 60f1b4e |
| 6 Continues | done | 729e940 |
| 7 Settings tabs and custom rules | done | 6e9e29d |
| 8 Petals vs Shards | done | 3524659 |
| 9a Block sets | done | 1254745 |
| 9b Boards and frames | done | 9cb5fd4 |
| 9c Pip looks | done | 461620f |
| 10 Story Book | done | 01a16eb |
| 11 Tall scrolling board | done (default height not flipped, see Unfinished) | 713b187 |
| 12 Pip's Insight | done | cfb2750 |
| 13 Fracture | done | a5a6cbd |
| 14 The Orb | done (Endless and Always rule; Story auto-orbs off, see Unfinished) | 5a5a440 |
| 15 Achievements | done | ada9c8b |
| 16 Performance | done | f41f667 |
| 17 Wrap-up | done | PENDING |

## Unfinished
- Phase 11: the default board height is still 16 rows. Tall boards (20-32) work through the player's own rules; making 24 the default needs the solver and the level snapshot set regenerated for it (see DECISIONS).
- Phase 14: Orbs appear in Endless and where the player's rules say Always. Orbs on their own in the Story from level 40 are off (`RuleFeatures.OrbsInStory`) because the level solver does not play them. No Orb sounds, spawn/break/corruption animations beyond the normal burst and crack effects, and Insight does not value breaking an Orb.
- Phase 16: no phone was available, so draw time was not measured and `scripts/Check-Permissions.ps1` was not run (it needs a signed Release APK). Offscreen caching of the board surface and cracked faces was left out; see `docs/overnight/PERFORMANCE.md`.
