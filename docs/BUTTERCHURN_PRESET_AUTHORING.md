# Butterchurn Preset Authoring Reference

Understanding of the butterchurn/MilkDrop preset format — enough to write new, original visualizations.

## Overview

Butterchurn is a WebGL 2 port of MilkDrop 2, the classic Winamp music visualizer. It renders real-time, audio-reactive visuals using presets — JavaScript objects containing equations for motion, warping, color, and waveform rendering.

- **Core library**: `butterchurn.min.js` (v2.6.7, UMD)
- **Preset bundles**: `butterchurn-presets.min.js`, `butterchurnPresetsExtra.min.js`, `butterchurnPresetsExtra2.min.js`
- **DotNetCloud integration**: `butterchurn-visualizer.js` (360-line IIFE at `window.dotnetcloudVisualizer`)

## Preset Object Structure

Each preset is a JavaScript object returned by `getPresets()`:

```js
{
  baseVals: { /* ~50+ tuneable parameters */ },
  init_eqs_str: "...",    // Runs once when preset loads
  frame_eqs_str: "...",   // Runs every frame (~60fps)
  pixel_eqs_str: "...",   // Runs per mesh vertex (per-pixel interpolation)
  shapes: [ /* up to 4 custom shapes */ ],
  waves: [ /* up to 4 custom waveforms */ ],
  warp: "...",            // HLSL warp shader (optional, MilkDrop 2 only)
  comp: "..."             // HLSL composite shader (optional, MilkDrop 2 only)
}
```

## Variables

Variables in equation strings use the `a.` prefix in butterchurn's bundled format (the original `.milk` format uses bare names). For example, `zoom` in a `.milk` file becomes `a.zoom` in the JS preset object.

### Audio-Reactive (read-only)

| Variable | Description |
|----------|-------------|
| `bass` | Immediate bass level (~0.7 quiet, ~1.3 loud) |
| `mid` | Middle frequencies |
| `treb` | Treble (high) frequencies |
| `bass_att` | Damped/attenuated bass (smoother) |
| `mid_att` | Damped mid |
| `treb_att` | Damped treble |
| `vol` | Immediate volume |

### Spatial (read-only, per-vertex/pixel code only)

| Variable | Range | Description |
|----------|-------|-------------|
| `x` | 0..1 | Horizontal position — 0=left, 0.5=center, 1=right |
| `y` | 0..1 | Vertical position — 0=top, 0.5=center, 1=bottom |
| `rad` | 0..1 | Distance from screen center — 0=center, 1=corners |
| `ang` | 0..2π | Angle from center — 0=right, π/2=up, π=left |

### Temporal (read-only)

| Variable | Description |
|----------|-------------|
| `time` | Seconds since visualization started |
| `frame` | Frames elapsed |
| `fps` | Current framerate |
| `progress` | 0..1 progress through current preset (freezes with Scroll Lock) |

### Motion Parameters (read/write, per-frame)

| Variable | Range | Description |
|----------|-------|-------------|
| `zoom` | >0 | Inward/outward motion. 0.9=zoom out 10%, 1.0=none, 1.1=zoom in 10% |
| `zoomexp` | >0 | Curvature of zoom; 1=normal |
| `rot` | any | Rotation. 0=none, 0.1=slightly right, -0.1=clockwise |
| `warp` | >0 | Warping magnitude. 0=none, 1=normal, 2=major |
| `cx` | 0..1 | Horizontal center of rotation/stretching |
| `cy` | 0..1 | Vertical center of rotation/stretching |
| `dx` | any | Horizontal motion per frame (-0.01=left 1%, 0.01=right 1%) |
| `dy` | any | Vertical motion per frame |
| `sx` | >0 | Horizontal stretch. 0.99=shrink 1%, 1.01=stretch 1% |
| `sy` | >0 | Vertical stretch |

### Waveform Parameters (read/write, per-frame)

| Variable | Range | Description |
|----------|-------|-------------|
| `wave_mode` | 0..7 | Waveform type (0-7 different patterns) |
| `wave_x` | 0..1 | Horizontal position |
| `wave_y` | 0..1 | Vertical position |
| `wave_r` | 0..1 | Red component |
| `wave_g` | 0..1 | Green component |
| `wave_b` | 0..1 | Blue component |
| `wave_a` | 0..1 | Opacity |
| `wave_scale` | >0 | Scale of waveform |
| `wave_smoothing` | 0..1 | Smoothing amount |
| `wave_mystery` | -1..1 | Different effect per waveform type |
| `wave_usedots` | 0/1 | Draw as dots instead of lines |
| `wave_thick` | 0/1 | Double-thickness lines/dots |
| `wave_additive` | 0/1 | Additive blending (saturates at white) |
| `wave_brighten` | 0/1 | Scale r/g/b until one reaches 1.0 |

### Border Parameters (read/write, per-frame)

