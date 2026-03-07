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

    /** @type {File[]} */
    let _pendingFiles = [];

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
            _pendingFiles.push(f);
            result.push({ name: f.name, size: f.size, type: f.type || "application/octet-stream" });
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
        const file = _pendingFiles[fileIndex];
        if (!file) {
            await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, "File reference not found.");
            return;
        }

        try {
            // 1. Chunk & hash the file on the client
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 0, "Preparing...");
            const chunks = await chunkAndHash(file, fileIndex, dotNetRef);

            const chunkHashes = chunks.map(c => c.hash);

            // 2. Initiate upload session
            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 5, "Starting upload...");
            const apiBase = "/api/v1/files";
            const initBody = {
                fileName: file.name,
                parentId: parentId,
                totalSize: file.size,
                mimeType: file.type || "application/octet-stream",
                chunkHashes: chunkHashes
            };

            const initResp = await fetch(
                `${apiBase}/upload/initiate?userId=${encodeURIComponent(userId)}`,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    credentials: "same-origin",
                    body: JSON.stringify(initBody)
                }
            );

            if (!initResp.ok) {
                const errText = await initResp.text();
                throw new Error(`Initiate failed (${initResp.status}): ${errText}`);
            }

            const initJson = await initResp.json();
            const session = initJson.data || initJson;
            const sessionId = session.sessionId;
            const missingSet = new Set((session.missingChunks || []).map(h => h.toLowerCase()));

            // 3. Upload missing chunks
            const totalMissing = missingSet.size;
            let uploaded = 0;

            for (const chunk of chunks) {
                if (!missingSet.has(chunk.hash)) continue;

                const putResp = await fetch(
                    `${apiBase}/upload/${sessionId}/chunks/${chunk.hash}?userId=${encodeURIComponent(userId)}`,
                    {
                        method: "PUT",
                        headers: { "Content-Type": "application/octet-stream" },
                        credentials: "same-origin",
                        body: chunk.data
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
                `${apiBase}/upload/${sessionId}/complete?userId=${encodeURIComponent(userId)}`,
                {
                    method: "POST",
                    credentials: "same-origin"
                }
            );

            if (!completeResp.ok) {
                const errText = await completeResp.text();
                throw new Error(`Complete failed (${completeResp.status}): ${errText}`);
            }

            await dotNetRef.invokeMethodAsync("OnJsUploadProgress", fileIndex, 100, "Complete");
            await dotNetRef.invokeMethodAsync("OnJsUploadComplete", fileIndex);
        } catch (err) {
            console.error("DotNetCloud upload error:", err);
            await dotNetRef.invokeMethodAsync("OnJsUploadError", fileIndex, err.message || "Unknown error");
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
     * @param {FileList|File[]} files
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function addExternalFiles(files) {
        const result = [];
        for (const f of files) {
            _pendingFiles.push(f);
            result.push({ name: f.name, size: f.size, type: f.type || "application/octet-stream" });
        }
        return result;
    }

    /**
     * Get metadata for all currently pending files.
     * @returns {{ name: string, size: number, type: string }[]}
     */
    function getPendingFileInfos() {
        return _pendingFiles.map(f => ({
            name: f.name,
            size: f.size,
            type: f.type || "application/octet-stream"
        }));
    }

    return {
        registerFiles: registerFiles,
        registerDroppedFiles: registerDroppedFiles,
        addExternalFiles: addExternalFiles,
        getPendingFileInfos: getPendingFileInfos,
        uploadFile: uploadFile,
        clearFiles: clearFiles,
        removeFile: removeFile
    };
})();
