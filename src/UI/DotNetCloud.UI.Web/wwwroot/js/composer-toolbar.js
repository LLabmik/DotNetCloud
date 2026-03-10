/**
 * Markdown toolbar helpers for the chat MessageComposer.
 * Invoked via JS interop from MessageComposer.razor.cs.
 */

window.composerToolbar = {
    _pasteHandlers: {},

    /**
     * Wraps the current selection in a textarea with the given prefix and suffix.
     * If no text is selected, inserts prefix+suffix at the cursor and places the
     * cursor between them so the user can type immediately.
     *
     * @param {string} textareaId - The id attribute of the target <textarea>
     * @param {string} prefix     - Markdown prefix (e.g. "**")
     * @param {string} suffix     - Markdown suffix (e.g. "**")
     * @returns {string}          - The new full value of the textarea
     */
    wrapSelection: function (textareaId, prefix, suffix) {
        const textarea = document.getElementById(textareaId);
        if (!textarea) return '';

        const start = textarea.selectionStart;
        const end   = textarea.selectionEnd;
        const value = textarea.value;
        const selected = value.substring(start, end);

        const wrapped = prefix + selected + suffix;
        textarea.value = value.substring(0, start) + wrapped + value.substring(end);

        // Restore / advance cursor position
        if (selected.length === 0) {
            // No selection — place cursor between prefix and suffix
            const cursor = start + prefix.length;
            textarea.setSelectionRange(cursor, cursor);
        } else {
            // Keep whole wrapped region selected
            textarea.setSelectionRange(start, start + wrapped.length);
        }

        textarea.focus();
        // Trigger Blazor's oninput binding so _messageText stays in sync
        textarea.dispatchEvent(new Event('input', { bubbles: true }));

        return textarea.value;
    },

    /**
     * Registers paste listener for image clipboard data and forwards payload to .NET.
     *
     * @param {string} textareaId
     * @param {any} dotNetRef
     */
    registerPasteImageHandler: function (textareaId, dotNetRef) {
        const textarea = document.getElementById(textareaId);
        if (!textarea || !dotNetRef) {
            return;
        }

        this.unregisterPasteImageHandler(textareaId);

        const onPaste = async (event) => {
            const clipboardData = event.clipboardData;
            if (!clipboardData || !clipboardData.items) {
                return;
            }

            for (const item of clipboardData.items) {
                if (!item.type || !item.type.startsWith('image/')) {
                    continue;
                }

                const file = item.getAsFile();
                if (!file) {
                    continue;
                }

                const dataUrl = await new Promise((resolve) => {
                    const reader = new FileReader();
                    reader.onload = () => resolve(reader.result || '');
                    reader.onerror = () => resolve('');
                    reader.readAsDataURL(file);
                });

                if (!dataUrl) {
                    continue;
                }

                await dotNetRef.invokeMethodAsync(
                    'HandlePastedImageFromJs',
                    file.name || 'pasted-image.png',
                    file.type || 'image/png',
                    dataUrl,
                    file.size || 0);

                event.preventDefault();
                break;
            }
        };

        textarea.addEventListener('paste', onPaste);
        this._pasteHandlers[textareaId] = { textarea: textarea, handler: onPaste };
    },

    /**
     * Unregisters paste listener previously attached for a textarea.
     *
     * @param {string} textareaId
     */
    unregisterPasteImageHandler: function (textareaId) {
        const entry = this._pasteHandlers[textareaId];
        if (!entry) {
            return;
        }

        entry.textarea.removeEventListener('paste', entry.handler);
        delete this._pasteHandlers[textareaId];
    }
};
