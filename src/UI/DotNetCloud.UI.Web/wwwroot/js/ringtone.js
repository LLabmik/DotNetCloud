/**
 * DotNetCloud ringtone player.
 * Plays a looping ringtone for incoming call notifications.
 */
(function () {
    "use strict";

    let _audio = null;
    let _isPlaying = false;

    window.dotnetcloudRingtone = {
        /**
         * Start playing the ringtone in a loop.
         * @param {string} src - Relative path to the audio file (e.g. "sounds/computer-ambience.mp3").
         * @param {number} [volume=0.6] - Volume level 0–1.
         */
        play: function (src, volume) {
            if (_isPlaying) return;

            try {
                if (!_audio) {
                    _audio = new Audio();
                    _audio.loop = true;
                    _audio.preload = "auto";
                }

                _audio.src = "_content/DotNetCloud.UI.Web/" + src;
                _audio.volume = Math.max(0, Math.min(1, volume ?? 0.6));
                _audio.currentTime = 0;

                var playPromise = _audio.play();
                if (playPromise) {
                    playPromise.then(function () {
                        _isPlaying = true;
                    }).catch(function (err) {
                        // Autoplay blocked by browser — user hasn't interacted yet.
                        console.warn("[ringtone] Autoplay blocked:", err.message);
                        _isPlaying = false;
                    });
                }
            } catch (ex) {
                console.error("[ringtone] play error:", ex);
            }
        },

        /**
         * Stop the ringtone and reset playback.
         */
        stop: function () {
            if (_audio) {
                _audio.pause();
                _audio.currentTime = 0;
            }
            _isPlaying = false;
        },

        /**
         * Set ringtone volume (0–1).
         */
        setVolume: function (level) {
            if (_audio) {
                _audio.volume = Math.max(0, Math.min(1, level));
            }
        },

        /**
         * Whether the ringtone is currently playing.
         */
        isPlaying: function () {
            return _isPlaying;
        }
    };
})();
