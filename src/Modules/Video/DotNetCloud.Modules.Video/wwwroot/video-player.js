/**
 * DotNetCloud Video Player — plain script (not ES module).
 * All functions are attached to window.DotNetCloudVideo.
 *
 * Load order: 1) hls.min.js  2) video-player.js
 */
(function () {
  "use strict";

  var videoPlayer = window.DotNetCloudVideo || {};
  window.DotNetCloudVideo = videoPlayer;

  /** @type {Object} Stored idle auto-hide handles keyed by containerId. */
  var idleHandles = {};

  /** @type {Object} Stored keyboard shortcut handles keyed by elementId. */
  var kbHandles = {};

  /**
   * Attaches an error listener to the <video> element and calls back to .NET
   * when the browser cannot play the media.
   */
  videoPlayer.attachVideoErrorListener = function (elementId, dotNetRef) {
    var video = document.getElementById(elementId);
    if (!video) return;

    video.addEventListener(
      "error",
      function () {
        var err = video.error;
        if (!err) return;

        var httpStatus = null;
        var httpStatusText = "";
        try {
          fetch(video.src, { method: "GET" })
            .then(function (resp) {
              httpStatus = resp.status;
              // Try to read the response body for server error details
              return resp
                .text()
                .then(function (body) {
                  httpStatusText = body || resp.statusText || "";
                  // Truncate long bodies
                  if (httpStatusText.length > 500)
                    httpStatusText = httpStatusText.substring(0, 500) + "...";
                  dotNetRef.invokeMethodAsync(
                    "OnVideoError",
                    err.code,
                    err.message || "",
                    httpStatus,
                    httpStatusText,
                  );
                })
                .catch(function () {
                  httpStatusText = resp.statusText || "";
                  dotNetRef.invokeMethodAsync(
                    "OnVideoError",
                    err.code,
                    err.message || "",
                    httpStatus,
                    httpStatusText,
                  );
                });
            })
            .catch(function (fetchErr) {
              dotNetRef.invokeMethodAsync(
                "OnVideoError",
                err.code,
                err.message || "",
                0,
                fetchErr.message || "fetch failed",
              );
            });
          return;
        } catch (e) {
          /* ignore */
        }
        dotNetRef.invokeMethodAsync(
          "OnVideoError",
          err.code,
          err.message || "",
          null,
          null,
        );
      },
      { once: true },
    );

    // Detect missing audio after playback starts
    video.addEventListener(
      "playing",
      function () {
        var hasAudio = true;
        if (typeof video.mozHasAudio === "boolean") {
          hasAudio = video.mozHasAudio;
        } else if (video.audioTracks && video.audioTracks.length === 0) {
          hasAudio = false;
        }
        if (!hasAudio) {
          dotNetRef.invokeMethodAsync("OnNoAudio");
        }
      },
      { once: true },
    );
  };

  /**
   * Auto-hides cursor after mouse inactivity.
   */
  videoPlayer.attachIdleAutoHide = function (containerId, idleMs) {
    var container = document.getElementById(containerId);
    if (!container) return;

    idleMs = idleMs || 3000;
    var timer = null;

    function showCursor() {
      container.classList.remove("idle-hide");
      clearTimeout(timer);
      timer = setTimeout(hideCursor, idleMs);
    }
    function hideCursor() {
      container.classList.add("idle-hide");
    }

    container.addEventListener("mousemove", showCursor);
    container.addEventListener("mousedown", showCursor);
    timer = setTimeout(hideCursor, idleMs);

    idleHandles[containerId] = {
      dispose: function () {
        clearTimeout(timer);
        container.removeEventListener("mousemove", showCursor);
        container.removeEventListener("mousedown", showCursor);
        container.classList.remove("idle-hide");
      },
    };
  };

  videoPlayer.disposeIdleAutoHide = function (containerId) {
    var h = idleHandles[containerId];
    if (h) {
      h.dispose();
      delete idleHandles[containerId];
    }
  };

  /**
   * Global keydown: Space = play/pause toggle.
   */
  videoPlayer.attachKeyboardShortcuts = function (elementId) {
    var video = document.getElementById(elementId);
    if (!video) return;

    function onKeyDown(e) {
      if (e.code !== "Space") return;
      var tag = (e.target.tagName || "").toLowerCase();
      if (
        tag === "input" ||
        tag === "textarea" ||
        tag === "select" ||
        e.target.isContentEditable
      )
        return;
      e.preventDefault();
      if (video.paused) {
        video.play();
      } else {
        video.pause();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    kbHandles[elementId] = {
      dispose: function () {
        document.removeEventListener("keydown", onKeyDown);
      },
    };
  };

  videoPlayer.disposeKeyboardShortcuts = function (elementId) {
    var h = kbHandles[elementId];
    if (h) {
      h.dispose();
      delete kbHandles[elementId];
    }
  };

  /**
   * Attaches HLS playback to a video element.
   * Safari (native HLS): sets src directly.
   * Other browsers: uses hls.js.
   */
  videoPlayer.attachHlsPlayer = function (elementId, streamUrl) {
    var video = document.getElementById(elementId);
    if (!video) return;

    // Native HLS (Safari)
    if (
      video.canPlayType &&
      video.canPlayType("application/vnd.apple.mpegurl")
    ) {
      video.src = streamUrl;
      video.play().catch(function () {});
      return;
    }

    // hls.js for Chrome/Firefox/Edge
    if (typeof Hls !== "undefined" && Hls.isSupported()) {
      var hls = new Hls({ enableWorker: true, lowLatencyMode: false });
      hls.loadSource(streamUrl);
      hls.attachMedia(video);

      hls.on(Hls.Events.MANIFEST_PARSED, function () {
        video.play().catch(function () {});
      });

      hls.on(Hls.Events.ERROR, function (event, data) {
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              console.error("HLS: Fatal network error", data);
              hls.startLoad();
              break;
            case Hls.ErrorTypes.MEDIA_ERROR:
              console.error("HLS: Fatal media error", data);
              hls.recoverMediaError();
              break;
            default:
              console.error("HLS: Fatal error", data);
              hls.destroy();
              break;
          }
        }
      });

      video._hls = hls;
      videoPlayer._hls = hls;
    } else {
      console.warn("HLS.js not available, setting src directly");
      video.src = streamUrl;
      video.play().catch(function () {});
    }
  };

  /**
   * Destroys the HLS instance on a video element.
   */
  videoPlayer.destroyHlsPlayer = function (elementId) {
    var video = document.getElementById(elementId);
    if (!video) return;
    if (video._hls) {
      video._hls.destroy();
      delete video._hls;
    }
    if (videoPlayer._hls) {
      delete videoPlayer._hls;
    }
  };
})();
