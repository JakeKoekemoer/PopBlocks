# PopBlocks: The Shattered Spectrum
## Visual Sprite Atlas Reference & Developer-Ready Artist Brief

**Art direction:** Clean vector fantasy  
**Purpose:** Reusable cutscene sprites, effects, props, and region art for an offline-first, ad-free puzzle game.  
**Status:** Production brief / concept direction. The generated atlas is a visual reference, not a set of production-ready sliced assets.

---

## 1. Creative direction

Create a cohesive, friendly, luminous fantasy world that feels mysterious and hopeful rather than grim. Art should read clearly on a mobile screen and remain legible at small sizes. Use clean vector shapes, controlled gradients, simple silhouettes, restrained glow, and consistent outlines. Prioritise assets that can be reused across multiple dialogue scenes.

The story's visual theme is **connection versus concentration**: colour and life fade when all power is pulled toward one centre; colour returns when light flows outward and is shared.

### Core principles
- Pip and the Warden must be unmistakably different characters, not recolours of the same silhouette.
- Every region has three visual states: **Faded → Recovering → Restored**.
- Use one consistent five-colour Spectrum across characters, crystals, UI, and effects.
- Keep characters, props, particles, and effects on separate layers wherever practical.
- Prefer vector/SVG assets or transparent PNG exports from vector source files. Do not bake text, dialogue, or UI labels into scene art.
- Build reusable elements instead of making a unique illustration for every dialogue line.
- Keep animation gentle and readable; effects must not obscure dialogue.
- No external asset downloads, network calls, advertising, or runtime asset dependencies. Bundle assets with the game.

## 2. Colour palette

The five colours below are the canonical Spectrum colours. Use these exact hex values unless a documented accessibility or contrast adjustment is needed.

| Colour | Hex | Story association | Suggested use |
|---|---|---|---|
| Cyan | `#00D4FF` | Flow, connection, discovery | Pip's main glow, flowing energy, active highlights |
| Magenta | `#FF4ED8` | Heart, emotion, memory | Prism Heart, emotional/memory accents |
| Amber | `#FFC837` | Energy, warmth, machinery | Lamps, sparks, restored machines |
| Lime | `#7CFF68` | Growth, life, renewal | Plants, garden restoration, healthy life |
| Violet | `#986BFF` | Wisdom, mystery, the Warden | Warden effects, ancient marks, uncertain history |

Supporting colours:

| Role | Hex | Notes |
|---|---|---|
| Deep background | `#081426` | Cutscene and atlas background |
| Secondary background | `#101F36` | Panels and deep scene shadows |
| Main text | `#F4F8FF` | High-contrast text |
| Secondary text | `#A8B8D0` | Labels and supporting copy |
| Faded world | `#526174` | Desaturated, low-energy state |
| Crystal highlight | `#FFFFFF` | Small, controlled specular accents |

**Important:** The atlas is a concept sheet and may contain approximate or visually shifted colour samples. Use the hex values in this table as the source of truth.

## 3. Character designs

### 3.1 Pip — The Spark

**Role:** Curious, loyal, upbeat companion; the player's emotional guide. Pip represents small lights finding one another.

**Silhouette:** Small floating spirit; rounded body, large readable eyes, two short leaf/flame-like antennae or fins, tiny lower glow/tail. Avoid humanoid anatomy. Must remain recognisable as a flat silhouette at 72 px portrait size.

**Material and lighting:** Pale cyan-white body with a cyan core and a soft outer glow. Use a limited number of smooth shapes; no noisy texture. The glow should remain optional as a separate layer/effect so it can be toned down.

**Expression/pose set (minimum):**
- Idle / neutral
- Happy
- Curious
- Excited
- Worried
- Sad / reflective
- Looking up
- Looking left and right
- Floating / bobbing
- Small jump or quick hop
- Gentle farewell / hopeful ending

