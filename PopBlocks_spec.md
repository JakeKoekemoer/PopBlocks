# Chromaburst — Project Specification

**Version:** 1.0  
**Platform:** Android first  
**IDE:** Microsoft Visual Studio  
**Language:** C#  
**Framework:** .NET Multi-platform App UI (.NET MAUI)  
**Graphics:** .NET MAUI GraphicsView / IDrawable  
**Distribution:** Standalone Android APK  
**Connectivity:** Offline-first, no internet required  
**Monetisation:** None — no advertisements or in-app purchases

---

## 1. Project Overview

Chromaburst is an original falling-piece colour puzzle game. Players position connected clusters of coloured cells on a grid. When enough cells of the same colour connect orthogonally, they burst. Gravity then compacts the remaining cells, potentially creating additional bursts and chain combos.

The game should feel responsive, polished and accessible while remaining lightweight, private and independent of online services.

The project must not copy the visual identity, artwork or proprietary mechanics of an existing game. Its piece generation, visual design, scoring, effects and special abilities should form a distinctive identity.

### Primary objectives

- Develop in Microsoft Visual Studio using C# and .NET MAUI.
- Target Android as the first supported platform.
- Draw all gameplay graphics directly from code.
- Avoid PNGs, sprite sheets and downloaded gameplay assets.
- Keep game logic separate from rendering and user interface code.
- Operate without an internet connection.
- Avoid advertisements, tracking, analytics and unnecessary third-party dependencies.
- Save personal-best scores and settings locally.
- Keep the code testable and maintainable.

---

## 2. Technology Stack

| Area | Technology |
|---|---|
| IDE | Microsoft Visual Studio |
| Language | C# |
| Application framework | .NET MAUI |
| UI layout | XAML and MAUI controls |
| Game rendering | GraphicsView and IDrawable |
| Game engine | Plain C# classes |
| State management | Observable ViewModel |
| Local settings | Preferences API |
| Unit testing | .NET test project with xUnit or MSTest |
| Android packaging | Android build tooling provided through .NET MAUI |
| Source control | Git, preferably local-first |

Prefer built-in .NET and .NET MAUI functionality. Add NuGet packages only when a concrete requirement justifies them.

### Important framework distinction

.NET MAUI is a cross-platform UI framework. It provides shared application structure and uses native platform facilities where appropriate. The initial release will target Android, even if the architecture leaves room for other platforms later.

---

## 3. Game Board

### Dimensions

- Width: 8 columns.
- Visible height: 16 rows.
- Hidden spawn area: 2 additional rows.
- Total logical height: 18 rows.
- Coordinate origin: top-left.
- X increases from left to right.
- Y increases from top to bottom.

The board dimensions must be defined in one central configuration class rather than repeated throughout the application.

### Board appearance

- Dark navy background.
- Subtle grid lines.
- Clearly defined cell boundaries.
- Rounded coloured cells with consistent spacing.
- Smooth, readable transitions when pieces move or cells disappear.
- No textures or raster graphics required.

The board must resize to the available screen space while preserving its proportions. The game should remain usable on different Android screen sizes and aspect ratios.

---

## 4. Visual Design System

All colours, dimensions and drawing constants must be defined centrally in `Graphics/VisualDesign.cs`.

Use hexadecimal ARGB values in C# as `Color.FromArgb("#RRGGBB")` or the corresponding supported MAUI colour format.

### 4.1 Core palette

| Element | Hex colour | Purpose |
|---|---|---|
| Background | `#0B1220` | Main game background |
| Board surface | `#111B2E` | Board interior |
| Empty cell | `#1E293B` | Empty cell fill |
| Grid line | `#334155` | Subtle grid boundaries |
| Primary text | `#F8FAFC` | Main text and score |
| Secondary text | `#94A3B8` | Labels and secondary information |
| Cyan | `#38BDF8` | Colour family 1 |
| Coral | `#FB7185` | Colour family 2 |
| Yellow | `#FBBF24` | Colour family 3 |
| Purple | `#A78BFA` | Colour family 4 |
| Lime | `#A3E635` | Colour family 5 |

