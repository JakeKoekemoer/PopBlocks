# PopBlocks

An offline falling-piece colour puzzle for Android, built with C# and .NET MAUI. Full spec: `PopBlocks_spec.md`. Developer documentation (functionality, class libraries and design rationale): [`docs/`](docs/README.md).

Connect four or more cells of one colour (sideways or up/down, not diagonally) and they burst. The rest settle downward, which can burst more: chains score up to ×5.

## Layout

```
PopBlocks.sln
src/
  PopBlocks.Engine/     Pure C# rules: board, pieces, collision, matching, gravity, scoring, game state, undo, board generation, Story levels and the
                        level solver; the campaign (regions, scenes, rewards, ending); the shop, helpers, cosmetics, achievements and the save code.
                        No MAUI references, so it is unit-testable without Android.
  PopBlocks/            The .NET MAUI app (net10.0-android): drawing, view model, pages, platform code.
    Graphics/           VisualDesign (palette, skins), CellRenderer, GameDrawable (board, neon pops, flip, boards, frames), BlastEffects (burst styles, flair), Pip and the story pictures
    ViewModels/         GameViewModel (observable state, commands, game timer, tutorial coaching)
    Views/              MainPage (menu), LevelSelectPage, GamePage (board, HUD, goals, helpers, controls drawer, overlays), WorkshopPage, ShopPage,
                        WardrobePage, ArchivePage, JournalPage, AchievementsPage, AboutPage (with the save code), DevMenuPage (development builds)
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

**Story** is the main mode, a campaign of **200 levels in eight regions** (the Forgotten Garden to the Heart of the Spectrum) followed by an endless band, Beyond the Spectrum. You restore a broken world, guided by Pip, and the story is told in short scenes that can always be skipped. Every level starts with a preset pile of blocks, gives you goals, and a limited number of pieces (moves). Meet every goal before the pieces run out to clear the level and unlock the next one. You lose if you run out of moves, or if the stack reaches the top.

| Levels | Tier | What changes |
|---|---|---|
| 1-10 | Opening | Ordinary Story levels that start playing at once, with no coaching. Easy piles, generous moves. No gold. |
| 11-50 | Easy | Four colours, low piles, generous move limits. Level 11 introduces non-Tetris shapes. New goal types arrive one at a time with a short card. Regions 1-2. |
| 51-100 | Medium | Five colours, taller piles, more pieces from the exotic set, fewer spare moves. Regions 3-4. |
| 101-150 | Hard | Dense scattered piles, faster pieces, two goals at once. Regions 5-6. |
| 151-200 | Extreme | Boards drawn **upside down**, a **penalty** for a piece that pops nothing, three goals at once, the fastest pieces. Regions 7-8; level 200 is a final puzzle of several separate chains. |

Each region has its own character (big groups, colour goals, tight moves, chains, unusual conditions, several goals, everything combined), set in one table, `Story/RegionRules.cs`. The last level of every region is a slightly tougher **finale**, played without helpers. Two levels in each region carry an optional objective that uncovers a **memory** in the Archive; these never block progress.

Goals: reach a score, pop N blocks, pop N blocks of one colour, make a chain of N, pop one group of N, leave N blocks or fewer ("clear the board" when N is 0), or make N separate chains. Several can be combined.

Every generated level is **proven winnable**: a bot plays it first, and the move limit is set from the number of pieces the bot needed plus slack that shrinks as the tiers get harder (`LevelCatalog.cs`, `LevelSolver.cs`). The bot plays with **no upgrades and no helpers**, so the whole campaign can be finished without buying anything. The test suite replays the bot's plan through the real engine for every level.

**The tutorial** is separate from the Story. It is ten hand-made, coached lessons (moving, rotating, dropping, speed, mixed colours, chains, moves, goals) with their own progress and no gold. A new player is asked once, on Story level 1, whether they want it; it can also be played any time from **More → TUTORIAL**. Lessons never move Story progress, play story scenes or count towards achievements and lifetime bests (`LevelCatalog.Lesson`, `GameSetup.Lesson`, `TutorialProgress`).

**The menu** opens on just **PLAY** (continue the Story; level 1 for a new player) and **MORE**, which shows the full menu: Story card, map, Classic, workshop, archive, levels, tutorial, settings and about.

**Gold and Bram's workshop.** Clearing a non-tutorial level for the **first time** pays gold; harder tiers pay more, restoring a region pays a one-off reward, and achievements pay small amounts. Replaying a level pays nothing. The workshop grows a lamp for every region you restore:

| Where | What | Effect |
|---|---|---|
| Workbench | Lookout, Pop Flair, Spare Moves | See up to 5 pieces ahead; richer pops; +2/+4/+6 moves per level |
| | Colour Lens, Chain Sense | Outline groups a block or two short of a burst; glow when a drop would start a chain |
| | Prism Reserve | Free Prism Shifts per level |
| | Second Chance, Prism Shift (supplies, up to 3 each) | Take back your last block; rearrange the colours of the next piece. Once per level, off on finales |
| Paint shelf | Skins, burst styles, boards, piece frames, Pip's accessories | How the game looks. Found through the story or an achievement, then bought; some are free rewards |

A locked item tells you how to find it and never shows a price. Prices, rewards and requirements are data (`Services/ShopCatalog.cs`, `ConsumableInventory.cs`, `CosmeticCatalog.cs`); the reasoning is in `docs/story/economy.md`. There is nothing to buy with real money.

**Save code.** The About page can copy your progress as one line of text and restore it on another phone (the app keeps no cloud backup, by design).

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

Covers collision, matching, gravity, scoring, chains, pause/restart/game over, best-score persistence, Story rules (move limits, every goal type, penalties, timed pop phases, previews), the tutorials (each is replayed to completion), gold and the shop, level generation, a check that the bot can clear every level from 1 to 220 through the real engine, the campaign (scenes, rewards, the ending), undo and the helpers, cosmetics, achievements, and the save code and save-compatibility rules.

## Tuning

All balance numbers (speed curve, level step, points, chain cap, board size) are in `src/PopBlocks.Engine/GameConfig.cs`. Story tiers, goals and move limits are in `StoryConfig.cs` and `Levels/LevelCatalog.cs`; pile shapes are in `DifficultyProfile` in `Logic/BoardGenerator.cs`. All colours and drawing proportions are in `src/PopBlocks/Graphics/VisualDesign.cs`.

## Not built

Haptics (would need the VIBRATE permission), music, Burst Charge, online features of any kind, other platforms. Trail effects, a chain celebration and victory effects were planned for the looks and cut; challenge levels outside the 200 are an optional later addition.

## Sound

Four short single-voice (mono-tone) effects: **rotate** (high tick), **place** (low thud), **hard drop** (falling whoosh ending in the thud) and **pop** (falling bloop, one tone higher for each step of a chain). They are generated in code (`PopBlocks.Engine/Audio/ToneSynth.cs`), so there are no audio files, and played through Android's `SoundPool` (`Platforms/Android/AndroidSoundPlayer.cs`). A **Sound effects** switch on the menu turns them off. No permission is needed.

## License

Copyright (c) 2026 Jake Koekemoer. All rights reserved. PopBlocks is proprietary: you may not copy, modify, distribute or sell it, or use any of its code or assets, without written permission. See [`LICENSE`](LICENSE).