**Animation guidance:** 6–10 frames for simple loops, or a small set of key poses if the game animates transforms procedurally. Idle bob should be subtle. Blink occasionally; expression changes should be readable without exaggerated squash-and-stretch. Separate eyes/mouth from body where feasible.

**Do not:** recolour Pip violet to represent the Warden, use Pip's face as a recording icon, or make Pip's glow so large that the silhouette disappears.

### 3.2 The Warden

**Role:** A guardian who shattered the Prism Heart to prevent a catastrophe, then failed the world by leaving it fractured. Morally complex, not a conventional villain.

**Silhouette:** Tall, narrow, cloaked figure with angular, layered shoulders and a tapered lower cloak. Face is mostly hidden; a restrained violet eye/visor or inner-light slit can appear in selected poses. No rounded body, large cute eyes, leaf antennae, or floating mascot proportions.

**Palette:** Deep indigo/near-black cloak, violet edge light `#986BFF`, limited pale-violet accents. Keep the face mostly obscured. Avoid making every part glow.

**Required views/poses:**
- Front / neutral reveal
- Three-quarter front (preferred primary portrait)
- Side silhouette
- Back silhouette
- Standing still
- Slow step / entrance
- Raise hand toward the mechanism
- Confession / lowered head
- Brief stillness for “A pause” beat
- Mark/recording silhouette, if required

**Animation guidance:** Slow, deliberate movement. Cloak edges can shift subtly. Reveal can be done with a mask/opacity transition and a controlled violet rim light. The pause should be achieved by stillness and reduced ambient motion, not a new character illustration.

### 3.3 Supporting characters

Mara (Crystal Keeper/archivist) and Bram (practical Tinkerer) can be added after Pip and the Warden are approved. Their silhouettes, facial proportions, outline weight, and shading must follow the same style guide. Bram's Workshop can communicate his personality through props even when he is not on screen.

## 4. Reusable asset catalogue

The following catalogue maps to the cutscenes identified in the dialogue audit. Asset IDs are suggested names for production and implementation.

### A. Restoration sweep
**Suggested IDs:** `fx_restore_sweep`, `env_garden_faded`, `env_garden_recovering`, `env_garden_restored`, `prop_pump`, `prop_workshop_lamps`

- Create a left-to-right or radial wave mask that can reveal the restored state over the faded state.
- Flowers open, lamps turn on one by one, crystals regain highlights, and machine indicators illuminate.
- Keep the wave/mask separate from the environment art so the effect is reusable in regions 1–7, the region 3 pump scene, and Workshop lamps.
- Provide a calm version with fewer particles for small devices and reduced-motion settings.

### B. Prism fragments and Heart
**Suggested IDs:** `prop_prism_shard_cyan`, `prop_prism_shard_magenta`, `prop_prism_shard_amber`, `prop_prism_shard_lime`, `prop_prism_shard_violet`, `prop_prism_heart_cracked`, `prop_prism_heart_whole`

- Each shard shares the same geometric construction, bevel language, and highlight placement.
- Each shard should work as a static prop and as a floating/pulsing sprite.
- Supply a separate glow layer; do not bake the glow into the only copy of the shard.
- The second shard in the Crystal Caves completion should pulse in sync with the first.
- The finale requires fragment alignment, joining, a complete Heart, then outward streams of all five colours.

### C. Pip sprite set
**Suggested IDs:** `char_pip_idle`, `char_pip_happy`, `char_pip_curious`, `char_pip_excited`, `char_pip_worried`, `char_pip_sad`, `char_pip_look_up`, `char_pip_blink`, `char_pip_float`

- Reuse these in cutscene panels and Pip banners.
- The opening scene needs a short reveal from behind a cracked stone, followed by an idle loop.
- Keep portrait crops and full-body sprites visually consistent.

### D. Light-flow effects
**Suggested IDs:** `fx_flow_inward`, `fx_flow_outward`, `fx_flow_stream_cyan`, `fx_flow_stream_magenta`, `fx_flow_stream_amber`, `fx_flow_stream_lime`, `fx_flow_stream_violet`

