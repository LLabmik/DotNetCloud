window.dotnetcloudFilesDrop = window.dotnetcloudFilesDrop || {
    /**
     * Initialise the drag-drop bridge for the file browser.
     * Dropped files are registered with the dotnetcloudUpload module (file-upload.js)
     * and the Blazor component is notified via DotNetObjectReference callback.
     * @param {string} dropZoneSelector - CSS selector for the drop zone element
     * @param {any} dotNetRef - DotNetObjectReference with OnFilesDropped(FileInfo[]) method
     */
    init: function(dropZoneSelector, dotNetRef) {
        const dropZone = document.querySelector(dropZoneSelector);
        if (!dropZone) {
            return false;
        }

        if (dropZone.dataset.dncDropBridgeInit === "1") {
            return true;
        }

        dropZone.dataset.dncDropBridgeInit = "1";

        dropZone.addEventListener("drop", function(event) {
            event.preventDefault();
            event.stopPropagation();

            const dt = event.dataTransfer;
            if (!dt || !dt.files || dt.files.length === 0) {
                return;
            }

            try {
                // Store files in the upload module's pending list
                if (window.dotnetcloudUpload && window.dotnetcloudUpload.addExternalFiles) {
                    const fileInfos = window.dotnetcloudUpload.addExternalFiles(dt.files);
                    // Notify Blazor so it can open the upload dialog with pre-populated files
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync("OnFilesDropped", fileInfos);
                    }
                }
            } catch (err) {
                console.error("DotNetCloud drop bridge failed:", err);
            }
        }, true);

        return true;
    }
};
