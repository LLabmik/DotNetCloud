# SyncTray Icon Enhancement Plan

> **Created:** 2026-03-29
> **Scope:** Add status indicator symbols to the existing colored circle tray icons (Windows + Linux)
> **Affected file:** `src/Clients/DotNetCloud.Client.SyncTray/TrayIconManager.cs`

---

## Goal

The current tray icons are plain colored circles. While the color conveys status, it's not immediately obvious what each color means — especially for new users or colorblind users. Add a **white symbol/glyph overlay** on each circle to reinforce the status meaning at a glance.

---

## Current State → Target State

| TrayState | Color | Current Icon | Proposed Overlay | Symbol Description |
|-----------|-------|-------------|-----------------|-------------------|
| **Idle** | Green `#00B040` | Plain circle | **✓ Checkmark** | White checkmark — "all good, fully synced" |
| **Syncing** | Blue `#0078D4` | Plain circle | **⟳ Sync arrows** | Two curved arrows forming a cycle — "syncing in progress" |
| **Paused** | RebeccaPurple `#663399` | Plain circle | **⏸ Pause bars** | Two vertical bars — "paused" |
| **Error** | Crimson `#C41E3A` | Plain circle | **✕ X mark** | Bold X — "error occurred" |
| **Conflict** | Dark Orange `#FF8C00` | Plain circle | **! Exclamation** | Exclamation mark — "action needed" |
| **Offline** | Grey `#707070` | Plain circle | **⏻ Power/loading** | Horizontal line or dash — "not connected / starting up" |

---

## Design Specifications

### General Rules

- **Icon size:** 32×32 pixels (unchanged)
- **Symbol color:** White (`#FFFFFF`) for all overlays — maximum contrast against every background color
- **Symbol area:** Centered within the circle, occupying roughly 50–60% of the circle diameter (~14–16px symbol within ~28px circle)
- **Anti-aliasing:** All symbol edges must be anti-aliased to match the existing circle edge quality
- **Chat badge:** Existing chat unread/mention badge (top-right corner) must remain functional and render ON TOP of the new symbols
- **Rendering order:** Circle → Symbol overlay → Chat badge

### Per-Symbol Pixel Rendering Details

All symbols are drawn into the same `byte[] pixels` buffer after the circle is drawn and before the chat badge is applied. White = `(R:255, G:255, B:255)` with premultiplied alpha.

#### 1. Checkmark (Idle/Green)

```
Shape: Two line segments forming a ✓
- Short leg: from (~8,16) to (~12,20)   — 45° downward-right
- Long leg:  from (~12,20) to (~22,10)  — ~45° upward-right
Stroke width: 2.5–3px with anti-aliased edges
```

Visual (approximate at 32×32):
```
        ╲
         ╲
    ╲     ╲
     ╲   ╱  (no, just two joined lines)
      ╲ ╱
       V
```

#### 2. Sync Arrows (Syncing/Blue)

```
Shape: Two curved arrows forming a circular refresh cycle
- Upper arrow: arc from ~2 o'clock to ~10 o'clock, arrowhead pointing clockwise
- Lower arrow: arc from ~8 o'clock to ~4 o'clock, arrowhead pointing clockwise
Alternative (simpler): Two straight arrows ↑↓ offset horizontally
Stroke width: 2px with anti-aliased edges
```

**Simpler alternative if curved arrows are too complex at 32px:**
Two opposing arrows (↻ style) — a downward arrow on the left, upward arrow on the right, suggesting circular motion.

#### 3. Pause Bars (Paused/Amber)

```
Shape: Two vertical rectangles (standard pause symbol)
- Left bar:  x=11–13, y=9–22  (3px wide, 14px tall)
- Right bar: x=18–20, y=9–22  (3px wide, 14px tall)
- Gap between bars: ~5px
Rounded corners optional (1px radius)
```

#### 4. X Mark (Error/Red)

```
Shape: Two diagonal lines crossing at center
- Line 1: from (~9,9)   to (~22,22)  — top-left to bottom-right
- Line 2: from (~22,9)  to (~9,22)   — top-right to bottom-left
Stroke width: 2.5–3px with anti-aliased edges
```

#### 5. Exclamation Mark (Conflict/Orange)

```
Shape: Vertical line + dot below
- Stem: centered at x=15.5, from y=8 to y=18 (stroke width ~3px)
- Dot:  centered at (15.5, 22), radius ~1.8px
```

#### 6. Dash / Horizontal Line (Offline/Grey)

```
Shape: Horizontal line (minus sign / em dash) — "disconnected"
- Line: from (~9, 15.5) to (~22, 15.5)
- Stroke width: 2.5–3px with anti-aliased edges
Alternative: Three dots (ellipsis ···) suggesting "loading/waiting"
```

---

## Implementation Approach

### Option A: Pixel-Level Drawing (Extend Current Approach) — **Recommended**

Continue the current `WriteableBitmap` pixel manipulation approach. Add a new method per symbol (or a single method with a switch) that draws white pixels into the buffer after the circle and before the badge.