### 4.2 Block styling

Each cell should be drawn procedurally using vector drawing operations.

- Shape: rounded square.
- Outer inset: 6% of the cell size.
- Corner radius: 18% of the cell size.
- Border width: approximately 3.5% of the cell size.
- Top highlight: approximately 4.5% of the cell size in stroke width.
- Use a darker border and a lighter highlight for each colour.
- Keep the visual treatment consistent across all five colours.

Suggested border and highlight colours:

| Base colour | Border | Highlight |
|---|---|---|
| Cyan `#38BDF8` | `#0284C7` | `#BAE6FD` |
| Coral `#FB7185` | `#E11D48` | `#FFE4E6` |
| Yellow `#FBBF24` | `#D97706` | `#FEF3C7` |
| Purple `#A78BFA` | `#7C3AED` | `#EDE9FE` |
| Lime `#A3E635` | `#65A30D` | `#ECFCCB` |

Keep the design clean. Avoid excessive shadows, gradients or effects that add visual clutter.

### 4.3 Accessibility symbols

Each colour receives a distinct central symbol so players do not need to distinguish colours alone.

| Colour | Symbol |
|---|---|
| Cyan | Diamond |
| Coral | Circle |
| Yellow | Star |
| Purple | Plus |
| Lime | Triangle |

Symbols should be drawn with vector paths or primitive drawing operations. Their dimensions should scale relative to the cell size.

Symbols must be legible at the smallest supported board cell size. Their stroke widths, positioning and proportions should be centrally defined.

### 4.4 Connected cells

When cells form a connected group:

- Keep each cell's colour and symbol identifiable.
- Avoid overly thick internal borders where cells touch.
- Preserve clear outer boundaries.
- Do not allow small gaps or overlapping strokes to make the cluster appear disconnected.

### 4.5 Blast effects

Burst effects must be generated from code rather than image assets.

Potential effects include:

- Expanding rings.
- Small circles or particles.
- Short-lived starbursts.
- Brief brightness flashes.
- A subtle screen-independent visual pulse around the affected cells.

Initial target duration: 160–240 milliseconds.

Effects should be short, optional where appropriate, and inexpensive to render. Avoid creating large numbers of independent animation objects.

---

## 5. Core Gameplay

### 5.1 Pieces

Pieces are connected clusters containing two to four cells.

Piece geometry must be generated or selected from original definitions. Do not copy a recognisable proprietary set of pieces from another game.

Each cell carries a colour. A piece may contain more than one colour, depending on the selected piece-generation rules.

The active piece must be represented as logical cell coordinates and colours, independently of the drawing surface.

### 5.2 Controls

The initial control set should support:

- Move left.
- Move right.
- Rotate.
- Soft drop.
- Hard drop.
- Pause.
- Resume.
- Restart.

Controls should be comfortable for one-handed mobile play.

Use standard MAUI touch handling and gestures where appropriate. Avoid third-party input libraries.

### 5.3 Falling speed

Falling speed should start at a balanced pace and increase gradually as the score rises.

Requirements:

- Make the initial pace forgiving.
- Increase difficulty progressively.
- Cap the maximum speed.
- Avoid abrupt or unpredictable jumps in difficulty.
- Keep timing logic independent of rendering frequency.

Suggested initial fall interval: 800 milliseconds per row.

The exact progression curve and speed cap should be configurable and tuned through playtesting.

### 5.4 Collision detection

A piece cannot move into occupied cells or beyond the board boundaries.

Collision detection must be implemented in the game engine rather than in drawing code.

When a piece can no longer descend, lock it into the board and evaluate possible colour matches.

### 5.5 Colour-blast rule