- Inward flow: thin ribbons and particles travel toward a centre point; edge illumination gradually decreases.
- Outward flow: ribbons expand from a centre and branch toward the scene edges; environmental lights return as the flow reaches them.
- Support adjustable centre point, direction, speed, colour, and opacity in the game implementation.
- Use the same base shapes for r2_mid, r4_complete, r5_open, r6_mid, r7_confession, r7_plan, r8_prep, and the ending.

### E. Memory crystal / recording
**Suggested IDs:** `prop_memory_crystal`, `fx_memory_ripple`, `ui_recording_marker`

- A floating or embedded crystal with a distinct inner core and a soft ripple ring.
- Provide idle, pulse, and playback states.
- For r2_record, r4_message, and r6_ice, show the crystal or a crystal embedded in the wall. Do not show a Pip portrait for a recording.
- If a recorded speaker silhouette is needed, use a deliberately abstract echo/mask rather than a tinted Pip.

### F. The Warden's mark
**Suggested IDs:** `symbol_warden_mark`, `fx_mark_draw_on`, `fx_mark_burn_in`

- One original, simple, symmetric mark with a central split and outward/inward directional reading.
- It must remain legible as a small icon and as a large wall symbol.
- Animate stroke reveal, then a restrained glow or burn-in.
- Reuse in the roots scene, Warden wall scene, and shutdown control discovery.

### G. Gears and machines
**Suggested IDs:** `prop_gear_small`, `prop_gear_large`, `prop_gear_assembly`, `prop_spectrum_pump`, `prop_workshop_machine`, `fx_machine_start`

- Build gears from reusable circles, teeth, hubs, and spokes wherever possible.
- Supply broken/stuck and active/restored states.
- In the early machine scenes, a gear catches and turns the chain.
- In the Clockwork Wastes, machines should first appear to wait for a central signal, then run from local power after restoration.
- Separate moving parts into layers if they will rotate independently.

### H. Regional ambience
**Suggested IDs:** `fx_ambience_fireflies`, `fx_ambience_crystal_glints`, `fx_ambience_spores`, `fx_ambience_bubbles`, `fx_ambience_sparks`, `fx_ambience_steam`, `fx_ambience_snow`, `fx_ambience_shards`, `fx_ambience_hum`

- One restrained particle loop per region, not a full-screen storm.
- Use simple vector particles or procedural primitives where practical.
- Effects must not compete with dialogue or reduce text contrast.
- Ambient loops should be pauseable and reduced-motion compatible.

### I. Finale sequence
**Suggested IDs:** `fx_fragment_orbit`, `fx_heart_reform`, `fx_spectrum_burst`, `fx_lights_multiply`, `prop_region_orbit_light`

- Fragments orbit, align, join, and form the complete Prism Heart.
- Five colour streams travel outward to the regions.
- Regional lights activate in sequence, then multiply to suggest “a thousand lights.” Use layered points of light rather than literally drawing a thousand unique objects.
- The eight orbit lights already planned for the preparation scene should be reusable and individually addressable.

### J. UI and supporting assets
**Suggested IDs:** `ui_dialogue_frame`, `ui_pip_banner`, `ui_choice_marker`, `ui_region_icon`, `ui_journal_icon`, `ui_fragment_icon`

- Keep dialogue text and labels as native UI text, not raster art.
- Dialogue portrait target is currently 72 px in the existing panel; provide vector source and export sizes appropriate for the app's actual device-density scaling.
- Provide safe padding around sprites so glows and motion are not clipped by portrait frames.

## 5. Region background direction

Create eight reusable background compositions. Each should have a clear focal area for dialogue characters and a lower-detail zone behind text. For each region, provide faded, recovering, and restored variants where story progression visibly changes the environment.

