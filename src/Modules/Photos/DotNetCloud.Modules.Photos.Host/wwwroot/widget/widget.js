/*
 * Photos home-page widget.
 *
 * A widget frame is the same opaque-origin sandbox as the module app, framed inside a shell card.
 * It reports its own height through the bridge (widget role) and opens links through the shell,
 * because a sandboxed frame may not navigate a top-level window.
 */
(function () {
  "use strict";

  var ENDPOINT = "../api/v1/photos?skip=0&take=5";

  var message = document.getElementById("message");
  var list = document.getElementById("items");
  var bridge = window.dncModuleBridge || null;

  /**
   * Authenticates a URL the browser loads by itself (<img src>): the frame's origin is opaque, so
   * neither the session cookie nor the ticket header reaches the core — the bridge puts the ticket
   * in the query string.
   */
  function mediaUrl(url) {
    return bridge && typeof bridge.mediaUrl === "function" ? bridge.mediaUrl(url) : url;
  }

  function setMessage(text) {
    if (!text) {
      message.hidden = true;
      message.textContent = "";
      return;
    }

    message.textContent = text;
    message.hidden = false;
  }

  function openPhoto(photoId) {
    if (!photoId) {
      return;
    }

    if (bridge && typeof bridge.openExternal === "function") {
      // Module-relative: the bridge resolves it against this module's framed app, and the core
      // forwards the query into the frame so the app opens that photo.
      bridge.openExternal("?photoId=" + encodeURIComponent(photoId));
      return;
    }

    window.location.href = "?photoId=" + encodeURIComponent(photoId);
  }

  function formatDate(value) {
    if (!value) {
      return "";
    }

    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return "";
    }

    return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
  }

  function render(photos) {
    if (!photos || photos.length === 0) {
      list.hidden = true;
      setMessage("No photos yet.");
      return;
    }

    setMessage("");
    list.hidden = false;
    list.textContent = "";

    photos.forEach(function (photo) {
      var row = document.createElement("li");
      row.className = "widget-list-item";

      var button = document.createElement("button");
      button.type = "button";
      button.className = "widget-photo";
      button.addEventListener("click", function () {
        openPhoto(photo.id);
      });

      var image = document.createElement("img");
      image.className = "widget-thumb";
      image.loading = "lazy";
      image.alt = "";
      image.src = mediaUrl(
        "../api/v1/photos/" + encodeURIComponent(photo.id) + "/thumbnail?size=grid",
      );
      button.appendChild(image);

      var main = document.createElement("span");
      main.className = "widget-photo-main";

      var name = document.createElement("span");
      name.className = "widget-photo-name";
      // textContent: a file name is user data.
      name.textContent = photo.fileName;
      main.appendChild(name);

      var meta = document.createElement("span");
      meta.className = "widget-photo-meta";
      meta.textContent = formatDate(photo.takenAt || photo.createdAt);
      main.appendChild(meta);

      button.appendChild(main);
      row.appendChild(button);
      list.appendChild(row);
    });
  }

  async function load() {
    try {
      var response = await fetch(ENDPOINT, {
        credentials: "same-origin",
        headers: { Accept: "application/json" },
      });

      if (response.status === 401) {
        setMessage("Sign in to see your photos.");
        return;
      }

      if (!response.ok) {
        setMessage("Photos are unavailable right now.");
        return;
      }

      var payload = await response.json();
      render(payload && payload.success ? payload.data : []);
    } catch (error) {
      setMessage("Photos are unavailable right now.");
    }
  }

  if (bridge) {
    bridge
      .on("theme", function (info) {
        document.documentElement.setAttribute(
          "data-theme",
          info && info.theme === "dark" ? "dark" : "light",
        );
      })
      .start({ title: "Photos", role: "widget" });
  }

  load();
})();