**Pros:**
- No new dependencies
- Consistent with existing codebase
- Full control over every pixel
- No font/glyph rendering complexity

**Cons:**
- More code to write (line drawing, anti-aliasing math)
- Harder to iterate on design

**Structure:**
```csharp
// After circle drawing, before badge:
DrawStatusSymbol(pixels, size, centre, radius, state);

private static void DrawStatusSymbol(byte[] pixels, int size, float centre, float radius, TrayState state)
{
    switch (state)
    {
        case TrayState.Idle:     DrawCheckmark(pixels, size, centre, radius); break;
        case TrayState.Syncing:  DrawSyncArrows(pixels, size, centre, radius); break;
        case TrayState.Paused:   DrawPauseBars(pixels, size, centre, radius); break;
        case TrayState.Error:    DrawXMark(pixels, size, centre, radius); break;
        case TrayState.Conflict: DrawExclamation(pixels, size, centre, radius); break;
        case TrayState.Offline:  DrawDash(pixels, size, centre, radius); break;
    }
}
```

Helper needed: `DrawAntiAliasedLine(pixels, size, x0, y0, x1, y1, thickness, r, g, b)` — reusable for checkmark, X, dash, and arrow segments.

### Option B: SkiaSharp Rendering

Use SkiaSharp (already an Avalonia dependency) to draw paths/shapes onto an `SKBitmap`, then convert to Avalonia `Bitmap`.

**Pros:**
- Rich path drawing API (arcs, beziers for sync arrows)
- Built-in anti-aliasing
- Easier to iterate on shapes

**Cons:**
- New dependency coupling for tray icons
- Bitmap format conversion step
- May be overkill for 6 simple symbols

### Recommendation

**Option A** — pixel-level drawing. The symbols are simple geometric shapes. A reusable anti-aliased line drawing helper covers checkmark, X, dash, and arrow segments. Pause bars are just filled rectangles. Exclamation is a filled rectangle + circle. This keeps the codebase consistent and dependency-free.

---

## Task Breakdown

| # | Task | Estimate | Notes |
|---|------|----------|-------|
| 1 | Create `DrawAntiAliasedLine()` helper | Small | Reusable for multiple symbols; Xiaolin Wu's algorithm or similar |
| 2 | Create `DrawFilledCircleAt()` helper | Small | For exclamation dot and potentially arrow tips |
| 3 | Implement `DrawCheckmark()` | Small | Two line segments joined at bottom vertex |
| 4 | Implement `DrawXMark()` | Small | Two crossing diagonal lines |
| 5 | Implement `DrawDash()` | Small | Single horizontal line |
| 6 | Implement `DrawPauseBars()` | Small | Two filled rectangles |
| 7 | Implement `DrawExclamation()` | Small | Vertical line + dot |
| 8 | Implement `DrawSyncArrows()` | Medium | Most complex — curved arrows or simplified opposing arrows |
| 9 | Wire `DrawStatusSymbol()` into `CreateCircleBitmap()` | Small | Insert between circle and badge rendering |
| 10 | Test on Windows (Windows11-TestDNC) | — | Visual verification of all 6 states |
| 11 | Test on Linux (mint-dnc-client) | — | Visual verification of all 6 states |
| 12 | Verify chat badges still render correctly on top | — | Ensure badge compositing works with new symbols |

---

## Visual Mockup (ASCII Art, 32×32 conceptual)

```
  IDLE (Green+✓)      SYNCING (Blue+↻)     PAUSED (Purple+⏸)

   ╭──────────╮        ╭──────────╮        ╭──────────╮
   │          │        │    ↓     │        │  ▐▌ ▐▌  │
   │      ╱   │        │  ╭─╯    │        │  ▐▌ ▐▌  │
   │    ╱╱    │        │  ╰─╮    │        │  ▐▌ ▐▌  │
   │  ╲╱     │        │    ↑     │        │  ▐▌ ▐▌  │
   ╰──────────╯        ╰──────────╯        ╰──────────╯

  ERROR (Red+✕)       CONFLICT (Orange+!)   OFFLINE (Grey+—)

   ╭──────────╮        ╭──────────╮        ╭──────────╮
   │  ╲    ╱  │        │    █     │        │          │
   │   ╲  ╱   │        │    █     │        │  ──────  │
   │   ╱  ╲   │        │    █     │        │          │
   │  ╱    ╲  │        │    ●     │        │          │
   ╰──────────╯        ╰──────────╯        ╰──────────╯
```

---

## Accessibility Notes

- White symbols on colored backgrounds meet WCAG contrast requirements for all 6 colors
- Symbols remove sole reliance on color to convey status (benefits colorblind users)
- Tooltip text already describes the status in words (no change needed)

---

## Files Modified

| File | Change |
|------|--------|
| `src/Clients/DotNetCloud.Client.SyncTray/TrayIconManager.cs` | Add symbol drawing methods, wire into `CreateCircleBitmap()` |

No new files, no new dependencies, no new assets needed.