1. **Forgotten Garden (levels 1–25):** cracked stone, dormant flowers, roots, small pools, first signs of lime/cyan life.
2. **Crystal Caves (26–50):** faceted crystal walls, reflective surfaces, memory-crystal niches, cyan/violet shadows.
3. **Verdant Wilds (51–75):** oversized leaves, glowing plants, tangled roots, reclaimed workshop edges.
4. **Sunken City (76–100):** submerged architecture, reflective water, faded civic lights, records of the Heart's instability.
5. **Clockwork Wastes (101–125):** gears, broken mechanisms, old conduits converging toward a dead central source; later local amber/cyan power.
6. **Frozen Expanse (126–150):** ice walls, snow, trapped light, a memory embedded in ice; maintain readable silhouettes against pale ice.
7. **Shattered Citadel (151–175):** fractured monumental architecture, Warden symbols, dramatic but uncluttered reveal space.
8. **Heart of Spectrum (176–200):** floating fragments, central Heart, five outward routes of light, then a balanced luminous world.

The generated visual atlas is a layout and mood reference. Do not treat its miniature region images as final approved backgrounds or as individually exportable sprites.

## 6. Cutscene asset mapping

| Priority | Visual beat | Reusable assets | Dialogue coverage |
|---|---|---|---|
| P0 | Restoration sweep | Environment state layers + restoration mask | Region completions r1–r7; r3 pump; r3 Workshop lamps |
| P0 | Prism fragment reveal | Shard sprites + glow/pulse | Completion scenes; paired pulse in r2 completion |
| P0 | Pip introduction and reactions | Pip pose/expression set | r1 opening and ordinary Pip dialogue scenes |
| P0 | Inward/outward light | Reversible flow effects | r2 mid, r4 completion, r5 opening, r6 mid, r7 confession, r7 plan, r8 prep, ending |
| P0 | Memory crystal playback | Crystal + ripple + recording marker | r2 record, r4 message, r6 ice |
| P0 | Warden mark | Vector symbol + stroke reveal | r1 roots, r4 Warden symbol, r5 shutdown discovery |
| P0 | Warden reveal | Warden poses + reveal lighting | r7 opening and confession pause |
| P1 | Gears/machines | Modular gears + machine states | r3 opening, r5 machine, r5 discovery, r5 completion |
| P1 | Finale | Heart, fragments, outward streams, orbit lights | r8 completion; r8 preparation |
| P1 | Regional ambience | Small reusable particle loops | All region openings and replay scenes |

**P0** means the first coherent cutscene slice should include it. **P1** can follow once the core pipeline is working.

## 7. Asset production and export requirements

### Source files
- Preferred: SVG for vector illustrations and simple effects; use a layered vector source file if SVG is not suitable for the artist's workflow.
- Include a source file for every delivered asset. Do not deliver flattened PNGs alone when the art is intended to be reused or animated.
- For effects that are easier to render than animate as vectors, supply transparent PNG sprite sheets plus the source project.

### Exports
- Transparent background for sprites, props, and effects unless the asset is explicitly a full background.
- No baked-in captions, dialogue, UI text, or colour labels.
- Provide a contact sheet for review and separate export files for implementation.
- Use consistent canvas bounds, anchor points, and naming.
- Supply @1x, @2x, and @3x raster exports only where raster exports are needed; vector source remains preferred.
- Keep glow on a separate layer or export where possible.

### Naming convention
Use lowercase `snake_case` with a category prefix:
- `char_pip_idle.svg`
- `char_warden_reveal.svg`
- `prop_prism_shard_cyan.svg`
- `fx_flow_outward.svg`
- `env_r1_garden_faded.svg`
- `ui_fragment_icon.svg`

### Suggested folder structure
```text
Assets/
  Characters/
    Pip/
    Warden/
    Supporting/
  Effects/
    Restoration/
    LightFlow/
    Particles/
    Crystals/
    Marks/
  Environments/
    Regions/
    Objects/
    Machinery/
  Props/
    Prism/
    MemoryCrystals/
    Gears/
  UI/
    Dialogue/
    Icons/
  SpriteSheets/
  Previews/
```

