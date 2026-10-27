# PopBlocks developer documentation

Reference material for anyone working on PopBlocks. It describes **how the game works and why it is built the way it is**, not the history of how it got here. If you are about to change something, find the matching document below first: most of the "why" is in the sections called *Design decisions*.

| # | Document | Read it when you want to know... |
|---|---|---|
| 1 | [Game design](01-game-design.md) | What the game is, the rules, the modes, the scoring, and the promises the product makes (offline, no ads). |
| 2 | [Architecture](02-architecture.md) | How the solution is split, which project may depend on which, how a game tick flows through the layers, and the main design rules. |
| 3 | [Engine reference](03-engine-reference.md) | Every class in `PopBlocks.Engine`: what it owns, its contract, and the decisions behind it. |
| 4 | [Story mode, levels, gold and shop](04-story-mode.md) | How levels are defined and generated, how the solver proves them winnable, the tutorial system, tiers, goals, gold and upgrades. |
| 5 | [App layer (MAUI)](05-app-layer.md) | Pages, the view model, input handling, dependency injection, persistence, navigation and the Android project. |
| 6 | [Graphics and visual design](06-graphics.md) | How the board is drawn, the pop animation pipeline, the upside-down view, the palette, and the Android drawing traps. |
| 7 | [Testing](07-testing.md) | What the tests cover, how they are built, and how to write a new one. |
| 8 | [Build, run and release](08-build-and-release.md) | Tooling requirements, running on a phone or emulator, making an APK, the no-internet check, versioning, logs and repository hygiene. |
| 9 | [Extending the game](09-extending.md) | Step-by-step checklists: add a shape, a goal type, a tier, an upgrade, a tutorial step, a setting, a region rule, a cosmetic, a helper. |
| 10 | [Campaign and content](10-campaign-and-content.md) | The eight regions and the ending, where the story text lives and how to edit it, what the player is given and when, and the rules that keep the campaign fair. |

The original product specification is `PopBlocks_spec.md` in the repository root. The spec says *what was asked for*; these documents say *what exists*. Where they disagree, the code and these documents win. Design notes for the campaign (the story bible, the economy, the helper rules and the playtest checklists) are in [`story/`](story/).

## The one-minute version

- PopBlocks is a falling-block colour puzzle for Android. Pieces of coloured cells fall; four or more cells of one colour that touch sideways or vertically burst; what is left falls down, which can burst more (a chain).
- **Story** (the main mode) is a 200-level campaign in eight regions, then an endless band. Each level has goals and a limited number of pieces; a short, skippable story (Pip, the Prism Heart, the Warden) runs through it. Gold from first clears buys upgrades, helpers and looks in Bram's workshop. **Classic** is endless play for a high score.
- The solution has three projects. **`PopBlocks.Engine`** holds every rule and has no UI dependency. **`PopBlocks`** is the .NET MAUI app: it draws the engine's state and turns touches into engine calls. **`PopBlocks.Tests`** tests the engine without a phone.
- The engine has no clock. The app calls `Tick()` on a timer and asks the engine how long to wait next.
- Levels are generated from the level number, so they are the same on every device. A bot proves each generated level can be won before its move limit is set.
- Everything stays on the device. There is no network permission, no ads, no analytics and no account.
