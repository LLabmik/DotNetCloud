/**
 * Attaches an error listener to the <video> element and calls back to .NET
 * when the browser cannot play the media.
 * Also detects missing audio (e.g. Firefox + Dolby Digital Plus) and notifies .NET.
 *
 * @param {string} elementId - The DOM id of the <video> element.
 * @param {object} dotNetRef - A DotNetObjectReference for calling OnVideoError / OnNoAudio.
 */
export function attachVideoErrorListener(elementId, dotNetRef) {
    const video = document.getElementById(elementId);
    if (!video) return;

    video.addEventListener('error', async function () {
        const err = video.error;
        if (!err) return;

        // Probe the stream URL to capture the HTTP status — a 4xx/5xx
        // response means the server rejected the request rather than the
        // browser failing to decode the media.
        let httpStatus = null;
        let httpStatusText = '';
        try {
            const resp = await fetch(video.src, { method: 'GET' });
            httpStatus = resp.status;
            httpStatusText = resp.statusText || '';
        } catch (fetchErr) {
            httpStatus = 0;
            httpStatusText = fetchErr.message || 'fetch failed';
        }

        dotNetRef.invokeMethodAsync('OnVideoError',
            err.code, err.message || '',
            httpStatus, httpStatusText);
    }, { once: true });

    // Detect missing audio after the video starts playing.
    // Firefox exposes mozHasAudio; other browsers may report via audioTracks.
    video.addEventListener('playing', function () {
        let hasAudio = true;

        if (typeof video.mozHasAudio === 'boolean') {
            // Firefox-specific property
            hasAudio = video.mozHasAudio;
        } else if (video.audioTracks && video.audioTracks.length === 0) {
            hasAudio = false;
        }

        if (!hasAudio) {
            dotNetRef.invokeMethodAsync('OnNoAudio');
        }
    }, { once: true });
}

/**
 * Auto-hides the cursor (and encourages browser to hide native controls)
 * after a period of mouse inactivity over the video player container.
 *
 * @param {string} containerId - The DOM id of the player container element.
 * @param {number} [idleMs=3000] - Milliseconds of inactivity before hiding.
 * @returns {{ dispose: function }} A handle to call dispose() for cleanup.
 */
export function attachIdleAutoHide(containerId, idleMs) {
    const container = document.getElementById(containerId);
    if (!container) return { dispose() {} };

    idleMs = idleMs || 3000;
    let timer = null;

    function showCursor() {
        container.classList.remove('idle-hide');
        clearTimeout(timer);
        timer = setTimeout(hideCursor, idleMs);
    }

    function hideCursor() {
        container.classList.add('idle-hide');
    }

    container.addEventListener('mousemove', showCursor);
    container.addEventListener('mousedown', showCursor);
    // Start the idle timer immediately
    timer = setTimeout(hideCursor, idleMs);

    return {
        dispose() {
            clearTimeout(timer);
            container.removeEventListener('mousemove', showCursor);
            container.removeEventListener('mousedown', showCursor);
            container.classList.remove('idle-hide');
        }
    };
}

/**
 * Attaches a global keydown listener so that pressing Space toggles
 * play/pause on the video element (preventing page scroll).
 *
 * @param {string} elementId - The DOM id of the <video> element.
 * @returns {{ dispose: function }} A handle to call dispose() for cleanup.
 */
export function attachKeyboardShortcuts(elementId) {
    const video = document.getElementById(elementId);
    if (!video) return { dispose() {} };

    function onKeyDown(e) {
        // Only handle Space; ignore if user is typing in an input/textarea
        if (e.code !== 'Space') return;
        const tag = (e.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable) return;

        e.preventDefault();
        if (video.paused) {
            video.play();
        } else {
            video.pause();
        }
    }

/**
 * Attaches an HLS.js player to the video element for HLS (.m3u8) streams.
 * On Safari (which natively supports HLS), sets the src directly.
 * On other browsers, uses hls.js for playback.
 *
 * @param {string} elementId - The DOM id of the <video> element.
 * @param {string} streamUrl - The URL of the HLS .m3u8 playlist.
 */
export function attachHlsPlayer(elementId, streamUrl) {
    const video = document.getElementById(elementId);
    if (!video) return;

    // Safari and other browsers with native HLS support — set src directly
    if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = streamUrl;
        video.play().catch(() => { /* autoplay may be blocked */ });
        return;
    }

    // Use hls.js for all other browsers (Chrome, Firefox, Edge)
    if (typeof Hls !== 'undefined' && Hls.isSupported()) {
        const hls = new Hls({
            enableWorker: true,
            lowLatencyMode: false
        });
        hls.loadSource(streamUrl);
        hls.attachMedia(video);

        hls.on(Hls.Events.MANIFEST_PARSED, function () {
            video.play().catch(() => { /* autoplay may be blocked */ });
        });

        hls.on(Hls.Events.ERROR, function (event, data) {
            if (data.fatal) {
                switch (data.type) {
                    case Hls.ErrorTypes.NETWORK_ERROR:
                        console.error('HLS: Fatal network error', data);
                        hls.startLoad();
                        break;
                    case Hls.ErrorTypes.MEDIA_ERROR:
                        console.error('HLS: Fatal media error', data);
                        hls.recoverMediaError();
                        break;
                    default:
                        console.error('HLS: Fatal error', data);
                        hls.destroy();
                        break;
                }
            }
        });

        // Store reference for cleanup
        video._hls = hls;
    } else {
        // No HLS support available — set src as fallback
        console.warn('HLS.js not available, setting src directly');
        video.src = streamUrl;
        video.play().catch(() => { /* autoplay may be blocked */ });
    }
}

/**
 * Destroys the HLS.js instance attached to a video element, if any.
 *
 * @param {string} elementId - The DOM id of the <video> element.
 */
export function destroyHlsPlayer(elementId) {
    const video = document.getElementById(elementId);
    if (!video) return;

    if (video._hls) {
        video._hls.destroy();
        delete video._hls;
    }
}

