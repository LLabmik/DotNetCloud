/**
 * DotNetCloud File Upload Module
 *
 * Handles file reading, SHA-256 hashing, chunking, and HTTP upload entirely
 * on the browser side. Communicates progress back to Blazor via
 * DotNetObjectReference callbacks. This bypasses Blazor Server's SignalR
 * channel, which cannot reliably stream large files.
 *
 * Flow:
 *   1. User selects files → Blazor calls registerFiles() → JS stores File refs
 *   2. Blazor calls uploadFile(fileIndex) for each file
 *   3. JS reads file in 4 MB chunks, SHA-256 hashes each, calls /api/v1/files/upload/initiate
 *   4. JS uploads only missing chunks via PUT, reporting per-chunk progress
 *   5. JS calls /api/v1/files/upload/{sessionId}/complete
 *   6. Blazor receives onFileComplete / onFileError callbacks
 */
window.dotnetcloudUpload = (function () {
    "use strict";

    const CHUNK_SIZE = 4 * 1024 * 1024; // 4 MB — matches server's ContentHasher.DefaultChunkSize

    /** @type {{ file: File, relativePath: string|null }[]} */
    let _pendingFiles = [];

    /** @type {Map<string, Map<string, string>>} */
    const _folderChildrenCache = new Map();

    /** @type {Map<string, string>} */
    const _folderPathCache = new Map();

    /** @type {number|null} Cached max upload size in bytes from server config. */
    let _maxUploadSizeBytes = null;

    /**
     * Per-file upload state for pause/resume/cancel support.
     * @type {Map<number, { abortController: AbortController|null, paused: boolean, cancelled: boolean, sessionId: string|null, lastChunkIndex: number }>}
     */
    const _uploadState = new Map();

    /**
     * Fetches and caches the max upload size from the server.
     * @returns {Promise<number>} Max file size in bytes (0 = unlimited).
     */
    async function getMaxUploadSize() {
        if (_maxUploadSizeBytes !== null) return _maxUploadSizeBytes;

        try {
            const resp = await fetch("/api/v1/files/config", { credentials: "same-origin" });
            if (resp.ok) {
                const data = await resp.json();
                _maxUploadSizeBytes = (data && data.maxUploadSizeBytes) || 0;
            } else {
                _maxUploadSizeBytes = 0;
            }
        } catch {
            _maxUploadSizeBytes = 0;
        }

        return _maxUploadSizeBytes;
    }

    /**
     * Formats bytes into a human-readable size string.
     * @param {number} bytes
     * @returns {string}
     */
    function formatSize(bytes) {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
        if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + " MB";
        return (bytes / (1024 * 1024 * 1024)).toFixed(2) + " GB";
    }

    /**
     * Register files selected by the user.
     * Called from Blazor after an <input type="file"> change event.
     * @param {string} inputId - DOM id of the hidden file input
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function registerFiles(inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files) return [];

        _pendingFiles = [];
        const result = [];
        for (const f of input.files) {
            const relativePath = (f.webkitRelativePath && f.webkitRelativePath.length > 0)
                ? normalizeRelativePath(f.webkitRelativePath)
                : null;
            _pendingFiles.push({ file: f, relativePath: relativePath });
            result.push({
                name: f.name,
                size: f.size,
                type: f.type || "application/octet-stream",
                relativePath: relativePath
            });
        }
        // Reset input so re-selecting same files fires change again
        input.value = "";
        return result;
    }

    /**
     * Register files from a drag-and-drop DataTransfer.
     * @param {string} inputId - DOM id of the hidden file input (used to read transferred files)
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function registerDroppedFiles(inputId) {
        return registerFiles(inputId);
    }

    /**
     * Upload a single file by its index in the pending list.
     * @param {number} fileIndex
     * @param {string} userId - GUID of the authenticated user
     * @param {string|null} parentId - GUID of the target folder (null = root)
     * @param {any} dotNetRef - DotNetObjectReference for progress callbacks
     */
    async function uploadFile(fileIndex, userId, parentId, dotNetRef) {
        const pending = _pendingFiles[fileIndex];
        const file = pending ? pending.file : null;
        if (!file || !pending) {
            await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, "File reference not found.");
            return;
        }

        // Initialize upload state
        const state = _uploadState.get(fileIndex) || {
            abortController: null,
            paused: false,
            cancelled: false,
            sessionId: null,
            lastChunkIndex: -1
        };
        state.abortController = new AbortController();
        state.cancelled = false;
        state.paused = false;
        _uploadState.set(fileIndex, state);

        try {
            // 0. Client-side size validation
            const maxSize = await getMaxUploadSize();
            if (maxSize > 0 && file.size > maxSize) {
                await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex,
                    "File exceeds maximum upload size (" + formatSize(maxSize) + ").");
                return;
            }

            // 1. Chunk & hash the file on the client
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 0, "Preparing...");
            const chunks = await chunkAndHash(file, fileIndex, dotNetRef);

            const chunkHashes = chunks.map(c => c.hash);

            // 2. Initiate upload session (or resume existing one)
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 5, "Starting upload...");
            const apiBase = "/api/v1/files";
            const targetParentId = await resolveTargetParentId(
                pending.relativePath,
                parentId,
                userId,
                apiBase);

            let sessionId = state.sessionId;
            let missingSet;

            if (!sessionId) {
                const initBody = {
                    fileName: file.name,
                    parentId: targetParentId,
                    totalSize: file.size,
                    mimeType: file.type || "application/octet-stream",
                    chunkHashes: chunkHashes
                };

                const initResp = await fetch(
                    `${apiBase}/upload/initiate`,
                    {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        credentials: "same-origin",
                        body: JSON.stringify(initBody),
                        signal: state.abortController.signal
                    }
                );

                if (!initResp.ok) {
                    const errText = await initResp.text();
                    throw new Error(`Initiate failed (${initResp.status}): ${errText}`);
                }

                const initJson = await initResp.json();
                const session = initJson.data || initJson;
                sessionId = session.sessionId;
                state.sessionId = sessionId;
                missingSet = new Set((session.missingChunks || []).map(h => h.toLowerCase()));
            } else {
                // Resume: query server for remaining missing chunks
                const statusResp = await fetch(
                    `${apiBase}/upload/${sessionId}`,
                    { credentials: "same-origin", signal: state.abortController.signal }
                );
                if (statusResp.ok) {
                    const statusJson = await statusResp.json();
                    const statusData = statusJson.data || statusJson;
                    missingSet = new Set((statusData.missingChunks || []).map(h => h.toLowerCase()));
                } else {
                    missingSet = new Set(chunkHashes);
                }
            }

            // 3. Upload missing chunks with abort/pause support
            const totalMissing = missingSet.size;
            let uploaded = 0;

            for (const chunk of chunks) {
                if (!missingSet.has(chunk.hash)) continue;

                // Check for pause
                if (state.paused) {
                    await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex,
                        10 + Math.round((uploaded / Math.max(1, totalMissing)) * 80), "Paused");
                    return; // Exit; will be resumed via resumeUpload()
                }

                // Check for cancel
                if (state.cancelled) {
                    await cancelUploadSession(sessionId);
                    await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, "Upload cancelled.");
                    _uploadState.delete(fileIndex);
                    return;
                }

                const putResp = await fetch(
                    `${apiBase}/upload/${sessionId}/chunks/${chunk.hash}`,
                    {
                        method: "PUT",
                        headers: { "Content-Type": "application/octet-stream" },
                        credentials: "same-origin",
                        body: chunk.data,
                        signal: state.abortController.signal
                    }
                );

                if (!putResp.ok) {
                    const errText = await putResp.text();
                    throw new Error(`Chunk upload failed (${putResp.status}): ${errText}`);
                }

                uploaded++;
                // Progress: 10-90% for chunk uploads
                const pct = 10 + Math.round((uploaded / Math.max(1, totalMissing)) * 80);
                await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, pct, "Uploading...");
            }

            // 4. Complete upload
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 95, "Finalizing...");

            const completeResp = await fetch(
                `${apiBase}/upload/${sessionId}/complete`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    signal: state.abortController.signal
                }
            );

            if (!completeResp.ok) {
                const errText = await completeResp.text();
                throw new Error(`Complete failed (${completeResp.status}): ${errText}`);
            }

            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 100, "Complete");
            await dotNetRef.invokeMethodAsync("OnJsUploadComplete", fileIndex);
            _uploadState.delete(fileIndex);
        } catch (err) {
            if (err.name === "AbortError") {
                // Aborted by pause or cancel — don't report as error
                const st = _uploadState.get(fileIndex);
                if (st && st.paused) {
                    await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex,
                        _getLastProgress(fileIndex), "Paused");
                } else if (st && st.cancelled) {
                    if (st.sessionId) await cancelUploadSession(st.sessionId);
                    await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, "Upload cancelled.");
                    _uploadState.delete(fileIndex);
                }
                return;
            }
            console.error("DotNetCloud upload error:", err);
            await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, err.message || "Unknown error");
        }
    }

    /**
     * Returns the last known progress for a file index.
     * @param {number} fileIndex
     * @returns {number}
     */
    function _getLastProgress(fileIndex) {
        // Approximate from state; default to 50 if unknown
        return 50;
    }

    /**
     * Pauses an in-progress upload.
     * @param {number} fileIndex
     */
    function pauseUpload(fileIndex) {
        const state = _uploadState.get(fileIndex);
        if (!state) return;
        state.paused = true;
        if (state.abortController) state.abortController.abort();
    }

    /**
     * Resumes a paused upload. Caller must provide the same arguments as uploadFile.
     * @param {number} fileIndex
     * @param {string} userId
     * @param {string|null} parentId
     * @param {any} dotNetRef
     */
    async function resumeUpload(fileIndex, userId, parentId, dotNetRef) {
        const state = _uploadState.get(fileIndex);
        if (!state) return;
        state.paused = false;
        state.abortController = new AbortController();
        await uploadFile(fileIndex, userId, parentId, dotNetRef);
    }

    /**
     * Cancels an upload (aborts in-flight requests and cleans up server session).
     * @param {number} fileIndex
     */
    function cancelUpload(fileIndex) {
        const state = _uploadState.get(fileIndex);
        if (!state) return;
        state.cancelled = true;
        if (state.abortController) state.abortController.abort();
    }

    /**
     * Sends DELETE to clean up a server-side upload session.
     * @param {string} sessionId
     */
    async function cancelUploadSession(sessionId) {
        try {
            await fetch(`/api/v1/files/upload/${sessionId}`, {
                method: "DELETE",
                credentials: "same-origin"
            });
        } catch {
            // Best-effort cleanup
        }
    }

    /**
     * Read a File in CHUNK_SIZE slices and SHA-256 hash each.
     * Reports hashing progress (0-5%) via callback.
     */
    async function chunkAndHash(file, fileIndex, dotNetRef) {
        const totalChunks = Math.ceil(file.size / CHUNK_SIZE) || 1;
        const chunks = [];

        for (let i = 0; i < totalChunks; i++) {
            const start = i * CHUNK_SIZE;
            const end = Math.min(start + CHUNK_SIZE, file.size);
            const blob = file.slice(start, end);
            const buffer = await blob.arrayBuffer();
            const hashBuffer = await crypto.subtle.digest("SHA-256", buffer);
            const hashHex = Array.from(new Uint8Array(hashBuffer))
                .map(b => b.toString(16).padStart(2, "0"))
                .join("");

            chunks.push({ hash: hashHex, data: new Uint8Array(buffer) });

            // Hashing progress: 0-5%
            const pct = Math.round(((i + 1) / totalChunks) * 5);
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, pct, "Hashing...");
        }

        return chunks;
    }

    /**
     * Cancel/clear pending files.
     */
    function clearFiles() {
        _pendingFiles = [];
        _folderChildrenCache.clear();
        _folderPathCache.clear();
    }

    /**
     * Remove a single file by index.
     */
    function removeFile(index) {
        if (index >= 0 && index < _pendingFiles.length) {
            _pendingFiles.splice(index, 1);
        }
    }

    /**
     * Add files from an external source (e.g. drag-and-drop DataTransfer).
     * Appends to the pending files array without clearing existing ones.
     * @param {FileList|File[]|{file: File, relativePath?: string|null}[]} files
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function addExternalFiles(files) {
        const result = [];
        for (const f of files) {
            const isWrapped = !!(f && typeof f === "object" && "file" in f);
            const file = isWrapped ? f.file : f;
            const explicitPath = isWrapped ? f.relativePath : null;
            const relativePath = normalizeRelativePath(explicitPath || file.webkitRelativePath || null);

            _pendingFiles.push({ file: file, relativePath: relativePath });
            result.push({
                name: file.name,
                size: file.size,
                type: file.type || "application/octet-stream",
                relativePath: relativePath
            });
        }
        return result;
    }

    /**
     * Get metadata for all currently pending files.
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function getPendingFileInfos() {
        return _pendingFiles.map(entry => ({
            name: entry.file.name,
            size: entry.file.size,
            type: entry.file.type || "application/octet-stream",
            relativePath: entry.relativePath
        }));
    }

    function normalizeRelativePath(path) {
        if (!path || typeof path !== "string") {
            return null;
        }

        const normalized = path.replace(/\\+/g, "/").replace(/^\/+/, "").trim();
        return normalized.length > 0 ? normalized : null;
    }

    function extractDirectoryPath(relativePath) {
        const normalized = normalizeRelativePath(relativePath);
        if (!normalized) {
            return "";
        }

        const parts = normalized.split("/").filter(Boolean);
        if (parts.length <= 1) {
            return "";
        }

        parts.pop();
        return parts.join("/");
    }

    function readEnvelopeData(payload) {
        let current = payload;
        while (current && typeof current === "object" && current.data !== undefined) {
            current = current.data;
        }

        return current;
    }

    function parentCacheKey(parentId) {
        return parentId || "__root__";
    }

    async function getFolderChildrenMap(parentId, userId, apiBase) {
        const key = parentCacheKey(parentId);
        const existing = _folderChildrenCache.get(key);
        if (existing) {
            return existing;
        }

        const query = parentId
            ? `?parentId=${encodeURIComponent(parentId)}`
            : ``;
        const response = await fetch(`${apiBase}${query}`, {
            method: "GET",
            credentials: "same-origin"
        });

        if (!response.ok) {
            const errText = await response.text();
            throw new Error(`List folder failed (${response.status}): ${errText}`);
        }

        const body = await response.json();
        const list = readEnvelopeData(body) || [];
        const map = new Map();
        for (const node of list) {
            if (!node || node.nodeType !== "Folder") {
                continue;
            }

            map.set((node.name || "").toLowerCase(), node.id);
        }

        _folderChildrenCache.set(key, map);
        return map;
    }

    async function ensureFolder(parentId, folderName, userId, apiBase) {
        const children = await getFolderChildrenMap(parentId, userId, apiBase);
        const key = folderName.toLowerCase();

        if (children.has(key)) {
            return children.get(key);
        }

        const createResponse = await fetch(`${apiBase}/folders`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "same-origin",
            body: JSON.stringify({
                name: folderName,
                parentId: parentId || null
            })
        });

        if (!createResponse.ok) {
            // Another upload may have created it concurrently; refresh and retry lookup.
            _folderChildrenCache.delete(parentCacheKey(parentId));
            const refreshed = await getFolderChildrenMap(parentId, userId, apiBase);
            if (refreshed.has(key)) {
                return refreshed.get(key);
            }

            const errText = await createResponse.text();
            throw new Error(`Create folder failed (${createResponse.status}): ${errText}`);
        }

        const createBody = await createResponse.json();
        const created = readEnvelopeData(createBody);
        const createdId = created && created.id;
        if (!createdId) {
            throw new Error("Create folder response missing id.");
        }

        children.set(key, createdId);
        return createdId;
    }

    async function resolveTargetParentId(relativePath, rootParentId, userId, apiBase) {
        const directoryPath = extractDirectoryPath(relativePath);
        if (!directoryPath) {
            return rootParentId || null;
        }

        const normalized = directoryPath.toLowerCase();
        const rootKey = rootParentId || "__root__";
        const cacheKey = `${rootKey}|${normalized}`;
        if (_folderPathCache.has(cacheKey)) {
            return _folderPathCache.get(cacheKey);
        }

        let currentParentId = rootParentId || null;
        const segments = directoryPath.split("/").filter(Boolean);
        for (const segment of segments) {
            currentParentId = await ensureFolder(currentParentId, segment, userId, apiBase);
        }

        _folderPathCache.set(cacheKey, currentParentId);
        return currentParentId;
    }

    return {
        registerFiles: registerFiles,
        registerDroppedFiles: registerDroppedFiles,
        addExternalFiles: addExternalFiles,
        getPendingFileInfos: getPendingFileInfos,
        uploadFile: uploadFile,
        pauseUpload: pauseUpload,
        resumeUpload: resumeUpload,
        cancelUpload: cancelUpload,
        clearFiles: clearFiles,
        removeFile: removeFile,
        getMaxUploadSize: getMaxUploadSize,
        formatSize: formatSize
    };
})();
