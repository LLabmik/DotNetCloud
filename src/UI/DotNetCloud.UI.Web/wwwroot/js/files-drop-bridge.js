window.dotnetcloudFilesDrop = window.dotnetcloudFilesDrop || {
    _normalizePath: function(path) {
        if (!path || typeof path !== "string") {
            return null;
        }

        const normalized = path.replace(/\\+/g, "/").replace(/^\/+/, "").trim();
        return normalized.length > 0 ? normalized : null;
    },

    _extractFromFileList: function(files) {
        const entries = [];
        for (const file of files || []) {
            const relativePath = this._normalizePath(file.webkitRelativePath || file.name || null);
            entries.push({ file: file, relativePath: relativePath });
        }

        return entries;
    },

    _readEntriesAsync: function(directoryReader) {
        return new Promise(function(resolve, reject) {
            directoryReader.readEntries(resolve, reject);
        });
    },

    _readFileEntryAsync: function(fileEntry) {
        return new Promise(function(resolve, reject) {
            fileEntry.file(resolve, reject);
        });
    },

    _walkEntryAsync: async function(entry, relativePrefix, output) {
        if (!entry) {
            return;
        }

        if (entry.isFile) {
            const file = await this._readFileEntryAsync(entry);
            const relativePath = this._normalizePath(relativePrefix + entry.name);
            output.push({ file: file, relativePath: relativePath });
            return;
        }

        if (!entry.isDirectory) {
            return;
        }

        const nextPrefix = `${relativePrefix}${entry.name}/`;
        const reader = entry.createReader();

        // readEntries returns chunks; keep reading until exhausted.
        while (true) {
            const batch = await this._readEntriesAsync(reader);
            if (!batch || batch.length === 0) {
                break;
            }

            for (const child of batch) {
                await this._walkEntryAsync(child, nextPrefix, output);
            }
        }
    },

    _extractFromDataTransferItemsAsync: async function(items) {
        const output = [];

        for (const item of items || []) {
            if (!item) {
                continue;
            }

            const asEntry = typeof item.webkitGetAsEntry === "function" ? item.webkitGetAsEntry() : null;
            if (asEntry) {
                await this._walkEntryAsync(asEntry, "", output);
                continue;
            }

            if (item.kind === "file") {
                const file = item.getAsFile();
                if (file) {
                    output.push({ file: file, relativePath: this._normalizePath(file.name) });
                }
            }
        }

        return output;
    },

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

        dropZone.addEventListener("drop", async (event) => {
            event.preventDefault();
            event.stopPropagation();

            const dt = event.dataTransfer;
            if (!dt) {
                return;
            }

            try {
                const droppedEntries = (dt.items && dt.items.length > 0)
                    ? await this._extractFromDataTransferItemsAsync(dt.items)
                    : this._extractFromFileList(dt.files);

                if (!droppedEntries || droppedEntries.length === 0) {
                    return;
                }

                // Store files in the upload module's pending list
                if (window.dotnetcloudUpload && window.dotnetcloudUpload.addExternalFiles) {
                    const fileInfos = window.dotnetcloudUpload.addExternalFiles(droppedEntries);
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
