// dncModuleBridge.js — module-side half of the DotNetCloud module UI plane.
//
// Copy this file into your module host's wwwroot and load it from the page the core frames:
//
//   <script src="/dncModuleBridge.js"></script>
//   <script>window.dncModuleBridge.start({ title: 'My App' });</script>
//
// The core frames your UI with sandbox="allow-scripts allow-forms allow-popups" and NO
// allow-same-origin, so this document has an opaque origin. Consequences you must design for:
//
//   * `document.cookie`, `localStorage` and `sessionStorage` are unavailable. Keep state on your
//     server; it is reached through the same proxy path the document was loaded from.
//   * Every message you receive reports `event.origin === "null"`. The bridge therefore
//     authenticates the core by IDENTITY (`event.source === window.parent`) and by a
//     direction-scoped type allow-list. Do not add your own origin check — it cannot work.
//   * `window.top` is cross-origin. Use `dncModuleBridge.navigate()` instead of touching the
//     parent URL.
//
// Protocol (mirrors DotNetCloud.Core.Modules.Ui.ModuleAppProtocol):
//   host → frame : dnc:init, dnc:theme, dnc:consentResult
//   frame → host : dnc:ready, dnc:navigate, dnc:resize, dnc:title, dnc:openExternal,
//                  dnc:requestConsent, dnc:notify
(function () {
  'use strict';

  var TO_FRAME = ['dnc:init', 'dnc:theme', 'dnc:consentResult'];
  var MAX_MESSAGE_BYTES = 8 * 1024;
  var REPORT_DEBOUNCE_MS = 150;

  var state = {
    started: false,
    role: 'app',
    moduleId: '',
    theme: 'light',
    locale: 'en',
    title: '',
    listeners: { init: [], theme: [], consent: [] },
    started_at: 0,
    resizeTimer: null,
    disposed: false
  };

  function post(message) {
    if (state.disposed) {
      return;
    }
    try {
      // '*' is required: the parent is same-origin with this document's URL but this document's
      // own origin is opaque. The payload never contains secrets (theme, locale, title, a consent
      // result) and frame-ancestors 'self' means the core is the only possible ancestor.
      window.parent.postMessage(message, '*');
    } catch (e) {
      /* detached frame */
    }
  }

  function emit(name, payload) {
    var handlers = state.listeners[name] || [];
    for (var i = 0; i < handlers.length; i++) {
      try {
        handlers[i](payload);
      } catch (e) {
        /* a broken listener must not break the bridge */
      }
    }
  }

  function onMessage(event) {
    if (state.disposed) {
      return;
    }

    // Identity check: the only peer authentication available to an opaque-origin document.
    if (event.source !== window.parent) {
      return;
    }

    var data = event.data;
    if (!data || typeof data !== 'object' || typeof data.type !== 'string') {
      return;
    }

    if (TO_FRAME.indexOf(data.type) < 0) {
      return;
    }

    var serialized;
    try {
      serialized = JSON.stringify(data);
    } catch (e) {
      return;
    }

    if (!serialized || serialized.length > MAX_MESSAGE_BYTES) {
      return;
    }

    switch (data.type) {
      case 'dnc:init':
        state.role = data.role === 'widget' ? 'widget' : 'app';
        state.moduleId = typeof data.moduleId === 'string' ? data.moduleId : '';
        state.theme = data.theme === 'dark' ? 'dark' : 'light';
        state.locale = typeof data.locale === 'string' ? data.locale : 'en';
        state.title = typeof data.title === 'string' ? data.title : '';

        document.documentElement.setAttribute('data-theme', state.theme);
        document.documentElement.setAttribute('lang', state.locale);

        // Announce readiness only after the host handshake: the host uses this as its liveness
        // signal, so a thrown listener must not swallow it.
        post({ type: 'dnc:ready' });
        emit('init', { role: state.role, moduleId: state.moduleId, theme: state.theme, locale: state.locale });
        break;

      case 'dnc:theme':
        state.theme = data.theme === 'dark' ? 'dark' : 'light';
        state.locale = typeof data.locale === 'string' ? data.locale : state.locale;
        document.documentElement.setAttribute('data-theme', state.theme);
        document.documentElement.setAttribute('lang', state.locale);
        emit('theme', { theme: state.theme, locale: state.locale });
        break;

      case 'dnc:consentResult':
        emit('consent', { scope: String(data.scope || ''), granted: data.granted === true });
        break;
    }
  }

  function measure() {
    var body = document.body;
    var root = document.documentElement;
    if (!body || !root) {
      return 0;
    }
    return Math.max(
      body.scrollHeight,
      body.offsetHeight,
      root.scrollHeight,
      root.offsetHeight
    );
  }

  function reportResize() {
    if (state.role !== 'widget' || state.disposed) {
      return;
    }

    if (state.resizeTimer !== null) {
      window.clearTimeout(state.resizeTimer);
    }

    state.resizeTimer = window.setTimeout(function () {
      state.resizeTimer = null;
      post({ type: 'dnc:resize', height: measure() });
    }, REPORT_DEBOUNCE_MS);
  }

  var bridge = {
    /**
     * Starts the bridge. Call once, after the DOM is ready.
     * @param {object} [options] { title, role: 'app' | 'widget' }
     */
    start: function (options) {
      options = options || {};
      if (state.started) {
        return bridge;
      }

      state.started = true;
      state.started_at = Date.now();
      state.role = options.role === 'widget' ? 'widget' : 'app';
      state.title = typeof options.title === 'string' ? options.title : '';

      window.addEventListener('message', onMessage);

      if (state.role === 'widget' && typeof ResizeObserver === 'function') {
        try {
          new ResizeObserver(reportResize).observe(document.body);
        } catch (e) {
          window.addEventListener('resize', reportResize);
        }
      }

      post({ type: 'dnc:ready' });
      return bridge;
    },

    /** Registers a listener for 'init' | 'theme' | 'consent'. */
    on: function (name, handler) {
      if (Object.prototype.hasOwnProperty.call(state.listeners, name) && typeof handler === 'function') {
        state.listeners[name].push(handler);
      }
      return bridge;
    },

    /** Asks the core to navigate within this module (module-relative path). */
    navigate: function (path) {
      post({ type: 'dnc:navigate', path: String(path || '/') });
      return bridge;
    },

    /** Asks the core to open an https: URL in a new tab. */
    openExternal: function (url) {
      post({ type: 'dnc:openExternal', url: String(url || '') });
      return bridge;
    },

    /** Asks the core to render its consent prompt for the given scopes. */
    requestConsent: function (scopes) {
      post({ type: 'dnc:requestConsent', scopes: Array.isArray(scopes) ? scopes.slice(0, 32) : [] });
      return bridge;
    },

    /** Shows a short toast in the core shell. The core rate-limits these to 5 per minute. */
    notify: function (message) {
      post({ type: 'dnc:notify', message: String(message || '') });
      return bridge;
    },

    /** Sets the page title shown in the core shell. */
    setTitle: function (title) {
      post({ type: 'dnc:title', title: String(title || '') });
      return bridge;
    },

    /** Re-measures and reports the document height (widget role only). */
    reportHeight: reportResize,

    /** Current theme handed down by the core ('light' | 'dark'). */
    getTheme: function () {
      return state.theme;
    },

    /** Stops the bridge and removes its listeners. */
    dispose: function () {
      state.disposed = true;
      window.removeEventListener('message', onMessage);
      window.removeEventListener('resize', reportResize);
      if (state.resizeTimer !== null) {
        window.clearTimeout(state.resizeTimer);
        state.resizeTimer = null;
      }
    }
  };

  window.dncModuleBridge = bridge;
})();