| Variable | Range | Description |
|----------|-------|-------------|
| `ob_size` | 0..0.5 | Outer border thickness |
| `ob_r/g/b/a` | 0..1 | Outer border color + opacity |
| `ib_size` | 0..0.5 | Inner border thickness |
| `ib_r/g/b/a` | 0..1 | Inner border color + opacity |

### Motion Vectors (read/write, per-frame)

| Variable | Range | Description |
|----------|-------|-------------|
| `mv_x` | 0..64 | Motion vector count in X |
| `mv_y` | 0..48 | Motion vector count in Y |
| `mv_l` | 0..5 | Motion vector trail length (0=no trail) |
| `mv_r/g/b/a` | 0..1 | Motion vector color + opacity |
| `mv_dx` | -1..1 | Horizontal drift |
| `mv_dy` | -1..1 | Vertical drift |

### Post-Processing (read/write, per-frame)

| Variable | Range | Description |
|----------|-------|-------------|
| `decay` | 0..1 | Fade to black. 1=no fade, 0.9=strong, 0.98=recommended |
| `gamma` | >0 | Display brightness. 1=normal, 2=double |
| `echo_zoom` | >0 | Second graphics layer size |
| `echo_alpha` | >0 | Second layer opacity |
| `echo_orient` | 0..3 | Orientation: 0=normal, 1=flip x, 2=flip y, 3=flip both |
| `darken_center` | 0/1 | Dim center to prevent over-brightening |
| `wrap` | 0/1 | Elements drift off one side onto the opposite |
| `invert` | 0/1 | Invert image colors |
| `brighten` | 0/1 | Brighten dark areas (sqrt filter) |
| `darken` | 0/1 | Darken bright areas (squaring filter) |
| `solarize` | 0/1 | Emphasize mid-range colors |

### Q-Variables (read/write, cross-pool communication)

`q1` through `q32` — carry values between variable pools:
- **Preset init code** sets base values
- **Per-frame code** can read/modify (resets to init values each frame)
- **Per-vertex code** receives values from end of per-frame code
- **Pixel shaders** receive as `float q1`..`float q32` inputs

### Custom User Variables

Any `a.varname` assignment creates a custom variable. Up to ~30 supported. Custom variables persist frame-to-frame *within* their variable pool but do NOT cross pools — use q1-q32 for that.

## Variable Pools and Execution Order

There are three separate variable pools:
1. **Preset init code + preset per-frame code** — Main effects
2. **Custom wave init + per-frame + per-point code** — Each wave has its own pool
3. **Custom shape init + per-frame code** — Each shape has its own pool

Init code runs once on preset load. Per-frame runs every frame. Values written in init are visible in the paired per-frame code. Values persist frame-to-frame.

## Math Functions

| Function | Description |
|----------|-------------|
| `Math.sin(x)` | Sine (radians) |
| `Math.cos(x)` | Cosine (radians) |
| `Math.atan2(y,x)` | Arctangent, returns radians |
| `Math.abs(x)` | Absolute value |
| `Math.min(a,b)` | Minimum |
| `Math.max(a,b)` | Maximum |
| `Math.pow(a,b)` | a raised to power b |
| `Math.sqrt(x)` | Square root |
| `Math.floor(x)` | Floor |
| `Math.ceil(x)` | Ceiling |
| `div(a,b)` | Safe division (returns 0 if b is 0) |
| `if(cond, then, else)` | Conditional |
| `bnot(x)` | Bitwise NOT |
| `rand(n)` | Random number (0..1) that changes every n seconds |
| `above(val, thresh)` | Returns 1 if val > thresh, else 0 |
| `below(val, thresh)` | Returns 1 if val < thresh, else 0 |
| `equal(val, target)` | Returns 1 if val == target, else 0 |
| `sign(x)` | Sign of x |

## Custom Waves

Up to 4 custom waves per preset. Each has:

```js
{
  baseVals: {
    enabled: 1,
    samples: 512,     // Number of waveform samples
    spectrum: 0,      // 0=waveform, 1=spectrum
    additive: 0,
    usedots: 0,
    scaling: 1,
    smoothing: 0.5,
    r: 1, g: 1, b: 1, a: 1,
    sep: 0
  },
  init_eqs_str: "...",
  frame_eqs_str: "...",   // Controls r, g, b, a, samples
  point_eqs_str: "..."    // Controls per-point x, y, r, g, b, a
}
```

**Per-point variables**: `x`, `y`, `sample` (0..1 through samples), `value1` (left channel), `value2` (right channel), `r`, `g`, `b`, `a`.

## Custom Shapes

Up to 4 custom shapes per preset. Draw n-sided polygons with optional texture feedback.

