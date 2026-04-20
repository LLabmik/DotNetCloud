/**
 * DotNetCloud — Video Effects Engine (Background Blur, Virtual Backgrounds, Blur Intensity)
 * Client-side video effects via MediaPipe Image Segmenter + canvas compositing.
 *
 * Namespace: window.dotnetcloudVideoEffects
 * Pattern: IIFE returning public API surface for Blazor JS interop.
 *
 * Processing pipeline:
 *  rawStream → hidden <video> → requestVideoFrameCallback loop →
 *  MediaPipe segmentation → canvas compositing (effect bg + sharp person) →
 *  canvas.captureStream() → processed MediaStreamTrack
 *
 * Supported effects:
 *  - Background blur (Gaussian, adjustable intensity 1-50px)
 *  - Virtual background (image replacement)
 *  - None (passthrough)
 */
window.dotnetcloudVideoEffects = window.dotnetcloudVideoEffects || (function () {
    "use strict";

    // ── Constants ──────────────────────────────────────────────
    const WASM_BASE_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/wasm';
    const MODEL_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/models/selfie_segmenter_landscape.tflite';
    const VISION_BUNDLE_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/vision_bundle.mjs';
    const DEFAULT_BLUR_AMOUNT = 10;
    const MIN_BLUR_AMOUNT = 1;
    const MAX_BLUR_AMOUNT = 50;
    const CAPTURE_FRAME_RATE = 30;

    // ── Effect Modes ───────────────────────────────────────────
    /** @enum {string} */
    const EffectMode = {
        NONE: 'none',
        BLUR: 'blur',
        VIRTUAL_BG: 'virtual-bg'
    };

    // ── State ──────────────────────────────────────────────────
    /** @type {object|null} Imported @mediapipe/tasks-vision module */
    let mp = null;
    /** @type {Promise<object>|null} */
    let moduleLoadPromise = null;
    /** @type {object|null} MediaPipe ImageSegmenter instance */
    let segmenter = null;
    /** @type {boolean} */
    let isInitialized = false;
    /** @type {boolean} */
    let isActive = false;

    /** @type {string} Current effect mode */
    let currentMode = EffectMode.NONE;
    /** @type {number} Blur intensity in pixels */
    let blurAmount = DEFAULT_BLUR_AMOUNT;
    /** @type {HTMLImageElement|null} Loaded virtual background image */
    let virtualBgImage = null;
    /** @type {string|null} URL of the currently loaded virtual background */
    let virtualBgUrl = null;

    // Processing resources — allocated on blur-enable, released on disable
    /** @type {HTMLVideoElement|null} */
    let hiddenVideo = null;
    /** @type {HTMLCanvasElement|null} */
    let outputCanvas = null;
    /** @type {CanvasRenderingContext2D|null} */
    let outputCtx = null;
    /** @type {HTMLCanvasElement|null} */
    let personCanvas = null;
    /** @type {CanvasRenderingContext2D|null} */
    let personCtx = null;
    /** @type {HTMLCanvasElement|null} */
    let maskCanvas = null;
    /** @type {CanvasRenderingContext2D|null} */
    let maskCtx = null;
    /** @type {ImageData|null} Reused per frame */
    let maskImageData = null;
    /** @type {MediaStream|null} */
    let processedStream = null;
    /** @type {number} Frame width */
    let frameWidth = 0;
    /** @type {number} Frame height */
    let frameHeight = 0;
    /** @type {boolean} Whether rVFC is available */
    let useRvfc = false;
    /** @type {number|null} rAF handle (used when rVFC not available) */
    let rafId = null;
    /** @type {number} Frame counter for debug logging */
    let frameCount = 0;

    // ── Module Loading ─────────────────────────────────────────

    /**
     * Lazily load the MediaPipe vision bundle.
     * @returns {Promise<object>}
     */
    function loadModule() {
        if (mp) return Promise.resolve(mp);
        if (moduleLoadPromise) return moduleLoadPromise;

        moduleLoadPromise = import(VISION_BUNDLE_PATH)
            .then(function (module) {
                mp = module;
                console.log('[VideoEffects] MediaPipe vision bundle loaded');
                return module;
            })
            .catch(function (e) {
                console.error('[VideoEffects] Failed to load vision bundle:', e.message);
                moduleLoadPromise = null;
                throw e;
            });

        return moduleLoadPromise;
    }

    // ── Support Check ──────────────────────────────────────────

    /**
     * Check whether background blur can be supported in this browser.
     * Requires WebAssembly + canvas 2D support.
     * @returns {boolean}
     */
    function isSupported() {
        try {
            if (typeof WebAssembly !== 'object') return false;
            var testCanvas = document.createElement('canvas');
            if (!testCanvas.getContext('2d')) return false;
            return true;
        } catch (e) {
            return false;
        }
    }

    // ── Initialization ─────────────────────────────────────────

    /**
     * Initialize the MediaPipe Image Segmenter (lazy, called on first use).
     * Tries GPU delegate first, falls back to CPU.
     * @returns {Promise<boolean>} True on success.
     */
    async function initialize() {
        if (isInitialized) return true;
        if (!isSupported()) {
            console.warn('[VideoEffects] Browser does not support background blur');
            return false;
        }

        try {
            var module = await loadModule();
            var FilesetResolver = module.FilesetResolver;
            var ImageSegmenter = module.ImageSegmenter;

            console.log('[VideoEffects] Initializing Image Segmenter (GPU)...');
            var vision = await FilesetResolver.forVisionTasks(WASM_BASE_PATH);

            try {
                segmenter = await ImageSegmenter.createFromOptions(vision, {
                    baseOptions: {
                        modelAssetPath: MODEL_PATH,
                        delegate: 'GPU'
                    },
                    outputCategoryMask: true,
                    outputConfidenceMasks: true,
                    runningMode: 'VIDEO'
                });
                console.log('[VideoEffects] Image Segmenter ready (GPU)');
            } catch (gpuErr) {
                console.warn('[VideoEffects] GPU init failed, falling back to CPU:', gpuErr.message);
                segmenter = await ImageSegmenter.createFromOptions(vision, {
                    baseOptions: {
                        modelAssetPath: MODEL_PATH,
                        delegate: 'CPU'
                    },
                    outputCategoryMask: true,
                    outputConfidenceMasks: true,
                    runningMode: 'VIDEO'
                });
                console.log('[VideoEffects] Image Segmenter ready (CPU)');
            }

            isInitialized = true;
            return true;
        } catch (e) {
            console.error('[VideoEffects] Initialization failed:', e.message);
            return false;
        }
    }

    // ── Background Blur ────────────────────────────────────────

    /**
     * Enable background blur on a raw camera MediaStream.
     * Creates a processed stream where the background is blurred and the person stays sharp.
     * @param {MediaStream} rawStream - Raw camera stream from getUserMedia.
     * @returns {Promise<MediaStreamTrack|null>} The processed video track, or null on failure.
     */
    async function enableBackgroundBlur(rawStream) {
        return await _enableEffect(rawStream, EffectMode.BLUR);
    }

    /**
     * Set a virtual background image on a raw camera MediaStream.
     * The background is replaced with the provided image and the person stays sharp.
     * @param {MediaStream} rawStream - Raw camera stream from getUserMedia.
     * @param {string} imageUrl - URL of the background image to use.
     * @returns {Promise<MediaStreamTrack|null>} The processed video track, or null on failure.
     */
    async function setVirtualBackground(rawStream, imageUrl) {
        if (!imageUrl) {
            console.error('[VideoEffects] setVirtualBackground: imageUrl is required');
            return null;
        }

        // Load the image before starting the effect
        try {
            var img = await _loadImage(imageUrl);
            virtualBgImage = img;
            virtualBgUrl = imageUrl;
        } catch (e) {
            console.error('[VideoEffects] Failed to load virtual background image:', e.message);
            return null;
        }

        return await _enableEffect(rawStream, EffectMode.VIRTUAL_BG);
    }

    /**
     * Set the blur intensity (only applies when blur mode is active).
     * @param {number} intensity - Blur amount in pixels (1-50).
     */
    function setBlurIntensity(intensity) {
        var val = Math.round(Number(intensity) || DEFAULT_BLUR_AMOUNT);
        if (val < MIN_BLUR_AMOUNT) val = MIN_BLUR_AMOUNT;
        if (val > MAX_BLUR_AMOUNT) val = MAX_BLUR_AMOUNT;
        blurAmount = val;
        console.log('[VideoEffects] Blur intensity set to ' + val + 'px');
    }

    /**
     * Get the current blur intensity.
     * @returns {number}
     */
    function getBlurIntensity() {
        return blurAmount;
    }

    /**
     * Get the current effect mode.
     * @returns {string} 'none', 'blur', or 'virtual-bg'
     */
    function getEffectMode() {
        return currentMode;
    }

    /**
     * Get the current virtual background URL (null if none set).
     * @returns {string|null}
     */
    function getVirtualBackgroundUrl() {
        return virtualBgUrl;
    }

    /**
     * Internal: Load an image from a URL.
     * @param {string} url
     * @returns {Promise<HTMLImageElement>}
     */
    function _loadImage(url) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.crossOrigin = 'anonymous';
            img.onload = function () { resolve(img); };
            img.onerror = function () { reject(new Error('Image load failed: ' + url)); };
            img.src = url;
        });
    }

    /**
     * Internal: Enable an effect mode on a raw camera stream.
     * If effects are already active, switches mode in-place (no re-init needed).
     * @param {MediaStream} rawStream
     * @param {string} mode - EffectMode value
     * @returns {Promise<MediaStreamTrack|null>}
     */
    async function _enableEffect(rawStream, mode) {
        // If already active, just switch the mode (no need to rebuild canvases)
        if (isActive && processedStream) {
            currentMode = mode;
            console.log('[VideoEffects] Switched to mode: ' + mode);
            return processedStream.getVideoTracks()[0] || null;
        }

        if (!rawStream || rawStream.getVideoTracks().length === 0) {
            console.error('[VideoEffects] _enableEffect: no video track in stream');
            return null;
        }

        var ok = await initialize();
        if (!ok) return null;

        // Determine output dimensions from track settings
        var videoTrack = rawStream.getVideoTracks()[0];
        var settings = videoTrack.getSettings();
        frameWidth = settings.width || 1280;
        frameHeight = settings.height || 720;

        // ── Create hidden video element ──
        hiddenVideo = document.createElement('video');
        hiddenVideo.setAttribute('playsinline', '');
        hiddenVideo.muted = true;
        hiddenVideo.srcObject = rawStream;
        hiddenVideo.style.cssText = 'position:absolute;opacity:0;pointer-events:none;width:1px;height:1px;top:-9999px;left:-9999px;';
        document.body.appendChild(hiddenVideo);

        try {
            await hiddenVideo.play();
        } catch (e) {
            console.error('[VideoEffects] Hidden video play failed:', e.message);
            _cleanup();
            return null;
        }

        // ── Allocate canvases ──
        outputCanvas = document.createElement('canvas');
        outputCanvas.width = frameWidth;
        outputCanvas.height = frameHeight;
        outputCtx = outputCanvas.getContext('2d');

        personCanvas = document.createElement('canvas');
        personCanvas.width = frameWidth;
        personCanvas.height = frameHeight;
        personCtx = personCanvas.getContext('2d');

        maskCanvas = document.createElement('canvas');
        maskCanvas.width = frameWidth;
        maskCanvas.height = frameHeight;
        maskCtx = maskCanvas.getContext('2d');

        maskImageData = new ImageData(frameWidth, frameHeight);

        // ── Start capture stream ──
        processedStream = outputCanvas.captureStream(CAPTURE_FRAME_RATE);

        // ── Start processing loop ──
        currentMode = mode;
        isActive = true;
        useRvfc = typeof hiddenVideo.requestVideoFrameCallback === 'function';

        if (useRvfc) {
            console.log('[VideoEffects] Processing loop: requestVideoFrameCallback');
            startRvfcLoop();
        } else {
            console.log('[VideoEffects] Processing loop: requestAnimationFrame fallback');
            startRafLoop();
        }

        var processedTrack = processedStream.getVideoTracks()[0];
        console.log('[VideoEffects] Effect enabled: ' + mode + ' (' + frameWidth + 'x' + frameHeight + ')');
        return processedTrack;
    }

    /**
     * Disable all background effects and release all processing resources.
     */
    function disableBackgroundBlur() {
        if (!isActive) return;
        console.log('[VideoEffects] Disabling effects...');
        isActive = false;
        currentMode = EffectMode.NONE;
        _cleanup();
    }

    /**
     * Get the current processed video track (null if blur not active).
     * @returns {MediaStreamTrack|null}
     */
    function getProcessedTrack() {
        if (!processedStream) return null;
        var tracks = processedStream.getVideoTracks();
        return tracks.length > 0 ? tracks[0] : null;
    }

    // ── Internal Cleanup ───────────────────────────────────────

    function _cleanup() {
        // Stop rAF loop
        if (rafId !== null) {
            cancelAnimationFrame(rafId);
            rafId = null;
        }

        // Stop processed stream
        if (processedStream) {
            processedStream.getTracks().forEach(function (t) { t.stop(); });
            processedStream = null;
        }

        // Remove and release hidden video
        if (hiddenVideo) {
            hiddenVideo.srcObject = null;
            if (hiddenVideo.parentNode) {
                hiddenVideo.parentNode.removeChild(hiddenVideo);
            }
            hiddenVideo = null;
        }

        // Release canvases
        outputCanvas = null;
        outputCtx = null;
        personCanvas = null;
        personCtx = null;
        maskCanvas = null;
        maskCtx = null;
        maskImageData = null;
        frameWidth = 0;
        frameHeight = 0;

        // Release virtual background (keep image cached for re-use, only clear on dispose)
        frameCount = 0;
    }

    // ── Processing Loops ───────────────────────────────────────

    function startRvfcLoop() {
        function onFrame(now, metadata) {
            if (!isActive || !hiddenVideo) return;
            processFrame(now);
            hiddenVideo.requestVideoFrameCallback(onFrame);
        }
        hiddenVideo.requestVideoFrameCallback(onFrame);
    }

    function startRafLoop() {
        function onFrame() {
            if (!isActive) return;
            processFrame(performance.now());
            rafId = requestAnimationFrame(onFrame);
        }
        rafId = requestAnimationFrame(onFrame);
    }

    // ── Core Frame Processor ───────────────────────────────────

    /**
     * Process one video frame: segment, composite based on current effect mode, write to outputCanvas.
     * @param {number} timestampMs - Current timestamp in milliseconds.
     */
    function processFrame(timestampMs) {
        if (!segmenter || !hiddenVideo || !outputCtx) return;
        if (hiddenVideo.readyState < 2) return; // HAVE_CURRENT_DATA

        var w = frameWidth;
        var h = frameHeight;

        try {
            // Run segmentation
            var result = segmenter.segmentForVideo(hiddenVideo, timestampMs);
            frameCount++;

            // Debug: log result structure on first few frames
            if (frameCount <= 3) {
                console.log('[VideoEffects] Frame ' + frameCount + ' result keys:', Object.keys(result));
                console.log('[VideoEffects] categoryMask:', result.categoryMask ? 'present' : 'null');
                console.log('[VideoEffects] confidenceMasks:', result.confidenceMasks ? ('array[' + result.confidenceMasks.length + ']') : 'null');
                if (result.categoryMask) {
                    var dbgBytes = result.categoryMask.getAsUint8Array();
                    var dbgMin = 999, dbgMax = -1, dbgPersonCount = 0;
                    for (var di = 0; di < Math.min(dbgBytes.length, w * h); di++) {
                        if (dbgBytes[di] < dbgMin) dbgMin = dbgBytes[di];
                        if (dbgBytes[di] > dbgMax) dbgMax = dbgBytes[di];
                        if (dbgBytes[di] > 0) dbgPersonCount++;
                    }
                    console.log('[VideoEffects] categoryMask stats: min=' + dbgMin + ' max=' + dbgMax + ' personPixels=' + dbgPersonCount + '/' + (w * h));
                }
                if (result.confidenceMasks && result.confidenceMasks.length >= 2) {
                    var dbgFloats = result.confidenceMasks[1].getAsFloat32Array();
                    var fMin = 999, fMax = -1, fAboveHalf = 0;
                    for (var fi = 0; fi < Math.min(dbgFloats.length, w * h); fi++) {
                        if (dbgFloats[fi] < fMin) fMin = dbgFloats[fi];
                        if (dbgFloats[fi] > fMax) fMax = dbgFloats[fi];
                        if (dbgFloats[fi] > 0.5) fAboveHalf++;
                    }
                    console.log('[VideoEffects] confidenceMask[1] stats: min=' + fMin.toFixed(4) + ' max=' + fMax.toFixed(4) + ' aboveHalf=' + fAboveHalf + '/' + (w * h));
                }
            }

            // Prefer confidence masks for smooth edges, fall back to category mask
            var useConfidence = result.confidenceMasks && result.confidenceMasks.length >= 2;
            var useCategoryMask = result.categoryMask;

            if (!useConfidence && !useCategoryMask) {
                // No masks at all — draw original frame as fallback
                outputCtx.drawImage(hiddenVideo, 0, 0, w, h);
                return;
            }

            // ── Step 1: Draw background to outputCanvas based on effect mode ──
            if (currentMode === EffectMode.VIRTUAL_BG && virtualBgImage) {
                // Virtual background: draw the image scaled to fill the canvas
                outputCtx.filter = 'none';
                outputCtx.drawImage(virtualBgImage, 0, 0, w, h);
            } else {
                // Blur mode (default): draw blurred frame
                outputCtx.filter = 'blur(' + blurAmount + 'px)';
                outputCtx.drawImage(hiddenVideo, 0, 0, w, h);
                outputCtx.filter = 'none';
            }

            // ── Step 2: Build the person mask ──
            var md = maskImageData.data;
            if (useConfidence) {
                // Use confidence mask for smooth alpha-blended edges
                // confidenceMasks[0] = background confidence; use it inverted so person=opaque
                var maskFloats = result.confidenceMasks[0].getAsFloat32Array();
                for (var i = 0; i < w * h; i++) {
                    var p = i * 4;
                    md[p]     = 255;
                    md[p + 1] = 255;
                    md[p + 2] = 255;
                    md[p + 3] = Math.round((1.0 - maskFloats[i]) * 255);
                }
            } else {
                // Fallback: category mask (0=background, 1=person)
                var maskBytes = useCategoryMask.getAsUint8Array();
                for (var i = 0; i < w * h; i++) {
                    var p = i * 4;
                    md[p]     = 255;
                    md[p + 1] = 255;
                    md[p + 2] = 255;
                    md[p + 3] = maskBytes[i] === 0 ? 255 : 0;
                }
            }

            // ── Step 3: Apply mask to sharp frame on personCanvas ──
            personCtx.clearRect(0, 0, w, h);
            personCtx.drawImage(hiddenVideo, 0, 0, w, h);

            maskCtx.putImageData(maskImageData, 0, 0);

            personCtx.globalCompositeOperation = 'destination-in';
            personCtx.drawImage(maskCanvas, 0, 0);
            personCtx.globalCompositeOperation = 'source-over';

            // ── Step 4: Composite sharp person over background ──
            outputCtx.drawImage(personCanvas, 0, 0);

            // Release mask resources
            if (result.categoryMask) result.categoryMask.close();
            if (result.confidenceMasks) {
                for (var j = 0; j < result.confidenceMasks.length; j++) {
                    result.confidenceMasks[j].close();
                }
            }

        } catch (e) {
            // Fallback: draw original frame unmodified
            if (outputCtx) {
                outputCtx.filter = 'none';
                outputCtx.drawImage(hiddenVideo, 0, 0, w, h);
            }
        }
    }

    // ── Dispose ────────────────────────────────────────────────

    /**
     * Full teardown: disable blur and release MediaPipe segmenter resources.
     * Call this when the video call ends.
     */
    function dispose() {
        disableBackgroundBlur();
        if (segmenter) {
            segmenter.close();
            segmenter = null;
        }
        isInitialized = false;
        mp = null;
        moduleLoadPromise = null;
        virtualBgImage = null;
        virtualBgUrl = null;
        blurAmount = DEFAULT_BLUR_AMOUNT;
        currentMode = EffectMode.NONE;
        console.log('[VideoEffects] Disposed');
    }

    // ── Public API ─────────────────────────────────────────────

    return {
        isSupported: isSupported,
        initialize: initialize,
        enableBackgroundBlur: enableBackgroundBlur,
        disableBackgroundBlur: disableBackgroundBlur,
        setVirtualBackground: setVirtualBackground,
        setBlurIntensity: setBlurIntensity,
        getBlurIntensity: getBlurIntensity,
        getEffectMode: getEffectMode,
        getVirtualBackgroundUrl: getVirtualBackgroundUrl,
        getProcessedTrack: getProcessedTrack,
        dispose: dispose
    };
})();
