# 5. App layer (.NET MAUI)

Code: `src/PopBlocks/`. Target: `net10.0-android`, minimum Android 7.0 (API 24), portrait only.

```
App.xaml / App.xaml.cs       resources (palette, brushes, styles) and window creation
MauiProgram.cs               dependency injection registrations
Diagnostics.cs               logcat breadcrumbs and unhandled-exception logging
Views/                       MainPage, WorldMapPage, WorkshopPage (and its panels), SettingsPage, AboutPage, GamePage
ViewModels/                  GameViewModel, ObservableObject
Graphics/                    drawing code (see doc 6)
Services/                    LocalSettingsService (MAUI Preferences)
Platforms/Android/           MainActivity, MainApplication, AndroidManifest.xml, colours
Resources/                   app icon and splash (SVG)
```

## 5.1 Start-up sequence

1. `MainApplication.OnCreate` installs `Diagnostics` (unhandled-exception logging).
2. `MauiProgram.CreateMauiApp` registers services (see [Architecture](02-architecture.md#23-dependency-injection-and-start-up)), builds the container, then runs `PersistentSettings.RestoreIfFresh()` (a reinstall picks its progress up from the save file), `SaveMigrator`, and `CampaignService.Reconcile`, and finally asks for the save file to be brought up to date.
3. `App` constructor loads `App.xaml` (colours, brushes, styles) and forces the dark theme.
4. `App.CreateWindow` resolves `MainPage` and wraps it in a `NavigationPage`.
5. `MainActivity.OnCreate` keeps the screen awake while the app is in front.

`MainActivity` is portrait-only and declares the configuration changes it handles itself, so rotating or resizing does not recreate the activity (which would reset a game in progress).

## 5.2 Theming: `App.xaml`

One dark navy look throughout. `App.xaml` defines:

- **Colours** (`Background`, `Surface`, `Panel`, `Line`, `TextPrimary`, `TextSecondary`, `Accent`, `Violet`, `GoldColor`, `Danger`, ...).
- **Brushes**: `PageGradient` (page background), `AccentGradient` (cyan to violet, primary buttons), `GoldGradient` (shop buy buttons).
- **Styles**: `Glass` (a translucent bordered card, the basic container), `CaptionLabel`, `BodyLabel`, `ControlButtonBase`/`ControlButton`/`AccentControlButton` (the on-screen game buttons), `PrimaryButton`, `SecondaryButton`, `GhostButton`, `RoundButton` (circular icon buttons).

Design decisions:

- **The palette is duplicated on purpose** in `Graphics/VisualDesign.cs` (C# `Color`s) because drawing code cannot read XAML resources cheaply on every frame. If you change a colour, change it in both places. The comment at the top of `VisualDesign` says so.
- **`ControlButton` is split into a base style plus a `BackgroundColor` variant and a `Background` (brush) variant.** In MAUI, setting both `BackgroundColor` and `Background` in one style makes them fight; keeping them in separate styles avoids it.
- Pages that need a resource in code (the shop and level-select pages build some UI in code) look resources up through `Application.Current.Resources` with a fallback colour, so a missing key degrades instead of crashing.

## 5.3 Pages

### `MainPage` (menu, singleton)
The page opens on a start screen with only **PLAY** (straight into the player's next Story level, which is level 1 for a new player) and **MORE**. MORE swaps in the full menu below, and BACK (or the Android Back button) swaps it out again. Which of the two is showing is kept in the page, so coming back from a game started with PLAY lands on the start screen and coming back from one started in the full menu lands on the full menu. The full menu also has a **TUTORIAL** button, which opens `GamePage` on lesson 1.

The full menu is: gold chip, logo, the **Story card** (shows "Start" with level 1, or "Continue" with the next level and a count of cleared levels), buttons for Map, Classic, Shop, Archive, Tutorial, **Settings** and About, and a how-to-play note. The settings switches used to live here; they are on the Settings page now. On appearing it also tells the player once if a reinstall restored its progress from the save file (or found one it could not use). The first-launch name prompt has an "I HAVE A SAVE FILE" button that lets the player choose a save file by hand. It refreshes in `OnAppearing`, so gold and progress are current after returning from a game or the shop. Starting a game resolves a fresh `GamePage` from the container, calls `Configure(setup)`, and pushes it.

### `WorldMapPage` (the Map: regions and levels together)
The one page for finding a level. It lists the eight regions as cards (picture, title, how restored it is, progress), then "Beyond the Spectrum", and each card opens to show **the levels that belong to it**: what has been found there (Prism fragment, journals), and a wrapped grid of numbered tiles. Region 1 is levels 1-25, region 2 is 26-50, and so on, so a level's area is always visible. Tiles are coloured by difficulty tier (the tiers happen to end on region boundaries: regions 1-2 Easy, 3-4 Medium, 5-6 Hard, 7-8 Extreme); cleared levels are marked, the next level is outlined, locked levels are dimmed.

- **One region is open at a time**, starting with the region the next level is in (the page scrolls to it); tapping another region's header opens it instead. A region's levels are built the first time it opens.
- **The detail bar** at the top shows the level last tapped: its number and tier, the region it belongs to, its seed (tap to copy; for information and bug reports) and PLAY / REPLAY / LOCKED. The next level starts selected. Tapping a tile selects it (only that tile is restyled, nothing is rebuilt), and tapping the selected tile again plays it.
- A locked region shows "Reach level N to open this region" and cannot be opened. "Beyond the Spectrum" (level 201 on) lists tiles in the same way, with SHOW MORE LEVELS to extend the list (it is endless, so it is never built whole).
- After the ending the card for the finished Spectrum is shown first.
- This replaces the earlier separate Levels page, World Map page and per-region page.

### `WorkshopPage`
Bram's hub, one screen with four tabs: UPGRADES, LOOKS, SUPPLIES and TROPHIES. The page owns the header (back, lamp summary, gold chip), the tab bar and a line that spells out the selected lamp ("LAMP LIT ✓" or "LAMP DARK · Restore ... to light it"). Each tab shows one panel, built the first time it is opened and kept. A panel implements `IWorkshopPanel`: `Refresh()` redraws it and `Changed` tells the page that gold may have moved, so the gold chip stays current. Every tab is open from the start; lighting a lamp is the only thing region progress changes. The Archive is not here: it opens from the main menu.

### `ShopPanel` (UPGRADES and SUPPLIES)
The workbench. One panel class, used twice with a `Category`. **UPGRADES** builds cards in code from `ShopService.Entries(...)` (title, description, level pips, "Now" and "Next" effects, a BUY button with the price). **SUPPLIES** shows the two used-up helpers from `ConsumableInventory.Entries()` (stock pips out of 3, the rules in one line, BUY ONE). What can be bought comes first and locked items follow. A button is enabled only when the player can afford it, a full stock says STOCK FULL, and a locked row shows a "Restore ..." hint and no price. After a purchase the panel raises `Changed` and rebuilds itself. The panel contains no pricing logic; it only displays what the services say.

### `WardrobePanel` (LOOKS)
Chips for blocks, bursts, boards, frames and Pip's accessories. Each look is a card with a live preview (`SkinPreviewDrawable`, `BoardPreviewDrawable`), and one of: EQUIPPED / EQUIP (owned), BUY with the price (found, not owned), or a "Not found yet" hint. All state comes from `CosmeticShop`; looks apply at the start of the next level, never mid-level.

### `ArchivePage`
The story the player has unlocked (scenes, characters, fragments, memories, with locked entries shown as hints). It opens from the main menu only. After the ending, it is also where the final look can be changed.

### `TrophiesPanel`, `AchievementsPanel`, `JournalPanel` (TROPHIES)
`TrophiesPanel` is a row of two chips over one of the other two. `AchievementsPanel` lists the achievements with their gold, what is still to earn first and then what is earned. `JournalPanel` shows the lifetime statistics. Neither spends gold.

### `DevMenuPage`
Testing shortcuts from `DevTools`. Shown only when `DevMenu.Enabled` is true (Debug builds, or any build made with `-p:DevMenu=true`). In a normal Release build its button and shortcuts never show.

### `SettingsPage`
Opened by SETTINGS on the full menu. Three cards:

- **Game**: pop effects, landing guide (off, outline, bold), reduced motion, sound effects, and board style (only once a style has been earned). All of them write straight to `GameSettings` / `CosmeticLoadout`.
- **Your name**: shows and renames the in-game name (renaming never changes a level).
- **Save data**: the state of the save file in words (`PersistentSettings.Status`), BACK UP NOW (writes now, and turns the file back on after it was deleted), FIND MY SAVE FILE (system file picker), the save code controls (COPY MY SAVE CODE, a box with PASTE and RESTORE), and DELETE SAVE DATA. Delete offers two choices, each with its own confirmation: the save file only (progress stays in the game but no longer survives an uninstall), or the file plus all progress. Restoring and erasing both end with a banner telling the player to close and reopen the game, and the page switches itself off, because `PersistentSettings` is then frozen.

Restoring from a file or a pasted code goes through `SaveRestoreFlow`, which checks the code without changing anything, shows what it holds, asks, and only then writes.

### `AboutPage`
Reads the version and build number from the installed package (`AppInfo`), so the text always matches the build. Contains the dedication and the product promises (see [Game design](01-game-design.md#18-product-promises)). Edit the wording in `AboutPage.xaml`. The name and save code controls moved to Settings.

### `GamePage`
The play screen. Layout (top to bottom): a glass HUD (pause button, level title, score, gold or best, the NEXT pieces), the **goal bar** (one row per goal with a progress bar, built with a `BindableLayout` over `Goals`), the board (`GraphicsView`), the collapsible controls drawer, and overlays. Overlays are a **coach card** (tutorial steps), a transient **toast** label (for example "No pop! -15"), and the pause / level complete / level failed / game over card with the gold reward line.

Code-behind responsibilities (and only these): wire gestures and buttons, host the animation timer, hook window deactivation to pause, apply drawer state, apply per-level view settings, and navigate back. Anything that is a decision belongs in the view model or the engine.

## 5.4 `GameViewModel`

The single class that drives the engine. Constructed with `(HighScoreService, PlayerProgress, ShopService, GameSettings, ISoundPlayer)`; it creates its own `GameEngine`. It also turns engine events into sound: `Rotated` plays the rotate tick, `PieceLocked` plays the place thud (or the hard-drop whoosh when `HardDropped` came just before it), and `Popped` plays the pop, pitched up one tone per chain step. Moving and soft-dropping are silent on purpose. Nothing plays when `GameSettings.SoundEffects` is off.

**Owns:**
- the dispatcher timer (`Attach(IDispatcher)` creates it once; `Detach()` pauses and stops it);
- the current `GameSetup` (`Configure`, `GoToNextLevel`);
- the `TutorialController` for the current level;
- the one-time gold payout (`HandleCompletion`);
- prefetching the next level on a background task.

**Exposes to XAML:**
- header: `TitleText`, `SubText`, `ScoreText`, `ChainText`;
- Story: `HasMoves`, `MovesText`, `MovesColour` (white, then amber at 4 left, then red at 2), `Goals`, `HasGoals`;
- overlays: `IsPaused`, `IsFailed`, `IsComplete`, `IsClassicOver`, `IsOverlayVisible`, `OverlayTitle`, `OverlayDetail`, `CanRetry`, `RetryText`, `HasGoldReward`, `GoldRewardText`, `GoldNote`;
- coaching: `CoachVisible`, `CoachTitle`, `CoachText`, `CoachNeedsTap`, `CoachFooter`, `CoachCanSkip`, `HintColumn`, `HintWidth`;
- commands: pause, resume, restart, next level, dismiss coach, skip coach.

**Raises to the page:** `Redraw`, `WaveResolved(BurstWave)`, `GameStarted`, `Toast(string)`.

**`NewGame()`** is the one place that prepares a game. It copies the player's shop upgrades (`PreviewCount`, `ExtraMoves`) and the pop-effects setting (`Timing`) into the engine, resets per-game flags, decides whether to coach, starts the engine, and raises `GameStarted`. Because upgrades and settings are read here, changing them mid-game has no effect until the next game, by design.

**Input methods** (`MoveLeft`, `MoveRight`, `Rotate`, `SoftDrop`, `HardDrop`) are guarded by `InputBlocked` (a Tap card is up) and call the engine. The page maps screen directions to these (see 5.5).

**Why a timer in the view model and not the page:** the timer's lifetime is the game's lifetime. The view model is the thing that knows when the game state changes (it re-arms the timer with the engine's `TimerIntervalMs`), and keeping it there means the page can be torn down and rebuilt without touching game timing.

**Story panel.** A scene is shown one message at a time. Each speaker is drawn as a portrait on their own side so it is clear who is talking: Pip is always on the left, and everyone else alternates (right, left, right...) in the order they first speak in that scene. Narration has no portrait. Pip keeps their accessory and lights; other speakers are the same shape in their own colour (`PortraitTints`, `PipDrawable.Tint`). SKIP leaves the panel, BACK (shown from the second message) returns to the previous message, and the Android Back button does the same, or skips on the first message. The side logic is in `GameViewModel.SpeakerOnRight`.

### Helpers in the view model

`GameViewModel` owns two commands, `SecondChanceCommand` and `PrismShiftCommand`, that call the engine and nothing else. The buttons (`UNDO x1`, `SHIFT x1`) appear only when the level allows helpers and the player brought one in; each is disabled with a one-line reason when it cannot be used right now (`AbilityStatus`), and carries a spoken description with its count. `NewGame` copies the stock into the engine (`ConsumableInventory.ApplyTo`); `ConsumableInventory.Attach` takes one out of stock when the engine reports a paid use. There is no helper logic in the page.

## 5.5 Input in `GamePage`

Two input paths feed the same view-model methods.

**Buttons.** `WireButton` calls the action on press. Left, Right and Down also **auto-repeat**: a 220 ms delay, then every 70 ms while held, stopping when released or when the game is no longer playing.

**Board gestures** (via `GraphicsView` interaction events):

| Gesture | Result |
|---|---|
| Touch down and up with under 12 px of movement | Rotate. |
| Drag sideways | One column per 0.85 cells of travel. |
| Slow drag down | One soft-drop row per 0.9 cells of travel. |
| Downward flick of at least 2.5 cells within 260 ms | Hard drop. |

The first movement past the 12 px slop locks the gesture to **one axis** (horizontal or vertical) for the rest of that touch, so a sideways drag does not accidentally soft-drop. Dragging back up does not "bank" drops. All thresholds are named constants at the top of `GamePage.xaml.cs`; they are in units of the current cell size so the feel is the same on every screen.

**Flipped boards.** When the level is flipped, the page swaps left and right (`MoveScreenLeft`/`MoveScreenRight`) and inverts the vertical direction (`DownSign`) before calling the view model. The view model and engine never know the picture is upside down. The DOWN button's text becomes UP.

## 5.6 Pause behaviour

- The pause button and the Android Back button pause. Back pauses while playing and only leaves the game when already paused or over.
- `Window.Deactivated` and `Window.Stopped` pause automatically (phone call, app switch, screen off). The page hooks and unhooks the window in `OnAppearing`/`OnDisappearing` so handlers do not leak.
- Pausing stops the game timer and the button auto-repeat timer.

## 5.7 The controls drawer

The row of five buttons can be folded away with a handle ("HIDE CONTROLS" / "SHOW CONTROLS"). The state is saved in `GameSettings.ControlsCollapsed` and restored without animation when the page first appears; toggling fades the row. When collapsed the board gets the extra height. Gestures work either way. This exists because many players prefer gestures only and want the extra screen.

## 5.8 Persistence in the app

`LocalSettingsService` implements `ILocalSettings` (and `IClearableSettings`) over `Preferences.Default`. `PersistentSettings` wraps it, and is what the services receive as `ILocalSettings`; it mirrors every change to the save file through `AndroidSaveFileStore` (MediaStore, `Documents/PopBlocks/`). Those two classes are where the platform storage API is touched. The other platform calls that handle progress are the clipboard and the file picker, used by the Settings page for the save code and "find my save file". The services that use it are in the engine project (see [Architecture](02-architecture.md#26-persistence)).

## 5.9 Android project notes

- `ApplicationId` `com.popblocks.game`. `ApplicationDisplayVersion` (user-visible version, shown on the About page) and `ApplicationVersion` (integer build number) are in `PopBlocks.csproj`. **Increase `ApplicationVersion` for every build you install over an older one**, or Android may refuse the update.
- `AndroidManifest.xml` declares **no permissions** and `allowBackup="false"` (the save file in Documents is written through MediaStore, which needs neither). Debug builds get `INTERNET` added automatically by the tooling so the debugger can attach; Release builds must not. Verify with `scripts/Check-Permissions.ps1`.
- Release builds are configured to produce an `.apk` (the default is `.aab` for the Play Store), so the file can be installed directly.
- App icon and splash are small SVGs. **No gameplay graphics are image files**; blocks, symbols, effects and the logo are all drawn in code. That keeps the app tiny and resolution-independent.
