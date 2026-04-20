/**
 * Chat image upload JS interop helpers.
 * Uploads images via HTTP POST to bypass SignalR message size limits.
 * Provides:
 *   - chatImageUpload.triggerFileInput(inputId, dotNetRef, channelId)
 *   - chatImageUpload.uploadImageFile(file, channelId)
 */
window.chatImageUpload = {
    _initialized: {},

    /**
     * Opens the native file picker by clicking the hidden <input type="file">.
     * @param {string} inputId - ID of the hidden file input element
     * @param {object} dotNetRef - .NET object reference for callbacks
     * @param {string} channelId - The channel GUID for the upload endpoint
     */
    triggerFileInput: function (inputId, dotNetRef, channelId) {
        const input = document.getElementById(inputId);
        if (!input) return;

        if (!this._initialized[inputId] && dotNetRef) {
            this._initListener(inputId, input, dotNetRef, channelId);
        }

        // Update channelId in case the user switched channels
        if (this._initialized[inputId]) {
            this._initialized[inputId].channelId = channelId;
        }

        // Reset value so the same file can be re-selected
        input.value = '';
        input.click();
    },

    _initListener: function (inputId, input, dotNetRef, channelId) {
        this._initialized[inputId] = { dotNetRef, channelId };
        input.addEventListener('change', async () => {
            const file = input.files?.[0];
            if (!file || !file.type.startsWith('image/')) return;

            const ctx = chatImageUpload._initialized[inputId];
            if (!ctx) return;

            const result = await chatImageUpload.uploadImageFile(file, ctx.channelId);
            if (!result) return;

            await ctx.dotNetRef.invokeMethodAsync(
                'HandleImageUploaded',
                result.url,
                result.fileName,
                result.mimeType,
                result.fileSize
            );
        });
    },

    /**
     * Uploads an image file to the server via HTTP POST.
     * @param {File|Blob} file - The image file to upload
     * @param {string} channelId - The channel GUID
     * @returns {Promise<{url: string, fileName: string, mimeType: string, fileSize: number}|null>}
     */
    uploadImageFile: async function (file, channelId) {
        if (!file || !channelId) return null;

        try {
            const response = await fetch(`/api/v1/chat/channels/${channelId}/upload-image`, {
                method: 'POST',
                headers: {
                    'Content-Type': file.type || 'image/png',
                    'X-File-Name': file.name || 'image.png'
                },
                body: file,
                credentials: 'same-origin'
            });

            if (!response.ok) return null;

            const envelope = await response.json();
            return envelope?.data || null;
        } catch {
            return null;
        }
    }
};
