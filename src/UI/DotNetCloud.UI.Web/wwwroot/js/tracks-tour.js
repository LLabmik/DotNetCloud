/**
 * Tracks Tour – JavaScript helpers for the onboarding tour.
 * Handles tooltip positioning, element highlighting, and scroll-to-target.
 */

window.tracksTour = {
    /** @type {HTMLElement|null} */
    _cutoutElement: null,

    /**
     * Positions the tour tooltip near a target element.
     * @param {string} selector - CSS selector for the target element.
     * @param {string} position - 'top', 'bottom', 'left', 'right'.
     * @returns {string} CSS style string for the tooltip wrapper.
     */
    positionTooltip: function (selector, position) {
        try {
            var target = document.querySelector(selector);
            if (!target) return '';

            // Scroll target into view if needed
            var targetRect = target.getBoundingClientRect();
            var viewportHeight = window.innerHeight;
            var viewportWidth = window.innerWidth;

            if (targetRect.bottom > viewportHeight || targetRect.top < 0 ||
                targetRect.right > viewportWidth || targetRect.left < 0) {
                target.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
            }

            // Recalculate after potential scroll
            targetRect = target.getBoundingClientRect();

            var tooltipWidth = 380; // approximate max-width
            var tooltipHeight = 200; // approximate height
            var gap = 16; // margin between target and tooltip

            var style = '';
            var tooltipLeft, tooltipTop;

            switch (position) {
                case 'top':
                    tooltipLeft = targetRect.left + (targetRect.width / 2) - (tooltipWidth / 2);
                    tooltipTop = targetRect.top - tooltipHeight - gap;
                    style = 'left: ' + Math.max(16, tooltipLeft) + 'px; top: ' + Math.max(16, tooltipTop) + 'px;';
                    break;

                case 'bottom':
                    tooltipLeft = targetRect.left + (targetRect.width / 2) - (tooltipWidth / 2);
                    tooltipTop = targetRect.bottom + gap;
                    style = 'left: ' + Math.max(16, tooltipLeft) + 'px; top: ' + Math.min(viewportHeight - tooltipHeight - 16, tooltipTop) + 'px;';
                    break;

                case 'left':
                    tooltipLeft = targetRect.left - tooltipWidth - gap;
                    tooltipTop = targetRect.top + (targetRect.height / 2) - (tooltipHeight / 2);
                    style = 'left: ' + Math.max(16, tooltipLeft) + 'px; top: ' + Math.max(16, Math.min(viewportHeight - tooltipHeight - 16, tooltipTop)) + 'px;';
                    break;

                case 'right':
                    tooltipLeft = targetRect.right + gap;
                    tooltipTop = targetRect.top + (targetRect.height / 2) - (tooltipHeight / 2);
                    style = 'left: ' + Math.min(viewportWidth - tooltipWidth - 16, tooltipLeft) + 'px; top: ' + Math.max(16, Math.min(viewportHeight - tooltipHeight - 16, tooltipTop)) + 'px;';
                    break;

                default:
                    // Center
                    style = 'top: 50%; left: 50%; transform: translate(-50%, -50%);';
                    break;
            }

            return style;
        } catch (e) {
            return '';
        }
    },

    /**
     * Creates a cutout highlight around the target element.
     * The cutout is a box with a massive box-shadow that acts as a dark overlay
     * with a transparent "hole" over the target.
     * @param {string} selector - CSS selector for the element to highlight.
     */
    highlightElement: function (selector) {
        try {
            var target = document.querySelector(selector);
            if (!target) return;

            var rect = target.getBoundingClientRect();
            var padding = 6; // extra space around the element

            var cutout = document.createElement('div');
            cutout.className = 'tour-highlight-cutout';
            cutout.style.position = 'fixed';
            cutout.style.zIndex = '9001';
            cutout.style.left = (rect.left - padding) + 'px';
            cutout.style.top = (rect.top - padding) + 'px';
            cutout.style.width = (rect.width + padding * 2) + 'px';
            cutout.style.height = (rect.height + padding * 2) + 'px';
            cutout.style.borderRadius = '8px';
            cutout.style.boxShadow = '0 0 0 9999px rgba(0, 0, 0, 0.55)';
            cutout.style.pointerEvents = 'none';
            cutout.style.transition = 'all 300ms ease';

            // Add a subtle glow ring around the target
            cutout.style.outline = '2px solid var(--color-primary, #4a90d9)';
            cutout.style.outlineOffset = '3px';

            document.body.appendChild(cutout);
            window.tracksTour._cutoutElement = cutout;
        } catch (e) {
            // Silently fail — the tour still works without highlights
        }
    },

    /**
     * Removes all highlight cutout elements from the DOM.
     */
    clearHighlights: function () {
        try {
            if (window.tracksTour._cutoutElement) {
                window.tracksTour._cutoutElement.remove();
                window.tracksTour._cutoutElement = null;
            }
            // Also remove any stray highlight elements
            var existing = document.querySelectorAll('.tour-highlight-cutout');
            existing.forEach(function (el) { el.remove(); });
        } catch (e) {
            // Silently fail
        }
    },

    /**
     * Scrolls an element into view smoothly.
     * @param {string} selector - CSS selector for the target element.
     */
    scrollToElement: function (selector) {
        try {
            var target = document.querySelector(selector);
            if (target) {
                target.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
            }
        } catch (e) {
            // Silently fail
        }
    }
};
