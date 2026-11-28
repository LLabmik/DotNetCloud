/*
 * Tracks home-page widget.
 *
 * A widget frame is the same opaque-origin sandbox as the module app, framed inside a shell card.
 * It reports its own height through the bridge (widget role) and navigates through the shell,
 * because a sandboxed frame may not navigate a top-level window. Everything rendered here is user
 * data, so it is written with textContent only.
 */
(function () {
  "use strict";

  var ENDPOINT = "../api/v1/widget/upcoming?take=5";

  var message = document.getElementById("message");
  var list = document.getElementById("items");
  var bridge = window.dncModuleBridge || null;

  function setMessage(text) {
    if (!text) {
      message.hidden = true;
      message.textContent = "";
      return;
    }

    message.textContent = text;
    message.hidden = false;
  }

  function openItem(workItemId) {
    if (!workItemId) {
      return;
    }

    var target = "?card=" + encodeURIComponent(workItemId);

    if (bridge && typeof bridge.openExternal === "function") {
      // Module-relative: the bridge resolves it against this module's framed app, and the core
      // forwards the query into the frame so the app opens that work item.
      bridge.openExternal(target);
      return;
    }

    window.location.href = target;
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

  function render(items) {
    if (!items || items.length === 0) {
      list.hidden = true;
      setMessage("No upcoming work items.");
      return;
    }

    setMessage("");
    list.hidden = false;
    list.textContent = "";

    items.forEach(function (item) {
      var row = document.createElement("li");
      row.className = "widget-list-item";

      var button = document.createElement("button");
      button.type = "button";
      button.className = "widget-item";
      button.addEventListener("click", function () {
        openItem(item.id);
      });

      var title = document.createElement("span");
      title.className = "widget-item-title";
      title.textContent = item.title;
      button.appendChild(title);

      var meta = document.createElement("span");
      meta.className = "widget-item-meta";
      meta.textContent = formatDate(item.dueDate);
      button.appendChild(meta);

      // The priority dot is decoration; the class carries the meaning for the stylesheet.
      var dot = document.createElement("span");
      dot.className = "widget-item-dot widget-item-dot--" + String(item.priority || "none").toLowerCase();
      dot.setAttribute("aria-hidden", "true");
      row.appendChild(dot);

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
        setMessage("Sign in to see your work items.");
        return;
      }

      if (!response.ok) {
        setMessage("Tracks is unavailable right now.");
        return;
      }

      var payload = await response.json();
      render(payload && payload.success ? payload.data : []);
    } catch (error) {
      setMessage("Tracks is unavailable right now.");
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
      .start({ title: "Tracks", role: "widget" });
  }

  load();
})();
