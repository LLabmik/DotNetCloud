/*
 * Crop-selection helper for the Photos viewer.
 *
 * The component needs the on-screen size of the crop layer to turn a pointer offset into a
 * normalized rectangle (a fraction of the preview), which Blazor's PointerEventArgs does not carry.
 * One measurement per drag start, no state kept here.
 */
window.dncPhotos = window.dncPhotos || {};

window.dncPhotos.measure = function (element) {
  "use strict";

  if (!element || typeof element.getBoundingClientRect !== "function") {
    return { width: 0, height: 0 };
  }

  var rect = element.getBoundingClientRect();
  return { width: rect.width, height: rect.height };
};
