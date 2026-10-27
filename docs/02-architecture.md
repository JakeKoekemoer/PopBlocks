# 2. Architecture

## 2.1 The solution at a glance

```
PopBlocks.sln
src/
  PopBlocks.Engine/     net10.0          Pure C#: all rules, levels, progress, shop. No UI, no MAUI, no Android.
  PopBlocks/            net10.0-android  The .NET MAUI app: pages, view model, drawing, Android platform code.
tests/
  PopBlocks.Tests/      net10.0          xUnit tests. References the engine only.
scripts/
  Check-Permissions.ps1                  Release check: the built APK must not request INTERNET.
docs/                                    This documentation.
```

Dependency direction is strictly one way:

```
PopBlocks.Tests ──▶ PopBlocks.Engine ◀── PopBlocks (MAUI app)
```

The engine references nothing of the app. The app references the engine. The tests reference the engine. Nothing references the tests.

### Why the engine is a separate project

This is the most important structural decision in the codebase.

- **Testability.** The engine targets plain `net10.0`, so the tests run in seconds on any machine, in Visual Studio's Test Explorer or `dotnet test`, with no emulator and no Android SDK. A game's rules are where bugs hide, and they are the part that is cheap to test if it is isolated.
- **Enforced separation.** Because the engine project cannot see `Microsoft.Maui.*`, a rule can never accidentally depend on a screen size, a colour or a timer. The compiler enforces what a code review would otherwise have to.
- **Reuse.** Another front end (a desktop test harness, an automated bot, another platform) can drive the same engine unchanged. The level solver already does something like this.
- **Level proof.** Story levels are validated by a bot that needs the real rules available without a phone (see [Story mode](04-story-mode.md#44-the-solver)).

The cost is a small amount of mapping code in the app (the view model, the drawing code). It is worth it.

### Why the MAUI project is a single project

`PopBlocks.csproj` uses `SingleProject` with only `net10.0-android` as a target. Android is the only supported platform. Other platforms can be added later by extending `TargetFrameworks`; nothing in the engine would change. Platform-specific code lives under `Platforms/Android/`.

## 2.2 Layers inside the app

```
Views (XAML + code-behind)         pages, gestures, animation timer, navigation
   │ binds to
ViewModels (GameViewModel)         observable state, commands, the game timer, tutorial coaching, gold payout
   │ owns / calls
Engine (GameEngine, services)      rules, progress, shop, settings
   ▲ read-only
Graphics (GameDrawable, ...)       draws engine state; never changes it
```

- **Views** know about screens: touch, layout, navigation, a 60 fps repaint timer while something animates.
- **GameViewModel** is the only thing that drives the engine. It turns engine changes into bindable properties, owns the dispatcher timer that calls `Tick()`, runs the tutorial controller, and pays gold when a level is completed.
- **Graphics** only reads the engine. Drawing code never decides a rule. If drawing needs to know something (for example "which cells are about to pop") the engine exposes it as state (`PendingGroups`).

Menu pages (`MainPage`, `LevelSelectPage`, `ShopPage`, `AboutPage`) are thin. They read services (`PlayerProgress`, `ShopService`, `GameSettings`) directly and navigate. They have no view model because they have almost no logic worth testing; the testable logic lives in the services in the engine project.

## 2.3 Dependency injection and start-up

`MauiProgram.cs` registers everything:

| Lifetime | Types | Reason |
|---|---|---|
| Singleton | `ILocalSettings`, `HighScoreService`, `GameSettings`, `PlayerProgress`, `ShopService`, `MainPage` | One source of truth for saved data. The main page is the root of navigation and lives for the whole run. |
| Transient | `GamePage`, `GameViewModel`, `LevelSelectPage`, `ShopPage`, `AboutPage` | A fresh game page and view model per game, so no state leaks between levels. |

Rules to keep:

- **Pages are not constructor parameters of `App`.** `App`'s constructor runs before `App.xaml` has loaded its colours and styles. If the container built a page first, every `StaticResource` in that page would fail to resolve. `App` therefore takes only an `IServiceProvider` and resolves `MainPage` in `CreateWindow`, after the resources exist.
- Navigation is a standard `NavigationPage` stack with the navigation bar hidden; each page draws its own header and back arrow.
- `ILocalSettings` is the seam between the engine and the platform (see 2.6).

## 2.4 State machine

`GameEngine.State` is a `GameState`:

```
Ready ──Start──▶ Playing ◀──────────────┐
                   │ piece locks         │ board settled, game goes on
                   ▼                     │
             ResolvingBursts ────────────┘
                   │                 │
         (goals met)           (overflow / out of moves)
                   ▼                 ▼
            LevelComplete        GameOver

Playing / ResolvingBursts ──Pause──▶ Paused ──Resume──▶ (previous state)
```

- `ResolvingBursts` is entered whenever a lock produces at least one group. It lasts until the board has no groups left, then `FinishResolution` decides what happens next, in this order: **goals met** (win), **stack overflow** (lose), **out of moves** (lose), otherwise spawn the next piece. The order matters: a player who clears the last goal with their last piece wins rather than loses.
- Pausing remembers the state it interrupted, so a pause during a pop resumes the pop.
- `FailReason` says why a `GameOver` happened (`StackOverflow` or `OutOfMoves`).

## 2.5 Time and the game loop

The engine has **no clock**. It never reads the time and never starts a timer. Instead:

- The app calls `GameEngine.Tick()` from an `IDispatcherTimer`.
- After each state change the app sets the timer's interval to `GameEngine.TimerIntervalMs`, which is the fall interval while playing and the length of the current pop phase while resolving.

Consequences, all intended:

- **Determinism.** Given the same sequence of calls the engine behaves identically. Tests drive it by calling `Tick()` directly; the level solver and tests need no waiting.
- **Falling speed is independent of frame rate.** Speed comes from the engine's interval, never from how fast the screen repaints.
- **Nothing accumulates while paused.** A paused game has a stopped timer. Returning from the background does not replay a backlog of ticks and does not drop a piece.
- **Pop pacing is data, not threads.** The app passes `PopTiming(HighlightMs, SettleMs)` to the engine. The engine walks Highlight, Pop, Settle by returning different intervals. `PopTiming.Instant` (the default) removes the phases, which is what tests and the "pop effects off" setting use.

A separate ~60 fps **animation timer** in `GamePage` repaints the board only while something is animating (a pop, an effect, a tutorial hint). Idle boards repaint only on real state changes. That saves battery and keeps drawing independent of the rules timer.

## 2.6 Persistence

The engine defines `ILocalSettings` (int, bool, string get/set). The app implements it with MAUI `Preferences` (Android SharedPreferences) in `Services/LocalSettingsService.cs`; the tests implement it in memory. Services in the engine (`PlayerProgress`, `ShopService`, `HighScoreService`, `GameSettings`) depend only on the interface.

Why: save logic (gold, unlocks, purchases) is some of the most important code in the game, because a bug there loses a player's progress. Putting it behind an interface keeps it fully testable.

Saved keys:

| Key | Owner | Meaning |
|---|---|---|
| `progress_highest` | `PlayerProgress` | Highest Story level cleared. |
| `gold` | `PlayerProgress` | Gold balance. |
| `upgrade_preview`, `upgrade_popflair`, `upgrade_extramoves` | `ShopService` | Level owned of each upgrade. |
| `best_score` | `HighScoreService` | Classic personal best. |
| `blast_effects` | `GameSettings` | Pop effects on or off. |
| `show_ghost` | `GameSettings` | Landing outline on or off. |
| `controls_collapsed` | `GameSettings` | Button drawer folded away. |

Changing a key name loses existing players' data. Add new keys; do not rename old ones.

## 2.7 Design rules to keep

1. **Rules live in the engine, never in drawing or XAML.** If you find yourself computing a game rule in `GameDrawable` or `GamePage`, move it into the engine and expose the result as state.
2. **One home for each constant.** Gameplay numbers: `GameConfig`. Level bands: `StoryConfig`. Colours, sizes and animation timings: `VisualDesign`. Level tuning tables: the bottom of `LevelCatalog`. Shop prices: `ShopService`. Gold amounts: `GoldRewards`.
3. **Levels are deterministic.** The same level number must produce the same pile, pieces, goals and move limit on every device and every run. Never use an unseeded `Random` in level generation.
4. **The engine is single-threaded.** It is called from the UI thread only. The one exception is `LevelCatalog.Get`, which is thread-safe because the next level is generated on a background thread (see 2.8).
5. **Immutable where it is cheap.** `Piece` is immutable (move and rotate return new pieces). `Board` is mutable for speed, with `Clone` and `CopyFrom`.
6. **No allocations in the hot drawing path where avoidable.** `GameDrawable` reuses buffers; effects use a fixed set of slots.
7. **Offline, always.** Do not add a package that needs a network permission.

## 2.8 Level generation threading

Generating a level on the harder tiers runs the solver, which takes a noticeable moment. `GameViewModel.NewGame` therefore starts building the **next** level on a background task as soon as a level begins. `LevelCatalog` caches built levels behind a lock, so the foreground call (`LevelCatalog.Get`) either finds the result already waiting or builds it itself. Failures in the background task are swallowed on purpose: the level is simply built when needed.

## 2.9 Diagnostics

`Diagnostics.cs` writes breadcrumbs and unhandled exceptions to Android's log under the tag `PopBlocks` and nowhere else. `Diagnostics.Install()` hooks the managed unhandled-exception events so a crash names its real exception. View them with:

```
adb logcat -s PopBlocks
```

This exists because MAUI start-up failures on a device are otherwise silent. `Install()` is called from `MainApplication` (the earliest point on Android). Keep the breadcrumbs in `App`, `MainActivity` and `MainPage`; they make the first minute of a crash report useful.
