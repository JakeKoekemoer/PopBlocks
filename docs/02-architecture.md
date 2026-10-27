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

Menu pages (`MainPage`, `LevelSelectPage`, `WorkshopPage`, `AboutPage`) are thin. They read services (`PlayerProgress`, `ShopService`, `GameSettings`) directly and navigate. They have no view model because they have almost no logic worth testing; the testable logic lives in the services in the engine project.

## 2.3 Dependency injection and start-up

`MauiProgram.cs` registers everything:

| Lifetime | Types | Reason |
|---|---|---|
| Singleton | `ILocalSettings`, `HighScoreService`, `GameSettings`, `PlayerProgress`, `ShopService`, `ConsumableInventory`, `UnlockStore`, `CampaignService`, `CosmeticLoadout`, `CosmeticShop`, `LifetimeStats`, `EndingService`, `AchievementService`, `WorkshopService`, `DevTools`, `ISoundPlayer`, `MainPage` | One source of truth for saved data. The main page is the root of navigation and lives for the whole run. |
| Transient | `GamePage`, `GameViewModel`, `LevelSelectPage`, `WorkshopPage`, `ShopPanel`, `WardrobePanel`, `TrophiesPanel`, `AchievementsPanel`, `JournalPanel`, `ArchivePage`, `DevMenuPage`, `AboutPage` | A fresh game page and view model per game, so no state leaks between levels. |

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

The engine defines `ILocalSettings` (int, bool, string get/set). The app implements it with MAUI `Preferences` (Android SharedPreferences) in `Services/LocalSettingsService.cs`; the tests implement it in memory. Services in the engine depend only on the interface.

Why: save logic (gold, unlocks, purchases) is some of the most important code in the game, because a bug there loses a player's progress. Putting it behind an interface keeps it fully testable.

**Every key lives in `Services/SaveKeys.cs`** (and in `SaveKeys.All`). The tests freeze the list: a key may be added, never renamed or removed, because an existing install would silently lose or misread the value.

