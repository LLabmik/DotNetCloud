window.dotnetcloudFilePreviewGestures = window.dotnetcloudFilePreviewGestures || (function () {
    "use strict";

    let nextId = 1;
    const handlers = new Map();

    function getTouchDistance(touchA, touchB) {
        const dx = touchA.clientX - touchB.clientX;
        const dy = touchA.clientY - touchB.clientY;
        return Math.sqrt((dx * dx) + (dy * dy));
    }

    function init(element, dotNetRef) {
        if (!element || !dotNetRef) {
            return 0;
        }

        const id = nextId++;
        const state = {
            startX: 0,
            startY: 0,
            startDistance: 0,
            isPinch: false,
            onTouchStart: null,
            onTouchMove: null,
            onTouchEnd: null
        };

        state.onTouchStart = function (event) {
            if (!event.touches || event.touches.length === 0) {
                return;
            }

            if (event.touches.length === 1) {
                state.isPinch = false;
                state.startX = event.touches[0].clientX;
                state.startY = event.touches[0].clientY;
                state.startDistance = 0;
                return;
            }

            if (event.touches.length === 2) {
                state.isPinch = true;
                state.startDistance = getTouchDistance(event.touches[0], event.touches[1]);
            }
        };

        state.onTouchMove = function (event) {
            if (!state.isPinch || !event.touches || event.touches.length !== 2) {
                return;
            }

            const currentDistance = getTouchDistance(event.touches[0], event.touches[1]);
            if (state.startDistance <= 0 || currentDistance <= 0) {
                return;
            }

            const scale = currentDistance / state.startDistance;
            if (Math.abs(scale - 1) < 0.05) {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("OnPinchScale", scale);
            state.startDistance = currentDistance;
        };

        state.onTouchEnd = function (event) {
            if (state.isPinch) {
                if (!event.touches || event.touches.length < 2) {
                    state.isPinch = false;
                    state.startDistance = 0;
                }

                return;
            }

            if (event.changedTouches.length === 0) {
                return;
            }

            const touch = event.changedTouches[0];
            const deltaX = touch.clientX - state.startX;
            const deltaY = touch.clientY - state.startY;

            if (Math.abs(deltaX) < 50 || Math.abs(deltaX) < Math.abs(deltaY) * 1.2) {
                return;
            }

            if (deltaX < 0) {
                dotNetRef.invokeMethodAsync("OnSwipeLeft");
            } else {
                dotNetRef.invokeMethodAsync("OnSwipeRight");
            }
        };

        element.addEventListener("touchstart", state.onTouchStart, { passive: true });
        element.addEventListener("touchmove", state.onTouchMove, { passive: false });
        element.addEventListener("touchend", state.onTouchEnd, { passive: true });

        handlers.set(id, { element: element, state: state });
        return id;
    }

    function dispose(id) {
        const entry = handlers.get(id);
        if (!entry) {
            return;
        }

        const element = entry.element;
        const state = entry.state;

        element.removeEventListener("touchstart", state.onTouchStart);
        element.removeEventListener("touchmove", state.onTouchMove);
        element.removeEventListener("touchend", state.onTouchEnd);

        handlers.delete(id);
    }

    return {
        init: init,
        dispose: dispose
    };
})();
