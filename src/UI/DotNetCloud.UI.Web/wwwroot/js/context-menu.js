/**
 * DotNetCloud File Context Menu Module
 *
 * Handles right-click events on file items in the file browser,
 * calculates viewport-aware positioning, and invokes Blazor callbacks.
 */
window.dotnetcloudContextMenu = (function () {
    "use strict";

    /** @type {AbortController|null} */
    let _controller = null;

    /** @type {any|null} */
    let _dotNetRef = null;

    /**
     * Initializes the context menu handler on the file browser container.
     * @param {string} containerSelector - CSS selector for the file browser element.
     * @param {any} dotNetRef - DotNetObjectReference for Blazor callbacks.
     */
    function init(containerSelector, dotNetRef) {
        dispose();
        _dotNetRef = dotNetRef;
        _controller = new AbortController();
        const signal = _controller.signal;

        // Right-click handler on file items
        document.addEventListener("contextmenu", function (event) {
            const container = document.querySelector(containerSelector);
            if (!container) return;

            const fileItem = event.target.closest(".file-item");
            if (!fileItem || !container.contains(fileItem)) return;

            event.preventDefault();

            const nodeId = fileItem.getAttribute("data-node-id");
            const nodeType = fileItem.getAttribute("data-node-type");
            if (!nodeId) return;

            // Calculate viewport-aware position
            const pos = calculatePosition(event.clientX, event.clientY);

            dotNetRef.invokeMethodAsync("OnContextMenu", nodeId, nodeType || "File", pos.x, pos.y);
        }, { signal: signal });

        // Dismiss on click outside, Escape, or scroll
        document.addEventListener("mousedown", function (event) {
            const menu = document.querySelector(".file-context-menu");
            if (menu && !menu.contains(event.target)) {
                dotNetRef.invokeMethodAsync("OnContextMenuDismiss");
            }
        }, { signal: signal });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                dotNetRef.invokeMethodAsync("OnContextMenuDismiss");
            }
        }, { signal: signal });

        document.addEventListener("scroll", function () {
            dotNetRef.invokeMethodAsync("OnContextMenuDismiss");
        }, { signal: signal, capture: true });
    }

    /**
     * Calculates menu position ensuring it stays within the viewport.
     * @param {number} clientX - Mouse X coordinate.
     * @param {number} clientY - Mouse Y coordinate.
     * @returns {{ x: number, y: number }}
     */
    function calculatePosition(clientX, clientY) {
        const menuWidth = 200;
        const menuHeight = 280;
        const padding = 8;

        let x = clientX;
        let y = clientY;

        // Keep within viewport bounds
        if (x + menuWidth + padding > window.innerWidth) {
            x = window.innerWidth - menuWidth - padding;
        }
        if (y + menuHeight + padding > window.innerHeight) {
            y = window.innerHeight - menuHeight - padding;
        }
        if (x < padding) x = padding;
        if (y < padding) y = padding;

        return { x: x, y: y };
    }

    /**
     * Focuses the first menu item for keyboard navigation.
     */
    function focusFirstItem() {
        requestAnimationFrame(function () {
            const menu = document.querySelector(".file-context-menu");
            if (menu) {
                const firstItem = menu.querySelector("[role='menuitem']");
                if (firstItem) firstItem.focus();
            }
        });
    }

    /**
     * Removes all event listeners.
     */
    function dispose() {
        if (_controller) {
            _controller.abort();
            _controller = null;
        }
        _dotNetRef = null;
    }

    return {
        init: init,
        dispose: dispose,
        focusFirstItem: focusFirstItem
    };
})();
