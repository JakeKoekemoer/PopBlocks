# 6. Graphics and visual design

Code: `src/PopBlocks/Graphics/`. Everything on the board is drawn from vector operations with `Microsoft.Maui.Graphics` (`ICanvas`) inside a `GraphicsView`. There are no sprite sheets or image assets for gameplay.

| File | Role |
|---|---|
| `VisualDesign.cs` | Every drawing constant: palette, block geometry, board geometry, animation timings. |
| `CellRenderer.cs` | Draws one block (and its neon "about to pop" version). Block symbols. Cluster bridging. |
| `GameDrawable.cs` | `IDrawable` for the board: layout, surface, grid, locked cells, pending groups, ghost, active piece, hint. |
| `BlastEffects.cs` | The burst after a pop: flash, rings, sparks, shockwave, confetti, shake, chain tag. |
| `BoardSpace.cs` | Board coordinate to screen coordinate mapping, including the upside-down view. |
| `PreviewDrawables.cs` | `UpcomingDrawable` (the NEXT pieces), `LogoDrawable` (the row of five blocks), `GoldCoinDrawable`. |

## 6.1 Principles

1. **Drawing reads state and never changes it.** `GameDrawable` takes a `GameEngine` and reads it. It does not decide a rule. The only exception to "never writes" is its own private animation bookkeeping (when the current pending group first appeared).
2. **Size from fractions.** Block geometry in `VisualDesign` is a fraction of the cell size (`CellInset`, `CornerRadius`, `BorderWidth`, ...). The board scales to the space available and cells stay square.
3. **One place for constants.** Change how a block looks in `VisualDesign`, not at the call site.
4. **Vector only.** Resolution-independent, tiny, and any palette change is one line.
5. **Colour is never the only cue.** Each colour has its own symbol (`CellRenderer.DrawSymbol`): diamond, circle, star, plus, triangle. Symbols are drawn in a dark tone that reads on all five fills.

## 6.2 Board layout

`GameDrawable.Draw` computes the biggest square-cell board that fits the view (with a margin and padding), `cell = floor(min(availW / 8, availH / 16))`, centres it, and records `CellSize` so touch handling can convert pixels to cells. If the cell would be smaller than 4 px it draws nothing (a view that is momentarily tiny during layout).

The picture is built in this order, all clipped to the board rectangle so falling cells never poke out:

1. Board surface: soft outer glow, a vertical gradient panel, a rim.
2. Empty-cell slots, grid lines, and a glowing floor line.
3. The tutorial hint column (a pulsing highlight), if any.
4. Locked cells (three passes, below).
5. Pending groups (the match about to pop).
6. The ghost outline of where the active piece will land.
7. The active piece (three passes).
8. Burst effects.

### Three-pass block drawing

A block is drawn as **border**, then **face**, then **detail** (highlight, sheen, shade, symbol). For a board of many blocks the renderer draws *all borders*, then *all faces*, then *all details*. Why: touching blocks of the same colour are drawn as one connected shape. If each block were drawn completely before the next, a neighbour's border would paint over the previous block's face and leave a seam. Passing over the whole board once per layer avoids that.

### Cluster bridging (`CellLinks`)

`LinksAt` records which of a cell's neighbours share its colour (four sides plus four corner flags for filled 2x2 corners). The renderer extends the face across the shared edge and squares off the corners there, so a group reads as one shape without a thick line between cells. `SeamOverlap` makes touching rectangles overlap slightly, because anti-aliasing would otherwise leave a one-pixel hairline. Face bridges overlap further than border bridges so the anti-aliased end of a bridge always lands on face colour, never on a border.

## 6.3 The pop pipeline

A pop is three visual stages, driven by the engine's phases:

1. **Highlight** (engine: `Highlight` phase, `PendingGroups` set, board unchanged; `HighlightMs` = 560). `DrawPendingGroups` lights the matching cells. For the first 45% (`GlowShare`) they glow in their own colour; then they turn into **solid neon** (`NeonColour`: cyan, hot pink, yellow, purple, lime) and join up as one shape. The elapsed time is measured from when those exact groups first appeared.
2. **Burst** (engine applies the wave and raises `WaveResolved`). The page calls `BlastEffects.Trigger(wave, now)`, which draws the burst on top: flash, expanding ring, sparks and a rising score tag. Higher **Pop Flair** (the shop upgrade) adds: level 1 a shockwave across the board, level 2 long sparks and a screen shake, level 3 a confetti shower (and a longer burst).
3. **Settle** (engine: `Settle` phase, `SettleMs` = 260). Cells above the burst slide down. `BlastEffects.FallOffsetCells(x, y, now)` returns how far above its final position a cell should still be drawn, easing out, so the engine can place cells at their final positions instantly while the picture animates the fall.

