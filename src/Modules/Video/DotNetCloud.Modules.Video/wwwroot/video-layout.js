/**
 * DotNetCloud Video Layout — measures the visible video grid and reports the
 * number of fixed-size cards that fit the viewport, so the Blazor page can size
 * its paging to fill the screen ("as many videos as possible") without resizing
 * the video cards themselves.
 *
 * Plain ES5 script (no modules, no build step), wrapped in an IIFE.
 * Exposes `window.DotNetCloudVideoLayout`.
 *
 * Public API:
 *   DotNetCloudVideoLayout.attach(dotNetRef, gridId) — observe `.video-main` and
 *       measure the grid with id `gridId`; report via dotNetRef.invokeMethodAsync(
 *       "OnVideoLayoutChanged", pageSize) whenever the computed page size changes.
 *   DotNetCloudVideoLayout.refresh()                  — re-measure immediately
 *       (used after a page load changes the rendered grid).
 *   DotNetCloudVideoLayout.detach()                   — disconnect observer/listeners.
 */
(function () {
  "use strict";

  var layout = window.DotNetCloudVideoLayout || {};
  window.DotNetCloudVideoLayout = layout;

  /** Debounce delay for resize callbacks (ms). */
  var DEBOUNCE_MS = 200;

  /** Bottom breathing room subtracted from the available height (px). */
  var BOTTOM_PAD = 24;

  /** @type {Object|null} Active measurement state, or null when detached. */
  var state = null;

  /** Coerces a computed float to a whole number >= `min` (NaN/inf → min). */
  function clampInt(value, min) {
    value = Math.floor(value);
    return isFinite(value) && value > min ? value : min;
  }

  /**
   * Computes columns × rows that fit in the visible area and reports the result
   * through the .NET callback. No-op when the grid (or a card inside it) is not
   * yet rendered, or when the computed page size hasn't changed.
   */
  function measure() {
    if (!state || !state.gridEl || !state.dotNetRef) return;

    var card = state.gridEl.querySelector(".video-card");
    if (!card) return;

    var gridRect = state.gridEl.getBoundingClientRect();
    var mainRect = state.mainEl.getBoundingClientRect();
    var cardRect = card.getBoundingClientRect();

    // Real, fixed-size card dimensions from the rendered element (240px-wide
    // columns — never stretched), so the math always matches the CSS layout.
    var cardWidth = cardRect.width;
    var cardHeight = cardRect.height;
    if (!cardWidth || !cardHeight) return;

    // Column gap == row gap on `.video-grid`; fall back to 20px.
    var gap = 20;
    var cs = window.getComputedStyle(state.gridEl);
    var colGap = parseFloat(cs.columnGap);
    if (isFinite(colGap) && colGap >= 0) gap = colGap;

    var columns = clampInt((gridRect.width + gap) / (cardWidth + gap), 1);

    // Vertical space from the top of the grid down to the bottom of the visible
    // main area (already accounts for the sticky toolbar + section headers).
    var availableHeight = mainRect.bottom - gridRect.top - BOTTOM_PAD;
    var rows = clampInt((availableHeight + gap) / (cardHeight + gap), 1);

    var pageSize = columns * rows;

    // Only notify Blazor when the computed size actually changed. This also
    // prevents feedback loops when a page load grows the grid content (the
    // available width/height for columns/rows does not change in that case).
    if (pageSize === state.lastPageSize) return;
    state.lastPageSize = pageSize;

    state.dotNetRef
      .invokeMethodAsync("OnVideoLayoutChanged", pageSize)
      .catch(function () {
        /* circuit may be gone */
      });
  }

  /** Debounced wrapper around measure(). */
  function scheduleMeasure() {
    if (!state) return;
    if (state.timer) clearTimeout(state.timer);
    state.timer = setTimeout(function () {
      state.timer = null;
      measure();
    }, DEBOUNCE_MS);
  }

  /**
   * Observes the main content area. A ResizeObserver fires on window resizes
   * AND on sidebar collapse (both change `.video-main`'s size) and reports once
   * immediately on attach. Falls back to a window `resize` listener in older
   * browsers without ResizeObserver.
   */
  layout.attach = function (dotNetRef, gridId) {
    layout.detach();

    var mainEl = document.querySelector(".video-main");
    var gridEl = document.getElementById(gridId);
    // Report success (true/false) so Blazor can retry on the next render if the
    // grid isn't mounted yet (e.g. during a section-switch loading spinner).
    if (!mainEl || !gridEl) return false;

    state = {
      mainEl: mainEl,
      gridEl: gridEl,
      dotNetRef: dotNetRef,
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
  };

  /** Re-measures immediately (called by Blazor after a page load re-renders). */
  layout.refresh = function () {
    measure();
  };

  layout.detach = function () {
    if (!state) return;
    if (state.observer) state.observer.disconnect();
    if (state.resizeListener)
      window.removeEventListener("resize", state.resizeListener);
    if (state.timer) clearTimeout(state.timer);
    state = null;
  };
})();
