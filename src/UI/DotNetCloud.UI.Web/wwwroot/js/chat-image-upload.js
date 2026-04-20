/**
 * Chat image upload JS interop helpers.
 * Provides:
 *   - chatImageUpload.triggerFileInput(inputId)  — clicks the hidden file input
 *   - chatImageUpload.init(inputId, dotNetRef)   — wires up the change listener
 */
window.chatImageUpload = {
    _initialized: {},

    /**
     * Opens the native file picker by clicking the hidden <input type="file">.
     * Lazily initialises the change listener on first call.
     */
    triggerFileInput: function (inputId, dotNetRef) {
        const input = document.getElementById(inputId);
        if (!input) return;

        if (!this._initialized[inputId] && dotNetRef) {
            this._initListener(inputId, input, dotNetRef);
        }

        // Reset value so the same file can be re-selected
        input.value = '';
        input.click();
    },

    _initListener: function (inputId, input, dotNetRef) {
        this._initialized[inputId] = true;
        input.addEventListener('change', async () => {
            const file = input.files?.[0];
            if (!file || !file.type.startsWith('image/')) return;

            const dataUrl = await new Promise((resolve) => {
                const reader = new FileReader();
                reader.onload = () => resolve(reader.result || '');
                reader.onerror = () => resolve('');
                reader.readAsDataURL(file);
            });
            if (!dataUrl) return;

            await dotNetRef.invokeMethodAsync(
                'HandleFileSelected',
                file.name,
                file.type,
                dataUrl,
                file.size
            );
        });
    }
};