## 8. Animation and runtime constraints

- Prefer transform-based animation (position, scale, rotation, opacity) for floating, pulsing, blinking, and rotating parts.
- Use short sprite sheets only for distinctive hand-drawn transitions that cannot be represented cleanly with transforms.
- Keep loop durations and anchor points documented.
- Provide static fallback frames for every animated asset.
- Ensure ambient particles can be reduced or disabled without losing story information.
- Keep effects behind dialogue text and away from text-safe zones.
- The game uses .NET MAUI; coordinate asset packaging and scaling with the developer before final export. The art brief does not prescribe a particular runtime animation library.

### Suggested initial animation targets

| Animation | Suggested duration | Loop? | Notes |
|---|---:|---|---|
| Pip idle bob | 2–3 s | Yes | Very small vertical movement |
| Pip blink | 0.12–0.2 s | No | Trigger occasionally, not constantly |
| Crystal pulse | 1.5–2.5 s | Yes | Gentle opacity/scale pulse |
| Restoration sweep | 1.5–3 s | No | Timing can be tuned per scene |
| Mark draw-on | 0.7–1.2 s | No | Follow with restrained glow |
| Inward/outward flow | 2–5 s | Usually | Can pause at story beats |
| Gear start | 0.8–1.5 s | No | Stuck → catch → steady rotation |
| Warden reveal | 1.5–2.5 s | No | Avoid jump-scare timing |
| Heart reform | 3–6 s | No | Allow the final reveal to breathe |

These are starting points for implementation, not fixed final timings.

## 9. Dialogue panel composition

- Preserve the existing dark scrim and dialogue card unless the developer deliberately changes the UI.
- Character portrait should occupy its own defined area; 72 px is the current reported portrait size, so source vectors must scale cleanly beyond that.
- Narration is not a reason to leave the screen visually empty. Use the scene's key prop or environmental action as the focal point while retaining readable text.
- Recordings should use the memory crystal or an echo symbol, not a living character portrait.
- Warden dialogue must use the Warden silhouette/portrait.
- Keep important action within the central safe area; avoid placing focal details underneath text or close to screen edges.
- Dialogue must remain readable with ambience and glow enabled.

## 10. Acceptance checklist

An asset set is ready for integration when:
- [ ] Pip and Warden are immediately distinguishable in silhouette and at portrait size.
- [ ] The five Spectrum colours match the canonical hex values.
- [ ] Sprite backgrounds are transparent where required.
- [ ] Source files are included, not just flattened exports.
- [ ] Faded, recovering, and restored states are distinguishable.
- [ ] Glow, particles, and moving components can be separated or disabled where needed.
- [ ] Recording scenes use a crystal/echo visual rather than a Pip tint.
- [ ] Inward and outward flow effects are visually distinct and reusable.
- [ ] The Warden's mark is consistent in all appearances.
- [ ] Every asset has a clear, consistent filename and folder location.
- [ ] Static fallbacks exist for animated assets.
- [ ] The final art has been previewed inside the real dialogue panel at mobile size.

## 11. Recommended production order

1. Approve Pip and Warden silhouette sheets first.
2. Approve the canonical palette and line/gradient treatment.
3. Produce one vertical slice: Forgotten Garden opening, Pip reveal, Warden's mark, one restoration sweep, and one Prism shard.
4. Integrate those assets in the real .NET MAUI cutscene panel and check legibility/performance.
5. Produce the remaining reusable effects and modular props.
6. Complete region backgrounds and the finale composition.
7. Polish optional ambience and secondary character poses last.

This order reduces the risk of producing dozens of assets before the visual style and in-game scale have been validated.

---

## Reference atlas note

The companion image titled **PopBlocks: The Shattered Spectrum — Visual Sprite Atlas** is a concept/reference sheet for style, composition, and asset inventory. It is not a production sprite sheet: individual objects must be redrawn/exported as separate assets, checked against the canonical palette, and approved at actual game scale.
