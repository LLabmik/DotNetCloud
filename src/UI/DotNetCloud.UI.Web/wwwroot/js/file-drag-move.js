/**
 * DotNetCloud — Drag-and-drop move (internal file/folder reordering).
 *
 * Allows users to drag a file or folder item and drop it onto a folder
 * to move it there. Uses a custom dataTransfer type to distinguish from
 * external file drops (handled by files-drop-bridge.js).
 */
window.dotnetcloudDragMove = (function () {
    "use strict";

    const DRAG_TYPE = "application/x-dotnetcloud-node-id";
    let _container = null;
    let _dotNetRef = null;
    let _draggedNodeId = null;

    function init(containerSelector, dotNetRef) {
        _container = document.querySelector(containerSelector);
        _dotNetRef = dotNetRef;
        if (!_container) return;

        _container.addEventListener("dragstart", onDragStart);
        _container.addEventListener("dragover", onDragOver);
        _container.addEventListener("dragleave", onDragLeave);
        _container.addEventListener("drop", onDrop);
        _container.addEventListener("dragend", onDragEnd);

        // Mark all file items as draggable
        _updateDraggable();
    }

    function dispose() {
        if (_container) {
            _container.removeEventListener("dragstart", onDragStart);
            _container.removeEventListener("dragover", onDragOver);
            _container.removeEventListener("dragleave", onDragLeave);
            _container.removeEventListener("drop", onDrop);
            _container.removeEventListener("dragend", onDragEnd);
        }
        _container = null;
        _dotNetRef = null;
        _draggedNodeId = null;
    }

    /** Re-apply draggable attribute after content changes (e.g. navigation). */
    function refresh() {
        _updateDraggable();
    }

    function _updateDraggable() {
        if (!_container) return;
        const items = _container.querySelectorAll(".file-item[data-node-id]");
        items.forEach(function (el) {
            el.setAttribute("draggable", "true");
        });
    }

    // ── Event handlers ──────────────────────────────────────────────

    function onDragStart(e) {
        const item = e.target.closest(".file-item[data-node-id]");
        if (!item) return;

        _draggedNodeId = item.getAttribute("data-node-id");
        e.dataTransfer.setData(DRAG_TYPE, _draggedNodeId);
        e.dataTransfer.effectAllowed = "move";
        item.classList.add("dragging");
    }

    function onDragOver(e) {
        // Only accept internal node drags, not external file drops
        if (!e.dataTransfer.types.includes(DRAG_TYPE)) return;

        const folder = _findDropTarget(e);
        if (!folder) return;

        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
        folder.classList.add("drag-over-target");
    }

    function onDragLeave(e) {
        const folder = _findDropTarget(e);
        if (folder) folder.classList.remove("drag-over-target");
    }

    function onDrop(e) {
        // Only handle internal drags
        const sourceId = e.dataTransfer.getData(DRAG_TYPE);
        if (!sourceId) return;

        e.preventDefault();
        _clearDragClasses();

        const folder = _findDropTarget(e);
        if (!folder) return;

        const targetFolderId = folder.getAttribute("data-node-id");
        if (!targetFolderId || targetFolderId === sourceId) return;

        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync("OnDragMoveNode", sourceId, targetFolderId);
        }
    }

    function onDragEnd() {
        _clearDragClasses();
        _draggedNodeId = null;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /** Find the nearest folder item under the cursor. */
    function _findDropTarget(e) {
        const el = document.elementFromPoint(e.clientX, e.clientY);
        if (!el) return null;
        const item = el.closest('.file-item[data-node-type="Folder"]');
        // Don't allow dropping on itself
        if (item && item.getAttribute("data-node-id") === _draggedNodeId) return null;
        return item;
    }

    function _clearDragClasses() {
        if (!_container) return;
        _container.querySelectorAll(".dragging").forEach(function (el) {
            el.classList.remove("dragging");
        });
        _container.querySelectorAll(".drag-over-target").forEach(function (el) {
            el.classList.remove("drag-over-target");
        });
    }

    return {
        init: init,
        dispose: dispose,
        refresh: refresh
    };
})();
