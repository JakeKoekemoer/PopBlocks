# PopBlocks

An offline falling-piece colour puzzle for Android, built with C# and .NET MAUI. Full spec: `PopBlocks_spec.md`. Developer documentation (functionality, class libraries and design rationale): [`docs/`](docs/README.md).

Connect four or more cells of one colour (sideways or up/down, not diagonally) and they burst. The rest settle downward, which can burst more: chains score up to ×5.

## Layout

```
PopBlocks.sln
src/
  PopBlocks.Engine/     Pure C# rules: board, pieces, collision, matching, gravity, scoring, game state, board generation, Story levels and the level solver.
                        No MAUI references, so it is unit-testable without Android.
  PopBlocks/            The .NET MAUI app (net10.0-android): drawing, view model, pages, platform code.
    Graphics/           VisualDesign (palette + constants), CellRenderer, GameDrawable (board, neon pops, flip), BlastEffects (burst, flair)
    ViewModels/         GameViewModel (observable state, commands, game timer, tutorial coaching)
    Views/              MainPage (menu), LevelSelectPage, ShopPage, AboutPage, GamePage (board, HUD, goals, controls drawer, overlays)
    Services/           LocalSettingsService (MAUI Preferences)
tests/
  PopBlocks.Tests/      xUnit tests for the engine
scripts/
  Check-Permissions.ps1 Release check: no INTERNET permission
```

## Run on a Samsung S24

1. Install **Visual Studio 2026** (version 18 or newer; Visual Studio 2022 cannot build `net10.0`) with the **.NET Multi-platform App UI development** workload (needs the .NET 10 SDK and Android SDK).
2. On the phone: Settings → About phone → Software information → tap **Build number** 7 times. Then Settings → Developer options → turn on **USB debugging** (or **Wireless debugging**).
3. Connect the phone, accept the "Allow USB debugging" prompt.
4. Open `PopBlocks.sln`, set **PopBlocks** as the startup project, choose your S24 from the Android device dropdown, press **F5**.

Debug builds run through the debugger. For a standalone install use a Release build (below).

## Game modes

**Story** is the main mode. Every level starts with a preset pile of blocks, gives you goals, and a limited number of pieces (moves). Meet every goal before the pieces run out to clear the level and unlock the next one. You lose if you run out of moves, or if the stack reaches the top.

| Levels | Tier | What changes |
|---|---|---|
| 1-10 | Lessons | Hand-made tutorials with interactive coaching: moving, rotating, dropping, speed, mixed colours, chains, moves, goals. No gold. |
| 11 | Easy | Introduces non-Tetris shapes (plus, diagonal, long, stairs...). |
| 12-50 | Easy | Four colours, low piles, generous move limits. New goal types are introduced one at a time with a short card. Variants toward the end: rainbow, tall, tight, shape-heavy and scattered piles. |
| 51-100 | Medium | Five colours, taller piles, more pieces from the exotic set, fewer spare moves. |
| 101-150 | Hard | Dense scattered piles, faster pieces, two goals at once. |
| 151+ | Extreme | Boards that are drawn **upside down** (pieces enter from the bottom of the screen), a **penalty** for a piece that pops nothing, three goals at once, the fastest pieces. |

The level numbers are tuning values, not caps: change them in one place (`StoryConfig.cs`). Levels are generated from the level number, so they are identical on every device and endless.

Goals: reach a score, pop N blocks, pop N blocks of one colour, make a chain of N, pop one group of N, or leave N blocks or fewer ("clear the board" when N is 0). Several can be combined.

Every generated level is **proven winnable**: a bot plays it first, and the move limit is set from the number of pieces the bot needed plus slack that shrinks as the tiers get harder (`LevelCatalog.cs`, `LevelSolver.cs`). If the bot cannot reach the goals they are eased until it can. The test suite replays the bot's plan through the real engine for levels 1-220.

**Gold and the shop.** Clearing a non-tutorial level for the **first time** pays gold; harder tiers pay more. Replaying a level pays nothing. Gold buys permanent upgrades in the shop:

| Upgrade | Levels | Effect |
|---|---|---|
| Lookout | 2 | See the next 2, then 3 pieces |
| Pop Flair | 3 | Shockwaves, then sparks and screen shake, then a confetti finale |
| Spare Moves | 3 | +2, +4, +6 moves on every non-tutorial Story level |

Prices and rewards are in `Services/Shop.cs` and `Services/PlayerProgress.cs`.

**Classic** starts from an empty board and speeds up with your score. It keeps a personal best.

**Pops.** A match first lights up in its own colour, turns solid neon, and then bursts. Pop effects can be switched off on the menu (pops are then instant).

## Controls

| Action | Touch | Button |
|---|---|---|
| Rotate | Tap the board | ROTATE |
| Move | Drag sideways | LEFT / RIGHT (hold to repeat) |
| Soft drop | Slow drag down | DOWN (hold to repeat) |
| Hard drop | Quick flick down | DROP |
| Pause | II button, or the Back button | |

The row of buttons under the board folds away with the **HIDE CONTROLS** handle (it remembers your choice); the gestures always work. On upside-down levels the controls follow the picture: drag toward the top of the screen to drop.

The game pauses by itself if the app loses focus (call, app switch, screen off).

## Build a standalone Release APK

```powershell
dotnet publish src/PopBlocks/PopBlocks.csproj -f net10.0-android -c Release
```

The `.apk` is written under `src/PopBlocks/bin/Release/net10.0-android/publish/`. Copy it to the phone and open it (allow "install unknown apps" for your file manager).

For a properly signed build you control, create a key once and keep it safe (never commit it):

```powershell
keytool -genkeypair -v -keystore popblocks.keystore -alias popblocks -keyalg RSA -keysize 2048 -validity 10000

$env:POPBLOCKS_KEY_PASS = "<your password>"
dotnet publish src/PopBlocks/PopBlocks.csproj -f net10.0-android -c Release `
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=popblocks.keystore -p:AndroidSigningKeyAlias=popblocks `
  -p:AndroidSigningKeyPass=env:POPBLOCKS_KEY_PASS -p:AndroidSigningStorePass=env:POPBLOCKS_KEY_PASS
```

## Offline and privacy checks (spec section 10)

- The source manifest declares **no permissions**. Debug builds still get `INTERNET` added automatically by Android tooling for the debugger; Release builds should not.
- After building Release, run `scripts\Check-Permissions.ps1 -Apk <path to apk>`; it fails if `INTERNET` is present.
- The only package dependencies are those the .NET MAUI SDK brings in. Review with `dotnet list src/PopBlocks/PopBlocks.csproj package --include-transitive`.
- Then test in airplane mode, and confirm the best score survives closing and reopening the app.

## Tests

```powershell
dotnet test
```

Covers collision, matching, gravity, scoring, chains, pause/restart/game over, best-score persistence, Story rules (move limits, every goal type, penalties, timed pop phases, previews), the tutorials (each is replayed to completion), gold and the shop, level generation, and a check that the bot can clear every level from 1 to 220 through the real engine.

## Tuning

All balance numbers (speed curve, level step, points, chain cap, board size) are in `src/PopBlocks.Engine/GameConfig.cs`. Story tiers, goals and move limits are in `StoryConfig.cs` and `Levels/LevelCatalog.cs`; pile shapes are in `DifficultyProfile` in `Logic/BoardGenerator.cs`. All colours and drawing proportions are in `src/PopBlocks/Graphics/VisualDesign.cs`.

## Not yet built (deferred by the spec)

Sound, haptics (would need the VIBRATE permission), Prism Shift / Burst Charge, other platforms.
