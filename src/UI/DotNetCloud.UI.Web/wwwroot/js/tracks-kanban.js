/**
 * Tracks Kanban – drag-and-drop helpers for enhanced card reordering.
 * Provides smooth visual feedback and position calculation.
 */
window.TracksKanban = {
    /**
     * Initialises drag-and-drop on a kanban container element.
     * @param {string} containerId - The DOM id of the kanban columns wrapper.
     * @param {DotNet.DotNetObjectReference} dotNetRef - Reference to the Blazor component.
     */
    init: function (containerId, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container._tracksRef = dotNetRef;
        container._tracksCleanup = [];

        // Add smooth scroll on drag near edges
        let scrollInterval = null;
        const scrollSpeed = 8;
        const edgeZone = 60;

        const onDragOver = function (e) {
            const rect = container.getBoundingClientRect();
            const x = e.clientX;

            clearInterval(scrollInterval);

            if (x < rect.left + edgeZone) {
                scrollInterval = setInterval(function () {
                    container.scrollLeft -= scrollSpeed;
                }, 16);
            } else if (x > rect.right - edgeZone) {
                scrollInterval = setInterval(function () {
                    container.scrollLeft += scrollSpeed;
                }, 16);
            }
        };

        const onDragEnd = function () {
            clearInterval(scrollInterval);
            scrollInterval = null;
        };

        container.addEventListener('dragover', onDragOver);
        container.addEventListener('dragend', onDragEnd);
        container.addEventListener('drop', onDragEnd);

        container._tracksCleanup.push(function () {
            container.removeEventListener('dragover', onDragOver);
            container.removeEventListener('dragend', onDragEnd);
            container.removeEventListener('drop', onDragEnd);
            clearInterval(scrollInterval);
        });
    },

    /**
     * Calculates the drop position of a card within a column based on mouse Y.
     * @param {string} columnElementId - The DOM id or data attribute of the column cards container.
     * @param {number} clientY - Current mouse Y position.
     * @returns {number} The 0-based index where the card should be inserted.
     */
    getDropPosition: function (columnElementId, clientY) {
        const column = document.querySelector('[data-list-id="' + columnElementId + '"] .tracks-kanban-cards');
        if (!column) return 0;

        const cards = column.querySelectorAll('.tracks-kanban-card:not(.dragging)');
        let position = cards.length;

        for (let i = 0; i < cards.length; i++) {
            const rect = cards[i].getBoundingClientRect();
            const midY = rect.top + rect.height / 2;
            if (clientY < midY) {
                position = i;
                break;
            }
        }

        return position;
    },

    /**
     * Clean up event listeners when the component disposes.
     * @param {string} containerId
     */
    dispose: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container || !container._tracksCleanup) return;

        container._tracksCleanup.forEach(function (fn) { fn(); });
        container._tracksCleanup = null;
        container._tracksRef = null;
    }
};