| Key | Owner | Meaning |
|---|---|---|
| `save_version` | `SaveMigrator` | Format version (currently 1). Not carried in a save code. |
| `progress_highest`, `gold` | `PlayerProgress` | Highest Story level cleared; gold balance. |
| `upgrade_preview`, `upgrade_popflair`, `upgrade_extramoves`, `upgrade_colourlens`, `upgrade_chainsense`, `upgrade_prismreserve` | `ShopService` | Level owned of each upgrade (the item's `SaveKey` in `ShopCatalog`). |
| `consumable_second_chance`, `consumable_prism_shift` | `ConsumableInventory` | Helpers in stock, 0 to 3. |
| `unlocks_v1` | `UnlockStore` | One comma-separated set of ids: scenes seen (`seen_*`), lore and memories (`lore_*`), Prism fragments (`frag_*`), region rewards paid (`gold_*`), achievements (`ach_*`), cosmetics owned (`cosmetic_*`, which also covers the look chosen after the ending). |
| `cos_board`, `cos_skin`, `cos_burst`, `cos_frame`, `cos_pip` | `CosmeticLoadout` | What is equipped (ids; empty means the default). |
| `ending_theme` | `EndingService` | The look chosen after the ending. |
| `stat_*` | `LifetimeStats` | Personal bests and counters only (largest group, longest chain, ...). |
| `best_score` | `HighScoreService` | Classic personal best. |
| `player_name` | `PlayerIdentity` | The in-game name (1 to 20 characters). Display only. |
| `player_salt` | `PlayerIdentity` | Hash of the name and a random install number, set once and never recomputed (so renaming changes no level). 0 means not set: the player has not been asked yet. |
| `current_level_for`, `current_level_seed`, `current_level_rerolls`, `current_level_failures` | `SeedStore` / `StoryLevels` | The player's current level (HighestCleared + 1): which level the other three belong to, its seed (0 = not yet assigned), the rerolls used and the failures so far. Ignored on load unless `current_level_for` is the current level, so a restored save code with mismatched fields cannot plant a seed or failure count on another level. |
| `first_clear_seeds` | `SeedStore` (written by `PlayerProgress.Complete`) | Comma-separated `level:seed` pairs, one per cleared level, never overwritten. A cleared level with no entry replays the original (legacy) level. A new save-code field kind, validated pair by pair (level 1 to 100,000, seed 1 to 1,073,741,823, no duplicates, at most 5,000 pairs). |
| `classic_gold_carry` | `ClassicGold` | Tenths of a gold earned in Classic and not yet paid (0 to 9). |
| `blast_effects`, `show_ghost`, `landing_guide_style`, `reduced_motion`, `controls_collapsed`, `sound_effects` | `GameSettings` | Options. |

Story state is **derived, not stored twice**: a region's state comes from `PlayerProgress.HighestCleared`, and everything else the story needs is one more id in `UnlockStore`. Rewards are granted through `UnlockStore.Unlock`, which returns true only the first time, so a reward is paid once however often a level is replayed or a scene is skipped.

### Save code

`Services/SaveCode.cs` turns the keys above (except the format version) into one line of text, `PB1.` plus checksummed, compressed, URL-safe base64, for moving progress to a new phone or past a reinstall. It needs no file access, permission or network. Reading a code never trusts it: the checksum is checked before unpacking, the unpacked size is capped, only known keys are accepted, values are range-checked and clamped, ids are character-checked, and nothing is written unless the whole code is valid. The app must be restarted after a restore, because services read their data once at start. `SaveCodeTests` covers the round trip, tampering, truncation, a forged compression bomb and out-of-range values; a test also fails when a new key is added without deciding whether the code should carry it.

Changing a key name loses existing players' data. Add new keys; do not rename old ones.

## 2.7 Design rules to keep

1. **Rules live in the engine, never in drawing or XAML.** If you find yourself computing a game rule in `GameDrawable` or `GamePage`, move it into the engine and expose the result as state.
2. **One home for each constant.** Gameplay numbers: `GameConfig`. Level bands: `StoryConfig`. Colours, sizes and animation timings: `VisualDesign`. Level tuning tables: the bottom of `LevelCatalog`. Shop prices: `ShopService`. Gold amounts: `GoldRewards`.
3. **Levels are deterministic.** The same level number *and seed* must produce the same pile, pieces, goals and move limit on every device and every run (`LevelCatalog.Get(level, seed)` is a pure function; seed 0 is the original number-based level). Levels are no longer promised to be the same for every player or after every app update: a roll picks a seed per player, and a later retune of the difficulty band changes which seeds a roll would pick, never a level already stored. Never use an unseeded `Random` in level generation, and never `string.GetHashCode()` for a seed (it differs between runs): use `SeedMix`.
4. **The engine is single-threaded.** It is called from the UI thread only. The exceptions are `LevelCatalog` and `StoryLevels`, which are thread-safe because levels are rolled and generated on background threads (see 2.8).
5. **Immutable where it is cheap.** `Piece` is immutable (move and rotate return new pieces). `Board` is mutable for speed, with `Clone` and `CopyFrom`.
6. **No allocations in the hot drawing path where avoidable.** `GameDrawable` reuses buffers; effects use a fixed set of slots.
7. **Offline, always.** Do not add a package that needs a network permission.

## 2.8 Level generation threading

Generating a level on the harder tiers runs the solver, which takes a noticeable moment, and a *roll* (the search for a seed that passes the difficulty gate) builds several candidates. Rolling is therefore always off the UI thread:

- `StoryLevels` owns the rolls. A roll for a level is started once and shared by everyone who asks for it (single flight, keyed by level and reroll number); a roll started early (a prefetch) is simply picked up later. `GameViewModel.NewGame` asks `StoryLevels.TryGetReady`; if the seed is not ready it shows nothing for 300 ms and then the **loading card** (a skippable recap of story already seen, held at least 600 ms, closed by itself when the level is ready), and carries on when `ResolveAsync` completes.
- A level's seed is known at once when it is stored (the current level), recorded (a cleared level) or rolled ahead. The next level is rolled when a level begins; after a failure the next reroll is rolled too (not before the first failure, to save battery). Prefetches are cancelled when the player leaves the level.
- `LevelCatalog` caches built levels by `(level, seed)` behind a lock but builds outside it, with a `Lazy` per key so two threads never solve the same level twice. Legacy levels are kept for good; seeded ones are bounded (`SeedingConfig.CacheSize`, oldest dropped), because only the current level, the next and a prefetched reroll are ever needed. A roll primes the cache with the level it already built.
- Failures in a background roll are forgotten, so asking again tries again; the cancelled or failed task never reaches the player.

## 2.9 Diagnostics

`Diagnostics.cs` writes breadcrumbs and unhandled exceptions to Android's log under the tag `PopBlocks` and nowhere else. `Diagnostics.Install()` hooks the managed unhandled-exception events so a crash names its real exception. View them with:

```
adb logcat -s PopBlocks
```

This exists because MAUI start-up failures on a device are otherwise silent. `Install()` is called from `MainApplication` (the earliest point on Android). Keep the breadcrumbs in `App`, `MainActivity` and `MainPage`; they make the first minute of a crash report useful.
