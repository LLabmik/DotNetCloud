/**
 * dotnetcloudChatScroll — JS interop for chat message list infinite scroll.
 * Provides IntersectionObserver-based sentinel detection, scroll-to-bottom,
 * and scroll position preservation when older messages are prepended.
 */
window.dotnetcloudChatScroll = (() => {
  let _observer = null;
  let _dotNetRef = null;
  let _invoking = false;
  let _bottomPin = null; // Active "keep pinned to the bottom" session, if any

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
            _dotNetRef
              .invokeMethodAsync("OnScrolledToTop")
              .catch(() => {})
              .finally(() => {
                _invoking = false;
              });
          }
        }
      },
      {
        // Use the scrollable message list container as the viewport
        root: sentinel.closest(".chat-message-list"),
        rootMargin: "0px",
        threshold: 0,
      },
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
   * Scrolls the message list container to the bottom and keeps it pinned there
   * while asynchronously-loaded content settles.
   *
   * A one-shot `scrollTop = scrollHeight` assignment is not enough: message
   * attachments, link-preview thumbnails, avatars and fonts load after the
   * initial render and grow the list, which pushes the newest message below the
   * fold. This keeps re-applying the scroll until the content stops growing, the
   * user scrolls away, or a short safety timeout elapses.
   * @param {string} containerSelector - CSS selector for the message list container.
   */
  function scrollToBottom(containerSelector) {
    const container = document.querySelector(containerSelector);
    if (!container) return;
    pinToBottom(container);
  }

  /**
   * Pins a scrollable container to its bottom until it settles.
   * @param {HTMLElement} container - The scrollable container element.
   */
  function pinToBottom(container) {
    // Replace any previous pin (e.g. the user switches channels quickly).
    if (_bottomPin) {
      _bottomPin.release();
    }

    const apply = () => {
      container.scrollTop = container.scrollHeight;
    };

    // Apply immediately, then again across two animation frames so the scroll
    // survives the layout pass that follows the Blazor DOM update.
    apply();
    requestAnimationFrame(() => {
      apply();
      requestAnimationFrame(apply);
    });

    let released = false;
    let timeoutId = null;

    const watchImages = () => {
      container.querySelectorAll("img").forEach((img) => {
        if (img.complete) return;
        img.addEventListener("load", onContentChanged, { once: true });
        img.addEventListener("error", onContentChanged, { once: true });
      });
    };

    const onContentChanged = () => {
      if (!released) apply();
    };

    const onScroll = () => {
      // Our own programmatic scrolls also fire 'scroll'; only release the
      // pin once the user has actually moved away from the bottom.
      requestAnimationFrame(() => {
        if (!released && !isNearBottomEl(container, 40)) release();
      });
    };

    const observer = new MutationObserver(() => {
      if (released) return;
      apply();
      watchImages();
    });

    const release = () => {
      if (released) return;
      released = true;
      clearTimeout(timeoutId);
      observer.disconnect();
      container.removeEventListener("scroll", onScroll);
      container.querySelectorAll("img").forEach((img) => {
        img.removeEventListener("load", onContentChanged);
        img.removeEventListener("error", onContentChanged);
      });
      if (_bottomPin && _bottomPin.container === container) {
        _bottomPin = null;
      }
    };

    observer.observe(container, { childList: true, subtree: true });
    container.addEventListener("scroll", onScroll, { passive: true });
    watchImages();

    // Safety net: never stay pinned indefinitely (e.g. a slow-loading image
    // that never fires load). The user scrolling away releases it earlier.
    timeoutId = setTimeout(release, 8000);

    _bottomPin = { container, release };
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
    if (!container) return;

    // An explicit position restore (after prepending older messages) must win
    // over any active bottom pin.
    if (_bottomPin && _bottomPin.container === container) {
      _bottomPin.release();
    }

    container.scrollTop = container.scrollHeight - previousScrollHeight;
  }

  /**
   * Returns true if the given container element is scrolled near its bottom.
   * @param {HTMLElement} container - The scrollable container element.
   * @param {number} threshold - Pixel distance from the bottom considered "near bottom". Default 150.
   * @returns {boolean}
   */
  function isNearBottomEl(container, threshold) {
    if (!container) return true;
    const distanceFromBottom =
      container.scrollHeight - container.scrollTop - container.clientHeight;
    return distanceFromBottom <= (threshold || 150);
  }

  /**
   * Returns true if the container is scrolled near the bottom.
   * Used to decide whether to auto-scroll when a new message arrives.
   * @param {string} containerSelector - CSS selector for the message list container.
   * @param {number} threshold - Pixel distance from the bottom considered "near bottom". Default 150.
   * @returns {boolean}
   */
  function isNearBottom(containerSelector, threshold) {
    return isNearBottomEl(document.querySelector(containerSelector), threshold);
  }

  return {
    observeSentinel,
    disconnectSentinel,
    scrollToBottom,
    preserveScrollPosition,
    restoreScrollPosition,
    isNearBottom,
  };
})();