A burst occurs when four or more cells of the same colour form a connected group using horizontal and vertical adjacency.

Diagonal adjacency does not count.

All qualifying groups should be identified from the same board state and cleared simultaneously.

The matching algorithm must not mutate the board while it is still discovering groups. First identify all qualifying cells, then clear them together.

### 5.6 Gravity

After a burst:

1. Identify all cells that were cleared.
2. Compact each column downward.
3. Preserve the vertical order of the remaining cells within each column.
4. Recheck the resulting board for qualifying groups.
5. Repeat the burst-and-gravity cycle until no qualifying group remains.

This process creates chain combos.

### 5.7 Chain combos

Track consecutive burst waves caused by one placement.

- First burst wave: multiplier ×1.
- Second consecutive wave: multiplier ×2.
- Third consecutive wave: multiplier ×3.
- Continue increasing the multiplier up to ×5.
- Reset the chain counter when the board stabilises.

Display the chain count clearly during gameplay. Use a short visual accent when the chain increases.

### 5.8 Scoring

Initial scoring proposal:

- 10 base points per cleared cell.
- Add a configurable group-size bonus for groups larger than four cells.
- Multiply wave points by the current chain multiplier.
- Add any future special-move bonuses explicitly rather than embedding them in the matching algorithm.

Centralise scoring rules in `ScoringSystem.cs`.

Use integer arithmetic and test the scoring calculations independently.

### 5.9 Burst Charge and special ability

A future gameplay enhancement may allow the player to accumulate Burst Charge from successful clears.

A proposed ability, Prism Shift, lets the player rearrange the colours within the next piece while preserving its geometry.

This feature should be implemented only after the core gameplay loop is stable. Its activation cost, limits and user interface must be defined before implementation.

### 5.10 Game over

The game ends when the next piece cannot spawn legally in the designated spawn area.

The game-over screen should show:

- Final score.
- Personal-best score.
- Restart option.
- Return-to-menu option.

The game must not lose a saved personal best if the app closes unexpectedly.

---

## 6. Proposed Project Structure

```text
Chromaburst/
├── Models/
│   ├── GameState.cs
│   ├── Board.cs
│   ├── Cell.cs
│   └── Piece.cs
├── Engine/
│   ├── GameEngine.cs
│   ├── PieceGenerator.cs
│   ├── CollisionDetector.cs
│   ├── ColourMatcher.cs
│   ├── GravityResolver.cs
│   └── ScoringSystem.cs
├── Graphics/
│   ├── GameDrawable.cs
│   ├── VisualDesign.cs
│   └── BlastEffects.cs
├── ViewModels/
│   └── GameViewModel.cs
├── Views/
│   ├── MainPage.xaml
│   └── GamePage.xaml
├── Services/
│   ├── LocalSettingsService.cs
│   └── HighScoreService.cs
├── Platforms/
│   └── Android/
├── Tests/
│   ├── CollisionDetectorTests.cs
│   ├── ColourMatcherTests.cs
│   ├── GravityResolverTests.cs
│   └── ScoringSystemTests.cs
└── Chromaburst.csproj
```

This is the proposed logical structure. Depending on the Visual Studio project template, the test project may be a separate solution project rather than a folder within the app project.

### Architectural rules

- Models hold data and domain state.
- The engine owns gameplay rules.
- The drawable renders the current game state.
- The ViewModel exposes state and commands to the UI.
- Views display controls and status information.
- Services handle local persistence and platform-specific capabilities.

The game engine must not depend on MAUI UI classes. This makes it easier to unit-test the rules without launching the Android app.

---

## 7. Graphics Implementation

Use .NET MAUI `GraphicsView` and `IDrawable` for the game board.

The drawing implementation should:

1. Receive the current game state.
2. Calculate the board's available drawing area.
3. Calculate cell size and board origin.
4. Draw the board surface and grid.
5. Draw locked cells.
6. Draw the active piece.
7. Draw its symbols and highlights.
8. Draw temporary burst effects.

