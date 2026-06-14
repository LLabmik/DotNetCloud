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
  videoPlayer.attachHlsPlayer = function (
    elementId,
    streamUrl,
    videoId,
    dotNetRef,
  ) {
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
      videoPlayer
        .showStreamProgress("player-stream-progress-area", videoId)
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
            dotNetRef
              .invokeMethodAsync("OnStreamStrategy", strategy)
              .catch(function () {});
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
  /**
   * Uses Blazor-rendered inline elements in #player-stream-progress-area.
   * Polls /api/v1/videos/{videoId}/stream-progress.
   * Resolves when stage=streaming, rejects on stage=failed.
   */
  videoPlayer.showStreamProgress = function (ignored, videoId) {
    // Find the player container and insert progress bar directly after it
    var playerContainer = document.getElementById("player-container");
    if (!playerContainer)
      return Promise.reject(new Error("Player container not found"));

    // Remove any existing progress bar
    var existing = document.getElementById("dnc-progress-bar");
    if (existing) existing.remove();

    // Create progress bar after the player container
    var bar = document.createElement("div");
    bar.id = "dnc-progress-bar";
    bar.innerHTML =
      '<div style="background:#0f172a;border-top:1px solid rgba(255,255,255,0.08);min-height:44px;display:flex;align-items:center;padding:6px 24px;font-size:13px;color:rgba(255,255,255,0.7);gap:10px;">' +
      '<div class="dnc-spinner" style="width:16px;height:16px;border:2px solid rgba(255,255,255,0.2);border-top-color:#3b82f6;border-radius:50%;flex-shrink:0;animation:dncSpin 0.8s linear infinite;"></div>' +
      '<span class="dnc-msg" style="white-space:nowrap;">Assembling video file\u2026</span>' +
      '<div style="flex:0 1 140px;height:4px;min-width:60px;background:rgba(255,255,255,0.12);border-radius:2px;overflow:hidden;">' +
      '<div class="dnc-fill" style="height:100%;background:#3b82f6;border-radius:2px;width:0%;transition:width .3s ease;"></div>' +
      "</div></div>";
    playerContainer.insertAdjacentElement("afterend", bar);
    bar._videoId = videoId;
    playerContainer._videoId = videoId;

    // Add keyframes for spinner if not already present
    if (!document.getElementById("dnc-spin-keyframes")) {
      var style = document.createElement("style");
      style.id = "dnc-spin-keyframes";
      style.textContent = "@keyframes dncSpin{to{transform:rotate(360deg)}}";
      document.head.appendChild(style);
    }

    var messageEl = bar.querySelector(".dnc-msg");
    var barFill = bar.querySelector(".dnc-fill");
    var cancelled = false;

    bar._cancel = function () {
      cancelled = true;
    };

    return new Promise(function (resolve, reject) {
      var poll = function () {
        if (cancelled) {
          bar.remove();
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
              setTimeout(function () {
                bar.remove();
              }, 300);
              resolve();
            } else if (stage === "failed") {
              if (messageEl)
                messageEl.textContent =
                  "Failed: " + (message || "Unknown error");
              if (barFill) {
                barFill.style.background = "#e74c3c";
                barFill.style.width = "100%";
              }
              reject(new Error(message || "Stream preparation failed"));
            } else {
              setTimeout(poll, 500);
            }
          })
          .catch(function (err) {
            if (cancelled) return;
            if (messageEl) messageEl.textContent = "Connecting to server\u2026";
            setTimeout(poll, 1000);
          });
      };

      poll();
    });
  };

  /**
   * Cancels the stream progress overlay for a container.
   */
  videoPlayer.cancelStreamProgress = function (videoId) {
    var bar = document.getElementById("dnc-progress-bar");
    var playerContainer = document.getElementById("player-container");
    var vid =
      videoId ||
      (bar && bar._videoId) ||
      (playerContainer && playerContainer._videoId);
    console.log(
      "DNC CANCEL: videoId=",
      videoId,
      "barVid=",
      bar && bar._videoId,
      "pcVid=",
      playerContainer && playerContainer._videoId,
      "final=",
      vid,
    );
    if (bar && bar._cancel) bar._cancel();
    if (bar) bar.remove();
    if (vid) {
      var url = "/api/v1/videos/cancel-stream/" + vid;
      console.log("DNC CANCEL: fetching", url);
      fetch(url, { method: "POST", credentials: "include" })
        .then(function (r) {
          console.log("DNC CANCEL: response", r.status);
        })
        .catch(function (e) {
          console.error("DNC CANCEL: fetch error", e);
        });
    } else {
      console.log("DNC CANCEL: no videoId, skipping DELETE");
    }
  };
})();
