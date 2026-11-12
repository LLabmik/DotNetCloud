/*
 * Blazor WebAssembly bootstrap for a sandboxed module frame.
 *
 * The frame is served without `allow-same-origin`, so its document has an opaque origin. A relative
 * dynamic import (`import("./dotnet.js")`) issued from a classic script in such a document fails in
 * Chromium with:
 *
 *   Failed to resolve module specifier './dotnet.js'. The base URL is about:blank because import()
 *   is called from a CORS-cross-origin script.
 *
 * `blazor.webassembly.js` does exactly that import while starting, so the app never leaves its
 * "Loading the module UI…" markup. Autostart is therefore disabled in index.html and Blazor is
 * started here with a `loadBootResource` hook that returns **absolute** URLs, derived from the
 * frame's own document URL — the module still never needs to know the /apps/<moduleId>/ prefix it is
 * served under.
 */
(function () {
  "use strict";

  var frameBase = new URL("./", window.location.href);

  function absolute(uri) {
    return new URL(uri, frameBase).toString();
  }

  function showError(error) {
    var bar = document.getElementById("blazor-error-ui");
    if (bar) {
      bar.textContent =
        "The Tracks module UI failed to load. " + (error && error.message ? error.message : error);
      bar.style.display = "block";
    }

    if (window.console && typeof console.error === "function") {
      console.error(error);
    }
  }

  function start() {
    if (!window.Blazor || typeof window.Blazor.start !== "function") {
      showError(new Error("The Blazor loader did not load."));
      return;
    }

    window.Blazor.start({
      loadBootResource: function (type, name, defaultUri) {
        return absolute(defaultUri);
      },
    }).catch(showError);
  }

  start();
})();
