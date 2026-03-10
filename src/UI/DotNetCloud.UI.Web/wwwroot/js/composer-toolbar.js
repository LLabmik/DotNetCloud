/**
 * Markdown toolbar helpers for the chat MessageComposer.
 * Invoked via JS interop from MessageComposer.razor.cs.
 */

window.composerToolbar = {
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
    }
};
