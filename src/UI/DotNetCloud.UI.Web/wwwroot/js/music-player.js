window.dotnetcloudMusicPlayer = window.dotnetcloudMusicPlayer || (function () {
    "use strict";

    /** @type {HTMLAudioElement|null} */
    let audio = null;
    /** @type {DotNet.DotNetObject|null} */
    let dotNetRef = null;
    let updateInterval = null;

    // ── Web Audio API for Equalizer ──
    /** @type {AudioContext|null} */
    let audioCtx = null;
    /** @type {MediaElementAudioSourceNode|null} */
    let sourceNode = null;
    /** @type {BiquadFilterNode[]} */
    let eqFilters = [];
    let eqConnected = false;

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

    function init(dotNetObjRef) {
        dotNetRef = dotNetObjRef;
        audio = document.getElementById("dnc-music-audio");
        if (!audio) {
            audio = new Audio();
            audio.id = "dnc-music-audio";
            audio.preload = "auto";
            audio.crossOrigin = "anonymous";
            audio.style.display = "none";
            document.body.appendChild(audio);
        }

        audio.addEventListener("ended", onEnded);
        audio.addEventListener("error", onError);
        audio.addEventListener("canplay", onCanPlay);
        audio.addEventListener("loadedmetadata", onMetadata);

        // Poll current time every 500ms (more responsive than timeupdate event)
        if (updateInterval) clearInterval(updateInterval);
        updateInterval = setInterval(onTimeUpdate, 500);
    }

    function dispose() {
        if (updateInterval) { clearInterval(updateInterval); updateInterval = null; }
        if (audio) {
            audio.pause();
            audio.removeEventListener("ended", onEnded);
            audio.removeEventListener("error", onError);
            audio.removeEventListener("canplay", onCanPlay);
            audio.removeEventListener("loadedmetadata", onMetadata);
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
        if (!audio) return;
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
        if (audio && audio.src) {
            if (audioCtx && audioCtx.state === "suspended") {
                audioCtx.resume();
            }
            audio.play().catch(function (e) {
                console.warn("Music resume failed:", e.message);
            });
        }
    }

    function pause() {
        if (audio) audio.pause();
    }

    function stop() {
        if (audio) { audio.pause(); audio.src = ""; }
    }

    function seek(seconds) {
        if (audio && isFinite(seconds)) audio.currentTime = seconds;
    }

    function setVolume(level) {
        // level: 0-100
        if (audio) audio.volume = Math.max(0, Math.min(1, level / 100));
    }

    function setMuted(muted) {
        if (audio) audio.muted = !!muted;
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

    return { init: init, dispose: dispose, play: play, resume: resume, pause: pause, stop: stop, seek: seek, setVolume: setVolume, setMuted: setMuted, setEqBand: setEqBand, setEqBands: setEqBands };
})();
