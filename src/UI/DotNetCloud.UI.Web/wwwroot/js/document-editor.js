// Collabora document editor interop.
//
// Provides the native Fullscreen API toggle used by the editor header button in
// DocumentEditor.razor, plus a `fullscreenchange` bridge so the .NET component keeps its
// button (icon + tooltip) in sync when fullscreen is left some other way — the browser's
// Esc key, or a fullscreen request from inside the Collabora iframe.
//
// Usage from Blazor:
//   dotnetcloudDocumentEditor.register(elementRef, dotNetRef)  // once, after first render
//   dotnetcloudDocumentEditor.toggle()                         // header button
//   dotnetcloudDocumentEditor.dispose()                        // component disposal
window.dotnetcloudDocumentEditor =
  window.dotnetcloudDocumentEditor ||
  (function () {
    "use strict";

    // The editor container that goes fullscreen (NOT the Collabora iframe itself).
    var target = null;
    var dotNetRef = null;
    var listening = false;

    function getFullscreenElement() {
      return (
        document.fullscreenElement ||
        document.webkitFullscreenElement ||
        document.msFullscreenElement ||
        null
      );
    }

    // True only when *this* editor holds fullscreen. A Collabora-requested fullscreen on a
    // different element must not flip the header button's state.
    function isFullscreen() {
      return target !== null && getFullscreenElement() === target;
    }

    function notify() {
      if (!dotNetRef) {
        return;
      }
      dotNetRef
        .invokeMethodAsync("OnFullscreenChanged", isFullscreen())
        .catch(function () {
          /* component disposed or circuit gone — nothing left to update */
        });
    }

    function onFullscreenChange() {
      notify();
    }

    function startListening() {
      if (listening) {
        return;
      }
      document.addEventListener("fullscreenchange", onFullscreenChange);
      document.addEventListener("webkitfullscreenchange", onFullscreenChange);
      document.addEventListener("MSFullscreenChange", onFullscreenChange);
      listening = true;
    }

    function stopListening() {
      if (!listening) {
        return;
      }
      document.removeEventListener("fullscreenchange", onFullscreenChange);
      document.removeEventListener(
        "webkitfullscreenchange",
        onFullscreenChange,
      );
      document.removeEventListener("MSFullscreenChange", onFullscreenChange);
      listening = false;
    }

    // Keeps the browser promise-based API and the older callback-style ones behind one shim.
    // Returns false when the target is gone or the browser has no Fullscreen API at all.
    function requestOnElement(element, exiting) {
      var promise = null;

      if (exiting) {
        if (document.exitFullscreen) {
          promise = document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
          promise = document.webkitExitFullscreen();
        } else if (document.msExitFullscreen) {
          promise = document.msExitFullscreen();
        } else {
          return false;
        }
      } else if (element.requestFullscreen) {
        promise = element.requestFullscreen();
      } else if (element.webkitRequestFullscreen) {
        promise = element.webkitRequestFullscreen();
      } else if (element.msRequestFullscreen) {
        promise = element.msRequestFullscreen();
      } else {
        return false;
      }

      if (promise && typeof promise.catch === "function") {
        // A denied request leaves the button exactly as it was — the change event, not this
        // call, is what drives the component state.
        promise.catch(function () {});
      }
      return true;
    }

    /* element: the editor container element (or null to reuse the registered one). */
    function register(element, ref) {
      if (element instanceof HTMLElement) {
        target = element;
      }
      if (ref) {
        dotNetRef = ref;
      }
      startListening();

      // Report the current state so a re-registered editor starts in sync (for example when
      // the browser restored a previous fullscreen session).
      notify();
    }

    function getTarget() {
      if (target instanceof HTMLElement && document.body.contains(target)) {
        return target;
      }
      return null;
    }

    function enter() {
      var element = getTarget();
      if (!element) {
        return false;
      }
      return requestOnElement(element, false);
    }

    function exit() {
      return requestOnElement(null, true);
    }

    function toggle() {
      // Note: the actual transition is asynchronous — the component is updated from the
      // fullscreenchange event, not from this call's return value.
      if (isFullscreen()) {
        exit();
      } else {
        enter();
      }
    }

    function dispose() {
      stopListening();

      // Leave fullscreen only if this editor is the element that holds it, so closing one
      // editor never collapses an unrelated fullscreen element.
      if (isFullscreen()) {
        exit();
      }

      target = null;
      dotNetRef = null;
    }

    return {
      register: register,
      enter: enter,
      exit: exit,
      toggle: toggle,
      isFullscreen: isFullscreen,
      dispose: dispose,
    };
  })();