Chains show a "CHAIN xN" accent for `ChainAccentMs`.

With **pop effects off** (`GameSettings.BlastEffects`), the app gives the engine `PopTiming.Instant` and sets `BlastEffects.Enabled = false`: there is no highlight phase, no burst, and chains resolve one wave per tick.

`BlastEffects` is purely visual and holds no game state. It uses a small fixed set of slots (four simultaneous bursts), reused, so a long chain never allocates unbounded memory.

## 6.4 Animation timing and repainting

The board does not repaint on a fixed schedule. `GamePage` repaints when the view model raises `Redraw` (engine state changed), and starts a ~16 ms animation timer only while `GameDrawable.NeedsAnimation(now)` is true (pending groups exist, effects are active, or a hint is pulsing). When nothing is animating, no timer runs. All timings are in `VisualDesign` (`HighlightMs`, `GlowShare`, `SettleMs`, `BurstDurationMs`, `FallDurationMs`, `ChainAccentMs`, `ShakeMs`).

`GameDrawable.Clock` is a replaceable `Func<long>` (default: `Environment.TickCount64`). Replace it to render a specific animation moment deterministically.

## 6.5 The ghost and the hint

- **Ghost:** a faint outline of the active piece at its landing position (`LandingDistance`), toggled by `GameSettings.ShowGhost`. A tutorial level can force it on (`LevelDefinition.ForceGhost`).
- **Hint column:** during a tutorial "Do" step the columns the player should use pulse (`HintColumn`, `HintWidth`). It is state set by the view model, not part of the engine.

## 6.6 The upside-down view

Extreme levels turn the whole picture 180 degrees so pieces enter from the bottom of the screen. This is **rendering only**: the engine runs exactly as normal (pieces still "fall" toward row 17).

- `BoardSpace.ScreenX/ScreenY/ScreenColumn` map board coordinates to screen coordinates, mirroring both axes when `flip` is on. Row 17 (the floor) becomes the top of the screen; column 0 becomes the rightmost column.
- `CellRenderer.Flip(links)` swaps up/down and left/right in the cluster-link flags so bridging still draws on the correct sides.
- Falling offsets are applied in the flipped direction.
- Touch input is mirrored in `GamePage` (screen-left is board-right, screen-up is board-down).

Why only a vertical flip: it needs no change to the engine, board width or rules, and it still changes how the player has to think. A sideways version would need a transposed board (8 wide, 16 tall becomes 16 wide, 8 tall) and a different layout; it was not built. If you want one, keep the "rendering only" principle: add a mapping in `BoardSpace` and mirrored input, and leave the engine alone.

## 6.7 Palette

Defined in two places that must be kept in step: `App.xaml` (XAML colours, brushes, styles) and `VisualDesign.cs` (C# colours for drawing). Base colours are bright and slightly desaturated so they read on the navy background; every colour also has a darker border, a pale highlight and a saturated neon variant.

| Colour | Base | Neon |
|---|---|---|
| Cyan | `#38BDF8` | `#00E5FF` |
| Coral | `#FB7185` | `#FF2D78` |
| Yellow | `#FBBF24` | `#FFE600` |
| Purple | `#A78BFA` | `#C040FF` |
| Lime | `#A3E635` | `#39FF14` |

The UI is dark-themed throughout, and `App` forces `AppTheme.Dark` so system chrome (status bar icons) matches.

## 6.8 Android drawing traps (learned the hard way)

These are the places where MAUI Graphics on a real Android device behaves differently from what you would assume. They cost real time; read them before touching drawing code.

1. **Do not use `SetFillPaint` (gradients) and then `FillColor`.** On Android the gradient shader stays attached to the paint, and later solid `FillColor` fills come out as the gradient (here: nearly invisible dark blocks). The board background is therefore drawn as **40 thin solid bands clipped to the rounded panel** (`DrawBoardSurface`). If you need a gradient, draw it like that, or wrap it so nothing after it inherits the shader.
2. **`ICanvas` properties are effectively set-only.** Never read `FillColor`, `StrokeColor`, `Alpha` and the like to "remember" a colour (for example `canvas.StrokeColor = canvas.FillColor`). Keep the colour in a local variable.
3. **Always `SaveState()`/`RestoreState()` around anything that sets a clip, translation or alpha.** The drawables do this at every level so one effect cannot leak into the next.
4. **Allocation in `Draw` costs frames.** `GameDrawable` reuses a mask array for pending groups and `BlastEffects` reuses slots. `Piece.BoardCells()` allocates and is for logic and tests; drawing code computes positions directly.
5. **Anti-aliasing seams.** See `SeamOverlap` above.

Also note that a missing fill looks like "the game is broken but the grid is there": outlines (ghost, hint) are strokes and still draw even when fills fail. If blocks disappear but ghosts do not, suspect a shader or paint-state leak first.
