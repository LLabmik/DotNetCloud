window.dotnetcloudMarkdownEditor = {
    /**
     * Wraps selected text with prefix/suffix, or inserts fallback if nothing selected.
     * Returns the new full value of the textarea so Blazor can update its binding.
     */
    applyFormat: function (textareaElement, prefix, suffix, fallback) {
        if (!textareaElement) return null;

        var start = textareaElement.selectionStart;
        var end = textareaElement.selectionEnd;
        var value = textareaElement.value;
        var selected = value.substring(start, end);
        var replacement;
        var cursorPos;

        if (selected.length > 0) {
            // Wrap selected text
            replacement = prefix + selected + suffix;
            cursorPos = start + replacement.length;
        } else {
            // No selection — insert fallback text
            replacement = fallback;
            // Place cursor inside the markers (after prefix, before suffix)
            cursorPos = start + prefix.length + fallback.length - prefix.length - suffix.length;
            if (cursorPos < start) cursorPos = start + replacement.length;
        }

        var newValue = value.substring(0, start) + replacement + value.substring(end);
        textareaElement.value = newValue;

        // Restore focus and cursor position
        textareaElement.focus();
        textareaElement.selectionStart = cursorPos;
        textareaElement.selectionEnd = cursorPos;

        return newValue;
    },

    /**
     * Inserts a block of text at the cursor position (for blocks like lists, tables).
     * Returns the new full value.
     */
    insertAtCursor: function (textareaElement, text) {
        if (!textareaElement) return null;

        var start = textareaElement.selectionStart;
        var end = textareaElement.selectionEnd;
        var value = textareaElement.value;

        var newValue = value.substring(0, start) + text + value.substring(end);
        textareaElement.value = newValue;

        var cursorPos = start + text.length;
        textareaElement.focus();
        textareaElement.selectionStart = cursorPos;
        textareaElement.selectionEnd = cursorPos;

        return newValue;
    }
};
