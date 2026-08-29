/**
 * DotNetCloud Video Player — self-contained HTML5 player.
 *
 * Plain ES5 script (no modules, no build step), wrapped in an IIFE.
 * Exposes `window.DotNetCloudVideoPlayer`. Depends on `window.Hls`
 * (hls.min.js must be loaded first — see VideoPage.razor.cs).
 *
 * Blazor renders only a host container (`#video-player-root`); this script
 * owns ALL player DOM (video element, OSD, menus, overlays) so Blazor
 * re-renders can never re-apply `video.src` and restart playback.
 *
 * ⚠️ ICON EXCEPTION (documented): The OSD renders inline Material SVG paths
 * (see the ICONS map below) because the DOM is built in raw JS and cannot use
 * the Razor `MaterialIcon` component, and the app does not load the Material
 * Icons font (so ligature text would display literally as characters). Inline
 * SVG matches the app's no-font-dependency pattern. This is a deliberate,
 * documented exception to the "all icons via MaterialIcon component" rule.
 *
 * Public API:
 *   DotNetCloudVideoPlayer.init(config)   — build DOM + start playback
 *   DotNetCloudVideoPlayer.destroy()      — full teardown
 *   DotNetCloudVideoPlayer.setAudioStream(index) — switch audio track
 */
(function () {
  "use strict";

  var player = window.DotNetCloudVideoPlayer || {};
  window.DotNetCloudVideoPlayer = player;

  // ── Inline SVG icon paths (Material Design, 24x24 viewBox, fill=currentColor) ──
  // The app does NOT load the Material Icons font, so ligature text would render
  // as literal characters — inline SVG paths match the no-font-dependency pattern
  // used by the rest of the UI (the Razor MaterialIcon component).
  var ICONS = {
    play: "M8 5v14l11-7z",
    pause: "M6 19h4V5H6v14zm8-14v14h4V5h-4z",
    cc: "M19 4H5c-1.11 0-2 .9-2 2v12c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm-8 7H9.5v-.5h-2v3h2V13H11v1c0 .55-.45 1-1 1H7c-.55 0-1-.45-1-1v-4c0-.55.45-1 1-1h3c.55 0 1 .45 1 1v1zm7 0h-1.5v-.5h-2v3h2V13H18v1c0 .55-.45 1-1 1h-3c-.55 0-1-.45-1-1v-4c0-.55.45-1 1-1h3c.55 0 1 .45 1 1v1z",
    audio:
      "M12 3v9.28c-.47-.17-.97-.28-1.5-.28C8.01 12 6 14.01 6 16.5S8.01 21 10.5 21c2.31 0 4.2-1.75 4.45-4H15V6h4V3h-7z",
    rate: "M20.38 8.57l-1.23 1.85a8 8 0 0 1-.22 7.58H5.07A8 8 0 0 1 15.58 6.85l1.85-1.23A10 10 0 0 0 3.35 19a2 2 0 0 0 1.72 1h13.85a2 2 0 0 0 1.74-1 10 10 0 0 0-.27-10.44zm-9.79 6.84a2 2 0 0 0 2.83 0l5.66-8.49-8.49 5.66a2 2 0 0 0 0 2.83z",
    volumeUp:
      "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z",
    volumeOff:
      "M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z",
    fullscreen:
      "M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z",
    fullscreenExit:
      "M5 16h3v3h2v-5H5v2zm3-8H5v2h5V5H8v3zm6 11h2v-3h3v-2h-5v5zm2-11V5h-2v5h5V8h-3z",
    replay10:
      "M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8zm-1.1 11h-.85v-3.26l-1.01.31v-.69l1.77-.63h.09V16zm4.28-1.76c0 .32-.03.6-.1.82s-.17.42-.29.57-.28.26-.45.33-.37.1-.59.1-.41-.03-.59-.1-.33-.18-.46-.33-.23-.34-.3-.57-.11-.5-.11-.82v-.74c0-.32.03-.6.1-.82s.17-.42.29-.57.28-.26.45-.33.37-.1.59-.1.41.03.59.1.33.18.46.33.23.34.3.57.11.5.11.82v.74zm-.85-.86c0-.19-.01-.35-.04-.48s-.07-.23-.12-.31-.11-.14-.19-.17-.16-.05-.25-.05-.18.02-.25.05-.14.09-.19.17-.09.18-.12.31-.04.29-.04.48v.97c0 .19.01.35.04.48s.07.24.12.32.11.14.19.17.16.05.25.05.18-.02.25-.05.14-.09.19-.17.09-.19.12-.32.04-.48.04-.48v-.97z",
    forward10:
      "M18 13c0 3.31-2.69 6-6 6s-6-2.69-6-6 2.69-6 6-6v4l5-5-5-5v4c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8h-2zM10.9 16v-4.27c-.1.07-.22.13-.35.2-.13.07-.25.14-.36.2l-.02-.66 1.76-.63h.09V16h-.12zm4.28 0c-.32 0-.58-.03-.78-.1s-.38-.17-.51-.32-.23-.34-.3-.57-.1-.5-.1-.82v-.74c0-.32.03-.6.1-.82s.17-.42.29-.57.28-.26.45-.33.37-.1.59-.1.41.03.59.1.33.18.46.33.23.34.3.57.11.5.11.82v.74c0 .32-.03.6-.1.82s-.17.42-.29.57-.28.26-.45.33-.37.1-.59.1-.41-.03-.59-.1-.33-.18-.46-.33-.23-.34-.3-.57-.11-.5-.11-.82v-.74zm-.85.86c0 .19.01.35.04.48s.07.23.12.31.11.14.19.17.16.05.25.05.18-.02.25-.05.14-.09.19-.17.09-.18.12-.31.04-.29.04-.48v-.97c0-.19-.01-.35-.04-.48s-.07-.23-.12-.31-.11-.14-.19-.17-.16-.05-.25-.05-.18.02-.25.05-.14.09-.19.17-.09.19-.12.32-.04.48-.04.48v.97z",
    skipPrevious: "M6 6h2v12H6zm3.5 6l8.5 6V6z",
    skipNext: "M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z",
    pip: "M19 11h-8v6h8v-6zm4 8V4.98C23 3.88 22.1 3 21 3H3c-1.1 0-2 .88-2 1.98V19c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2zm-2 .02H3V4.97h18v14.05z",
  };

  /** Returns inline SVG markup for a Material icon path. */
  function iconSvg(name) {
    var d = ICONS[name] || "";
    return (
      '<svg class="dnc-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="' +
      d +
      '"/></svg>'
    );
  }

  /**
   * Normalizes the server-reported strategy to the player's canonical names.
   * GET /stream-progress returns the StreamingStrategy enum name ("DirectPlay",
   * "StreamCopy", "Transcode"), while the player branches on "direct"/"remux"/"transcode".
   */
  function normalizeStrategy(s) {
    s = String(s || "").toLowerCase();
    if (s === "streamcopy") return "remux";
    if (s === "directplay") return "direct";
    return s; // "transcode", or unknown (left as-is)
  }

  /**
   * Returns true when the strategy is played through hls.js (both full
   * transcodes and stream-copy remuxes are served as HLS after the remux
   * pipeline was converted from a progressive pipe to HLS segments).
   */
  function isHlsStrategy(s) {
    return s === "transcode" || s === "remux";
  }

  /** @type {Object|null} The single active player instance. */
  var instance = null;

  /**
   * Initializes the player inside `config.containerId`.
   * Any existing player is torn down first.
   */
  player.init = function (config) {
    player.destroy();
    instance = createPlayer(config);
    return !!instance;
  };

  /** Tears down the active player completely. */
  player.destroy = function () {
    if (instance) {
      instance.dispose();
      instance = null;
    }
  };

  /** Convenience: switch the audio track on the active player. */
  player.setAudioStream = function (index) {
    if (instance) instance.selectAudioStream(index);
  };

  /** Pauses the active player (used by the download button to free bandwidth). */
  player.pauseIfPlaying = function () {
    if (instance && instance._video && !instance._video.paused) {
      instance._video.pause();
    }
  };

  /** Triggers a file download via a temporary anchor element. */
  player.triggerDownload = function (url, filename) {
    var a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  /**
   * Best-effort server-side cancel of the active HLS transcode for a video.
   * Called when the player is destroyed or the page is being unloaded so a
   * background ffmpeg process isn't left running for a stream nobody is
   * watching anymore. Uses sendBeacon (which keeps the request alive during
   * unload) with a fetch keepalive fallback. Errors are swallowed — this is
   * best-effort; the server-side idle watchdog is the backstop.
   */
  function cancelServerStream(videoId) {
    if (!videoId) return;
    var url = "/api/v1/videos/cancel-stream/" + videoId;
    try {
      if (navigator.sendBeacon) {
        navigator.sendBeacon(url);
        return;
      }
      fetch(url, {
        method: "POST",
        credentials: "same-origin",
        keepalive: true,
      }).catch(function () {});
    } catch (e) {
      /* ignore */
    }
  }

  /**
   * Creates a new player instance and starts playback.
   */
  function createPlayer(config) {
    var container = document.getElementById(config.containerId);
    if (!container) {
      // Blazor may not have rendered the host yet — retry briefly.
      var attempts = 0;
      var timer = setInterval(function () {
        container = document.getElementById(config.containerId);
        if (container || ++attempts > 50) {
          clearInterval(timer);
          if (container) instance = buildPlayer(container, config);
        }
      }, 100);
      return {
        _deferred: true,
        dispose: function () {
          clearInterval(timer);
        },
      };
    }
    return buildPlayer(container, config);
  }

  /**
   * Builds the player DOM inside `container`, wires events, and starts
   * the stream pipeline.
   */
  function buildPlayer(container, config) {
    var p = {
      _container: container,
      _config: config,
      _dotNetRef: config.dotNetRef || null,
      _videoId: config.videoId,
      _title: config.title || "",
      _posterUrl: config.posterUrl || null,
      _streamBaseUrl: config.streamUrl,
      _durationSeconds: config.durationSeconds || 0,
      _resumeSeconds: config.resumeSeconds || 0,
      _subtitles: config.subtitles || [],
      _audioStreams: config.audioStreams || [],
      _defaultAudioIndex: config.defaultAudioIndex,
      _currentAudioIndex: config.defaultAudioIndex,
      _strategy: null, // "direct" | "remux" | "transcode"
      _seekStartOffset: 0, // absolute-time offset for remux reloads
      _hls: null, // active hls.js instance
      _video: null, // the <video> element
      _tracks: [], // { sub, track, url, textTrack } subtitle list
      _blobUrls: [], // subtitle blob URLs to revoke on destroy
      _osdTimer: null,
      _pollTimer: null,
      _disposed: false,
      _progressReportedAt: 0,
      _openMenu: null,
      _menu: null, // currently open popover menu
      _seeking: false,
      _scrubValue: null,
      _activeSubtitle: null,
      _seekOverlay: null,
      _durationFetching: false, // guards the one-time /stream-probe duration fetch
      _expectingHls: true, // suppress native m3u8 error until strategy is finalized
    };

    // ── Instance methods (assigned here so they close over `p`) ──
    p._duration = function () {
      if (p._durationSeconds > 0) return p._durationSeconds;
      if (p._video && isFinite(p._video.duration) && p._video.duration > 0)
        return p._video.duration;
      return 0;
    };

    p._absoluteTime = function () {
      return (p._video ? p._video.currentTime : 0) + p._seekStartOffset;
    };

    p.selectAudioStream = function (index) {
      selectAudioStream(p, index);
    };

    p._showFatalError = function (code, message) {
      hidePreparing(p);
      p._error.innerHTML = "";
      p._error.style.display = "flex";

      var card = document.createElement("div");
      card.className = "dnc-error-card";
      card.innerHTML =
        '<div class="dnc-error-icon">' +
        iconSvg("cc") +
        "</div>" +
        '<h4 class="dnc-error-title">Video Cannot Be Played</h4>' +
        '<p class="dnc-error-msg"></p>' +
        '<p class="dnc-error-hint">This can happen if the video uses an unsupported codec or the stream failed to load. ' +
        "Try refreshing the page, or use Chrome or Firefox for the widest format compatibility.</p>" +
        '<button type="button" class="dnc-error-close">Dismiss</button>';
      var msgEl = card.querySelector(".dnc-error-msg");
      msgEl.textContent = stringifyError(message) || "Unknown playback error";
      card
        .querySelector(".dnc-error-close")
        .addEventListener("click", function () {
          p._error.style.display = "none";
        });
      p._error.appendChild(card);

      if (p._dotNetRef) {
        p._dotNetRef
          .invokeMethodAsync("OnError", code, stringifyError(message) || "")
          .catch(function () {});
      }
    };

    p.dispose = function () {
      if (p._disposed) return;
      p._disposed = true;

      // Remove the page-unload handler first — it must not fire after the DOM
      // teardown below triggers a redundant cancel.
      if (p._pageHideHandler) {
        window.removeEventListener("pagehide", p._pageHideHandler);
        p._pageHideHandler = null;
      }

      clearTimeout(p._osdTimer);
      clearInterval(p._pollTimer);
      if (p._keyHandler) document.removeEventListener("keydown", p._keyHandler);

      // Remove seek document listeners
      if (p._seekHandlers) {
        document.removeEventListener("mousemove", p._seekHandlers.onMove);
        document.removeEventListener("mouseup", p._seekHandlers.onUp);
        document.removeEventListener("touchmove", p._seekHandlers.onMove);
        document.removeEventListener("touchend", p._seekHandlers.onUp);
      }

      // Revoke subtitle blob URLs
      for (var i = 0; i < p._blobUrls.length; i++) {
        try {
          URL.revokeObjectURL(p._blobUrls[i]);
        } catch (e) {
          /* ignore */
        }
      }
      p._blobUrls = [];

      teardownEngine(p);
      if (p._root && p._root.parentNode) {
        p._root.parentNode.removeChild(p._root);
      }
      p._container = null;

      // Stop the server-side transcode for this video (best effort) so a
      // background ffmpeg process isn't left running for an unwatched stream.
      cancelServerStream(p._videoId);
    };

    // ── DOM ──
    p._root = document.createElement("div");
    p._root.className = "dnc-player";

    p._video = document.createElement("video");
    p._video.id = "dnc-video";
    p._video.className = "dnc-video";
    p._video.setAttribute("playsinline", "");
    p._video.preload = "auto";
    if (p._posterUrl) p._video.poster = p._posterUrl;

    p._spinner = document.createElement("div");
    p._spinner.className = "dnc-spinner";
    p._spinner.style.display = "none";

    p._bigPlay = document.createElement("button");
    p._bigPlay.className = "dnc-big-play";
    p._bigPlay.type = "button";
    p._bigPlay.innerHTML = iconSvg("play");
    p._bigPlay.title = "Play";

    p._error = document.createElement("div");
    p._error.className = "dnc-error";
    p._error.style.display = "none";

    p._osd = document.createElement("div");
    p._osd.className = "dnc-osd";
    p._osd.innerHTML =
      '<div class="dnc-osd-title">' +
      escapeHtml(p._title) +
      "</div>" +
      buildSeekMarkup() +
      buildControlsMarkup(p);

    p._root.appendChild(p._video);
    p._root.appendChild(p._spinner);
    p._root.appendChild(p._bigPlay);
    p._root.appendChild(p._error);
    p._root.appendChild(p._osd);
    container.appendChild(p._root);

    // Wire controls
    wireControls(p);
    wireVideoEvents(p);
    wireOsdAutoHide(p);
    wireKeyboard(p);

    // If the page is unloaded (tab close / refresh) while this player is still
    // alive, cancel the server-side transcode — Blazor may not get a chance to
    // run DisposeAsync in that case. Removed again in p.dispose().
    p._pageHideHandler = function () {
      cancelServerStream(p._videoId);
    };
    window.addEventListener("pagehide", p._pageHideHandler);

    // Load subtitles (async, client-side SRT→VTT)
    loadSubtitles(p);

    // Start playback
    startPlayback(p);

    return p;
  }

  // ── Markup builders ─────────────────────────────────────────────

  function buildSeekMarkup() {
    return (
      '<div class="dnc-seek" id="dnc-seek">' +
      '<div class="dnc-seek-buffered" id="dnc-seek-buffered"></div>' +
      '<div class="dnc-seek-played" id="dnc-seek-played"></div>' +
      '<div class="dnc-seek-thumb" id="dnc-seek-thumb"></div>' +
      "</div>" +
      '<div class="dnc-time"><span id="dnc-time-start">0:00</span>' +
      '<span class="dnc-time-sep">/</span>' +
      '<span id="dnc-time-end">' +
      formatTime(0) +
      "</span></div>"
    );
  }

  function buildControlsMarkup(p) {
    var hasPrev = !!p._config.hasPrevious;
    var hasNext = !!p._config.hasNext;
    var hasCc = (p._subtitles || []).length > 0;
    var hasAudio = (p._audioStreams || []).length > 1;
    var html =
      '<div class="dnc-controls">' +
      '<button type="button" class="dnc-btn dnc-btn-play" data-action="playpause" title="Play / Pause (Space)">' +
      iconSvg("play") +
      "</button>" +
      '<button type="button" class="dnc-btn" data-action="back10" title="Back 10 seconds (J)">' +
      iconSvg("replay10") +
      "</button>" +
      '<button type="button" class="dnc-btn" data-action="fwd10" title="Forward 10 seconds (L)">' +
      iconSvg("forward10") +
      "</button>";
    if (hasPrev) {
      html +=
        '<button type="button" class="dnc-btn dnc-btn-nav" data-action="prev" title="Previous episode">' +
        iconSvg("skipPrevious") +
        "</button>";
    }
    if (hasNext) {
      html +=
        '<button type="button" class="dnc-btn dnc-btn-nav" data-action="next" title="Next episode">' +
        iconSvg("skipNext") +
        "</button>";
    }
    html +=
      '<span class="dnc-controls-spacer"></span>' +
      '<div class="dnc-volume">' +
      '<button type="button" class="dnc-btn" data-action="mute" title="Mute (M)">' +
      iconSvg("volumeUp") +
      "</button>" +
      '<input type="range" class="dnc-volume-slider" min="0" max="100" step="1" value="100" title="Volume" />' +
      "</div>";
    if (hasCc) {
      html +=
        '<div class="dnc-menu-wrap"><button type="button" class="dnc-btn dnc-btn-cc" data-menu="cc" title="Subtitles">' +
        iconSvg("cc") +
        "</button></div>";
    }
    if (hasAudio) {
      html +=
        '<div class="dnc-menu-wrap"><button type="button" class="dnc-btn dnc-btn-audio" data-menu="audio" title="Audio">' +
        iconSvg("audio") +
        "</button></div>";
    }
    html +=
      '<div class="dnc-menu-wrap"><button type="button" class="dnc-btn dnc-btn-rate" data-menu="rate" title="Playback speed">' +
      iconSvg("rate") +
      "</button></div>" +
      '<button type="button" class="dnc-btn" data-action="pip" title="Picture-in-picture">' +
      iconSvg("pip") +
      "</button>" +
      '<button type="button" class="dnc-btn" data-action="fullscreen" title="Fullscreen (F)">' +
      iconSvg("fullscreen") +
      "</button>" +
      "</div>";
    return html;
  }

  // ── Controls wiring ──────────────────────────────────────────────

  function wireControls(p) {
    var controls = p._osd.querySelector(".dnc-controls");
    controls.addEventListener("click", function (e) {
      var btn = closest(e.target, "button[data-action]");
      var menuBtn = closest(e.target, "button[data-menu]");
      if (menuBtn) {
        e.stopPropagation();
        toggleMenu(p, menuBtn);
        return;
      }
      if (!btn) return;
      e.stopPropagation();
      handleAction(p, btn.getAttribute("data-action"));
    });

    // Volume slider
    var vol = controls.querySelector(".dnc-volume-slider");
    vol.addEventListener("input", function () {
      var v = parseInt(vol.value, 10) / 100;
      p._video.volume = v;
      p._video.muted = v === 0;
      updateVolumeIcon(p, v);
    });
    vol.addEventListener("click", function (e) {
      e.stopPropagation();
    });

    // Seek bar (mousedown/move/up + touch)
    var seek = p._osd.querySelector("#dnc-seek");
    var dragging = false;
    function clientX(e) {
      return e.touches && e.touches[0] ? e.touches[0].clientX : e.clientX;
    }
    function updateScrub(x) {
      var rect = seek.getBoundingClientRect();
      var ratio = rect.width > 0 ? (x - rect.left) / rect.width : 0;
      ratio = Math.max(0, Math.min(1, ratio));
      p._scrubValue = ratio;
      var dur = p._duration();
      renderSeek(p, dur > 0 ? ratio * dur : 0);
    }
    function onDown(e) {
      e.preventDefault();
      dragging = true;
      p._seeking = true;
      updateScrub(clientX(e));
      showOsd(p);
    }
    function onMove(e) {
      if (!dragging) return;
      updateScrub(clientX(e));
    }
    function onUp(e) {
      if (!dragging) return;
      dragging = false;
      var dur = p._duration();
      var target = p._scrubValue !== null ? p._scrubValue * dur : 0;
      p._scrubValue = null;
      if (target > dur) target = dur;
      if (target < 0) target = 0;
      seekTo(p, target);
      p._seeking = false;
      showOsd(p);
    }
    seek.addEventListener("mousedown", onDown);
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
    seek.addEventListener("touchstart", onDown, { passive: false });
    document.addEventListener("touchmove", onMove, { passive: false });
    document.addEventListener("touchend", onUp);
    p._seekHandlers = { onMove: onMove, onUp: onUp };
  }

  function handleAction(p, action) {
    switch (action) {
      case "playpause":
        togglePlayPause(p);
        break;
      case "back10":
        seekBy(p, -10);
        break;
      case "fwd10":
        seekBy(p, 10);
        break;
      case "prev":
        if (p._dotNetRef)
          p._dotNetRef
            .invokeMethodAsync("OnNavigateEpisode", -1)
            .catch(function () {});
        break;
      case "next":
        if (p._dotNetRef)
          p._dotNetRef
            .invokeMethodAsync("OnNavigateEpisode", 1)
            .catch(function () {});
        break;
      case "mute":
        toggleMute(p);
        break;
      case "pip":
        togglePip(p);
        break;
      case "fullscreen":
        toggleFullscreen(p);
        break;
      default:
        break;
    }
  }

  // ── Menu (popover) handling ──────────────────────────────────────

  function toggleMenu(p, menuBtn) {
    var key = menuBtn.getAttribute("data-menu");
    closeMenu(p);
    if (p._openMenu === key) {
      p._openMenu = null;
      return;
    }
    p._openMenu = key;

    var wrap = menuBtn.parentNode;
    var menu = document.createElement("div");
    menu.className = "dnc-menu";

    if (key === "cc") buildCcMenu(p, menu);
    else if (key === "audio") buildAudioMenu(p, menu);
    else if (key === "rate") buildRateMenu(p, menu);

    wrap.appendChild(menu);
    p._menu = menu;
    // Click-away closes the menu
    setTimeout(function () {
      document.addEventListener("mousedown", closeMenuClick, true);
    }, 0);
    function closeMenuClick(ev) {
      if (!menu.contains(ev.target)) {
        closeMenu(p);
        document.removeEventListener("mousedown", closeMenuClick, true);
      }
    }
  }

  function closeMenu(p) {
    if (p._menu && p._menu.parentNode) {
      p._menu.parentNode.removeChild(p._menu);
    }
    p._menu = null;
    p._openMenu = null;
  }

  function buildCcMenu(p, menu) {
    menu.appendChild(
      menuItem(null, "Off", p._activeSubtitle == null, function () {
        setSubtitle(p, null);
        closeMenu(p);
      }),
    );
    for (var i = 0; i < p._tracks.length; i++) {
      (function (t) {
        menu.appendChild(
          menuItem(
            t.sub.id,
            t.sub.label || t.sub.language,
            p._activeSubtitle === t.sub.id,
            function () {
              setSubtitle(p, t.sub.id);
              closeMenu(p);
            },
          ),
        );
      })(p._tracks[i]);
    }
  }

  function buildAudioMenu(p, menu) {
    for (var i = 0; i < p._audioStreams.length; i++) {
      (function (s) {
        var label = s.title || s.language || "Track " + (s.index + 1);
        menu.appendChild(
          menuItem(
            s.index,
            label,
            p._currentAudioIndex === s.index,
            function () {
              selectAudioStream(p, s.index);
              closeMenu(p);
            },
          ),
        );
      })(p._audioStreams[i]);
    }
  }

  function buildRateMenu(p, menu) {
    var rates = [0.5, 0.75, 1, 1.25, 1.5, 2];
    for (var i = 0; i < rates.length; i++) {
      (function (r) {
        menu.appendChild(
          menuItem(
            r,
            r + "x",
            Math.abs(p._video.playbackRate - r) < 0.01,
            function () {
              p._video.playbackRate = r;
              closeMenu(p);
            },
          ),
        );
      })(rates[i]);
    }
  }

  function menuItem(value, label, isActive, onClick) {
    var item = document.createElement("button");
    item.type = "button";
    item.className = "dnc-menu-item" + (isActive ? " active" : "");
    item.textContent = label;
    item.addEventListener("click", function (e) {
      e.stopPropagation();
      onClick();
    });
    return item;
  }

  // ── Subtitles ────────────────────────────────────────────────────

  function loadSubtitles(p) {
    if (!p._subtitles || p._subtitles.length === 0) return;
    var loaded = 0;
    for (var i = 0; i < p._subtitles.length; i++) {
      (function (sub) {
        var url =
          "/api/v1/videos/" + p._videoId + "/subtitles/" + sub.id + "/content";
        fetch(url, { credentials: "same-origin" })
          .then(function (r) {
            if (!r.ok) throw new Error("HTTP " + r.status);
            return r.text();
          })
          .then(function (text) {
            var vtt = text;
            if (vtt.indexOf("WEBVTT") !== 0) {
              vtt = srtToVtt(vtt);
            }
            var blob = new Blob([vtt], { type: "text/vtt" });
            var blobUrl = URL.createObjectURL(blob);
            p._blobUrls.push(blobUrl);

            var track = document.createElement("track");
            track.kind = "subtitles";
            track.src = blobUrl;
            track.srclang = sub.language || "und";
            track.label = sub.label || sub.language || "Subtitles";
            track.default = !!sub.isDefault;
            p._video.appendChild(track);

            p._tracks.push({ sub: sub, track: track, url: blobUrl });

            loaded++;
            if (loaded === p._subtitles.length) {
              mapTextTracks(p);
              // Apply initial default (prefer config default subtitle)
              var def = null;
              for (var j = 0; j < p._subtitles.length; j++) {
                if (p._subtitles[j].isDefault) {
                  def = p._subtitles[j];
                  break;
                }
              }
              setSubtitle(p, def ? def.id : null, true);
            }
          })
          .catch(function () {
            /* subtitle load failure is non-fatal */
          });
      })(p._subtitles[i]);
    }
  }

  /** Maps each appended <track> to its textTracks index. */
  function mapTextTracks(p) {
    var tt = p._video.textTracks;
    for (var i = 0; i < p._tracks.length; i++) {
      var t = p._tracks[i];
      for (var j = 0; j < tt.length; j++) {
        if (tt[j].label === t.track.label) {
          t.textTrack = tt[j];
          break;
        }
      }
    }
  }

  function setSubtitle(p, subId, initial) {
    p._activeSubtitle = subId;
    var modes = p._video.textTracks;
    for (var i = 0; i < modes.length; i++) {
      var tt = modes[i];
      var isActive = false;
      for (var j = 0; j < p._tracks.length; j++) {
        if (p._tracks[j].textTrack === tt && p._tracks[j].sub.id === subId) {
          isActive = true;
          break;
        }
      }
      tt.mode = isActive ? "showing" : "disabled";
    }
    if (!initial) renderMenus(p);
  }

  function srtToVtt(srt) {
    var body = srt.replace(/(\d{2}:\d{2}:\d{2}),(\d{3})/g, "$1.$2");
    return "WEBVTT\n\n" + body;
  }

  // ── Playback / engine ────────────────────────────────────────────

  function startPlayback(p) {
    // Kick off the stream pipeline by loading the source, then poll the
    // progress endpoint to learn the strategy and finalize the engine.
    showPreparing(p);
    p._video.src = p._streamBaseUrl;
    p._video.load();
    pollProgressThen(p, function (strategy) {
      if (p._disposed) return;
      if (isHlsStrategy(strategy)) {
        p._strategy = strategy;
        replaceVideoElement(p);
        startHls(p, p._streamBaseUrl);
        p._expectingHls = false;
        reportStrategy(p, strategy);
      } else {
        p._strategy = strategy || "direct";
        p._expectingHls = false;
        reportStrategy(p, p._strategy);
        hidePreparing(p);
        playWithPromise(p);
      }
    });
  }

  /**
   * Polls GET /stream-progress until stage=streaming (resolves with strategy),
   * stage=failed (shows error), or a 60s safety timeout (resolves "transcode").
   */
  function pollProgressThen(p, done) {
    var timeoutId = setTimeout(function () {
      clearInterval(p._pollTimer);
      p._pollTimer = null;
      hidePreparing(p);
      done("transcode");
    }, 60000);

    p._pollTimer = setInterval(function () {
      fetch("/api/v1/videos/" + p._videoId + "/stream-progress", {
        credentials: "same-origin",
      })
        .then(function (r) {
          if (!r.ok) throw new Error("HTTP " + r.status);
          return r.json();
        })
        .then(function (data) {
          var d = data.data || data;
          var stage = d.stage || "unknown";
          var message = d.message || "";
          var percent = d.percent || 0;
          updatePreparing(p, message, percent);

          if (stage === "streaming") {
            clearTimeout(timeoutId);
            clearInterval(p._pollTimer);
            p._pollTimer = null;
            hidePreparing(p);
            done(normalizeStrategy(d.strategy) || "direct");
          } else if (stage === "failed") {
            clearTimeout(timeoutId);
            clearInterval(p._pollTimer);
            p._pollTimer = null;
            p._showFatalError(2, message || "Stream preparation failed");
            done(null);
          }
        })
        .catch(function () {
          updatePreparing(p, "Connecting to server…", 0);
        });
    }, 500);
  }

  /**
   * Ensures we know the full, authoritative video length. For stream-copy (remux)
   * playback the <video> duration only reflects what has been remuxed so far, and
   * some videos have no stored duration — so we ask /stream-probe (ffprobe) once.
   * Guarded by _durationFetching so it never fires more than once while unknown.
   */
  function ensureDuration(p) {
    if (p._duration() > 0 || p._durationFetching) return;
    p._durationFetching = true;
    fetch("/api/v1/videos/" + p._videoId + "/stream-probe", {
      credentials: "same-origin",
    })
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (data) {
        p._durationFetching = false;
        if (p._disposed) return;
        var d = data.data || data;
        var dur = typeof d.durationSeconds === "number" ? d.durationSeconds : 0;
        if (dur > 0 && dur > p._durationSeconds) {
          p._durationSeconds = dur;
          renderSeek(p, p._absoluteTime());
          renderBuffered(p);
        }
      })
      .catch(function () {
        p._durationFetching = false;
      });
  }

  /**
   * After a stream-copy (remux) seek reload, polls /stream-progress until the server
   * reports the actualStartSeconds it used (the position rounded down to a video
   * keyframe for A/V sync). The player then adopts that offset so the displayed time,
   * the slider, and subtitles all match the content actually being played.
   */
  function pollActualStart(p, done) {
    var tries = 0;
    var timer = setInterval(function () {
      tries++;
      fetch("/api/v1/videos/" + p._videoId + "/stream-progress", {
        credentials: "same-origin",
      })
        .then(function (r) {
          if (!r.ok) throw new Error("HTTP " + r.status);
          return r.json();
        })
        .then(function (data) {
          if (p._disposed) {
            clearInterval(timer);
            return;
          }
          var d = data.data || data;
          var stage = d.stage || "unknown";
          if (
            stage === "streaming" &&
            typeof d.actualStartSeconds === "number"
          ) {
            clearInterval(timer);
            done(d.actualStartSeconds);
          } else if (stage === "failed" || tries > 120) {
            clearInterval(timer);
            done(null);
          }
        })
        .catch(function () {
          if (tries > 120) {
            clearInterval(timer);
            done(null);
          }
        });
    }, 500);
  }

  function startHls(p, url) {
    if (typeof Hls === "undefined" || !Hls.isSupported()) {
      // Native HLS fallback (Safari)
      p._video.src = url;
      p._video.load();
      playWithPromise(p);
      return;
    }
    if (!Hls.DefaultConfig._dncConfigured) {
      Hls.DefaultConfig.lowLatencyMode = false;
      Hls.DefaultConfig.backBufferLength = Infinity;
      Hls.DefaultConfig._dncConfigured = true;
    }
    var hls = new Hls({
      manifestLoadingTimeOut: 20000,
      xhrSetup: function (xhr) {
        xhr.withCredentials = true;
      },
    });
    hls.loadSource(url);
    hls.attachMedia(p._video);
    hls.on(Hls.Events.MANIFEST_PARSED, function () {
      hidePreparing(p);
      playWithPromise(p);
    });
    hls.on(Hls.Events.ERROR, function (event, data) {
      if (!data.fatal) return;
      if (data.type === Hls.ErrorTypes.NETWORK_ERROR) {
        hls.startLoad();
      } else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) {
        hls.recoverMediaError();
        if (
          data.details === "bufferStalledError" ||
          data.details === "bufferAppendError"
        ) {
          hls.swapAudioCodec();
        }
      } else {
        p._hls = null;
        hls.destroy();
        var hlsMsg = data.details || "HLS playback error";
        if (data.reason) hlsMsg += ": " + data.reason;
        else if (data.error && data.error.message)
          hlsMsg += ": " + data.error.message;
        p._showFatalError(2, hlsMsg);
      }
    });
    p._hls = hls;
  }

  function teardownEngine(p) {
    if (p._hls) {
      p._hls.destroy();
      p._hls = null;
    }
    p._video.pause();
    p._video.removeAttribute("src");
    p._video.load();
  }

  /** Replaces the <video> element (clears native-error state from m3u8). */
  function replaceVideoElement(p) {
    var old = p._video;
    var parent = old.parentNode;
    if (!parent) return;
    var fresh = document.createElement("video");
    fresh.id = "dnc-video";
    fresh.className = "dnc-video";
    fresh.setAttribute("playsinline", "");
    fresh.preload = "auto";
    if (p._posterUrl) fresh.poster = p._posterUrl;
    // Move subtitle <track> elements over
    var tracks = old.querySelectorAll("track");
    for (var i = 0; i < tracks.length; i++) {
      fresh.appendChild(tracks[i].cloneNode());
    }
    parent.replaceChild(fresh, old);
    p._video = fresh;
    wireVideoEvents(p);
    // Rebuild the subtitle track mapping from the cloned <track> elements.
    // The cloned tracks preserve order and labels, so map them back to the
    // original subtitle entries 1:1 (only when counts match).
    var freshTracks = fresh.querySelectorAll("track");
    if (freshTracks.length === p._tracks.length) {
      for (var j = 0; j < p._tracks.length; j++) {
        p._tracks[j].track = freshTracks[j];
      }
    } else {
      p._tracks = [];
    }
    mapTextTracks(p);
  }

  function playWithPromise(p) {
    var pr = p._video.play();
    if (pr && typeof pr.catch === "function") {
      pr.catch(function (err) {
        // NotAllowedError (autoplay blocked) / AbortError — ignore like Jellyfin
        if (
          err &&
          (err.name === "NotAllowedError" || err.name === "AbortError")
        )
          return;
        p._showFatalError(
          2,
          err && err.message ? err.message : "Playback failed to start",
        );
      });
    }
  }

  // ── Video events ─────────────────────────────────────────────────

  function wireVideoEvents(p) {
    var v = p._video;

    v.addEventListener("play", function () {
      updatePlayIcon(p, true);
    });
    v.addEventListener("pause", function () {
      updatePlayIcon(p, false);
      reportProgress(p, true);
    });
    v.addEventListener("timeupdate", function () {
      if (!p._seeking) {
        renderSeek(p, p._absoluteTime());
        ensureDuration(p);
      }
      reportProgress(p, false);
    });
    v.addEventListener("progress", function () {
      renderBuffered(p);
    });
    v.addEventListener("durationchange", function () {
      // For stream-copy (remux) playback the element duration only grows as content
      // is remuxed — never treat it as authoritative. Ask /stream-probe (ffprobe)
      // once so the time/slider reflect the whole video, not just what's remuxed.
      ensureDuration(p);
      renderSeek(p, p._absoluteTime());
    });
    v.addEventListener("loadedmetadata", onLoaded(p, v));
    v.addEventListener("loadeddata", onLoaded(p, v));
    v.addEventListener("volumechange", function () {
      updateVolumeIcon(p, p._video.muted ? 0 : p._video.volume);
    });
    v.addEventListener("ended", onEnded);
    v.addEventListener("error", onVideoError);
    v.addEventListener("click", function (e) {
      if (e.target === v) togglePlayPause(p);
    });
    v.addEventListener("dblclick", function (e) {
      if (e.target === v) toggleFullscreen(p);
    });

    function onEnded() {
      updatePlayIcon(p, false);
      reportProgress(p, true);
      if (p._dotNetRef)
        p._dotNetRef.invokeMethodAsync("OnEnded").catch(function () {});
    }

    function onVideoError() {
      var err = v.error;
      if (!err) return;
      // Suppress the native error that fires when the browser tries to play an
      // .m3u8 directly (expected HLS) or while hls.js is managing MSE errors.
      if (p._expectingHls || p._hls) return;
      p._showFatalError(err.code || 2, err.message || "");
    }
  }

  /** Resume-position handling once the video is loadable (port of seekOnPlaybackStart). */
  function onLoaded(p, v) {
    var applied = false;
    return function () {
      if (applied) return;
      var resume = p._resumeSeconds;
      // After a remux seek reload (_seekStartOffset set) the server stream already
      // starts at the target position — don't override it with the original resume point.
      if (
        resume > 0 &&
        isFinite(v.duration) &&
        v.duration >= resume &&
        p._seekStartOffset <= 0
      ) {
        applied = true;
        v.currentTime = resume;
      }
    };
  }

  // ── Seek ─────────────────────────────────────────────────────────

  function seekTo(p, seconds) {
    var v = p._video;
    if (!isFinite(seconds) || seconds < 0) return;
    var dur = p._duration();
    if (seconds > dur) seconds = dur;

    if (isHlsStrategy(p._strategy)) {
      var bufferedEnd = bufferedEndOf(v);
      if (seconds <= bufferedEnd + 1) {
        v.currentTime = seconds;
        if (v.paused) playWithPromise(p);
        renderSeek(p, seconds);
        return;
      }
      // Beyond buffer — ask the server to restart the HLS stream (remux or transcode).
      showSeekOverlay(p, seconds);
      p._expectingHls = true;
      fetch("/api/v1/videos/" + p._videoId + "/stream/seek", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({
          positionSeconds: seconds,
          audioStreamIndex: p._currentAudioIndex,
        }),
      })
        .then(function (r) {
          if (!r.ok) {
            return r.json().then(function (err) {
              throw new Error(
                (err && (err.message || err.error)) ||
                  "Seek failed (HTTP " + r.status + ")",
              );
            });
          }
          return r.json();
        })
        .then(function (data) {
          hideSeekOverlay(p);
          var d = data && data.data ? data.data : data;
          var actualStart =
            d && typeof d.actualStartSeconds === "number"
              ? d.actualStartSeconds
              : seconds;
          // The reloaded HLS stream starts at the server-chosen position (keyframe
          // for remux, exact for transcode); hls.js restarts its own timeline at 0.
          // Adopt the server's start as the absolute offset so the slider/elapsed
          // time keep their correct position instead of resetting to the beginning.
          p._seekStartOffset = actualStart;
          teardownEngine(p);
          startHls(p, p._streamBaseUrl);
          p._expectingHls = false;
          renderSeek(p, seconds);
        })
        .catch(function (err) {
          hideSeekOverlay(p);
          p._showFatalError(
            2,
            err && err.message ? err.message : "Seek failed",
          );
        });
      return;
    }

    // Direct play — browser handles random access.
    v.currentTime = seconds;
    if (v.paused) playWithPromise(p);
    renderSeek(p, seconds);
  }

  function seekBy(p, delta) {
    seekTo(p, p._absoluteTime() + delta);
  }

  function bufferedEndOf(v) {
    if (v.buffered && v.buffered.length > 0) {
      return v.buffered.end(v.buffered.length - 1);
    }
    return 0;
  }

  // ── Audio stream selection ───────────────────────────────────────

  function selectAudioStream(p, index) {
    if (index === p._currentAudioIndex) return;
    var position = p._absoluteTime();

    if (isHlsStrategy(p._strategy)) {
      // Position the new HLS stream for the selected audio, then reload.
      showSeekOverlay(p, position);
      p._expectingHls = true;
      fetch("/api/v1/videos/" + p._videoId + "/stream/seek", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({
          positionSeconds: position,
          audioStreamIndex: index,
        }),
      })
        .then(function (r) {
          if (!r.ok) {
            return r.json().then(function (err) {
              throw new Error(
                (err && (err.message || err.error)) ||
                  "Audio switch failed (HTTP " + r.status + ")",
              );
            });
          }
          return r.json();
        })
        .then(function (data) {
          var d = data.data || data;
          hideSeekOverlay(p);
          p._currentAudioIndex = index;
          // The reloaded HLS stream starts at the server-chosen keyframe position;
          // adopt it as the absolute offset so the slider/elapsed time stay correct.
          p._seekStartOffset =
            typeof d.actualStartSeconds === "number"
              ? d.actualStartSeconds
              : position;
          teardownEngine(p);
          var url = buildStreamUrl(p, { audioStreamIndex: index });
          startHls(p, url);
          p._expectingHls = false;
          var s = normalizeStrategy(d.strategy) || "transcode";
          p._strategy = s;
          reportStrategy(p, s);
        })
        .catch(function (err) {
          hideSeekOverlay(p);
          p._showFatalError(
            2,
            err && err.message ? err.message : "Failed to switch audio track",
          );
        });
      return;
    }

    // Direct play — reload with the selected audio + position.
    p._currentAudioIndex = index;
    p._seekStartOffset = 0;
    teardownEngine(p);
    var url = buildStreamUrl(p, {
      audioStreamIndex: index,
      startSeconds: position,
    });
    p._video.src = url;
    p._video.load();
    // Server may decide remux or transcode; re-learn via progress poll.
    showPreparing(p);
    p._expectingHls = true;
    pollProgressThen(p, function (strategy) {
      if (p._disposed) return;
      if (isHlsStrategy(strategy)) {
        p._strategy = strategy;
        replaceVideoElement(p);
        startHls(p, url);
        p._expectingHls = false;
        reportStrategy(p, strategy);
      } else {
        p._strategy = strategy || "direct";
        p._expectingHls = false;
        reportStrategy(p, p._strategy);
        playWithPromise(p);
      }
    });
  }

  function buildStreamUrl(p, options) {
    options = options || {};
    var url = p._streamBaseUrl;
    var sep = url.indexOf("?") === -1 ? "?" : "&";
    var parts = [];
    if (
      options.audioStreamIndex !== undefined &&
      options.audioStreamIndex !== null
    ) {
      parts.push("audioStreamIndex=" + options.audioStreamIndex);
    }
    if (options.startSeconds !== undefined && options.startSeconds !== null) {
      parts.push("startSeconds=" + options.startSeconds);
    }
    if (options.forceTranscode) {
      parts.push("forceTranscode=true");
    }
    parts.push("_=" + Date.now());
    return url + sep + parts.join("&");
  }

  // ── Transport controls ───────────────────────────────────────────

  function togglePlayPause(p) {
    if (p._video.paused) {
      playWithPromise(p);
    } else {
      p._video.pause();
      reportProgress(p, true);
    }
  }

  function toggleMute(p) {
    p._video.muted = !p._video.muted;
    updateVolumeIcon(p, p._video.muted ? 0 : p._video.volume);
  }

  function togglePip(p) {
    if (document.pictureInPictureElement) {
      document.exitPictureInPicture().catch(function () {});
    } else if (p._video.requestPictureInPicture) {
      p._video.requestPictureInPicture().catch(function () {});
    }
  }

  function toggleFullscreen(p) {
    var el = p._root;
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(function () {});
    } else if (el.requestFullscreen) {
      el.requestFullscreen().catch(function () {});
    }
  }

  function updatePlayIcon(p, playing) {
    var btn = p._osd.querySelector(".dnc-btn-play");
    if (btn) btn.innerHTML = iconSvg(playing ? "pause" : "play");
    if (p._bigPlay) {
      if (playing) p._bigPlay.classList.add("hidden");
      else if (!p._video.ended) p._bigPlay.classList.remove("hidden");
    }
  }

  function updateVolumeIcon(p, level) {
    var btn = p._osd.querySelector('.dnc-btn[data-action="mute"]');
    if (btn) btn.innerHTML = iconSvg(level > 0 ? "volumeUp" : "volumeOff");
  }

  // ── OSD rendering / auto-hide ────────────────────────────────────

  function renderSeek(p, seconds) {
    var dur = p._duration();
    if (dur <= 0) return;
    var ratio = Math.max(0, Math.min(1, seconds / dur)) * 100;
    var played = p._osd.querySelector("#dnc-seek-played");
    var thumb = p._osd.querySelector("#dnc-seek-thumb");
    if (played) played.style.width = ratio + "%";
    if (thumb) thumb.style.left = ratio + "%";
    var start = p._osd.querySelector("#dnc-time-start");
    if (start) start.textContent = formatTime(seconds);
    var end = p._osd.querySelector("#dnc-time-end");
    if (end) end.textContent = formatTime(dur);
  }

  function renderBuffered(p) {
    var v = p._video;
    if (!v.buffered || v.buffered.length === 0) return;
    var dur = p._duration();
    if (dur <= 0) return;
    var end = v.buffered.end(v.buffered.length - 1);
    var pct = Math.max(0, Math.min(100, (end / dur) * 100));
    var buffered = p._osd.querySelector("#dnc-seek-buffered");
    if (buffered) buffered.style.width = pct + "%";
  }

  function showOsd(p) {
    p._osd.classList.remove("hidden");
    clearTimeout(p._osdTimer);
    p._osdTimer = setTimeout(function () {
      if (!p._seeking && !p._video.paused) {
        p._osd.classList.add("hidden");
      }
    }, 3000);
  }

  function wireOsdAutoHide(p) {
    p._root.addEventListener("mousemove", function () {
      showOsd(p);
    });
    p._root.addEventListener("mousedown", function () {
      showOsd(p);
    });
    p._root.addEventListener("touchstart", function () {
      showOsd(p);
    });
    p._osd.addEventListener("mousemove", function (e) {
      e.stopPropagation();
    });
  }

  // ── Keyboard shortcuts ───────────────────────────────────────────

  function wireKeyboard(p) {
    function onKey(e) {
      var tag = (e.target.tagName || "").toLowerCase();
      if (
        tag === "input" ||
        tag === "textarea" ||
        tag === "select" ||
        e.target.isContentEditable
      )
        return;
      if (!document.body.contains(p._root)) return;

      switch (e.key) {
        case " ":
          e.preventDefault();
          togglePlayPause(p);
          break;
        case "ArrowLeft":
          e.preventDefault();
          seekBy(p, -10);
          break;
        case "ArrowRight":
          e.preventDefault();
          seekBy(p, 10);
          break;
        case "j":
        case "J":
          seekBy(p, -10);
          break;
        case "l":
        case "L":
          seekBy(p, 10);
          break;
        case "f":
        case "F":
          toggleFullscreen(p);
          break;
        case "m":
        case "M":
          toggleMute(p);
          break;
        case "n":
        case "N":
          if (p._dotNetRef)
            p._dotNetRef
              .invokeMethodAsync("OnNavigateEpisode", 1)
              .catch(function () {});
          break;
        case "p":
        case "P":
          if (p._dotNetRef)
            p._dotNetRef
              .invokeMethodAsync("OnNavigateEpisode", -1)
              .catch(function () {});
          break;
        case "Escape":
          if (document.fullscreenElement)
            document.exitFullscreen().catch(function () {});
          break;
        default:
          break;
      }
    }
    document.addEventListener("keydown", onKey);
    p._keyHandler = onKey;
  }

  // ── Overlays ─────────────────────────────────────────────────────

  function showPreparing(p) {
    p._spinner.style.display = "block";
    ensurePreparingMsg(p);
  }

  function ensurePreparingMsg(p) {
    var msg = p._root.querySelector(".dnc-preparing");
    if (!msg) {
      msg = document.createElement("div");
      msg.className = "dnc-preparing";
      msg.innerHTML =
        '<span class="dnc-preparing-text">Preparing stream…</span>' +
        '<div class="dnc-preparing-bar"><div class="dnc-preparing-fill"></div></div>';
      p._root.appendChild(msg);
    }
    return msg;
  }

  function updatePreparing(p, message, percent) {
    var msg = ensurePreparingMsg(p);
    var text = msg.querySelector(".dnc-preparing-text");
    if (text && message) text.textContent = message;
    var fill = msg.querySelector(".dnc-preparing-fill");
    if (fill) fill.style.width = Math.min(100, percent) + "%";
  }

  function hidePreparing(p) {
    p._spinner.style.display = "none";
    var msg = p._root.querySelector(".dnc-preparing");
    if (msg && msg.parentNode) msg.parentNode.removeChild(msg);
  }

  function showSeekOverlay(p, seconds) {
    hideSeekOverlay(p);
    var overlay = document.createElement("div");
    overlay.className = "dnc-seek-overlay";
    overlay.innerHTML =
      '<div class="dnc-seek-spinner"></div>' +
      '<div class="dnc-seek-label">Jumping to ' +
      formatTime(seconds) +
      "…</div>";
    p._root.appendChild(overlay);
    p._seekOverlay = overlay;
  }

  function hideSeekOverlay(p) {
    if (p._seekOverlay && p._seekOverlay.parentNode) {
      p._seekOverlay.parentNode.removeChild(p._seekOverlay);
    }
    p._seekOverlay = null;
  }

  // ── Watch progress ───────────────────────────────────────────────

  function reportProgress(p, force) {
    if (!p._videoId) return;
    var now = Date.now();
    if (!force && now - p._progressReportedAt < 60000) return;
    p._progressReportedAt = now;
    var positionTicks = Math.round(p._absoluteTime() * 10000000);
    fetch("/api/v1/videos/" + p._videoId + "/progress", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ positionTicks: positionTicks }),
    }).catch(function () {});
  }

  function reportStrategy(p, strategy) {
    if (p._dotNetRef) {
      p._dotNetRef
        .invokeMethodAsync("OnStrategy", strategy)
        .catch(function () {});
    }
  }

  // ── Utility functions ────────────────────────────────────────────

  function formatTime(seconds) {
    if (!isFinite(seconds) || seconds < 0) return "0:00";
    var h = Math.floor(seconds / 3600);
    var m = Math.floor((seconds % 3600) / 60);
    var s = Math.floor(seconds % 60);
    if (h > 0) {
      return h + ":" + pad2(m) + ":" + pad2(s);
    }
    return m + ":" + pad2(s);
  }

  function pad2(n) {
    return n < 10 ? "0" + n : String(n);
  }

  /**
   * Coerces any error value to a displayable string. Prevents "[object Object]"
   * when an error object (rather than a string) is passed to the error card.
   */
  function stringifyError(m) {
    if (m == null) return "";
    if (typeof m === "string") return m;
    if (m instanceof Error) return m.message || m.name || "Error";
    if (typeof m === "object") {
      try {
        return JSON.stringify(m);
      } catch (e) {
        return String(m);
      }
    }
    return String(m);
  }

  function escapeHtml(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function closest(el, selector) {
    while (el && el.nodeType === 1) {
      if (el.matches && el.matches(selector)) return el;
      el = el.parentNode;
    }
    return null;
  }
})();
