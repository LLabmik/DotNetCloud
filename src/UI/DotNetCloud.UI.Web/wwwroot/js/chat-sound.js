/**
 * DotNetCloud chat notification sound.
 * Plays a short "ding" when a new chat message arrives.
 * A single shared Audio element is reused so rapid consecutive messages
 * restart the sound instead of stacking overlapping playback.
 */
(function () {
  "use strict";

  var _audio = null;

  window.dotnetcloudChatSound = {
    /**
     * Play the chat message "ding" once. Restarts the sound if already playing.
     */
    playDing: function () {
      try {
        if (!_audio) {
          _audio = new Audio();
          _audio.preload = "auto";
        }

        _audio.src = "_content/DotNetCloud.UI.Web/sounds/chat-ding.mp3";
        _audio.volume = 0.7;
        _audio.currentTime = 0;

        var playPromise = _audio.play();
        if (playPromise) {
          playPromise.catch(function () {
            // Autoplay blocked by the browser (no prior user gesture).
            // Ignored — the next message after an interaction will play.
          });
        }
      } catch (ex) {
        // Sound is best-effort: never let a failed ding break the chat.
        console.debug("[chat-sound] play error:", ex);
      }
    },
  };
})();