Rendering must not change the game state.

Do not implement gameplay rules inside the drawable. The renderer should visualise the state it receives.

### Performance considerations

Procedural drawing avoids loading and storing a separate image for every cell or colour. Simple vector shapes are a sensible fit for this game.

However, procedural drawing is not automatically faster than every image-based approach. Performance depends on the number of drawing operations, invalidation frequency, animations and device hardware.

To keep rendering lightweight:

- Reuse drawing calculations where practical.
- Avoid unnecessary allocations during frequent redraws.
- Invalidate the view when state changes instead of running a continuous animation loop unnecessarily.
- Keep animation effects short and bounded.
- Profile on a real Android device before attempting low-level optimisation.

No external graphics engine should be needed for the initial board and gameplay.

---

## 8. Game Timing and State

The engine should provide explicit operations such as:

- Start game.
- Move piece.
- Rotate piece.
- Soft drop.
- Hard drop.
- Advance one timed tick.
- Pause.
- Resume.
- Restart.

Use a controlled timing mechanism in the application layer to advance the engine. Do not tie falling speed to the frame rate.

When the app enters the background, pause gameplay. When the player returns, do not apply a large backlog of missed falling ticks.

Represent gameplay states explicitly, for example:

- Ready.
- Playing.
- Paused.
- Resolving bursts.
- Game over.

This prevents input, gravity and animations from accidentally operating in incompatible states.

---

## 9. Local Persistence

Only essential data should be stored:

- Personal-best score.
- Sound setting, if sound is implemented.
- Haptic-feedback setting, if haptics are implemented.
- Optional accessibility and gameplay preferences.

Use the MAUI Preferences API for small settings. A full database is unnecessary for the initial release.

Do not collect names, email addresses, location or other personal information.

Do not implement cloud saves, accounts or online leaderboards in the first version.

---

## 10. Offline and Security Requirements

Offline operation is a design requirement, not merely a feature.

### Required restrictions

- No advertising SDKs.
- No analytics or tracking SDKs.
- No cloud services.
- No accounts or authentication.
- No remote configuration.
- No remote code loading.
- No embedded web browser or external web links in the game.
- No in-app purchases.
- No unnecessary permissions.
- No internet dependency for gameplay or saving scores.

### Android manifest review

Inspect the final merged Android manifest, not only the project's source manifest.

Verify that the final package does not request `android.permission.INTERNET` unless a future, explicitly approved feature requires it.

Also inspect the resolved NuGet dependencies and their transitive dependencies.

### Release checks

Before distribution:

1. Build a release APK.
2. Inspect the manifest and permissions.
3. Review the dependency list.
4. Test all gameplay in airplane mode.
5. Verify scores and settings survive a restart.
6. Test app backgrounding and resuming.
7. Test on more than one screen size.
8. Sign and archive the release build securely.

An offline app reduces network-related exposure, but cannot guarantee immunity from operating-system or device vulnerabilities.

---

## 11. Testing Requirements

### Unit tests

Test the engine independently of the user interface.

**Collision detection**
- Movement within boundaries.
- Movement blocked by another cell.
- Rotation near walls.
- Spawn collision.
- Locking when descent is blocked.

**Colour matching**
- Groups of three do not burst.
- Groups of four do burst.
- Horizontal groups are recognised.
- Vertical groups are recognised.
- Diagonal-only groups do not burst.
- Separate qualifying groups are cleared simultaneously.

**Gravity**
- Empty spaces are removed correctly.
- Remaining cell order is preserved.
- Cascading matches are detected after compaction.

**Scoring**
- Base points are correct.
- Group-size bonus is correct.
- Chain multipliers are correct.
- The maximum multiplier is respected.

**Game state**
- Game-over detection works.
- Restart resets the current game.
- Personal-best score persists.
- Pausing stops timed gameplay.

### Manual tests

