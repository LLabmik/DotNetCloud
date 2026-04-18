/**
 * AI Assistant clipboard helpers.
 * Provides copy-as-markdown (plain text) and copy-as-formatted (rich HTML) functions.
 */
window.aiClipboard = {
    /**
     * Copy raw markdown text to the clipboard.
     * @param {string} text - The markdown source text.
     * @returns {Promise<boolean>} true on success.
     */
    copyMarkdown: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },

    /**
     * Copy rendered HTML to the clipboard as rich text so it pastes formatted
     * in editors that support it.  Also includes a plain-text fallback.
     * @param {string} html - The rendered HTML string.
     * @param {string} plainText - Plain-text fallback (the raw markdown).
     * @returns {Promise<boolean>} true on success.
     */
    copyFormatted: async function (html, plainText) {
        try {
            const htmlBlob = new Blob([html], { type: 'text/html' });
            const textBlob = new Blob([plainText], { type: 'text/plain' });
            const item = new ClipboardItem({
                'text/html': htmlBlob,
                'text/plain': textBlob
            });
            await navigator.clipboard.write([item]);
            return true;
        } catch {
            // Fallback: copy as plain text
            try {
                await navigator.clipboard.writeText(plainText);
                return true;
            } catch {
                return false;
            }
        }
    }
};
