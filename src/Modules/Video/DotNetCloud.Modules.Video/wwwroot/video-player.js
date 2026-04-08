/**
 * Attaches an error listener to the <video> element and calls back to .NET
 * when the browser cannot play the media.
 *
 * @param {string} elementId - The DOM id of the <video> element.
 * @param {object} dotNetRef - A DotNetObjectReference for calling OnVideoError.
 */
export function attachVideoErrorListener(elementId, dotNetRef) {
    const video = document.getElementById(elementId);
    if (!video) return;

    video.addEventListener('error', function () {
        const err = video.error;
        if (!err) return;
        dotNetRef.invokeMethodAsync('OnVideoError', err.code, err.message || '');
    }, { once: true });
}