- Check readability on a small screen.
- Check touch controls with one hand.
- Check colour symbols are recognisable.
- Check rapid inputs do not corrupt the board.
- Check animations do not block gameplay.
- Check the game behaves correctly when interrupted by a phone call or app switch.

---

## 12. Implementation Milestones

### Milestone 1 — Project setup

- Install the required Visual Studio .NET MAUI workload.
- Create the MAUI app project.
- Configure the Android target.
- Run the untouched app on an Android emulator or device.
- Establish source control.

**Acceptance criterion:** a clean MAUI app builds and launches on Android.

### Milestone 2 — Visual board

- Implement the central colour palette.
- Implement cell geometry and symbols.
- Implement the board drawable.
- Draw an empty board and sample clusters.
- Verify resizing on different screen sizes.

**Acceptance criterion:** the board and pieces render entirely from code with no gameplay image assets.

### Milestone 3 — Game engine

- Implement board and piece models.
- Implement piece generation.
- Implement movement, rotation and collision detection.
- Implement piece locking and timed falling.

**Acceptance criterion:** a playable piece can fall, move, rotate and lock without corrupting the board.

### Milestone 4 — Bursts and scoring

- Implement group detection.
- Implement simultaneous clears.
- Implement gravity.
- Implement chain resolution.
- Implement scoring and difficulty progression.

**Acceptance criterion:** automated tests validate the full burst-and-gravity loop.

### Milestone 5 — Game interface

- Add score and chain indicators.
- Add pause, restart and game-over screens.
- Add responsive touch controls.
- Add short procedural effects.

**Acceptance criterion:** the game can be played from start to finish with clear feedback.

### Milestone 6 — Persistence and polish

- Save personal-best scores.
- Add optional local settings.
- Refine animations and responsiveness.
- Add accessibility refinements.

**Acceptance criterion:** settings and scores persist locally, and the game remains usable across supported screen sizes.

### Milestone 7 — Security and release

- Audit dependencies and permissions.
- Test in airplane mode.
- Build and sign the release APK.
- Test installation and restart behaviour on a real device.

**Acceptance criterion:** a standalone Android release operates without internet access or unnecessary permissions.

---

## 13. Initial Scope Boundaries

The first release should prioritise a reliable, enjoyable core game.

Included:
- Single-player gameplay.
- Five colour families.
- Procedurally generated graphics.
- Falling pieces.
- Colour blasts.
- Gravity and chain combos.
- Progressive difficulty.
- Local scoring.
- Pause and restart.
- Offline operation.

Deferred until the core game is stable:
- Prism Shift.
- Additional game modes.
- Achievements.
- Online leaderboards.
- Cloud saves.
- Social features.
- Monetisation.
- Remote services.

These deferred features should not be introduced unless there is a clear product requirement.

---

## 14. Definition of Done

Chromaburst's first release is complete when:

- The Android app builds successfully in Visual Studio.
- All gameplay shapes and symbols are drawn from code.
- The palette matches the defined design specification.
- The game engine passes its unit tests.
- Falling, collision, matching, gravity and scoring behave correctly.
- Chain combos work reliably.
- The game is playable on a real Android device.
- Scores and settings are stored locally.
- The app works in airplane mode.
- The release manifest contains no unnecessary internet permission.
- No advertising, tracking or unnecessary external dependencies are included.
- The release APK is signed and can be installed independently.

---

## 15. First Action

Start with the official Microsoft .NET MAUI setup guide:

https://dotnet.microsoft.com/en-us/learn/maui/first-app-tutorial/device-setup

Create a new .NET MAUI app in Visual Studio and verify that it runs on Android before adding game code.

Then implement Milestone 2: a board drawable with the exact palette, rounded-square cells and five distinct symbols.

**Do not implement all gameplay at once.** First prove that the project builds, the canvas scales correctly and the graphics render cleanly. Then add the game engine in small, testable steps.
