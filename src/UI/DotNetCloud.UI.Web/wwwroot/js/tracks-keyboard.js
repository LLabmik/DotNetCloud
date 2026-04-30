/**
 * Tracks Keyboard – global keyboard shortcuts for the Tracks module.
 * Provides fast keyboard navigation that works anywhere on the page.
 * Shortcuts are disabled when typing in text fields (except '?' and 'Escape').
 */

window.tracksKeyboard = {
    /** @type {DotNet.DotNetObjectReference|null} */
    _dotNetRef: null,

    /**
     * Initialises the global keyboard shortcut listener.
     * Call this once from Blazor OnAfterRenderAsync.
     * @param {DotNet.DotNetObjectReference} dotNetRef - Reference to the TracksPage component.
     */
    init: function (dotNetRef) {
        if (window.tracksKeyboard._listenerAttached) {
            // Update reference but don't reattach
            window.tracksKeyboard._dotNetRef = dotNetRef;
            return;
        }

        window.tracksKeyboard._dotNetRef = dotNetRef;
        window.tracksKeyboard._listenerAttached = true;

        document.addEventListener('keydown', function (e) {
            // Check if user is typing in an input, textarea, or contenteditable
            var tag = document.activeElement ? document.activeElement.tagName : '';
            var isInput = (
                tag === 'INPUT' ||
                tag === 'TEXTAREA' ||
                tag === 'SELECT' ||
                (document.activeElement && document.activeElement.isContentEditable)
            );

            // Always allow Escape and ? even in inputs
            if (isInput && e.key !== 'Escape' && e.key !== '?') {
                return;
            }

            // Check for shortcuts
            var handled = false;

            switch (e.key) {
                case '?':
                    handled = true;
                    break;
                case 'Escape':
                    handled = true;
                    break;
                case 'n':
                case 'N':
                    if (!e.ctrlKey && !e.metaKey && !e.altKey) handled = true;
                    break;
                case '/':
                    if (!e.ctrlKey && !e.metaKey && !e.altKey) handled = true;
                    break;
                case 'ArrowLeft':
                    if (!e.ctrlKey && !e.metaKey && !e.altKey) handled = true;
                    break;
                case 'ArrowRight':
                    if (!e.ctrlKey && !e.metaKey && !e.altKey) handled = true;
                    break;
                case 'Enter':
                    if (e.ctrlKey || e.metaKey) handled = true;
                    break;
            }

            if (handled) {
                e.preventDefault();
                var ctrlOrMeta = e.ctrlKey || e.metaKey;
                dotNetRef.invokeMethodAsync('HandleKeyDownAsync', e.key, ctrlOrMeta, isInput);
            }
        });
    },

    /**
     * Focuses the first search/filter input on the page.
     * Called when the user presses '/'.
     */
    focusSearch: function () {
        var searchInput = document.querySelector('.tracks-search-input, [data-tracks-search]');
        if (searchInput) {
            searchInput.focus();
            searchInput.select();
        }
    },

    /**
     * Submits the currently active form (if any).
     * Called when the user presses Ctrl+Enter.
     */
    submitActiveForm: function () {
        var activeEl = document.activeElement;
        if (!activeEl) return;

        // Find the closest form or dialog
        var form = activeEl.closest('form');
        if (form) {
            // Look for a submit button
            var submitBtn = form.querySelector('button[type="submit"], .btn-primary');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.click();
            }
            return;
        }

        // Check for dialogs/modals
        var dialog = activeEl.closest('.tracks-wizard-overlay, .tracks-confirm-dialog');
        if (dialog) {
            var primaryBtn = dialog.querySelector('.btn-primary');
            if (primaryBtn && !primaryBtn.disabled) {
                primaryBtn.click();
            }
        }
    }
};
