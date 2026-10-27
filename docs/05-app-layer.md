# 5. App layer (.NET MAUI)

Code: `src/PopBlocks/`. Target: `net10.0-android`, minimum Android 7.0 (API 24), portrait only.

```
App.xaml / App.xaml.cs       resources (palette, brushes, styles) and window creation
MauiProgram.cs               dependency injection registrations
Diagnostics.cs               logcat breadcrumbs and unhandled-exception logging
Views/                       MainPage, LevelSelectPage, ShopPage, AboutPage, GamePage
ViewModels/                  GameViewModel, ObservableObject
Graphics/                    drawing code (see doc 6)
Services/                    LocalSettingsService (MAUI Preferences)
Platforms/Android/           MainActivity, MainApplication, AndroidManifest.xml, colours
Resources/                   app icon and splash (SVG)
```

## 5.1 Start-up sequence

1. `MainApplication.OnCreate` installs `Diagnostics` (unhandled-exception logging).
2. `MauiProgram.CreateMauiApp` registers services (see [Architecture](02-architecture.md#23-dependency-injection-and-start-up)).
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
Gold chip, logo, the **Story card** (shows "Start" with level 1, or "Continue" with the next level and a count of cleared levels), buttons for Levels, Classic, Shop and About, two switches (pop effects, landing guide) bound to `GameSettings`, and a how-to-play note. It refreshes in `OnAppearing`, so gold and progress are current after returning from a game or the shop. Starting a game resolves a fresh `GamePage` from the container, calls `Configure(setup)`, and pushes it.

### `LevelSelectPage`
Sections per tier, each a wrapped grid of numbered tiles. Cleared levels are marked, the next level is highlighted, locked levels are dimmed and not tappable. Only the first batch of levels is built; "SHOW MORE LEVELS" appends more (the list is endless in principle, so it is never built whole). Tiles are built in code because they are numerous and uniform.

### `ShopPage`
Cards are built in code from `ShopService.Items()` (title, description, level pips, "Now" and "Next" effects, a BUY button with the price). A button is enabled only when the player can afford it. After a purchase the page rebuilds itself. The page contains no pricing logic; it only displays what the service says.

### `AboutPage`
Reads the version and build number from the installed package (`AppInfo`), so the text always matches the build. Contains the dedication and the product promises (see [Game design](01-game-design.md#18-product-promises)). Edit the wording in `AboutPage.xaml`.

### `GamePage`
The play screen. Layout (top to bottom): a glass HUD (pause button, level title, score, gold or best, the NEXT pieces), the **goal bar** (one row per goal with a progress bar, built with a `BindableLayout` over `Goals`), the board (`GraphicsView`), the collapsible controls drawer, and overlays. Overlays are a **coach card** (tutorial steps), a transient **toast** label (for example "No pop! -15"), and the pause / level complete / level failed / game over card with the gold reward line.

Code-behind responsibilities (and only these): wire gestures and buttons, host the animation timer, hook window deactivation to pause, apply drawer state, apply per-level view settings, and navigate back. Anything that is a decision belongs in the view model or the engine.

## 5.4 `GameViewModel`

The single class that drives the engine. Constructed with `(HighScoreService, PlayerProgress, ShopService, GameSettings)`; it creates its own `GameEngine`.

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

`LocalSettingsService` implements `ILocalSettings` over `Preferences.Default`. That is the only place the platform storage API is touched. The services that use it are in the engine project (see [Architecture](02-architecture.md#26-persistence)).

## 5.9 Android project notes

- `ApplicationId` `com.popblocks.game`. `ApplicationDisplayVersion` (user-visible version, shown on the About page) and `ApplicationVersion` (integer build number) are in `PopBlocks.csproj`. **Increase `ApplicationVersion` for every build you install over an older one**, or Android may refuse the update.
- `AndroidManifest.xml` declares **no permissions** and `allowBackup="false"`. Debug builds get `INTERNET` added automatically by the tooling so the debugger can attach; Release builds must not. Verify with `scripts/Check-Permissions.ps1`.
- Release builds are configured to produce an `.apk` (the default is `.aab` for the Play Store), so the file can be installed directly.
- App icon and splash are small SVGs. **No gameplay graphics are image files**; blocks, symbols, effects and the logo are all drawn in code. That keeps the app tiny and resolution-independent.
