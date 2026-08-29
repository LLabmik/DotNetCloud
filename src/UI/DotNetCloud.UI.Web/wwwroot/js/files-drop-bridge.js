window.dotnetcloudFilesDrop = window.dotnetcloudFilesDrop || {
    _normalizePath: function(path) {
        if (!path || typeof path !== "string") {
            return null;
        }

        const normalized = path.replace(/\\+/g, "/").replace(/^\/+/, "").trim();
        return normalized.length > 0 ? normalized : null;
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

    /**
     * Synchronously snapshot the drop payload.
     *
     * IMPORTANT: DataTransfer items/files are only valid during the synchronous
     * execution of the drop event. Reading them after an `await` loses all but
     * the first file in Chrome and can drop items in Firefox. Every browser call
     * (webkitGetAsEntry / getAsFile) must happen here, before any async work.
     * @param {DataTransfer} dt
     * @returns {{ entry?: any, file?: File, relativePath?: string|null }[]}
     */
    _snapshotDropSync: function(dt) {
        const snapshot = [];
        const items = dt.items;

        if (items && items.length > 0) {
            for (const item of items) {
                if (!item) {
                    continue;
                }

                const asEntry = typeof item.webkitGetAsEntry === "function" ? item.webkitGetAsEntry() : null;
                if (asEntry) {
                    snapshot.push({ entry: asEntry });
                    continue;
                }

                if (item.kind === "file") {
                    const file = item.getAsFile();
                    if (file) {
                        snapshot.push({ file: file, relativePath: this._normalizePath(file.name) });
                    }
                }
            }
        }

        // Fallback: some browsers expose files only through dt.files (or
        // expose an empty items list). Snapshot them synchronously too.
        if (snapshot.length === 0) {
            const files = dt.files;
            for (const file of files || []) {
                snapshot.push({
                    file: file,
                    relativePath: this._normalizePath(file.webkitRelativePath || file.name || null)
                });
            }
        }

        return snapshot;
    },

    /**
     * Asynchronously materialise a synchronous drop snapshot into upload
     * entries (traversing dropped directories) and hand them to the upload
     * module. Safe to run after the drop event because all DataTransfer
     * references were already captured synchronously.
     * @param {{ entry?: any, file?: File, relativePath?: string|null }[]} snapshot
     * @param {any} dotNetRef
     */
    _processSnapshotAsync: async function(snapshot, dotNetRef) {
        const output = [];

        for (const item of snapshot) {
            if (item.entry) {
                await this._walkEntryAsync(item.entry, "", output);
            } else if (item.file) {
                output.push({ file: item.file, relativePath: item.relativePath });
            }
        }

        if (!output || output.length === 0) {
            return;
        }

        // Store files in the upload module's pending list
        if (window.dotnetcloudUpload && window.dotnetcloudUpload.addExternalFiles) {
            const fileInfos = window.dotnetcloudUpload.addExternalFiles(output);
            // Notify Blazor so it can open the upload dialog with pre-populated files
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnFilesDropped", fileInfos);
            }
        }
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

        dropZone.addEventListener("drop", (event) => {
            event.preventDefault();
            event.stopPropagation();

            const dt = event.dataTransfer;
            if (!dt) {
                return;
            }

            // Snapshot synchronously while the DataTransfer is still valid.
            // Async processing (directory walking, file reads) happens after.
            const snapshot = this._snapshotDropSync(dt);
            if (!snapshot || snapshot.length === 0) {
                return;
            }

            this._processSnapshotAsync(snapshot, dotNetRef).catch((err) => {
                console.error("DotNetCloud drop bridge failed:", err);
            });
        }, true);

        return true;
    }
};
