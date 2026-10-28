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
| `StoryPalette.cs` | The five Spectrum colours from the artist brief, for story pictures only (not the block colours), plus easing helpers. |
| `StoryDrawables.cs` | `PipDrawable` (Pip: leaf antennae, glow tail, moods, bob and blink), `RegionSceneDrawable`, `PortraitTints`. |
| `WardenDrawables.cs` | `WardenDrawable` (the cloaked silhouette) and `WardenMarkDrawable` (the mark, drawn on a stroke at a time). |
| `StageProps.cs` | Memory crystal, Prism shard (glow and body as separate passes), cracked stone, ice slab. |
| `StageEffects.cs` | `LightFlow` (inward and outward) and `Ambience` (one loop per region). |
| `StageDrawable.cs` | `StageDrawable` (the picture above a dialogue panel) and `PortraitDrawable` (who is drawn beside a line). |

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
4. The hanging-block cue (below), under the blocks.
5. Locked cells (three passes, below).
6. Pending groups (the match about to pop).
7. The ghost outline of where the active piece will land.
8. The active piece (three passes).
9. Burst effects.

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

### Pip looks added in v1.1

Six looks for Pip are sold from the first day (Daisy 150, Nerdy Glasses 180, Sunglasses 200, Sporty Pip 220, Shy Pip 220, Party Pip 260), drawn by `PipDrawable.DrawAccessory` at the same anchors as the earned ones. Shy and Party Pip also set a mood of their own: `PipMood.Shy` (eyes down and to the side, a small wavy mouth, and a peek up every six seconds while a clock runs) and `PipMood.Party`. The rule is `PipDrawable.EffectiveMood`: a story scene's expression (`MoodRequested`, set by `GamePage.ApplySpeaker`) always wins, otherwise the look's mood applies. Party Pip's drifting confetti is switched on with `PipDrawable.Confetti` and shows only where Pip has a running clock (the story stage); Pip has no picture on the menu or the map.

### Boards and frames added in v1.1

Seven boards (Night Sky 300, Nebula Drift 400, Tide Pool 300, Canyon Strata 300, Ember Forge 400, Blossom Grove 350, Patchwork Quilt 500) and six frames (Starlight 220, Ripple 220, Stone Edge 200, Flame Edge 260, Vine 240, Neon Pulse 300) are sold from the first day. A board's still picture is `GameDrawable.DrawMotifs` (clouds, ripple lines, rock layers and quilt squares are whole-panel pictures; stars, embers and petals are scattered shapes). Five of them also move, through `BoardAmbient` (at most twelve things, placed by a function of the time rounded to 15 steps a second, so nothing is stored or allocated): the page runs a repaint timer for this only while a game is being played, and stops it when the game is paused, after ten seconds with no input, and under Reduced motion (`GamePage.UpdateAmbient`; any input wakes it). The animated frames (Starlight orbiting sparkles, Ripple wobble, Flame flicker, Neon pulse at 0.8 Hz) repaint only while a piece is falling and are still pictures under Reduced motion. The Neon glow is a wide soft line under the sharp one rather than a blurred image.

### Block sets added in v1.1

Seven sets are open from the first day (`Requirement.None`, bought with gold, see `CosmeticCatalog.NewIds`): Pure Solid 350, Starfield 450, Tidepool 450, Granite 450, Ember 500, Blossom 450 and Patchwork 900. They add `SkinPattern.Stars / Waves / Rock / Flame / Flower`, `SkinSpec.HideSymbol` and `SkinSpec.InnerBevel`. The detailed patterns (`Graphics/RichPatterns.cs`) are painted once per set, colour and size into an image by `PatternCache` and blitted (the face is one `DrawImage`); if an image cannot be made they are painted straight onto the canvas. Every pattern stays clear of the middle where the symbol sits. **Pure Solid hides its symbols** unless Colour assist is on or the High Contrast board is selected (`VisualDesign.ColourAssist`, set when a game starts); `CellRenderer.DrawSymbol` is the one place that checks. **Patchwork** draws each colour as the set the player chose for it (`cosmetic_mix_*`, only sets they own, Classic by default): `VisualDesign.UsePatchwork` fills the per-colour tables once when a game starts and `CornerFor / BorderFor / HighlightFor / PatternFor / SymbolFor` read the set for a colour. Starfield and Ember use dark symbols, not the pale ones the plan named: their bright faces cannot reach the 3:1 symbol contrast the skin tests require with a pale symbol. New looks wear a NEW ribbon until the tab they are on has been left (`cosmetic_seen`).

### Petals and Shards (v1.1)

