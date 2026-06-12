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
   * Attaches video playback with progress overlay support.
   *
   * When called with 2 args (elementId, streamUrl): legacy mode, plays immediately.
   * When called with 4 args (elementId, streamUrl, videoId, dotNetRef):
   *   shows progress overlay, triggers server-side stream preparation,
   *   polls for readiness, then plays when ready.
   *
   * @param {string} elementId - The video element ID.
   * @param {string} streamUrl - The stream endpoint URL.
   * @param {string=} videoId - Optional video GUID for progress polling.
   * @param {object=} dotNetRef - Optional .NET reference for callbacks.
   */
  videoPlayer.attachHlsPlayer = function (elementId, streamUrl, videoId, dotNetRef) {
    var video = document.getElementById(elementId);
    if (!video) {
      // DOM may not have rendered yet (race with Blazor render) — retry once
      if (videoPlayer._attachRetry) return; // already retrying
      videoPlayer._attachRetry = true;
      setTimeout(function () {
        videoPlayer._attachRetry = false;
        videoPlayer.attachHlsPlayer(elementId, streamUrl, videoId, dotNetRef);
      }, 200);
      return;
    }

    // If videoId is provided, show progress overlay and poll for readiness
    if (videoId) {
      // Trigger the stream pipeline first
      video.preload = "auto";

      // Show progress overlay (starts polling immediately)
      videoPlayer.showStreamProgress("player-container", videoId)
        .then(function () {
          // Server says stream is ready
          playStream(video, streamUrl, dotNetRef);
        })
        .catch(function (err) {
          console.error("DotNetCloud Video: Stream preparation failed", err);
        });

      // Trigger GET request after a short delay (lets progress poll start first)
      setTimeout(function () {
        video.src = streamUrl;
      }, 100);
      return;
    }

    // Legacy mode: no progress polling, play immediately
    playStream(video, streamUrl, dotNetRef);

    function playStream(video, streamUrl, dotNetRef) {
      // Native HLS (Safari)
      if (
        video.canPlayType &&
        video.canPlayType("application/vnd.apple.mpegurl")
      ) {
        video.src = streamUrl;
        video.play().catch(function () {});
        return;
      }

      // Try HEAD to detect strategy
      fetch(streamUrl, { method: "HEAD" })
        .then(function (resp) {
          var contentType = resp.headers.get("Content-Type") || "";
          var strategy = resp.headers.get("X-Stream-Strategy") || "";

          if (dotNetRef && strategy) {
            dotNetRef.invokeMethodAsync("OnStreamStrategy", strategy).catch(function() {});
          }

          // Direct play or remuxed MP4 — native video
          if (
            contentType.indexOf("video/mp4") !== -1 ||
            strategy === "direct" ||
            strategy === "remux"
          ) {
            video.play().catch(function () {});
            return;
          }

          // HLS
          useHls(video, streamUrl);
        })
        .catch(function () {
          // HEAD failed — try HLS.js
          if (typeof Hls !== "undefined" && Hls.isSupported()) {
            useHls(video, streamUrl);
          } else {
            video.play().catch(function () {});
          }
        });
    }

    function useHls(video, streamUrl) {
      if (typeof Hls === "undefined" || !Hls.isSupported()) {
        video.play().catch(function () {});
        return;
      }
      if (!Hls.DefaultConfig._dncConfigured) {
        Hls.DefaultConfig.lowLatencyMode = false;
        Hls.DefaultConfig.backBufferLength = Infinity;
        Hls.DefaultConfig._dncConfigured = true;
      }
      var hls = new Hls({ manifestLoadingTimeOut: 20000 });
      hls.loadSource(streamUrl);
      hls.attachMedia(video);
      hls.on(Hls.Events.MANIFEST_PARSED, function () {
        video.play().catch(function () {});
      });
      hls.on(Hls.Events.ERROR, function (event, data) {
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              hls.startLoad();
              break;
            case Hls.ErrorTypes.MEDIA_ERROR:
              hls.recoverMediaError();
              break;
            default:
              hls.destroy();
              break;
          }
        }
      });
      video._hls = hls;
      videoPlayer._hls = hls;
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

  /**
   * Shows a loading overlay with a progress bar, polling the server for stream
   * preparation progress. Resolves when the stream is ready (stage=streaming).
   *
   * @param {string} containerId - The player container element ID.
   * @param {string} videoId - The video GUID (e.g. "a1b2c3d4-...").
   * @returns {Promise} Resolves when ready, rejects on failure.
   */
  videoPlayer.showStreamProgress = function (containerId, videoId) {
    var container = document.getElementById(containerId);
    if (!container) return Promise.reject(new Error("Container not found"));

    // Find or create the progress overlay
    var overlay = container.querySelector(".dnc-stream-progress");
    if (!overlay) {
      overlay = document.createElement("div");
      overlay.className = "dnc-stream-progress";
      overlay.innerHTML =
        '<div class="dnc-stream-progress-inner">' +
        '<div class="dnc-stream-spinner"></div>' +
        '<div class="dnc-stream-message">Assembling video file…</div>' +
        '<div class="dnc-stream-bar-track">' +
        '<div class="dnc-stream-bar-fill"></div>' +
        "</div>" +
        "</div>";
      container.appendChild(overlay);
    }

    var messageEl = overlay.querySelector(".dnc-stream-message");
    var barFill = overlay.querySelector(".dnc-stream-bar-fill");
    var cancelled = false;

    // Store cancellation handle
    overlay._cancel = function () {
      cancelled = true;
    };

    return new Promise(function (resolve, reject) {
      var poll = function () {
        if (cancelled) {
          overlay.remove();
          reject(new Error("Cancelled"));
          return;
        }

        fetch("/api/v1/videos/" + videoId + "/stream-progress")
          .then(function (resp) {
            if (!resp.ok) throw new Error("HTTP " + resp.status);
            return resp.json();
          })
          .then(function (data) {
            var d = data.data || data;
            var stage = d.stage || "unknown";
            var message = d.message || "";
            var percent = d.percent || 0;

            if (messageEl) messageEl.textContent = message;
            if (barFill) barFill.style.width = Math.min(100, percent) + "%";

            if (stage === "streaming") {
              // Remove overlay after a brief moment so the player is visible
              setTimeout(function () {
                if (overlay.parentNode) overlay.remove();
              }, 300);
              resolve();
            } else if (stage === "failed") {
              if (messageEl)
                messageEl.textContent = "Failed: " + (message || "Unknown error");
              if (barFill) barFill.style.background = "#e74c3c";
              reject(new Error(message || "Stream preparation failed"));
            } else {
              // Still preparing — poll again
              setTimeout(poll, 500);
            }
          })
          .catch(function (err) {
            if (cancelled) return;
            // Network error — retry a few times then give up
            if (messageEl)
              messageEl.textContent = "Connecting to server…";
            setTimeout(poll, 1000);
          });
      };

      poll();
    });
  };

  /**
   * Cancels the stream progress overlay for a container.
   */
  videoPlayer.cancelStreamProgress = function (containerId) {
    var container = document.getElementById(containerId);
    if (!container) return;
    var overlay = container.querySelector(".dnc-stream-progress");
    if (overlay && overlay._cancel) {
      overlay._cancel();
    }
  };
})();
