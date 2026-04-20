/**
 * dotnetcloudChatScroll — JS interop for chat message list infinite scroll.
 * Provides IntersectionObserver-based sentinel detection, scroll-to-bottom,
 * and scroll position preservation when older messages are prepended.
 */
window.dotnetcloudChatScroll = (() => {
    let _observer = null;
    let _dotNetRef = null;
    let _invoking = false;

    /**
     * Starts observing a sentinel element at the top of the message list.
     * When the sentinel becomes visible (user scrolled to near the top),
     * invokes OnScrolledToTop on the provided .NET reference.
     * @param {string} sentinelId - The id of the sentinel element.
     * @param {object} dotNetRef - DotNet object reference for invokeMethodAsync.
     */
    function observeSentinel(sentinelId, dotNetRef) {
        disconnectSentinel();
        _dotNetRef = dotNetRef;
        _invoking = false;

        const sentinel = document.getElementById(sentinelId);
        if (!sentinel) return;

        _observer = new IntersectionObserver(
            (entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting && !_invoking) {
                        _invoking = true;
                        _dotNetRef.invokeMethodAsync('OnScrolledToTop')
                            .catch(() => { })
                            .finally(() => { _invoking = false; });
                    }
                }
            },
            {
                // Use the scrollable message list container as the viewport
                root: sentinel.closest('.chat-message-list'),
                rootMargin: '0px',
                threshold: 0
            }
        );

        _observer.observe(sentinel);
    }

    /**
     * Disconnects the sentinel observer. Call on channel switch or dispose.
     */
    function disconnectSentinel() {
        if (_observer) {
            _observer.disconnect();
            _observer = null;
        }
        _dotNetRef = null;
        _invoking = false;
    }

    /**
     * Scrolls the message list container to the bottom.
     * @param {string} containerSelector - CSS selector for the message list container.
     */
    function scrollToBottom(containerSelector) {
        const container = document.querySelector(containerSelector);
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    }

    /**
     * Captures the current scrollHeight of the container before prepending messages.
     * Returns the scrollHeight so it can be passed to restoreScrollPosition after render.
     * @param {string} containerSelector - CSS selector for the message list container.
     * @returns {number} The current scrollHeight.
     */
    function preserveScrollPosition(containerSelector) {
        const container = document.querySelector(containerSelector);
        return container ? container.scrollHeight : 0;
    }

    /**
     * Restores scroll position after older messages have been prepended.
     * Sets scrollTop = newScrollHeight - previousScrollHeight so the user stays
     * at the same visual position.
     * @param {string} containerSelector - CSS selector for the message list container.
     * @param {number} previousScrollHeight - The scrollHeight captured before prepend.
     */
    function restoreScrollPosition(containerSelector, previousScrollHeight) {
        const container = document.querySelector(containerSelector);
        if (container) {
            container.scrollTop = container.scrollHeight - previousScrollHeight;
        }
    }

    /**
     * Returns true if the container is scrolled near the bottom.
     * Used to decide whether to auto-scroll when a new message arrives.
     * @param {string} containerSelector - CSS selector for the message list container.
     * @param {number} threshold - Pixel distance from the bottom considered "near bottom". Default 150.
     * @returns {boolean}
     */
    function isNearBottom(containerSelector, threshold) {
        const container = document.querySelector(containerSelector);
        if (!container) return true;
        const distanceFromBottom = container.scrollHeight - container.scrollTop - container.clientHeight;
        return distanceFromBottom <= (threshold || 150);
    }

    return {
        observeSentinel,
        disconnectSentinel,
        scrollToBottom,
        preserveScrollPosition,
        restoreScrollPosition,
        isNearBottom
    };
})();