`Graphics/BurstParticles.cs` draws the two bought pop styles that are whole particle systems. Both are stateless (a particle's place is a function of its cell, its number and the burst's age), so nothing is stored or allocated per frame; the petal and shard outlines are built once and drawn through canvas transforms. **Petals**: five teardrops (two quadratic curves, 7 to 10 dp) per popped block, the block's face lightened 35% with a paler tip (one in five is `#FFE8F0`), launched up and sideways at 40 to 90 dp/s with drag, gravity 60 dp/s^2, a 10 dp sine sway at 1.4 Hz and a 90 deg/s tumble, alive 1100 ms and fading over the last 500; each group opens one bloom ring (0 to 1.2 cells, alpha 0.35 to 0). **Shards**: four to six thin triangles (4 to 14 dp) per block in the block's saturated pop colour with a white edge, thrown outwards at 220 to 380 dp/s, gravity 900 dp/s^2, spinning 540 to 900 deg/s, one bounce off the floor of the board that keeps 30%, alive 480 ms and fading over the last 150, after a 60 ms white flash on the popped cells and a five-line crack star for 120 ms. Under Reduced motion petals fade in place (450 ms) and shards are a single flash. `BlastEffects` hands the whole burst to `BurstParticles` for these two styles and uses their lifetimes. The wardrobe's burst tiles (`BurstPreviewDrawable`) replay the real effect on a loop, with a repaint timer that runs only while the tile is on screen.

### Floating gold (v1.1)

`Graphics/FloatingText.cs` holds `FloatingText`, a pool of twelve slots drawn by `GameDrawable.Floaters` after the burst effects. `Spawn(BurstWave)` floats one `+0.6` (one decimal under 10 gold, whole numbers from 10; a 12 px coin to its left; 15 sp bold `#FFD54A` with a 2 px `#3A2A00` outline) per group in `wave.Gold`, at the group's centre, rising 36 dp over 900 ms with a 120 ms pop-in and the alpha held for 500 ms. Chain waves add a second line `×N CHAIN` in `#FF7A45`. The strings are formatted at spawn, so drawing allocates nothing. Reduced motion fades the number in place for 600 ms. `CoinFlyDrawable` (same file) draws the 6 to 10 coins that fly from a result card to the gold chip; the page animates it only while the coins are in the air.

## 6.5 The ghost and the hint

- **Ghost:** a faint outline of the active piece at its landing position (`LandingDistance`), toggled by `GameSettings.ShowGhost`. A tutorial level can force it on (`LevelDefinition.ForceGhost`).
- **Hint column:** during a tutorial "Do" step the columns the player should use pulse (`HintColumn`, `HintWidth`). It is state set by the view model, not part of the engine.

**Hanging blocks.** A piece locks exactly where it lands, and gravity runs only after a pop (then over the whole board), so a stack can hang over a gap for several moves and then drop when something else pops. `GravityResolver.FindHanging` lists those stacks (the lowest block, the colour and how many empty cells are under it). `GameDrawable.DrawHanging` tints those empty cells faintly in the stack's colour and puts a small arrow in the lowest one, where the stack would come to rest. The arrow means the cue does not depend on colour, nothing moves (so Reduced motion needs no special case), and `GameDrawable.ShowHanging` turns it off. The fall itself, once a pop lets the stack go, is the existing slide animation (`BlastEffects.FallOffsetCells`).

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

## 6.8 Looks: skins, bursts, boards, frames and Pip

All looks are data in `CosmeticCatalog` and drawn by the existing pipeline; none changes a rule.

- **Skins** (`SkinSpec`, applied with `VisualDesign.UseSkin` when a level starts): each of the five colours is defined as a hex value, plus a corner radius, border weight, highlight style (soft or facet) and optional texture (lines, dots, grain, kept away from the centre). **The symbols are never part of a skin.** `SkinRuleTests` enforces that every skin keeps each colour's hue within 30 degrees of the classic one, keeps the five colours at least 28 apart (Lab delta E), and keeps the symbol readable (contrast at least 3.0), with the same checks against every board. The shipped hand-tuned colours stay as `VisualDesign.Classic*`.
- **Bursts** (`BurstStyle`): `BlastEffects.DrawSpark` draws petals, shards, bubbles, cogs, frost or a starburst per fragment. Pop Flair still decides how many; Reduced motion caps a burst at 450 ms with no fall slide, shake or sparks.
- **Boards** (`GameDrawable.BoardPalette`, built from the catalogue): colours, rim and a faint motif that is kept quiet so blocks stay legible. `board_contrast` is black with a white grid and is always free.
- **Piece frames** outline only the piece in play (`DrawFrame`).
- **Pip's accessories** and the orbiting lights (one per restored region) are drawn by `PipDrawable`.
- **Helpers** draw nothing of their own beyond the Colour Lens outline (`DrawLens`, solid for groups of 3, dashed for 2) and the Chain Sense glow on the landing outline.

