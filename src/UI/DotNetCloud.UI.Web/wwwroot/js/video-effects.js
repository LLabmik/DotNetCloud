/**
 * DotNetCloud — Video Effects Engine (Background Blur)
 * Phase 1: Client-side background blur via MediaPipe Image Segmenter + canvas compositing.
 *
 * Namespace: window.dotnetcloudVideoEffects
 * Pattern: IIFE returning public API surface for Blazor JS interop.
 *
 * Processing pipeline:
 *  rawStream → hidden <video> → requestVideoFrameCallback loop →
 *  MediaPipe segmentation → canvas compositing (blurred bg + sharp person) →
 *  canvas.captureStream() → processed MediaStreamTrack
 */
window.dotnetcloudVideoEffects = window.dotnetcloudVideoEffects || (function () {
    "use strict";

    // ── Constants ──────────────────────────────────────────────
    const WASM_BASE_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/wasm';
    const MODEL_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/models/selfie_segmenter_landscape.tflite';
    const VISION_BUNDLE_PATH = '/_content/DotNetCloud.UI.Web/lib/mediapipe/vision_bundle.mjs';
    const BLUR_AMOUNT = '10px';
    const CAPTURE_FRAME_RATE = 30;

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
                    outputConfidenceMasks: false,
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
                    outputConfidenceMasks: false,
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
        if (isActive) {
            console.warn('[VideoEffects] Background blur already active');
            return processedStream ? processedStream.getVideoTracks()[0] : null;
        }

        if (!rawStream || rawStream.getVideoTracks().length === 0) {
            console.error('[VideoEffects] enableBackgroundBlur: no video track in stream');
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
        // outputCanvas: the processed stream is captured from this
        outputCanvas = document.createElement('canvas');
        outputCanvas.width = frameWidth;
        outputCanvas.height = frameHeight;
        outputCtx = outputCanvas.getContext('2d');

        // personCanvas: holds the sharp person layer (after mask clipping)
        personCanvas = document.createElement('canvas');
        personCanvas.width = frameWidth;
        personCanvas.height = frameHeight;
        personCtx = personCanvas.getContext('2d');

        // maskCanvas: temporary canvas to convert mask bytes → ImageData for compositing
        maskCanvas = document.createElement('canvas');
        maskCanvas.width = frameWidth;
        maskCanvas.height = frameHeight;
        maskCtx = maskCanvas.getContext('2d');

        // Pre-allocate reusable ImageData for the mask
        maskImageData = new ImageData(frameWidth, frameHeight);

        // ── Start capture stream ──
        processedStream = outputCanvas.captureStream(CAPTURE_FRAME_RATE);

        // ── Start processing loop ──
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
        console.log('[VideoEffects] Background blur enabled (' + frameWidth + 'x' + frameHeight + ')');
        return processedTrack;
    }

    /**
     * Disable background blur and release all processing resources.
     */
    function disableBackgroundBlur() {
        if (!isActive) return;
        console.log('[VideoEffects] Disabling background blur...');
        isActive = false;
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
     * Process one video frame: segment, composite, and write to outputCanvas.
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
            var categoryMask = result.categoryMask;

            if (!categoryMask) {
                // No mask — draw original frame as fallback
                outputCtx.drawImage(hiddenVideo, 0, 0, w, h);
                return;
            }

            // ── Step 1: Draw blurred background to outputCanvas ──
            outputCtx.filter = 'blur(' + BLUR_AMOUNT + ')';
            outputCtx.drawImage(hiddenVideo, 0, 0, w, h);
            outputCtx.filter = 'none';

            // ── Step 2: Build the person mask as ImageData ──
            // categoryMask: Uint8 per pixel, 0=background, 1=person
            var maskBytes = categoryMask.getAsUint8Array();
            var md = maskImageData.data;
            for (var i = 0; i < w * h; i++) {
                var p = i * 4;
                md[p]     = 255;
                md[p + 1] = 255;
                md[p + 2] = 255;
                md[p + 3] = maskBytes[i] === 1 ? 255 : 0;
            }

            // ── Step 3: Apply mask to sharp frame on personCanvas ──
            // Draw sharp frame, then clip to person using destination-in compositing
            personCtx.clearRect(0, 0, w, h);
            personCtx.drawImage(hiddenVideo, 0, 0, w, h);

            // Write mask to maskCanvas so we can drawImage it into personCanvas
            maskCtx.putImageData(maskImageData, 0, 0);

            personCtx.globalCompositeOperation = 'destination-in';
            personCtx.drawImage(maskCanvas, 0, 0);
            personCtx.globalCompositeOperation = 'source-over';

            // ── Step 4: Composite sharp person over blurred background ──
            outputCtx.drawImage(personCanvas, 0, 0);

            // Release mask resources
            categoryMask.close();

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
        console.log('[VideoEffects] Disposed');
    }

    // ── Public API ─────────────────────────────────────────────

    return {
        isSupported: isSupported,
        initialize: initialize,
        enableBackgroundBlur: enableBackgroundBlur,
        disableBackgroundBlur: disableBackgroundBlur,
        getProcessedTrack: getProcessedTrack,
        dispose: dispose
    };
})();
