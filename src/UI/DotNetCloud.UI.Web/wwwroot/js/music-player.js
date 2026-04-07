window.dotnetcloudMusicPlayer = window.dotnetcloudMusicPlayer || (function () {
    "use strict";

    /** @type {HTMLAudioElement|null} */
    let audio = null;
    /** @type {DotNet.DotNetObject|null} */
    let dotNetRef = null;
    let updateInterval = null;

    function init(dotNetObjRef) {
        dotNetRef = dotNetObjRef;
        audio = document.getElementById("dnc-music-audio");
        if (!audio) {
            audio = new Audio();
            audio.id = "dnc-music-audio";
            audio.preload = "auto";
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
        dotNetRef = null;
    }

    function play(url) {
        if (!audio) return;
        audio.src = url;
        audio.load();
        audio.play().catch(function (e) {
            console.warn("Music play failed:", e.message);
        });
    }

    function resume() {
        if (audio && audio.src) {
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

    return { init: init, dispose: dispose, play: play, resume: resume, pause: pause, stop: stop, seek: seek, setVolume: setVolume, setMuted: setMuted };
})();
