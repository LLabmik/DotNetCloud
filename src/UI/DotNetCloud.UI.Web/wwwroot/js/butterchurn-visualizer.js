window.dotnetcloudVisualizer = window.dotnetcloudVisualizer || (function () {
    "use strict";

    /** @type {HTMLCanvasElement|null} */
    var canvas = null;
    /** @type {HTMLCanvasElement|null} */
    var miniCanvas = null;
    var visualizer = null;
    var animFrameId = null;
    var running = false;

    /** @type {Object<string, object>} */
    var presets = {};
    var presetNames = [];
    var currentPresetName = null;
    var allPresetsLoaded = false;

    // Auto-cycle
    var cycleTimerId = null;
    var cycleBlendSeconds = 2.0;

    // ResizeObserver
    var resizeObserver = null;

    // Mini-canvas: draw via 2D context from main canvas
    var miniCtx = null;
    var miniAnimFrameId = null;

    // ── Helpers ──

    function loadCuratedPresets() {
        if (typeof butterchurnPresets !== "undefined" && butterchurnPresets.getPresets) {
            // Full library loaded — filter to curated names
            var all = butterchurnPresets.getPresets();
            var names = window.dotnetcloudCuratedPresetNames || [];
            for (var i = 0; i < names.length; i++) {
                if (all[names[i]]) {
                    presets[names[i]] = all[names[i]];
                }
            }
        }
        presetNames = Object.keys(presets);
    }

    // ── Public API ──

    function isSupported() {
        if (typeof isButterchurnSupported === "function") {
            return isButterchurnSupported();
        }
        // Fallback: check for WebGL2 manually
        try {
            var c = document.createElement("canvas");
            return !!c.getContext("webgl2");
        } catch (_) {
            return false;
        }
    }

    function init(canvasId) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error("[Visualizer] Canvas not found:", canvasId);
            return false;
        }
        if (!isSupported()) {
            console.warn("[Visualizer] WebGL2 not supported in this browser");
            return false;
        }
        // Load curated presets from the full butterchurn-presets library
        // (The presets lib must be loaded before this script, or lazy-loaded later)
        loadCuratedPresets();
        return true;
    }

    function start() {
        if (!canvas || running) return false;

        var audioCtx = window.dotnetcloudMusicPlayer && window.dotnetcloudMusicPlayer.getAudioContext
            ? window.dotnetcloudMusicPlayer.getAudioContext()
            : null;
        var sourceNode = window.dotnetcloudMusicPlayer && window.dotnetcloudMusicPlayer.getSourceNode
            ? window.dotnetcloudMusicPlayer.getSourceNode()
            : null;

        if (!audioCtx || !sourceNode) {
            console.warn("[Visualizer] AudioContext or sourceNode not available. Play a track first.");
            return false;
        }

        // Resume AudioContext if suspended
        if (audioCtx.state === "suspended") {
            audioCtx.resume();
        }

        try {
            var rect = canvas.parentElement
                ? canvas.parentElement.getBoundingClientRect()
                : { width: 800, height: 600 };
            var w = Math.min(Math.floor(rect.width), 1920);
            var h = Math.min(Math.floor(rect.height), 1080);
            canvas.width = w;
            canvas.height = h;

            visualizer = butterchurn.createVisualizer(audioCtx, canvas, {
                width: w,
                height: h,
                pixelRatio: 1
            });

            visualizer.connectAudio(sourceNode);

            // Load a default preset
            if (presetNames.length > 0) {
                var defaultPreset = currentPresetName && presets[currentPresetName]
                    ? currentPresetName
                    : presetNames[0];
                visualizer.loadPreset(presets[defaultPreset], 0);
                currentPresetName = defaultPreset;
            }

            running = true;
            renderLoop();
            startMiniCanvasLoop();
            setupResizeObserver();
            return true;
        } catch (e) {
            console.error("[Visualizer] Failed to start:", e);
            return false;
        }
    }

    function stop() {
        running = false;
        if (animFrameId) {
            cancelAnimationFrame(animFrameId);
            animFrameId = null;
        }
        stopMiniCanvasLoop();
        stopAutoCycle();
        teardownResizeObserver();
    }

    function renderLoop() {
        if (!running || !visualizer) return;
        visualizer.render();
        animFrameId = requestAnimationFrame(renderLoop);
    }

    // ── Presets ──

    function getPresetNames() {
        return presetNames.slice();
    }

    function getCurrentPresetName() {
        return currentPresetName;
    }

    function loadPreset(presetName, blendSeconds) {
        if (!visualizer || !presets[presetName]) return false;
        visualizer.loadPreset(presets[presetName], blendSeconds || 0);
        currentPresetName = presetName;
        return true;
    }

    function randomPreset(blendSeconds) {
        if (presetNames.length === 0) return null;
        var idx = Math.floor(Math.random() * presetNames.length);
        // Avoid repeating the same preset
        if (presetNames.length > 1 && presetNames[idx] === currentPresetName) {
            idx = (idx + 1) % presetNames.length;
        }
        var name = presetNames[idx];
        loadPreset(name, blendSeconds || 2.0);
        return name;
    }

    function loadAllPresets() {
        return new Promise(function (resolve, reject) {
            if (allPresetsLoaded) {
                resolve(presetNames.slice());
                return;
            }
            // Check if already loaded in global scope
            if (typeof butterchurnPresets !== "undefined" && butterchurnPresets.getPresets) {
                var all = butterchurnPresets.getPresets();
                presets = all;
                presetNames = Object.keys(presets);
                allPresetsLoaded = true;
                resolve(presetNames.slice());
                return;
            }
            // Dynamic script injection
            var script = document.createElement("script");
            script.src = "_content/DotNetCloud.UI.Web/lib/butterchurn/butterchurn-presets.min.js";
            script.onload = function () {
                if (typeof butterchurnPresets !== "undefined" && butterchurnPresets.getPresets) {
                    var all = butterchurnPresets.getPresets();
                    presets = all;
                    presetNames = Object.keys(presets);
                    allPresetsLoaded = true;
                    resolve(presetNames.slice());
                } else {
                    reject(new Error("butterchurnPresets not available after script load"));
                }
            };
            script.onerror = function () {
                reject(new Error("Failed to load butterchurn-presets.min.js"));
            };
            document.head.appendChild(script);
        });
    }

    // ── Resize ──

    function setSize(width, height) {
        if (!visualizer || !canvas) return;
        var w = Math.min(Math.floor(width), 1920);
        var h = Math.min(Math.floor(height), 1080);
        if (w < 1 || h < 1) return;
        canvas.width = w;
        canvas.height = h;
        visualizer.setRendererSize(w, h);
    }

    function setupResizeObserver() {
        if (!canvas || !canvas.parentElement) return;
        teardownResizeObserver();
        resizeObserver = new ResizeObserver(function (entries) {
            if (!running || !visualizer) return;
            var entry = entries[0];
            if (entry && entry.contentRect) {
                setSize(entry.contentRect.width, entry.contentRect.height);
            }
        });
        resizeObserver.observe(canvas.parentElement);
    }

    function teardownResizeObserver() {
        if (resizeObserver) {
            resizeObserver.disconnect();
            resizeObserver = null;
        }
    }

    // ── Mini-canvas ──

    function initMiniCanvas(canvasId) {
        miniCanvas = document.getElementById(canvasId);
        if (miniCanvas) {
            miniCtx = miniCanvas.getContext("2d");
        }
        return !!miniCanvas;
    }

    function startMiniCanvasLoop() {
        if (!miniCanvas || !miniCtx || !canvas) return;
        stopMiniCanvasLoop();
        function miniLoop() {
            if (!running || !canvas || !miniCanvas || !miniCtx) return;
            try {
                miniCtx.drawImage(canvas, 0, 0, miniCanvas.width, miniCanvas.height);
            } catch (_) { /* cross-origin or canvas issues — ignore */ }
            miniAnimFrameId = requestAnimationFrame(miniLoop);
        }
        miniLoop();
    }

    function stopMiniCanvasLoop() {
        if (miniAnimFrameId) {
            cancelAnimationFrame(miniAnimFrameId);
            miniAnimFrameId = null;
        }
    }

    // ── Fullscreen ──

    function enterFullscreen() {
        if (!canvas) return false;
        var container = canvas.parentElement || canvas;
        if (container.requestFullscreen) {
            container.requestFullscreen();
        } else if (container.webkitRequestFullscreen) {
            container.webkitRequestFullscreen();
        }
        return true;
    }

    function exitFullscreen() {
        if (document.fullscreenElement) {
            document.exitFullscreen();
        } else if (document.webkitFullscreenElement) {
            document.webkitExitFullscreen();
        }
    }

    // ── Auto-cycle ──

    function startAutoCycle(intervalSeconds, blendSeconds) {
        stopAutoCycle();
        cycleBlendSeconds = blendSeconds || 2.0;
        var ms = (intervalSeconds || 30) * 1000;
        cycleTimerId = setInterval(function () {
            if (running) randomPreset(cycleBlendSeconds);
        }, ms);
    }

    function stopAutoCycle() {
        if (cycleTimerId) {
            clearInterval(cycleTimerId);
            cycleTimerId = null;
        }
    }

    // ── Cleanup ──

    function dispose() {
        stop();
        if (visualizer) {
            visualizer = null;
        }
        canvas = null;
        miniCanvas = null;
        miniCtx = null;
        currentPresetName = null;
    }

    return {
        isSupported: isSupported,
        init: init,
        start: start,
        stop: stop,
        getPresetNames: getPresetNames,
        getCurrentPresetName: getCurrentPresetName,
        loadPreset: loadPreset,
        randomPreset: randomPreset,
        loadAllPresets: loadAllPresets,
        setSize: setSize,
        initMiniCanvas: initMiniCanvas,
        enterFullscreen: enterFullscreen,
        exitFullscreen: exitFullscreen,
        startAutoCycle: startAutoCycle,
        stopAutoCycle: stopAutoCycle,
        dispose: dispose
    };
})();
