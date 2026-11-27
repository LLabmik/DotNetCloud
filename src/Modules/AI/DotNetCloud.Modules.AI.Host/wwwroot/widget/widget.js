/*
 * AI Assistant Home-page widget.
 *
 * A widget is a sandboxed card: no cookies, no storage, an opaque origin, and the only way back into
 * the module is the bridge (`dnc:navigate`). Requests are relative so they stay inside
 * /apps/dotnetcloud.ai/ and reach this module's own API through the core proxy.
 */
(function () {
  "use strict";

  var bridge = window.dncModuleBridge || null;
  var STATS = "../api/v1/ai/conversations/stats";

  function setText(id, text) {
    var node = document.getElementById(id);
    if (node) {
      node.textContent = text;
    }
  }

  function formatLastActivity(value) {
    if (!value) {
      return "No conversations yet.";
    }

    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return "No conversations yet.";
    }

    var day = 24 * 60 * 60 * 1000;
    var elapsed = Date.now() - date.getTime();

    if (elapsed < day) {
      return "Last activity today";
    }
    if (elapsed < 7 * day) {
      return "Last activity " + date.toLocaleDateString(undefined, { weekday: "long" });
    }
    return "Last activity " + date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  }

  function load() {
    fetch(STATS, { credentials: "include", headers: { Accept: "application/json" } })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("stats unavailable");
        }
        return response.json();
      })
      .then(function (stats) {
        setText("conversation-count", String(stats.totalConversations || 0));
        setText("last-activity", formatLastActivity(stats.lastActivityUtc));
      })
      .catch(function () {
        setText("conversation-count", "—");
        setText("last-activity", "Assistant unavailable right now.");
      })
      .then(function () {
        if (bridge) {
          bridge.reportHeight();
        }
      });
  }

  document.addEventListener("DOMContentLoaded", function () {
    if (bridge) {
      bridge.start({ title: "AI Assistant", role: "widget" });
    }

    document.getElementById("open-assistant").addEventListener("click", function () {
      if (bridge) {
        bridge.navigate("/");
      }
    });

    load();
  });
})();
