/**
 * DotNetCloud File Paste Upload Module
 *
 * Listens for paste events on the file browser container and captures
 * clipboard images. Generates timestamped filenames and triggers the
 * upload flow via the standard dotnetcloudUpload module.
 */
window.dotnetcloudFilePaste = (function () {
    "use strict";

    /** @type {AbortController|null} */
    let _controller = null;

    /**
     * Initializes the paste listener on the file browser container.
     * @param {string} containerSelector - CSS selector for the file browser element.
     * @param {any} dotNetRef - DotNetObjectReference for Blazor callbacks.
     */
    function init(containerSelector, dotNetRef) {
        dispose();

        _controller = new AbortController();

        // Listen on document so paste works when the file browser has focus
        document.addEventListener("paste", async (event) => {
            // Only handle paste when the file browser is visible
            const container = document.querySelector(containerSelector);
            if (!container) return;

            const clipboardData = event.clipboardData;
            if (!clipboardData || !clipboardData.items) return;

            for (const item of clipboardData.items) {
                if (!item.type || !item.type.startsWith("image/")) continue;

                const file = item.getAsFile();
                if (!file) continue;

                // Client-side size validation
                const maxSize = await window.dotnetcloudUpload.getMaxUploadSize();
                if (maxSize > 0 && file.size > maxSize) {
                    await dotNetRef.invokeMethodAsync("OnPasteError",
                        "Image exceeds maximum upload size (" +
                        window.dotnetcloudUpload.formatSize(maxSize) + ").");
                    event.preventDefault();
                    return;
                }

                // Generate timestamped filename
                const now = new Date();
                const pad = (n) => String(n).padStart(2, "0");
                const timestamp = now.getFullYear() + "-" +
                    pad(now.getMonth() + 1) + "-" +
                    pad(now.getDate()) + "-" +
                    pad(now.getHours()) +
                    pad(now.getMinutes()) +
                    pad(now.getSeconds());

                const ext = file.type === "image/png" ? "png" :
                    file.type === "image/jpeg" ? "jpg" :
                    file.type === "image/gif" ? "gif" :
                    file.type === "image/webp" ? "webp" : "png";

                const fileName = "paste-" + timestamp + "." + ext;

                // Create a new File with the generated name
                const namedFile = new File([file], fileName, { type: file.type });

                // Add to upload queue via the standard upload module
                window.dotnetcloudUpload.addExternalFiles([namedFile]);

                // Notify Blazor
                await dotNetRef.invokeMethodAsync("OnImagePasted", fileName, file.size);

                event.preventDefault();
                break; // Only handle the first image
            }
        }, { signal: _controller.signal });
    }

    /**
     * Removes the paste listener.
     */
    function dispose() {
        if (_controller) {
            _controller.abort();
            _controller = null;
        }
    }

    return {
        init: init,
        dispose: dispose
    };
})();