```js
{
  baseVals: {
    enabled: 1,
    sides: 4,          // 3-100
    x: 0.5, y: 0.5,   // Position
    rad: 0.5,          // Radius
    ang: 0,            // Rotation
    textured: 0,       // 0=solid, 1=map previous frame (feedback)
    tex_zoom: 1,
    tex_ang: 0,
    additive: 0,
    thick: 0,
    r: 1, g: 1, b: 1, a: 1,    // Center color
    r2: 0, g2: 0, b2: 0, a2: 0, // Edge color
    border_r: 0, border_g: 0, border_b: 0, border_a: 0,
    num_inst: 1        // Instance count (1-1024)
  },
  init_eqs_str: "...",
  frame_eqs_str: "..."
}
```

When `textured=1`, the shape samples the previous frame's image, enabling video-feedback fractal effects.

Instancing (`num_inst` > 1): the `instance` variable (0 to num_inst-1) is available in per-frame code to vary each instance.

## Techniques for Original Presets

### Audio-Reactive Motion
```
a.zoom = 1 + 0.02*a.bass_att;
a.rot = 0.1*a.bass*Math.sin(a.time);
a.warp = 0.5 + 0.3*a.treb_att;
```

### Spatial Distortion (pixel equations)
```
a.zoom = 1 + 0.05*Math.sin(a.rad*10 + a.time);
a.rot = 0.1*a.rad*Math.cos(a.ang*3 + a.time*0.5);
```

### Color Cycling
```
a.wave_r = 0.5 + 0.5*Math.sin(a.time*1.13);
a.wave_g = 0.5 + 0.5*Math.sin(a.time*1.23 + 2.1);
a.wave_b = 0.5 + 0.5*Math.sin(a.time*1.33 + 4.2);
```

### Stateful Growth via Q-Variables
```
// init
a.phase = 0;

// per-frame
a.phase += 0.01*a.bass;
a.q1 = Math.sin(a.phase);

// per-pixel
a.zoom = 1 + 0.1*a.q1*Math.sin(a.rad*5);
```

### Organic Flow Fields (pixel equations)
```
a.dx = 0.005*Math.sin(a.x*6 + a.time)*Math.cos(a.y*4 + a.time*0.7);
a.dy = 0.005*Math.cos(a.x*5 + a.time*0.8)*Math.sin(a.y*7 + a.time);
```

### Feedback Fractals (custom shapes)
```
// per-frame on a textured custom shape
a.rad = 0.5 + 0.2*a.bass_att;
a.ang = a.ang + 0.01;
a.tex_zoom = 1.01;
a.r = 0.5 + 0.5*Math.sin(a.time*0.5);
a.g = 0.5 + 0.5*Math.sin(a.time*0.7 + 1);
a.b = 0.5 + 0.5*Math.sin(a.time*0.6 + 2);
```

### Motion Vector Smearing
```
a.mv_x = 32;
a.mv_y = 24;
a.mv_l = 2 + 3*a.bass_att;
a.mv_r = 0.3;
a.mv_g = 0.3;
a.mv_b = 0.5;
a.mv_a = 0.5*a.bass;
```

## Integration Into DotNetCloud

To add custom presets to DotNetCloud:

1. Create a new JS file (e.g., `dotnetcloud-presets.js`) following the UMD pattern:
```js
window.dotnetcloudPresets = (function() {
  function getPresets() {
    return {
      "My Custom Preset": {
        baseVals: { /* ... */ },
        init_eqs_str: "...",
        frame_eqs_str: "...",
        pixel_eqs_str: "...",
        shapes: [ /* 4 disabled shapes */ ],
        waves: [ /* 4 disabled waves */ ]
      }
    };
  }
  return { getPresets: getPresets };
})();
```

2. Add a `<script>` tag to `App.razor`
3. Register in `butterchurn-visualizer.js`'s `loadAllPresetsFromLibs()` function

## Preset Design Categories

Based on understanding of the parameter space, new presets could target:

| Style | Key Techniques |
|-------|---------------|
| **Ambient/atmospheric** | Slow zoom drift, subtle bass-reactive glow, high decay, low warp |
| **Geometric/architectural** | Sharp custom shapes, structured rotation, grid-like warp patterns |
| **Chaotic/glitch** | High warp, fast rotation, bass-driven jumps, low decay |
| **Organic/fluid** | Sin/cos flow fields in pixel equations, smooth Q-variable state machines |
| **Minimal/clean** | Single waveform, monochromatic/duotone, subtle motion |
| **Retrowave/synthwave** | Sun color palette via wave_r/g/b cycling, grid warps, slow persistent zoom |
| **Tunnel/vortex** | Strong radial zoom + rot in pixel equations |
| **Reaction-diffusion** | Custom shapes with texture feedback + Q-variable state |

## Limitations

- **Pixel shaders (warp/comp HLSL)** — understood structurally but non-trivial shaders require visual testing and iteration
- **Complex instanced shapes** — `num_inst` with per-instance parameterization needs visual feedback to tune
- **No live preview** — preset tuning is inherently visual; real development requires a rendering test harness
- **Some MilkDrop 2 features** — custom messages, sprites, and disk-loaded textures are butterchurn-supported but less documented
