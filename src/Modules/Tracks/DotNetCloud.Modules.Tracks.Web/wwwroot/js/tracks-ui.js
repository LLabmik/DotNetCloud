/**
 * Tracks module UI helpers — the bits the ported Blazor components call by name.
 *
 * Named functions, never `eval`: the frame inherits the core's Content-Security-Policy, which has no
 * 'unsafe-eval'.
 */
window.tracksList =
  window.tracksList ||
  (function () {
    "use strict";

    /**
     * Downloads CSV bytes that Blazor already has in memory.
     * @param {string} base64 - Base64-encoded CSV payload.
     * @param {string} fileName - Suggested file name.
     */
    function downloadCsv(base64, fileName) {
      window.dotnetcloudDom.downloadData(
        base64,
        fileName,
        "text/csv;charset=utf-8",
      );
    }

    return {
      downloadCsv: downloadCsv,
    };
  })();
