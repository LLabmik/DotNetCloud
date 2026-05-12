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

    // ── DotNetCloud custom presets ──

    function disabledShapes() {
        return [
            {baseVals:{enabled:0}}, {baseVals:{enabled:0}},
            {baseVals:{enabled:0}}, {baseVals:{enabled:0}}
        ];
    }

    function disabledWaves() {
        return [
            {baseVals:{enabled:0}}, {baseVals:{enabled:0}},
            {baseVals:{enabled:0}}, {baseVals:{enabled:0}}
        ];
    }

    var dotnetcloudPresets = {};

    // ── DotNetCloud-Basic: Aurora Drift ──
    dotnetcloudPresets["DotNetCloud-Basic - Aurora Drift"] = {
        baseVals: {
            rating:5, decay:0.98, gammaadj:1.0, zoom:1, zoomexp:1, rot:0,
            warp:0.5, cx:0.5, cy:0.5, dx:0, dy:0, sx:1, sy:1,
            wave_mode:2, wave_x:0.5, wave_y:0.5,
            wave_r:0.3, wave_g:0.5, wave_b:0.9, wave_a:0.7,
            wave_scale:0.5, wave_smoothing:0.7, wave_mystery:0,
            wave_usedots:0, wave_thick:0, wave_additive:0, wave_brighten:0,
            ob_size:0.008, ob_r:0.3, ob_g:0.5, ob_b:0.9, ob_a:0.4,
            ib_size:0, ib_r:0, ib_g:0, ib_b:0, ib_a:0,
            mv_x:12, mv_y:9, mv_l:0, mv_r:0.5, mv_g:0.5, mv_b:0.5, mv_a:0,
            mv_dx:0, mv_dy:0,
            echo_zoom:1, echo_alpha:0, echo_orient:0,
            darken_center:0, wrap:0, invert:0, brighten:0, darken:0, solarize:0,
            warpscale:1, warpanimspeed:1, modwavealphastart:0.75, modwavealphaend:0.95,
            modwavealphabyvolume:0, additivewave:0, fshader:0, bmotionvectorson:0,
            red_blue:0
        },
        shapes: disabledShapes(),
        waves: disabledWaves(),
        init_eqs_str: "a.phase1=0;a.phase2=0;",
        frame_eqs_str:
            "a.decay=0.98;" +
            "a.zoom=1+0.015*a.bass_att;" +
            "a.warp=0.4+0.3*a.mid_att;" +
            "a.rot=0.02*Math.sin(a.time*0.3)*a.bass;" +
            "a.wave_r=0.2+0.3*Math.sin(a.time*0.7);" +
            "a.wave_g=0.4+0.4*Math.sin(a.time*0.8+2.1);" +
            "a.wave_b=0.6+0.4*Math.sin(a.time*0.6+3.5);" +
            "a.wave_a=0.5+0.3*a.bass_att;" +
            "a.wave_x=0.5+0.08*Math.sin(a.time*0.4);" +
            "a.wave_y=0.5+0.08*Math.cos(a.time*0.5);" +
            "a.wave_mode=2;" +
            "a.wave_scale=0.4+0.15*a.bass;" +
            "a.ob_r=a.wave_r;a.ob_g=a.wave_g;a.ob_b=a.wave_b;" +
            "a.ob_a=0.3;",
        pixel_eqs_str:
            "a.zoom=a.zoom+0.008*Math.sin(a.rad*6+a.time*0.5);" +
            "a.rot=a.rot+0.005*a.rad*Math.cos(a.ang*3+a.time*0.3);",
        warp: "",
        comp: ""
    };

    // ── DotNetCloud-Crazy: Fractal Onslaught ──
    dotnetcloudPresets["DotNetCloud-Crazy - Fractal Onslaught"] = {
        baseVals: {
            rating:5, decay:0.96, gammaadj:1.0, zoom:1, zoomexp:1, rot:0,
            warp:0.8, cx:0.5, cy:0.5, dx:0, dy:0, sx:1, sy:1,
            wave_mode:0, wave_x:0.5, wave_y:0.5,
            wave_r:0.8, wave_g:0.4, wave_b:0.2, wave_a:0.6,
            wave_scale:0.4, wave_smoothing:0.5, wave_mystery:0,
            wave_usedots:0, wave_thick:0, wave_additive:0, wave_brighten:0,
            ob_size:0.003, ob_r:0.5, ob_g:0.5, ob_b:0.5, ob_a:0.2,
            ib_size:0, ib_r:0, ib_g:0, ib_b:0, ib_a:0,
            mv_x:48, mv_y:32, mv_l:3, mv_r:0.4, mv_g:0.3, mv_b:0.6, mv_a:0.3,
            mv_dx:0, mv_dy:0,
            echo_zoom:1, echo_alpha:0, echo_orient:0,
            darken_center:0, wrap:0, invert:0, brighten:0, darken:0, solarize:0,
            warpscale:1, warpanimspeed:1, modwavealphastart:0.75, modwavealphaend:0.95,
            modwavealphabyvolume:0, additivewave:0, fshader:0, bmotionvectorson:0,
            red_blue:0
        },
        shapes: [
            {
                baseVals: {
                    enabled:1, sides:4, additive:0, textured:1, thick:0,
                    x:0.5, y:0.5, rad:0.35, ang:0,
                    r:0.8, g:0.3, b:0.2, a:0.7,
                    r2:0.2, g2:0.1, b2:0.4, a2:0.3,
                    tex_zoom:1, tex_ang:0,
                    border_r:0, border_g:0, border_b:0, border_a:0
                },
                init_eqs_str: "a.phase=0;",
                frame_eqs_str:
                    "a.phase+=0.05+0.03*a.bass;" +
                    "a.ang=a.phase;" +
                    "a.rad=0.3+0.15*a.bass_att;" +
                    "a.x=0.5+0.1*a.q1;" +
                    "a.y=0.5+0.1*a.q2;" +
                    "a.tex_zoom=1.005;" +
                    "a.r=0.8+0.2*Math.sin(a.phase*0.7);" +
                    "a.g=0.2+0.3*Math.cos(a.phase*0.6);" +
                    "a.b=0.3+0.4*Math.sin(a.phase*0.5+1);" +
                    "a.a=0.6+0.2*a.bass;"
            },
            {
                baseVals: {
                    enabled:1, sides:3, additive:1, textured:1, thick:0,
                    x:0.5, y:0.5, rad:0.28, ang:1.5,
                    r:0.2, g:0.5, b:0.8, a:0.5,
                    r2:0.1, g2:0.3, b2:0.5, a2:0.2,
                    tex_zoom:0.99, tex_ang:0,
                    border_r:0, border_g:0, border_b:0, border_a:0
                },
                init_eqs_str: "a.phase=1.5;",
                frame_eqs_str:
                    "a.phase-=0.07+0.04*a.mid;" +
                    "a.ang=a.phase;" +
                    "a.rad=0.25+0.2*a.mid_att;" +
                    "a.x=0.5+0.1*a.q2;" +
                    "a.y=0.5-0.1*a.q3;" +
                    "a.tex_zoom=0.995;" +
                    "a.r=0.2+0.3*Math.cos(a.phase*0.5);" +
                    "a.g=0.4+0.3*Math.sin(a.phase*0.8+2);" +
                    "a.b=0.6+0.3*Math.cos(a.phase*0.6+1);" +
                    "a.a=0.4+0.2*a.mid;"
            },
            {baseVals:{enabled:0}},
            {baseVals:{enabled:0}}
        ],
        waves: disabledWaves(),
        init_eqs_str: "a.chaos=0;a.q1=0;a.q2=0;a.q3=0;a.phase=0;",
        frame_eqs_str:
            "a.chaos=0.3+0.7*a.bass;" +
            "a.decay=0.99-0.04*a.chaos;" +
            "a.zoom=1+0.03*a.treb_att*a.chaos;" +
            "a.warp=0.5+1.5*a.bass;" +
            "a.rot=0.04*Math.sin(a.time*1.7)*a.chaos;" +
            "a.dx=0.008*Math.sin(a.time*3.3)*a.mid;" +
            "a.dy=0.008*Math.cos(a.time*3.7)*a.mid;" +
            "a.wave_r=0.5+0.5*Math.sin(a.time*2.3);" +
            "a.wave_g=0.3+0.4*Math.sin(a.time*2.7+1.5);" +
            "a.wave_b=0.4+0.6*Math.sin(a.time*1.9+3);" +
            "a.wave_a=0.5*a.bass;" +
            "a.wave_mode=Math.floor(3+3*a.bass)%8;" +
            "a.wave_scale=0.2+0.5*a.chaos;" +
            "a.wave_mystery=a.rot;" +
            "a.phase+=0.03*a.bass;" +
            "a.q1=Math.sin(a.phase);" +
            "a.q2=Math.cos(a.phase*1.3);" +
            "a.q3=Math.sin(a.phase*0.7+a.time*0.2);" +
            "a.mv_a=0.2+0.5*a.bass;" +
            "a.mv_l=2+3*a.chaos;" +
            "a.mv_dx=0.03*Math.sin(a.time*1.9)*a.chaos;" +
            "a.mv_dy=0.03*Math.cos(a.time*2.1)*a.chaos;" +
            "a.mv_r=0.3+0.4*Math.abs(a.q1);" +
            "a.mv_g=0.2+0.3*Math.abs(a.q2);" +
            "a.mv_b=0.4+0.4*Math.abs(a.q3);",
        pixel_eqs_str:
            "a.rot=a.rot+0.1*a.rad*a.q1*Math.sin(a.ang*2+a.time);" +
            "a.zoom=a.zoom+0.03*a.rad*a.q2;" +
            "a.dx=a.dx+0.004*Math.sin(a.x*10+a.time*2)*a.chaos;" +
            "a.dy=a.dy+0.004*Math.cos(a.y*9+a.time*2.3)*a.chaos;",
        warp: "",
        comp: ""
    };

    // ── DotNetCloud-Fonda: Tropical Flamingo Fever ──
    dotnetcloudPresets["DotNetCloud-Fonda - Tropical Flamingo Fever"] = {
        baseVals: {
            rating:5, decay:0.96, gammaadj:1.1, zoom:1, zoomexp:1, rot:0,
            warp:0.3, cx:0.5, cy:0.5, dx:0, dy:0, sx:1, sy:1,
            wave_mode:7, wave_x:0.5, wave_y:0.12,
            wave_r:0.1, wave_g:0.7, wave_b:0.8, wave_a:0.5,
            wave_scale:0.3, wave_smoothing:0.6, wave_mystery:-0.1,
            wave_usedots:0, wave_thick:0, wave_additive:0, wave_brighten:0,
            ob_size:0.02, ob_r:1.0, ob_g:0.2, ob_b:0.5, ob_a:0.35,
            ib_size:0, ib_r:0, ib_g:0, ib_b:0, ib_a:0,
            mv_x:12, mv_y:9, mv_l:0, mv_r:0.5, mv_g:0.5, mv_b:0.5, mv_a:0,
            mv_dx:0, mv_dy:0,
            echo_zoom:1.02, echo_alpha:0.15, echo_orient:0,
            darken_center:0, wrap:0, invert:0, brighten:0, darken:0, solarize:0,
            warpscale:1, warpanimspeed:1, modwavealphastart:0.75, modwavealphaend:0.95,
            modwavealphabyvolume:0, additivewave:0, fshader:0, bmotionvectorson:0,
            red_blue:0
        },
        shapes: [
            {
                // Flamingo body - large pink circle
                baseVals: {
                    enabled:1, sides:36, additive:0, textured:0, thick:0,
                    x:0.45, y:0.38, rad:0.16, ang:0,
                    r:1.0, g:0.18, b:0.4, a:0.85,
                    r2:0.92, g2:0.12, b2:0.5, a2:0.6,
                    tex_zoom:1, tex_ang:0,
                    border_r:1.0, border_g:0.25, border_b:0.5, border_a:0.3
                },
                init_eqs_str: "",
                frame_eqs_str:
                    "a.y=0.38+0.03*Math.sin(a.time*0.8);" +
                    "a.x=0.45+0.02*Math.cos(a.time*0.6);" +
                    "a.rad=0.15+0.03*a.bass_att;" +
                    "a.r=0.95+0.05*Math.sin(a.time*0.4);" +
                    "a.g=0.15+0.08*Math.sin(a.time*0.5+1);" +
                    "a.b=0.35+0.12*Math.sin(a.time*0.3+2);"
            },
            {
                // Flamingo head - small circle offset up-right from body
                baseVals: {
                    enabled:1, sides:24, additive:0, textured:0, thick:0,
                    x:0.55, y:0.54, rad:0.05, ang:0,
                    r:1.0, g:0.22, b:0.42, a:0.9,
                    r2:0.9, g2:0.18, b2:0.5, a2:0.5,
                    tex_zoom:1, tex_ang:0,
                    border_r:1.0, border_g:0.3, border_b:0.52, border_a:0.2
                },
                init_eqs_str: "",
                frame_eqs_str:
                    "a.y=0.54+0.04*Math.sin(a.time*0.8);" +
                    "a.x=0.55+0.03*Math.cos(a.time*0.6);" +
                    "a.rad=0.045+0.01*a.bass_att;" +
                    "a.r=0.95+0.05*Math.sin(a.time*0.5);" +
                    "a.g=0.18+0.08*Math.sin(a.time*0.6+1.5);" +
                    "a.b=0.38+0.1*Math.sin(a.time*0.4+1);"
            },
            {
                // Flamingo neck - elongated connector between body and head
                baseVals: {
                    enabled:1, sides:20, additive:0, textured:0, thick:1,
                    x:0.5, y:0.46, rad:0.07, ang:0.55,
                    r:0.95, g:0.2, b:0.44, a:0.8,
                    r2:0.88, g2:0.15, b2:0.5, a2:0.5,
                    tex_zoom:1, tex_ang:0,
                    border_r:0.95, border_g:0.22, border_b:0.5, border_a:0.25
                },
                init_eqs_str: "",
                frame_eqs_str:
                    "a.y=0.46+0.04*Math.sin(a.time*0.8);" +
                    "a.x=0.5+0.03*Math.cos(a.time*0.6);" +
                    "a.rad=0.065+0.02*a.bass_att;" +
                    "a.ang=0.55+0.15*Math.sin(a.time*0.5);" +
                    "a.r=0.92+0.08*Math.sin(a.time*0.5);" +
                    "a.g=0.16+0.08*Math.sin(a.time*0.6+1);" +
                    "a.b=0.4+0.1*Math.sin(a.time*0.4+2);"
            },
            {baseVals:{enabled:0}}
        ],
        waves: disabledWaves(),
        init_eqs_str: "a.bob=0;",
        frame_eqs_str:
            "a.decay=0.96;" +
            "a.bob+=0.04+0.03*a.bass_att;" +
            "a.zoom=1+0.01*Math.sin(a.bob*0.5);" +
            "a.warp=0.25+0.15*a.bass;" +
            "a.rot=0.01*Math.sin(a.bob*0.3);" +
            "a.wave_r=0.1+0.2*Math.sin(a.time*0.5);" +
            "a.wave_g=0.6+0.3*Math.sin(a.time*0.7);" +
            "a.wave_b=0.7+0.3*Math.sin(a.time*0.6+1.5);" +
            "a.wave_a=0.3+0.2*a.bass;" +
            "a.wave_y=0.12+0.05*Math.sin(a.time*0.3);" +
            "a.wave_scale=0.25+0.1*a.bass_att;" +
            "a.ob_r=1.0;" +
            "a.ob_g=0.15+0.1*Math.sin(a.time*0.4);" +
            "a.ob_b=0.4+0.2*Math.sin(a.time*0.5+1);" +
            "a.ob_a=0.3+0.1*a.bass;",
        pixel_eqs_str:
            "a.warp=a.warp+0.05*Math.sin(a.x*8+a.time)*Math.cos(a.y*6+a.time*0.7);" +
            "a.dx=0.003*Math.sin(a.y*5+a.time)*Math.cos(a.x*4+a.time*0.6);" +
            "a.dy=0.003*Math.cos(a.x*6+a.time*0.8)*Math.sin(a.y*5+a.time);",
        warp: "",
        comp: ""
    };

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
        // DotNetCloud custom presets (defined inline above)
        if (Object.keys(dotnetcloudPresets).length > 0) {
            Object.assign(presets, dotnetcloudPresets);
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
