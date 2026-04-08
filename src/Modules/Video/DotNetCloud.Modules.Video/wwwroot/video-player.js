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

    video.addEventListener('error', function () {
        const err = video.error;
        if (!err) return;
        dotNetRef.invokeMethodAsync('OnVideoError', err.code, err.message || '');
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
