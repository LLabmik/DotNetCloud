/**
 * DotNetCloud Video — on-demand script loader (ES module).
 *
 * WHY THIS EXISTS
 * ---------------
 * The player scripts (`hls.min.js` → `video-player.js`) and the grid layout
 * observer (`video-layout.js`) are plain classic scripts that must be injected
 * into the document at runtime. Blazor cannot inject executing `<script>` tags
 * from a component, so this used to be done with
 * `Js.InvokeVoidAsync("eval", "...")` — which the app's hardened CSP now
 * blocks (see `CspPolicy.cs`: `script-src` has `'wasm-unsafe-eval'` but NOT
 * `'unsafe-eval'`), producing:
 *
 *   EvalError: Evaluating a string as JavaScript violates the following
 *   Content Security Policy directive because 'unsafe-eval' is not an
 *   allowed source of script
 *
 * The fix: Blazor imports this ES module instead (`script-src 'self'` allows
 * same-origin dynamic `import()`), and this module creates the `<script src>`
 * elements itself. Same-origin script tags are allowed by CSP — only `eval`
 * and inline script are not.
 *
 * EXPORTS (called from VideoPage.razor.cs via IJSObjectReference):
 *   ensurePlayer()          — load hls.min.js + video-player.js (idempotent)
 *   ensureLayout()          — load video-layout.js (idempotent)
 *   pauseIfPlaying()        — pause the active player; no-op when not loaded
 *   downloadUrl(url, name)  — anchor-click download; works with no player
 */

const HLS_URL = "/_content/DotNetCloud.Modules.Video/hls.min.js?v=1";
const PLAYER_URL = "/api/v1/videos/video-player-js";
const LAYOUT_URL = "/_content/DotNetCloud.Modules.Video/video-layout.js?v=2";

/** In-flight/recent load promises keyed by script URL, so a load is never duplicated. */
const loads = new Map();

/**
 * Injects a classic `<script src>` tag and resolves once it has executed.
 * @param {string} url Same-origin script URL.
 * @returns {Promise<void>} Resolves on load, rejects on error.
 */
function loadScript(url) {
  const existing = loads.get(url);
  if (existing) return existing;

  const promise = new Promise((resolve, reject) => {
    const el = document.createElement("script");
    el.src = url;
    el.onload = () => {
      loads.delete(url);
      resolve();
    };
    el.onerror = () => {
      loads.delete(url);
      reject(new Error("Failed to load script: " + url));
    };
    document.head.appendChild(el);
  });

  loads.set(url, promise);
  return promise;
}

/**
 * Loads a script unless the global it defines already exists, then verifies the
 * global really appeared. Rejecting here (instead of resolving blindly) makes a
 * script failure surface as a logged warning at the call site rather than as a
 * confusing "X was undefined" error later on.
 * @param {string} globalName Global the script is expected to define.
 * @param {string} url Script URL.
 * @returns {Promise<void>}
 */
function ensureGlobal(globalName, url) {
  if (window[globalName]) return Promise.resolve();
  return loadScript(url).then(() => {
    if (!window[globalName]) {
      throw new Error(globalName + " was not defined by " + url);
    }
  });
}

/** Loads hls.js then the video player. Idempotent. */
export function ensurePlayer() {
  return ensureGlobal("Hls", HLS_URL).then(() =>
    // Timestamp cache-buster so a freshly deployed video-player.js is never
    // served stale from the browser cache.
    ensureGlobal("DotNetCloudVideoPlayer", PLAYER_URL + "?_=" + Date.now()),
  );
}

/** Loads the grid layout observer script. Idempotent. */
export function ensureLayout() {
  return ensureGlobal("DotNetCloudVideoLayout", LAYOUT_URL);
}

/**
 * Pauses the active player, if any. No-op when the player script isn't loaded
 * (e.g. it failed to load), so callers never have to guard this.
 */
export function pauseIfPlaying() {
  const player = window.DotNetCloudVideoPlayer;
  if (player && typeof player.pauseIfPlaying === "function") {
    player.pauseIfPlaying();
  }
}

/**
 * Triggers a download via a temporary anchor element. Kept here (rather than in
 * video-player.js) so the download button works even when the player script
 * could not be loaded.
 * @param {string} url Download URL.
 * @param {string} filename Suggested file name.
 */
export function downloadUrl(url, filename) {
  const a = document.createElement("a");
  a.href = url;
  a.download = filename || "";
  a.style.display = "none";
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}
