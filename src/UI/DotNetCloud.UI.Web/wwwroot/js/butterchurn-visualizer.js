window.dotnetcloudVisualizer = window.dotnetcloudVisualizer || (function () {
    "use strict";

    /** @type {HTMLCanvasElement|null} */
    var canvas = null;
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

    // ── Helpers ──

    function getButterchurn() {
        // UMD webpack builds wrap ES default exports: window.butterchurn = { default: TheClass }
        if (typeof butterchurn !== "undefined") {
            return butterchurn.default || butterchurn;
        }
        return null;
    }

    function resolvePresetLib(global) {
        if (!global) return null;
        var mod = global.default || global;
        if (mod && mod.default) mod = mod.default;
        if (typeof mod.getPresets === "function") return mod.getPresets();
        // Maybe the object IS the presets map directly
        var keys = Object.keys(mod).filter(function(k) { return k !== "default" && k !== "__esModule"; });
        if (keys.length > 5) return mod;
        return null;
    }

    function loadAllPresetsFromLibs() {
        presets = {};
        // Base presets
        if (typeof butterchurnPresets !== "undefined") {
            var base = resolvePresetLib(butterchurnPresets);
            if (base) Object.assign(presets, base);
        }
        // Extra presets
        if (typeof butterchurnPresetsExtra !== "undefined") {
            var extra = resolvePresetLib(butterchurnPresetsExtra);
            if (extra) Object.assign(presets, extra);
        }
        // Extra2 presets
        if (typeof butterchurnPresetsExtra2 !== "undefined") {
            var extra2 = resolvePresetLib(butterchurnPresetsExtra2);
            if (extra2) Object.assign(presets, extra2);
        }
        presetNames = Object.keys(presets).sort();
        allPresetsLoaded = true;
        console.log("[Visualizer] Loaded " + presetNames.length + " presets from all libraries");
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
        // Load all available presets from all included preset libraries
        loadAllPresetsFromLibs();
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
            var w = Math.floor(rect.width);
            var h = Math.floor(rect.height);
            canvas.width = w;
            canvas.height = h;

            var bc = getButterchurn();
            if (!bc || typeof bc.createVisualizer !== "function") {
                console.error("[Visualizer] butterchurn.createVisualizer not found. Keys:", typeof butterchurn !== "undefined" ? Object.keys(butterchurn) : "undefined");
                return false;
            }
            visualizer = bc.createVisualizer(audioCtx, canvas, {
                width: w,
                height: h,
                pixelRatio: 1
            });

            visualizer.connectAudio(sourceNode);

            // Load a random preset
            if (presetNames.length > 0) {
                var defaultPreset = currentPresetName && presets[currentPresetName]
                    ? currentPresetName
                    : presetNames[Math.floor(Math.random() * presetNames.length)];
                visualizer.loadPreset(presets[defaultPreset], 0);
                currentPresetName = defaultPreset;
            }

            running = true;
            renderLoop();
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
        return new Promise(function (resolve) {
            // All preset libs are eagerly loaded via script tags — just re-merge
            loadAllPresetsFromLibs();
            resolve(presetNames.slice());
        });
    }

    // ── Resize ──

    function setSize(width, height) {
        if (!visualizer || !canvas) return;
        var w = Math.floor(width);
        var h = Math.floor(height);
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

    // ── Fullscreen ──

    var fsHideTimer = null;
    var fsHideDelay = 5000; // ms before hiding controls in fullscreen

    function fsShowControls() {
        if (!canvas) return;
        var container = canvas.parentElement || canvas;
        container.classList.remove('fs-controls-hidden');
        clearTimeout(fsHideTimer);
        fsHideTimer = setTimeout(fsHideControls, fsHideDelay);
    }

    function fsHideControls() {
        if (!canvas) return;
        if (!document.fullscreenElement && !document.webkitFullscreenElement) return;
        var container = canvas.parentElement || canvas;
        container.classList.add('fs-controls-hidden');
    }

    function onFsMouseMove() {
        fsShowControls();
    }

    function onFullscreenChange() {
        if (!canvas) return;
        var container = canvas.parentElement || canvas;
        if (document.fullscreenElement || document.webkitFullscreenElement) {
            // Entered fullscreen — show controls initially, start hide timer
            container.addEventListener('mousemove', onFsMouseMove);
            fsShowControls();
        } else {
            // Exited fullscreen — clean up
            container.removeEventListener('mousemove', onFsMouseMove);
            container.classList.remove('fs-controls-hidden');
            clearTimeout(fsHideTimer);
            fsHideTimer = null;
        }
    }

    document.addEventListener('fullscreenchange', onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onFullscreenChange);

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

    function toggleFullscreen() {
        if (document.fullscreenElement || document.webkitFullscreenElement) {
            exitFullscreen();
        } else {
            enterFullscreen();
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
        currentPresetName = null;
    }

    return {
        isSupported: isSupported,
        init: init,
        start: start,
        stop: stop,
        getPresetNames: getPresetNames,
        getCurrentPresetName: getCurrentPresetName,
        isAllPresetsLoaded: function () { return allPresetsLoaded; },
        loadPreset: loadPreset,
        randomPreset: randomPreset,
        loadAllPresets: loadAllPresets,
        setSize: setSize,
        enterFullscreen: enterFullscreen,
        exitFullscreen: exitFullscreen,
        toggleFullscreen: toggleFullscreen,
        startAutoCycle: startAutoCycle,
        stopAutoCycle: stopAutoCycle,
        dispose: dispose
    };
})();
