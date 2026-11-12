/*
 * C#-facing wrapper over the SDK frame bridge (dncModuleBridge.js), shared by every module UI.
 *
 * The bridge MUST be started before Blazor runs: it patches window.fetch so every request the module
 * UI makes carries the frame ticket, which is how the core proxy authenticates a sandboxed frame
 * (opaque origin => no session cookie). Starting it later would let the first API call escape
 * unauthenticated, so this file starts it as soon as it is loaded, using the document's own title and
 * the `app` role.
 */
(function () {
  "use strict";

  var activeStream = null;

  function bridge() {
    return window.dncModuleBridge;
  }

  var api = {
    start: function (options) {
      var b = bridge();
      if (!b) {
        return false;
      }

      b.start({
        title: (options && options.title) || document.title || "Module",
        role: (options && options.role) || "app",
      });
      return true;
    },

    onTheme: function (dotNetRef) {
      var b = bridge();
      if (!b) {
        return;
      }

      b.on("theme", function (payload) {
        dotNetRef.invokeMethodAsync(
          "OnThemeChanged",
          payload && payload.theme === "dark" ? "dark" : "light",
          (payload && payload.locale) || "",
        );
      });
    },

    mediaUrl: function (url) {
      return bridge().mediaUrl(url);
    },

    navigate: function (path) {
      bridge().navigate(path);
    },

    setTitle: function (title) {
      bridge().setTitle(title);
    },

    notify: function (message) {
      bridge().notify(message);
    },

    openExternal: function (url) {
      bridge().openExternal(url);
    },

    requestConsent: function (scopes) {
      bridge().requestConsent(scopes || []);
    },

    scrollToBottom: function (elementId) {
      var element = document.getElementById(elementId);
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    },

    /**
     * Copies plain text to the clipboard. Returns true on success so the caller can report a failure
     * inline instead of silently claiming success.
     */
    copyText: function (text) {
      var value = String(text == null ? "" : text);

      if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
        return navigator.clipboard.writeText(value).then(
          function () {
            return true;
          },
          function () {
            return legacyCopy(value);
          },
        );
      }

      return Promise.resolve(legacyCopy(value));
    },

    /**
     * Copies a rendered element's content as rich text, so headings, lists and code blocks survive.
     * Falls back to the element's plain text when the browser refuses the selection copy.
     */
    copyRendered: function (elementId) {
      var element = document.getElementById(elementId);
      if (!element) {
        return false;
      }

      var selection = window.getSelection ? window.getSelection() : null;
      if (!selection) {
        return legacyCopy(element.innerText || "");
      }

      var range = document.createRange();
      range.selectNodeContents(element);
      selection.removeAllRanges();
      selection.addRange(range);

      var copied = false;
      try {
        copied = document.execCommand("copy");
      } catch (e) {
        copied = false;
      }
      selection.removeAllRanges();

      if (!copied) {
        return legacyCopy(element.innerText || "");
      }

      return true;
    },

    /**
     * POSTs a JSON body and pumps a Server-Sent-Events response, forwarding each `data:` payload to
     * .NET one at a time. HttpClient cannot surface a streaming body in WebAssembly, so the pump lives
     * here; the request itself is still authenticated by the bridge's fetch patch.
     */
    stream: function (url, body, dotNetRef) {
      api.stopStream();

      var controller = new AbortController();
      activeStream = controller;

      fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          Accept: "text/event-stream",
        },
        body: JSON.stringify(body || {}),
        signal: controller.signal,
      })
        .then(function (response) {
          if (!response.ok || !response.body) {
            dotNetRef.invokeMethodAsync("OnStreamFailed", response.status);
            return null;
          }

          var reader = response.body.getReader();
          var decoder = new TextDecoder();
          var buffer = "";

          function pump() {
            return reader.read().then(function (result) {
              if (result.done) {
                return;
              }

              buffer += decoder.decode(result.value, { stream: true });

              var boundary;
              while ((boundary = buffer.indexOf("\n\n")) >= 0) {
                var rawEvent = buffer.slice(0, boundary);
                buffer = buffer.slice(boundary + 2);

                var dataLine = null;
                rawEvent.split("\n").forEach(function (line) {
                  var trimmed = line.trim();
                  if (trimmed.indexOf("data:") === 0) {
                    dataLine = trimmed.slice(5).trim();
                  }
                });

                if (dataLine) {
                  dotNetRef.invokeMethodAsync("OnStreamEvent", dataLine);
                }
              }

              return pump();
            });
          }

          return pump();
        })
        .catch(function (error) {
          if (error && error.name === "AbortError") {
            return;
          }

          dotNetRef.invokeMethodAsync("OnStreamFailed", 0);
        })
        .then(function () {
          // An aborted stream clears activeStream first, so its completion is not reported.
          if (activeStream !== controller) {
            return;
          }

          activeStream = null;
          dotNetRef.invokeMethodAsync("OnStreamCompleted");
        });
    },

    /**
     * Routes Enter (without Shift) in a composer textarea to a .NET handler and cancels the browser's
     * newline. Shift+Enter keeps its default, so multi-line messages still work; the handler cannot be
     * expressed in markup because `@onkeydown:preventDefault` would cancel every key, including typing.
     */
    bindEnterToSend: function (elementId, dotNetRef) {
      var element = document.getElementById(elementId);
      if (!element) {
        return false;
      }

      element.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" || event.shiftKey || event.isComposing) {
          return;
        }

        event.preventDefault();
        dotNetRef.invokeMethodAsync("OnComposerEnter");
      });

      return true;
    },

    stopStream: function () {
      if (!activeStream) {
        return;
      }

      var controller = activeStream;
      activeStream = null;

      try {
        controller.abort();
      } catch (e) {
        /* already finished */
      }
    },
  };

  function legacyCopy(text) {
    var area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "readonly");
    area.style.position = "fixed";
    area.style.top = "-1000px";
    document.body.appendChild(area);
    area.select();

    var copied = false;
    try {
      copied = document.execCommand("copy");
    } catch (e) {
      copied = false;
    }

    document.body.removeChild(area);
    return copied;
  }

  window.dncModuleFrame = api;
  api.start({ title: document.title, role: "app" });
})();
