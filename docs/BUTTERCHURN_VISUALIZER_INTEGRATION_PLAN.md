# Butterchurn Visualizer Integration — Music Module

## TL;DR

Integrate [Butterchurn](https://github.com/jberg/butterchurn) (WebGL MilkDrop visualizer) into the DotNetCloud music module by extending the existing Web Audio API infrastructure in `music-player.js`, adding a visualizer canvas to the Blazor UI, and exposing JS interop controls for toggling, preset selection, and fullscreen mode.

**Zero server-side changes** — this is entirely client-side (JS + Blazor components). No new API endpoints, database changes, or module host changes.

## Key Architecture Insight

The existing `music-player.js` already creates an `AudioContext` and `MediaElementAudioSourceNode` for the 10-band EQ. Butterchurn's `connectAudio(audioNode)` internally creates an `AnalyserNode` and connects to the provided node. Web Audio API supports fan-out — a single `sourceNode` can connect to both the EQ chain AND butterchurn's analyser simultaneously. No audio routing changes needed.

**Audio graph after integration:**
```
sourceNode → eqFilter[0] → ... → eqFilter[9] → audioCtx.destination  (existing)
     ↓
butterchurn.analyserNode  (new — passive listener, no audible effect)
```

---

## Decisions (Resolved)

| Decision | Resolution |
|----------|-----------|
| **Library version** | v2.6.7 stable (UMD). The 3.0.0-beta.5 ES module build can be revisited later. UMD works with simple `<script>` tags. |
| **Presets** | Ship a curated subset (~20-30 best presets) eagerly. Full `butterchurn-presets` bundle (~3.5MB) lazy-loaded on demand via "Load All Presets" button + dynamic `<script>` injection. |
| **Canvas placement** | Overlay on main content area (sidebar stays visible). Most natural UX pattern. |
| **Mini-visualizer** | Small canvas in the now-playing bar placed NEXT TO album art (does NOT replace it). Hidden when visualizer is off. |
| **EQ icon** | Replace `⚙` gear with inline SVG of 3 vertical sliders. Resolves conflict with `⚙` used for Library Settings in sidebar. |
| **Visualizer toggle placement** | In `np-extras`, grouped with Queue (☰) and Equalizer buttons. Order: Queue, Equalizer, Visualizer (🎆), Volume. |
| **User preference persistence** | **DEFERRED** to follow-up. Session-only state for initial implementation. See [Deferred Work](#deferred-work). |

---

## Phase 0 (Pre-work): EQ Icon Cleanup

### Step 0.1 — Replace EQ gear icon with inline SVG sliders

- In `MusicPage.razor`, replace the `⚙` character on the Equalizer button with an inline SVG depicting 3 vertical sliders (equalizer bars at varying heights)
- Small (~16×16 or 1em) inline SVG, styled to match existing `np-btn` icon color/size
- Removes semantic conflict with `⚙` used for "Library Settings" in the sidebar nav

**File:** `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor`

---

## Phase 1: Asset Acquisition & Setup

### Step 1.1 — Download Butterchurn bundles into wwwroot

- Download pre-built `butterchurn.min.js` (v2.6.7 stable UMD from unpkg/jsdelivr) into `src/UI/DotNetCloud.UI.Web/wwwroot/lib/butterchurn/`
- Download `butterchurn-presets.min.js` (full set) into the same directory — lazy-loaded only on demand
- Download `isSupported.min.js` for feature detection
- Create `curated-presets.js` — a small file exporting ~20-30 hand-picked presets (sourced from butterchurn-presets). Loaded eagerly with the page.

**Note:** Project uses no npm — all third-party JS is vendored in `wwwroot/lib/` (same pattern as highlight.js in `wwwroot/lib/highlightjs/`).

### Step 1.2 — Add script tags to App.razor

- Add `<script>` tags in `src/UI/DotNetCloud.UI.Web/Components/App.razor` after existing scripts
- Use `_content/DotNetCloud.UI.Web/lib/butterchurn/` path prefix with cache-bust query string
- Load eagerly: `butterchurn.min.js`, `isSupported.min.js`, `curated-presets.js`
- Do NOT eagerly load: `butterchurn-presets.min.js` (lazy-loaded on demand from JS)

---

## Phase 2: JavaScript Interop Layer

### Step 2.1 — Create `butterchurn-visualizer.js`

**New file:** `src/UI/DotNetCloud.UI.Web/wwwroot/js/butterchurn-visualizer.js`

Exposes `window.dotnetcloudVisualizer` with these functions:

| Function | Purpose |
|----------|---------|
| `init(canvasId)` | Get canvas element, check WebGL2 support |
| `start(audioContext, sourceNode)` | Create butterchurn visualizer, connect audio, start `requestAnimationFrame` render loop |
| `stop()` | Cancel animation frame, disconnect |
| `loadPreset(presetName, blendSeconds)` | Switch preset with blend transition |
| `getPresetNames()` | Return array of available preset names (curated initially; expanded after full load) |
| `loadAllPresets()` | Dynamically inject `butterchurn-presets.min.js` script tag, merge into available presets, return updated list |
| `randomPreset(blendSeconds)` | Pick random preset from available set |
| `setSize(width, height)` | Resize renderer |
| `initMiniCanvas(canvasId)` | Set up a second small canvas for the now-playing bar thumbnail visualizer |
| `enterFullscreen()` / `exitFullscreen()` | Fullscreen API |
| `isSupported()` | WebGL2 feature gate |
| `dispose()` | Full cleanup (cancel rAF, disconnect audio, release WebGL context) |

**Internals:** `requestAnimationFrame` render loop calls `visualizer.render()` each frame. Loop starts/stops with `start()`/`stop()`.

### Step 2.2 — Extend `music-player.js` to expose audio internals

Add to the return object of `music-player.js`:
- `getAudioContext()` — returns `audioCtx`
- `getSourceNode()` — returns `sourceNode`

These allow `butterchurn-visualizer.js` to tap into the existing audio graph without duplicating the AudioContext.

**File:** `src/UI/DotNetCloud.UI.Web/wwwroot/js/music-player.js`

---

## Phase 3: Blazor UI — Visualizer Component

### Step 3.1 — Add visualizer canvas and toggle to MusicPage.razor

- Add a `<canvas id="dnc-visualizer-canvas">` element in the main content area
- When visualizer is active, canvas overlays the main content (absolute positioned, z-indexed above content but below dialogs)
- Add a visualizer toggle button (🎆) in the `np-extras` section of the now-playing bar, grouped with the existing Queue (☰) and Equalizer buttons
- Canvas is hidden when visualizer is off

### Step 3.2 — Mini-visualizer in now-playing bar

- Add a small `<canvas id="dnc-mini-visualizer">` (~52×52px) in the `np-track-info` section, placed NEXT TO (not replacing) the album art
- When visualizer is active: mini canvas is visible alongside album art
- When visualizer is off: mini canvas is hidden, album art displays normally
- Mini canvas renders a downscaled copy of the same butterchurn output (same preset/state)
- JS `initMiniCanvas()` sets this up as a secondary render target at low resolution

### Step 3.3 — Add visualizer controls overlay to MusicPage.razor

- Preset selector: dropdown or horizontal scrollable list of preset names
- "Load All Presets" button — triggers `loadAllPresets()`, shows loading spinner while `butterchurn-presets.min.js` is fetched, then refreshes preset list
- Auto-cycle toggle: checkbox + interval input (e.g., cycle every 30s)
- Fullscreen button
- Close/minimize button
- Blend duration slider (0-5s for preset transitions)

### Step 3.4 — Visualizer state in MusicPage.razor.cs

**New state fields:**
```csharp
private bool _showVisualizer;
private string[] _visualizerPresets = [];
private string? _selectedPreset;
private bool _autoCyclePresets;
private int _autoCycleInterval = 30;        // seconds
private double _visualizerBlendDuration = 2; // seconds
private bool _allPresetsLoaded;
private bool _loadingAllPresets;
```

**New methods:**
- `ToggleVisualizerAsync()` — Init/destroy visualizer via JS interop
- `ChangePresetAsync(string presetName)` — Load preset via JS interop
- `RandomPresetAsync()` — Random via JS interop
- `LoadAllPresetsAsync()` — Trigger lazy-load of full preset bundle via JS interop
- `ToggleFullscreenAsync()` — Fullscreen via JS interop

### Step 3.5 — CSS for visualizer

In `MusicPage.razor.css`:
- `.visualizer-container` — Absolute fill within `.music-main`, hidden by default
- `.visualizer-canvas` — Full width/height, border-radius matching content area
- `.visualizer-controls` — Overlay bar at top of visualizer (semi-transparent, fade in on hover)
- `.visualizer-active .music-main` — Content hidden or dimmed when visualizer is on
- `.mini-visualizer` — 52×52px canvas next to album art, rounded corners, hidden by default
- Fullscreen styles

---

## Phase 4: Lifecycle & Performance

### Step 4.1 — Start/stop with playback

- When a track starts playing AND visualizer is toggled on → `start()`
- When playback stops/pauses → `stop()` the render loop (saves GPU)
- When track changes → continue rendering (no restart needed, audio node stays connected)
- On `dispose()` (page navigation) → full cleanup

### Step 4.2 — Auto-cycle presets

- If `_autoCyclePresets` is on, start a `setInterval` in JS that calls `randomPreset()` every N seconds
- Cancel timer when visualizer is turned off or playback stops

### Step 4.3 — Responsive canvas sizing

- `ResizeObserver` on the canvas container to match dimensions
- Call `visualizer.setRendererSize()` on resize
- Cap resolution for performance (e.g., max 1920×1080 even on 4K displays)

---

## Files Changed / Created

| File | Action | Description |
|------|--------|-------------|
| `src/UI/DotNetCloud.UI.Web/wwwroot/lib/butterchurn/butterchurn.min.js` | **New** | Vendored butterchurn v2.6.7 UMD bundle |
| `src/UI/DotNetCloud.UI.Web/wwwroot/lib/butterchurn/isSupported.min.js` | **New** | WebGL2 feature detection |
| `src/UI/DotNetCloud.UI.Web/wwwroot/lib/butterchurn/curated-presets.js` | **New** | ~20-30 hand-picked presets (eagerly loaded) |
| `src/UI/DotNetCloud.UI.Web/wwwroot/lib/butterchurn/butterchurn-presets.min.js` | **New** | Full preset bundle (~3.5MB, lazy-loaded) |
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/butterchurn-visualizer.js` | **New** | All butterchurn integration + render loop logic |
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/music-player.js` | **Edit** | Add `getAudioContext()` and `getSourceNode()` to return object |
| `src/UI/DotNetCloud.UI.Web/Components/App.razor` | **Edit** | Add `<script>` tags for butterchurn + curated presets |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor` | **Edit** | Canvas elements, toggle button, preset controls, EQ icon |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.cs` | **Edit** | Visualizer state fields + JS interop methods |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.css` | **Edit** | Visualizer container, controls overlay, mini-visualizer, fullscreen styles |

---

## Verification Checklist

| # | Test | Expected Result |
|---|------|----------------|
| 1 | Toggle visualizer on browser without WebGL2 | Graceful fallback message, no crash |
| 2 | Toggle visualizer on/off while music plays | Audio uninterrupted, EQ still works |
| 3 | Open browser DevTools Performance tab | rAF loop starts on open, stops on close |
| 4 | Cycle through 5+ presets | Smooth blend transitions, no visual glitches |
| 5 | Enter/exit fullscreen | Canvas resizes correctly, controls accessible |
| 6 | Navigate away from music page | No orphaned rAF loops, no JS console errors |
| 7 | Pause playback with visualizer on | Render loop pauses, GPU usage drops |
| 8 | Click "Load All Presets" | Spinner shows, full preset list loads, dropdown updates |
| 9 | Mini-visualizer in now-playing bar | Renders alongside album art, does not replace it |
| 10 | New EQ icon | SVG sliders visible, correct size, no visual regression |

---

## Deferred Work

### Visualizer preference persistence (follow-up)

**Status:** Deferred — not included in initial implementation.

**Scope:** Save the following per-user via `UserMusicPreference` entity (or new `UserVisualizerPreference`):
- Visualizer on/off default state
- Last-used preset name
- Auto-cycle on/off + interval
- Blend duration preference

**Reason for deferral:** Requires DB migration + service changes. Initial implementation uses session-only state to ship the feature faster and validate UX before committing to a schema.

**Tracked in:** This section. Will be picked up in a follow-up sprint.
