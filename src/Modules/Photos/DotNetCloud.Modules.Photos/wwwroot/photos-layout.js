/**
 * DotNetCloud Photos — viewport-driven grid measurement (ES module).
 *
 * WHY THIS EXISTS
 * ---------------
 * The photo gallery used a fixed page size (60), so on most screens the grid
 * overflowed and pushed the pager below the fold: the user had to scroll to
 * reach it, and "page 3 of 4" was already scrolling before it could be seen.
 *
 * This module measures the *visible* photo grid and reports how many fixed-size
 * cards fit on the screen **after** reserving room for the pager row, so the
 * Blazor page can pick a page size that fills the screen and still leaves the
 * pager visible at the bottom — no scrolling for the common case.
 *
 * The card itself is never resized (columns come from CSS `auto-fill`), so the
 * math always matches the rendered layout instead of duplicating breakpoints.
 *
 * Loaded from Blazor via a dynamic `import` of the module static asset
 * (`/_content/DotNetCloud.Modules.Photos/photos-layout.js`), which the app's
 * hardened CSP allows (`script-src 'self'`) — unlike `eval`.
 *
 * EXPORTS (called from PhotosPage.razor.cs via IJSObjectReference):
 *   attach(dotNetRef, gridId) — observe `.photos-main` and measure the grid with
 *       id `gridId`; report via dotNetRef.invokeMethodAsync("OnPhotosLayoutChanged",
 *       pageSize) whenever the computed page size changes. Returns `false` when the
 *       grid isn't mounted yet (e.g. the loading spinner is showing) so Blazor can
 *       retry on the next render.
 *   refresh()                 — re-measure immediately (called by Blazor after a
 *       page load re-renders the grid).
 *   detach()                  — disconnect the observer/listener.
 */

/** Debounce delay for resize callbacks (ms). */
const DEBOUNCE_MS = 200;

/** Pager height reserved when `.photos-pagination` isn't rendered yet (px). */
const PAGER_FALLBACK_HEIGHT = 74;

/** Upper bound on a single page, so an absurd viewport can never ask for thousands. */
const MAX_PAGE_SIZE = 500;

/** Active measurement state, or null when detached. */
let state = null;

/** Coerces a computed float to a whole number within [min, max] (NaN → min). */
function clampInt(value, min, max) {
  const whole = Math.floor(value);
  if (!isFinite(whole) || whole < min) return min;
  return whole > max ? max : whole;
}

/** Parses a CSS length, falling back when the value is missing or not finite. */
function px(value, fallback) {
  const parsed = parseFloat(value);
  return isFinite(parsed) ? parsed : fallback;
}

/**
 * Computes the columns × rows that fit the visible area (minus the pager row)
 * and reports the result through the .NET callback. No-op when the grid — or a
 * card inside it — isn't rendered yet, or when the computed page size is
 * unchanged (which also prevents feedback loops on re-render).
 */
function measure() {
  if (!state || !state.gridEl || !state.dotNetRef) return;

  const card = state.gridEl.querySelector(".photo-card");
  if (!card) return;

  const gridRect = state.gridEl.getBoundingClientRect();
  const mainRect = state.mainEl.getBoundingClientRect();
  const cardRect = card.getBoundingClientRect();

  // Real card dimensions from the rendered element, so grid *and* list view
  // (56px rows) are both handled without duplicating the CSS in JS.
  const cardWidth = cardRect.width;
  const cardHeight = cardRect.height;
  if (!cardWidth || !cardHeight) return;

  const cs = window.getComputedStyle(state.gridEl);
  const colGap = px(cs.columnGap, 8);
  const rowGap = px(cs.rowGap, colGap);
  const padTop = px(cs.paddingTop, 0);
  const padBottom = px(cs.paddingBottom, 0);

  const columns = clampInt((gridRect.width + colGap) / (cardWidth + colGap), 1, 64);

  // Reserve the pager row so it stays visible below the grid instead of being
  // pushed under the fold. Measured when it is already rendered (it collapses on
  // narrow screens), otherwise the CSS-derived fallback is used.
  const pager = document.querySelector(".photos-pagination");
  const pagerHeight = pager
    ? pager.getBoundingClientRect().height
    : PAGER_FALLBACK_HEIGHT;

  // Vertical space from the top of the grid down to the bottom of the visible
  // main area, minus the grid's own padding and the reserved pager row.
  const availableHeight =
    mainRect.bottom - gridRect.top - padTop - padBottom - pagerHeight;

  const rows = clampInt((availableHeight + rowGap) / (cardHeight + rowGap), 1, 64);

  const pageSize = clampInt(columns * rows, 1, MAX_PAGE_SIZE);

  // Only notify Blazor when the computed size actually changed.
  if (pageSize === state.lastPageSize) return;
  state.lastPageSize = pageSize;

  state.dotNetRef.invokeMethodAsync("OnPhotosLayoutChanged", pageSize).catch(() => {
    /* circuit may be gone */
  });
}

/** Debounced wrapper around measure(). */
function scheduleMeasure() {
  if (!state) return;
  if (state.timer) clearTimeout(state.timer);
  state.timer = setTimeout(() => {
    state.timer = null;
    measure();
  }, DEBOUNCE_MS);
}

/**
 * Observes the main content area. A ResizeObserver fires on window resizes AND
 * on sidebar collapse (both change `.photos-main`'s size) and reports once
 * immediately on attach. Falls back to a window `resize` listener when
 * ResizeObserver is unavailable.
 *
 * @param {object} dotNetRef Blazor DotNetObjectReference exposing OnPhotosLayoutChanged(int).
 * @param {string} gridId Id of the grid element to measure.
 * @returns {boolean} True when the observer was attached, false when the grid isn't rendered yet.
 */
export function attach(dotNetRef, gridId) {
  detach();

  const mainEl = document.querySelector(".photos-main");
  const gridEl = document.getElementById(gridId);
  if (!mainEl || !gridEl) return false;

  state = {
    mainEl,
    gridEl,
    dotNetRef,
    observer: null,
    timer: null,
    resizeListener: null,
    lastPageSize: -1,
  };

  if (window.ResizeObserver) {
    state.observer = new ResizeObserver(scheduleMeasure);
    state.observer.observe(mainEl);
  } else {
    state.resizeListener = scheduleMeasure;
    window.addEventListener("resize", state.resizeListener);
  }

  // Initial measurement (fires even before any resize).
  measure();
  return true;
}

/** Re-measures immediately (called by Blazor after a page load re-renders). */
export function refresh() {
  measure();
}

/** Disconnects the observer/listener and clears the state. */
export function detach() {
  if (!state) return;
  if (state.observer) state.observer.disconnect();
  if (state.resizeListener) window.removeEventListener("resize", state.resizeListener);
  if (state.timer) clearTimeout(state.timer);
  state = null;
}
