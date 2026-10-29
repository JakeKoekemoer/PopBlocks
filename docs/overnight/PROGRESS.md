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
| 11 Tall scrolling board | done (24 rows the default since ca51ed5) | 713b187 |
| 12 Pip's Insight | done | cfb2750 |
| 13 Fracture | done | a5a6cbd |
| 14 The Orb | done (Story Orbs f9419ee; sounds, animations, Insight bc84226) | 5a5a440 |
| 15 Achievements | done | ada9c8b |
| 16 Performance | done | f41f667 |
| 17 Wrap-up | done | 4aa89c0 |

## Unfinished
- Nothing from the phases is switched off. What is still not done is only what needs a phone: draw time was not measured (there is no device here), `scripts/Check-Permissions.ps1` was not run (it needs a signed Release APK), and the new Orb sounds and animations, the 24-row default and the vector-cached grid have been checked by tests and by reading, not by eye or ear. The board surface is not a cached bitmap on purpose; see `docs/overnight/PERFORMANCE.md`.
