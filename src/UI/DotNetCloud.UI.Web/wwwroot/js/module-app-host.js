// Module UI plane host bridge (phase A5).
//
// The shell renders a module's own UI inside a sandboxed iframe WITHOUT `allow-same-origin`, so the
// frame has an opaque origin and every message it sends reports `event.origin === "null"`. Origin
// checks are therefore useless: the peer is authenticated by identity (`event.source` must be the
// exact frame this bridge was mounted on) plus a direction-scoped type allow-list.
//
// Mirrors DotNetCloud.Core.Modules.Ui.ModuleAppProtocol. A test asserts the two stay in sync.
(function () {
  'use strict';

  var FROM_FRAME = [
    'dnc:ready',
    'dnc:navigate',
    'dnc:resize',
    'dnc:title',
    'dnc:openExternal',
    'dnc:requestConsent',
    'dnc:notify'
  ];

  var MAX_MESSAGE_BYTES = 8 * 1024;
  var MAX_TITLE = 200;
  var MAX_NOTIFY_BODY = 500;
  var MAX_SCOPES = 32;
  var MIN_HEIGHT = 200;
  var MAX_HEIGHT = 20000;
  var NOTIFY_PER_MINUTE = 5;

  // How often the host repeats dnc:init while waiting for the frame's dnc:ready. Both sides announce
  // until the other answers, so this only has to be short enough to feel instant.
  var INIT_RETRY_MS = 400;

  // The shell applies the user's theme with the `dark-mode` class (see theme.js), not a data-theme
  // attribute. Reading only data-theme left every module frame stuck in light mode. data-theme stays
  // as a fallback for a host that sets the attribute instead.
  function currentTheme() {
    if (
      document.documentElement.classList.contains('dark-mode') ||
      (document.body && document.body.classList.contains('dark-mode'))
    ) {
      return 'dark';
    }

    var theme = document.documentElement.getAttribute('data-theme');
    return theme === 'dark' ? 'dark' : 'light';
  }

  function currentLocale() {
    return document.documentElement.getAttribute('lang') || 'en';
  }

  function clampHeight(value) {
    var height = Number(value);
    if (!isFinite(height)) {
      return MIN_HEIGHT;
    }
    return Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, Math.round(height)));
  }

  window.dotnetcloudModuleApp = {
    /**
     * Mounts a host bridge on an iframe element.
     * @param {HTMLIFrameElement} frame the sandboxed module frame
     * @param {object} dotNetRef DotNetObjectReference to the host component
     * @param {object} options { moduleId, title, role: 'app' | 'widget', handshakeTimeoutMs }
     * @returns {object} handle with refresh/navigate/openExternal/sendConsentResult/dispose
     */
    mount: function (frame, dotNetRef, options) {
      if (!frame || !dotNetRef) {
        return { dispose: function () { } };
      }

      options = options || {};
      var role = options.role === 'widget' ? 'widget' : 'app';
      var notifyTimestamps = [];
      var disposed = false;
      var handshakeTimer = null;
      var initTimer = null;
      var ready = false;

      function post(message) {
        try {
          if (frame.contentWindow) {
            // targetOrigin must be '*' for an opaque-origin frame. The payload carries no
            // secrets (theme, locale, title and a consent result), and frame-ancestors 'self'
            // means the only possible ancestor is the core itself.
            frame.contentWindow.postMessage(message, '*');
          }
        } catch (e) {
          // Frame torn down between render and post; nothing to do.
        }
      }

      function sendInit() {
        post({
          type: 'dnc:init',
          role: role,
          moduleId: options.moduleId || '',
          theme: currentTheme(),
          locale: currentLocale(),
          title: options.title || ''
        });
      }

      function clearHandshake() {
        if (handshakeTimer !== null) {
          window.clearTimeout(handshakeTimer);
          handshakeTimer = null;
        }
        stopInitRetries();
      }

      function stopInitRetries() {
        if (initTimer !== null) {
          window.clearInterval(initTimer);
          initTimer = null;
        }
      }

      // mount() and the frame's own boot are two asynchronous races: the app can post its dnc:ready
      // before this listener exists (that message is dropped), and this dnc:init can arrive before the
      // bridge started (that one is dropped too). Whichever order they land in, one message is lost and
      // the shell would sit on "Starting ..." until it timed out. Repeating init until the frame
      // answers closes the window without needing to know which side was late.
      function beginHandshake() {
        sendInit();
        if (initTimer === null && !ready && !disposed) {
          initTimer = window.setInterval(function () {
            if (disposed || ready) {
              stopInitRetries();
              return;
            }
            sendInit();
          }, INIT_RETRY_MS);
        }
      }

      function onLoad() {
        if (disposed) {
          return;
        }
        beginHandshake();

        // Start the budget from the load event, not from mount(): a cold WebAssembly bundle can take
        // far longer than the timeout to fetch, and timing the document fetch as if it were the app's
        // boot is what made a healthy module look dead after every deploy.
        if (!ready) {
          startHandshakeTimer();
        }
      }

      function startHandshakeTimer() {
        clearHandshake();
        handshakeTimer = window.setTimeout(function () {
          handshakeTimer = null;
          if (!disposed && !ready) {
            dotNetRef.invokeMethodAsync('OnHandshakeTimeout');
          }
        }, options.handshakeTimeoutMs || 60000);
      }

      function onError() {
        if (!disposed) {
          dotNetRef.invokeMethodAsync('OnLoadError');
        }
      }

      function onMessage(event) {
        if (disposed) {
          return;
        }

        // Identity check — the only usable peer authentication for an opaque-origin frame. A re-render
        // can replace the element while the app inside is booting; adopt the live one instead of
        // dropping every message that app sends.
        if (event.source !== frame.contentWindow) {
          var liveFrame = document.querySelector('iframe.module-app-frame');
          if (!liveFrame || event.source !== liveFrame.contentWindow) {
            return;
          }
          frame = liveFrame;
        }

        var data = event.data;
        if (!data || typeof data !== 'object' || typeof data.type !== 'string') {
          return;
        }

        if (FROM_FRAME.indexOf(data.type) < 0) {
          return; // unknown / host-only type: drop it
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
          case 'dnc:ready':
            // The frame repeats this until it hears from the host, so most of these are duplicates.
            if (ready) {
              return;
            }
            ready = true;
            clearHandshake();
            dotNetRef.invokeMethodAsync('OnReady');
            break;

          case 'dnc:resize':
            dotNetRef.invokeMethodAsync('OnResize', clampHeight(data.height));
            break;

          case 'dnc:title':
            if (typeof data.title === 'string' && data.title.length > 0) {
              dotNetRef.invokeMethodAsync('OnTitle', data.title.slice(0, MAX_TITLE));
            }
            break;

          case 'dnc:navigate':
            if (typeof data.path === 'string') {
              dotNetRef.invokeMethodAsync('OnNavigate', data.path.slice(0, 2048));
            }
            break;

          case 'dnc:openExternal':
            if (typeof data.url === 'string') {
              // Re-validated in C#; the host calls back into openExternal below.
              dotNetRef.invokeMethodAsync('OnOpenExternal', data.url.slice(0, 2048));
            }
            break;

          case 'dnc:requestConsent':
            var scopes = Array.isArray(data.scopes)
              ? data.scopes.filter(function (s) { return typeof s === 'string' && s.length <= 128; }).slice(0, MAX_SCOPES)
              : [];
            dotNetRef.invokeMethodAsync('OnRequestConsent', scopes);
            break;

          case 'dnc:notify':
            var cutoff = Date.now() - 60000;
            notifyTimestamps = notifyTimestamps.filter(function (ts) { return ts > cutoff; });
            if (notifyTimestamps.length >= NOTIFY_PER_MINUTE) {
              return;
            }
            if (typeof data.message === 'string' && data.message.length > 0) {
              notifyTimestamps.push(Date.now());
              dotNetRef.invokeMethodAsync('OnNotify', data.message.slice(0, MAX_NOTIFY_BODY));
            }
            break;
        }
      }

      frame.addEventListener('load', onLoad);
      frame.addEventListener('error', onError);
      window.addEventListener('message', onMessage);

      // Push a theme/locale change into the frame as it happens. Nothing calls refresh() on a theme
      // toggle, so without this observer a module keeps the theme it loaded with until it is
      // remounted. The shell flips a class on <html>/<body> and may set lang on <html>.
      var lastTheme = currentTheme();
      var lastLocale = currentLocale();
      var themeObserver = null;

      function pushThemeIfChanged() {
        if (disposed) {
          return;
        }

        var theme = currentTheme();
        var locale = currentLocale();
        if (theme === lastTheme && locale === lastLocale) {
          return;
        }

        lastTheme = theme;
        lastLocale = locale;
        post({ type: 'dnc:theme', theme: theme, locale: locale });
      }

      if (typeof MutationObserver === 'function') {
        themeObserver = new MutationObserver(pushThemeIfChanged);
        themeObserver.observe(document.documentElement, {
          attributes: true,
          attributeFilter: ['class', 'data-theme', 'lang']
        });
        if (document.body) {
          themeObserver.observe(document.body, { attributes: true, attributeFilter: ['class'] });
        }
      }

      // A fast local module host can finish loading the frame before mount() runs. The load event
      // will then never fire again, and the frame's own dnc:ready was posted before this listener
      // existed — the shell would sit on "Starting …" forever. Sending dnc:init now makes the
      // bridge answer with dnc:ready; the load event (if it comes) repeats it harmlessly.
      onLoad();

      return {
        refresh: function () {
          lastTheme = currentTheme();
          lastLocale = currentLocale();
          post({ type: 'dnc:theme', theme: lastTheme, locale: lastLocale });
        },

        navigate: function (path) {
          if (typeof path === 'string' && path.length > 0 && path.charAt(0) === '/') {
            frame.src = path;
          }
        },

        openExternal: function (url) {
          var parsed;
          try {
            parsed = new URL(url);
          } catch (e) {
            return false;
          }

          if (parsed.protocol !== 'https:') {
            return false;
          }

          window.open(parsed.href, '_blank', 'noopener,noreferrer');
          return true;
        },

        sendConsentResult: function (scope, granted) {
          post({ type: 'dnc:consentResult', scope: String(scope), granted: granted === true });
        },

        dispose: function () {
          disposed = true;
          clearHandshake();
          if (themeObserver !== null) {
            themeObserver.disconnect();
            themeObserver = null;
          }
          frame.removeEventListener('load', onLoad);
          frame.removeEventListener('error', onError);
          window.removeEventListener('message', onMessage);
        }
      };
    }
  };
})();
