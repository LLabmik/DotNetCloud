window.dotnetcloudMusicPlayer = window.dotnetcloudMusicPlayer || (function () {
    "use strict";

    /** @type {HTMLAudioElement|null} */
    let audio = null;
    /** @type {DotNet.DotNetObject|null} */
    let dotNetRef = null;
    let updateInterval = null;
    let listenersAttached = false;

    // ── Web Audio API for Equalizer ──
    /** @type {AudioContext|null} */
    let audioCtx = null;
    /** @type {MediaElementAudioSourceNode|null} */
    let sourceNode = null;
    /** @type {BiquadFilterNode[]} */
    let eqFilters = [];
    let eqConnected = false;

    // ── Browser tab title (now-playing) ──
    /** @type {string|null} Label to force into the tab title while a track is loaded. */
    let nowPlayingTitle = null;
    /** @type {string|null} Last page-managed title, restored when the override is cleared. */
    let pageTitle = null;
    /** @type {MutationObserver|null} Watches <head> so the override survives Blazor <PageTitle> updates. */
    let titleObserver = null;
    let applyingTitle = false;
    let titleWriteCount = 0;
    let titleCountResetTimer = null;

    // 10-band EQ center frequencies (Hz)
    const EQ_FREQUENCIES = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    function ensureEqChain() {
        if (eqConnected || !audio) return;
        try {
            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            sourceNode = audioCtx.createMediaElementSource(audio);

            eqFilters = EQ_FREQUENCIES.map(function (freq, i) {
                var filter = audioCtx.createBiquadFilter();
                if (i === 0) {
                    filter.type = "lowshelf";
                } else if (i === EQ_FREQUENCIES.length - 1) {
                    filter.type = "highshelf";
                } else {
                    filter.type = "peaking";
                }
                filter.frequency.value = freq;
                filter.Q.value = 1.4;
                filter.gain.value = 0;
                return filter;
            });

            // Chain: source → filter0 → filter1 → … → filter9 → destination
            sourceNode.connect(eqFilters[0]);
            for (var i = 1; i < eqFilters.length; i++) {
                eqFilters[i - 1].connect(eqFilters[i]);
            }
            eqFilters[eqFilters.length - 1].connect(audioCtx.destination);

            eqConnected = true;
        } catch (e) {
            console.warn("EQ init failed, falling back to direct output:", e.message);
            // Fallback: connect source directly if something went wrong
            if (sourceNode && audioCtx) {
                try { sourceNode.connect(audioCtx.destination); } catch (_) { /* ignore */ }
            }
        }
    }

    /**
     * Creates (or reuses) the hidden audio element.
     * Idempotent, so play() works even when the playbar has not called init() yet
     * (previously play() silently did nothing in that case — the first track click
     * after a page load produced no audio and no network request).
     * @returns {HTMLAudioElement}
     */
    function ensureAudio() {
        if (audio && document.body.contains(audio)) return audio;
        audio = document.getElementById("dnc-music-audio");
        if (!audio) {
            audio = new Audio();
            audio.id = "dnc-music-audio";
            audio.preload = "auto";
            audio.crossOrigin = "anonymous";
            audio.style.display = "none";
            document.body.appendChild(audio);
        }
        return audio;
    }

    /** (Re)attaches the media event listeners. Idempotent. */
    function attachAudioListeners() {
        if (!audio) return;
        audio.removeEventListener("ended", onEnded);
        audio.removeEventListener("error", onError);
        audio.removeEventListener("canplay", onCanPlay);
        audio.removeEventListener("loadedmetadata", onMetadata);

        audio.addEventListener("ended", onEnded);
        audio.addEventListener("error", onError);
        audio.addEventListener("canplay", onCanPlay);
        audio.addEventListener("loadedmetadata", onMetadata);
        listenersAttached = true;
    }

    /** Starts the 500ms progress poll if it is not already running. */
    function ensurePolling() {
        if (updateInterval) return;
        updateInterval = setInterval(onTimeUpdate, 500);
    }

    function init(dotNetObjRef) {
        dotNetRef = dotNetObjRef;
        ensureAudio();
        attachAudioListeners();

        // Poll current time every 500ms (more responsive than timeupdate event)
        if (updateInterval) clearInterval(updateInterval);
        updateInterval = setInterval(onTimeUpdate, 500);
    }

    /**
     * Detach the .NET reference without stopping audio or destroying the audio element.
     * Used when the Blazor component re-renders or disposes but audio should keep playing.
     */
    function detach() {
        dotNetRef = null;
        if (updateInterval) { clearInterval(updateInterval); updateInterval = null; }
    }

    /**
     * Returns true if audio is currently loaded and not paused.
     */
    function isPlaying() {
        return !!audio && !audio.paused && !!audio.src;
    }

    function dispose() {
        if (updateInterval) { clearInterval(updateInterval); updateInterval = null; }
        stopTitleObserver();
        if (audio) {
            audio.pause();
            audio.removeEventListener("ended", onEnded);
            audio.removeEventListener("error", onError);
            audio.removeEventListener("canplay", onCanPlay);
            audio.removeEventListener("loadedmetadata", onMetadata);
            listenersAttached = false;
            audio.src = "";
        }
        if (audioCtx) {
            try { audioCtx.close(); } catch (_) { /* ignore */ }
            audioCtx = null;
            sourceNode = null;
            eqFilters = [];
            eqConnected = false;
        }
        dotNetRef = null;
    }

    function play(url) {
        // Lazy-initialise: the playbar may not have called init() yet on the first track click.
        if (!ensureAudio()) return;
        if (!listenersAttached) attachAudioListeners();
        ensurePolling();

        // Initialise the Web Audio EQ chain on first play (requires user gesture)
        ensureEqChain();
        // Resume AudioContext if suspended (browser autoplay policy)
        if (audioCtx && audioCtx.state === "suspended") {
            audioCtx.resume();
        }
        audio.src = url;
        audio.load();
        audio.play().catch(function (e) {
            console.warn("Music play failed:", e.message);
        });
    }

    function resume() {
        if (ensureAudio() && audio.src) {
            if (audioCtx && audioCtx.state === "suspended") {
                audioCtx.resume();
            }
            audio.play().catch(function (e) {
                console.warn("Music resume failed:", e.message);
            });
        }
    }

    function pause() {
        if (ensureAudio()) audio.pause();
    }

    function stop() {
        if (ensureAudio()) { audio.pause(); audio.src = ""; }
    }

    function seek(seconds) {
        if (ensureAudio() && isFinite(seconds)) audio.currentTime = seconds;
    }

    /**
     * Handle a click on the progress bar using the actual rendered width.
     * @param {number} clientX - The clientX coordinate from the MouseEvent.
     */
    function handleProgressClick(clientX) {
        var progressBar = document.querySelector(".gmp-progress-bar");
        if (!progressBar || !ensureAudio() || !isFinite(audio.duration) || audio.duration <= 0) return;
        var rect = progressBar.getBoundingClientRect();
        var fraction = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
        audio.currentTime = fraction * audio.duration;
        // Immediately notify .NET of the new position
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnJsTimeUpdate", audio.currentTime, audio.duration || 0);
        }
    }

    function setVolume(level) {
        // level: 0-100
        if (ensureAudio()) audio.volume = Math.max(0, Math.min(1, level / 100));
    }

    function setMuted(muted) {
        if (ensureAudio()) audio.muted = !!muted;
    }

    /**
     * Set the gain for a single EQ band.
     * @param {number} bandIndex - 0-9
     * @param {number} gainDb - dB value (-12 to +12)
     */
    function setEqBand(bandIndex, gainDb) {
        if (bandIndex >= 0 && bandIndex < eqFilters.length) {
            eqFilters[bandIndex].gain.value = gainDb;
        }
    }

    /**
     * Set all 10 EQ bands at once.
     * @param {number[]} gains - array of 10 dB values
     */
    function setEqBands(gains) {
        if (!gains || !Array.isArray(gains)) return;
        for (var i = 0; i < gains.length && i < eqFilters.length; i++) {
            eqFilters[i].gain.value = gains[i];
        }
    }

    // ── Browser tab title (now-playing) ──

    /**
     * Collapses whitespace runs so values compare the way the browser reports document.title.
     * Chrome (and the HTML spec) normalize the title's text: writing "09  The Chauffeur" reads
     * back as "09 The Chauffeur". Comparing the raw written string against the readback never
     * matches, which used to make the MutationObserver re-apply the title forever — an infinite
     * main-thread loop that hard-locked the tab (and starved queued JS interop calls, so playback
     * never even issued its request).
     * @param {string|null|undefined} value
     * @returns {string}
     */
    function normalizeTitle(value) {
        return value == null ? "" : String(value).replace(/\s+/g, " ").trim();
    }

    function applyNowPlayingTitle() {
        if (!nowPlayingTitle) return;
        if (normalizeTitle(document.title) === normalizeTitle(nowPlayingTitle)) return;

        applyingTitle = true;
        document.title = nowPlayingTitle;
        // Clear the flag on a LATER microtask: the MutationObserver record for this write is
        // delivered first, so it must still see applyingTitle === true. Clearing the flag
        // synchronously reopened the loop described above.
        queueMicrotask(function () { applyingTitle = false; });

        // Safety valve: never let the title sync spin the main thread, whatever the cause.
        if (titleWriteCount === 0) {
            titleCountResetTimer = setTimeout(function () { titleWriteCount = 0; titleCountResetTimer = null; }, 1000);
        }
        titleWriteCount++;
        if (titleWriteCount > 20) {
            stopTitleObserver();
            console.warn("[music] now-playing title sync disabled: too many rewrites in one second");
        }
    }

    function startTitleObserver() {
        if (titleObserver || !document.head) return;
        titleObserver = new MutationObserver(function () {
            if (applyingTitle) return;
            if (normalizeTitle(document.title) !== normalizeTitle(nowPlayingTitle)) {
                // Blazor's <PageTitle> (via HeadOutlet) changed the tab title — remember it
                // so it can be restored later, then re-apply the now-playing override on top.
                pageTitle = document.title;
                applyNowPlayingTitle();
            }
        });
        titleObserver.observe(document.head, { childList: true, subtree: true, characterData: true });
    }

    function stopTitleObserver() {
        if (titleObserver) {
            titleObserver.disconnect();
            titleObserver = null;
        }
        if (titleCountResetTimer) {
            clearTimeout(titleCountResetTimer);
            titleCountResetTimer = null;
        }
        titleWriteCount = 0;
    }

    /**
     * Force a now-playing label into the browser tab title, keeping it applied even as
     * Blazor updates the page title during navigation.
     * @param {string} label - e.g. "♪ Song Title — Artist".
     */
    function setNowPlayingTitle(label) {
        if (!label) { clearNowPlayingTitle(); return; }
        if (!nowPlayingTitle) {
            // First override — capture the page's own title so it can be restored later.
            pageTitle = document.title;
        }
        nowPlayingTitle = label;
        applyNowPlayingTitle();
        startTitleObserver();
    }

    /** Removes the now-playing override and restores the page's own tab title. */
    function clearNowPlayingTitle() {
        nowPlayingTitle = null;
        stopTitleObserver();
        if (pageTitle !== null && normalizeTitle(document.title) !== normalizeTitle(pageTitle)) {
            applyingTitle = true;
            document.title = pageTitle;
            queueMicrotask(function () { applyingTitle = false; });
        }
    }

    // ── Event handlers ──

    function onTimeUpdate() {
        if (!audio || !dotNetRef || audio.paused) return;
        dotNetRef.invokeMethodAsync("OnJsTimeUpdate", audio.currentTime, audio.duration || 0);
    }

    function onEnded() {
        if (dotNetRef) dotNetRef.invokeMethodAsync("OnJsTrackEnded");
    }

    function onError() {
        var msg = audio && audio.error ? audio.error.message : "Unknown audio error";
        console.error("Audio error:", msg);
        if (dotNetRef) dotNetRef.invokeMethodAsync("OnJsPlaybackError", msg);
    }

    function onCanPlay() {
        // Ensure play continues after buffering
    }

    function onMetadata() {
        if (dotNetRef && audio) {
            dotNetRef.invokeMethodAsync("OnJsMetadataLoaded", audio.duration || 0);
        }
    }

    /**
     * Scrolls the currently-playing track row (.track-row.playing) into view.
     * Named function (no eval) so it complies with the strict CSP that forbids 'unsafe-eval'.
     */
    function scrollToPlaying() {
        var el = document.querySelector(".track-row.playing");
        if (el) {
            el.scrollIntoView({ behavior: "smooth", block: "center" });
        }
    }

    return { init: init, detach: detach, isPlaying: isPlaying, dispose: dispose, play: play, resume: resume, pause: pause, stop: stop, seek: seek, setVolume: setVolume, setMuted: setMuted, setEqBand: setEqBand, setEqBands: setEqBands, handleProgressClick: handleProgressClick, scrollToPlaying: scrollToPlaying, setNowPlayingTitle: setNowPlayingTitle, clearNowPlayingTitle: clearNowPlayingTitle, getAudioContext: function () { return audioCtx; }, getSourceNode: function () { return sourceNode; } };
})();

/**
 * Triggers a browser file download without navigating away from the current page.
 * Creates a temporary anchor element, sets its href to the download URL, and clicks it.
 * @param {string} url - The download endpoint URL.
 */
window.triggerDownload = function (url) {
    var a = document.createElement('a');
    a.href = url;
    a.download = '';
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
};