Not drawn, by decision: trail effects, chain celebrations, victory effects. Nothing is for sale that the game does not draw.

## 6.9 The story stage (pictures in dialogue panels)

Code: `Graphics/Stage*.cs`, `WardenDrawables.cs`, `Engine/Story/StageDirections.cs`. Art direction: `docs/PopBlocks_Shattered_Spectrum_Artist_Brief.md` (the atlas `docs/ArtBoard.png` is a mood reference, not slicing material). Tests: `StageDirectionTests`.

A dialogue panel has a **stage strip** above the text and a small **portrait** beside each line. Both are drawn, not image files, like everything else.

**What decides what is shown.** `StageDirections` (engine, no graphics) maps a scene id and line to a `StageCue` (flags): `PipHidden`/`PipReveal`, `Mark`, `RestoreSweep`, `Shard`/`ShardPair`, `FlowInward`/`FlowOutward`, `MemoryCrystal`, `WardenEntrance`/`WardenStand`, `Stillness`. A cue stays in force until the scene's next entry. `Describe(scene, line)` returns a `StageInfo` (region, how restored the picture starts, where a sweep ends, the cues, and the line they began on). `GameViewModel.SceneStage` exposes it; `GamePage.ApplyStage` hands it to `StageDrawable`. A scene with no entry still gets its region's picture and ambience, so no scene is an empty box.

**One-shot animations** (the sweep, the mark drawing on, a shard rising, Pip coming out) play from the line where the cue begins and are finished on any later line, so moving to the next line never replays them. **Loops** (ambience, flow, crystal pulse, Pip's bob and blink) run on a clock.

**Portraits follow the brief:** Pip is Pip; the Warden is the Warden's silhouette (never a recoloured Pip); a recorded voice is a memory crystal and the Warden's message is the mark (a recording is never a character). Mara and Bram are still a tinted stand-in until they are designed.

**Reduced motion** shows every picture as its final still frame: no ambience, no loops, no timer. The stage timer (~30 fps) runs only while a panel is open and not reduced. Skipping a scene closes the panel as before; nothing a cue draws is needed to understand the story, because the text carries it.

**Palette.** Story pictures use the brief's Spectrum (`StoryPalette`: cyan `#00D4FF`, magenta `#FF4ED8`, amber `#FFC837`, lime `#7CFF68`, violet `#986BFF`). The block colours in `VisualDesign` are a separate set on purpose: they are tuned and tested for contrast and colour-blind reading (`SkinRuleTests`), and a story palette change must never alter play.

**Adding a cue to a scene:** add an entry in `StageDirections` (`["scene_id"] = new[] { new StageEntry(line, cue) }`); `StageDirectionTests` checks the scene and line exist. **Adding a new kind of cue:** add a flag, draw it in `StageDrawable.DrawProps` from a prop in `StageProps` or `StageEffects`, and give it a final still frame for Reduced motion.

Not built yet (the brief's P1): gears and machine states, the finale (fragments orbit and join, the Heart reforms, five coloured streams), and the larger Pip pose set (look, hop and the sad and excited moods exist as properties but no scene uses them yet).

## 6.10 Android drawing traps (learned the hard way)

These are the places where MAUI Graphics on a real Android device behaves differently from what you would assume. They cost real time; read them before touching drawing code.

1. **Do not use `SetFillPaint` (gradients) and then `FillColor`.** On Android the gradient shader stays attached to the paint, and later solid `FillColor` fills come out as the gradient (here: nearly invisible dark blocks). The board background is therefore drawn as **40 thin solid bands clipped to the rounded panel** (`DrawBoardSurface`). If you need a gradient, draw it like that, or wrap it so nothing after it inherits the shader.
2. **`ICanvas` properties are effectively set-only.** Never read `FillColor`, `StrokeColor`, `Alpha` and the like to "remember" a colour (for example `canvas.StrokeColor = canvas.FillColor`). Keep the colour in a local variable.
3. **Always `SaveState()`/`RestoreState()` around anything that sets a clip, translation or alpha.** The drawables do this at every level so one effect cannot leak into the next.
4. **Allocation in `Draw` costs frames.** `GameDrawable` reuses a mask array for pending groups and `BlastEffects` reuses slots. `Piece.BoardCells()` allocates and is for logic and tests; drawing code computes positions directly.
5. **Anti-aliasing seams.** See `SeamOverlap` above.

Also note that a missing fill looks like "the game is broken but the grid is there": outlines (ghost, hint) are strokes and still draw even when fills fail. If blocks disappear but ghosts do not, suspect a shader or paint-state leak first.
