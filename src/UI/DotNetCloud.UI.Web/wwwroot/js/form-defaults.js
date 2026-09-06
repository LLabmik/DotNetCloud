// form-defaults.js — DotNetCloud shared form defaults.
// Rule: Enter in a single-line TEXT BOX submits the enclosing form/dialog; TEXT AREAS and
// rich editors are exempt (Enter = newline). Attribute-driven so any form/dialog can opt in:
//   [data-enter-submit]   container: Enter in a text box submits (form) / clicks primary button.
//   [data-autosubmit="N"] input:     auto-submit once value length >= N (native forms; see MFA plan).
//   [data-autofocus]      input:     focus it when available.
//   [data-autofocus-first] container: focus first visible text input inside.
// Opt out a special input: data-no-enter-submit (self or ancestor).
(function () {
  "use strict";

  var TEXT_LIKE = [
    "text",
    "password",
    "email",
    "url",
    "tel",
    "number",
    "search",
    "date",
    "datetime-local",
    "month",
    "week",
    "time",
  ];
  var PRIMARY_SELECTOR =
    'button[data-primary-action], button[type="submit"], button.btn-primary';

  function isTextInput(el) {
    if (!el || el.tagName !== "INPUT") return false;
    var type = (el.getAttribute("type") || "text").toLowerCase();
    return TEXT_LIKE.indexOf(type) !== -1 && !el.disabled && !el.readOnly;
  }

  function isExcluded(el) {
    if (el.tagName === "TEXTAREA" || el.isContentEditable) return true;
    return !!el.closest("[data-no-enter-submit]");
  }

  // Returns the submit scope: a real <form> wins, else the nearest opted-in container.
  function submitScopeFor(el) {
    var form = el.closest("form");
    if (form) return { kind: "form", node: form };
    var container = el.closest("[data-enter-submit]");
    if (container) return { kind: "container", node: container };
    return null;
  }

  function runSubmit(el) {
    var scope = submitScopeFor(el);
    if (!scope) return false;
    if (scope.kind === "form") {
      var form = scope.node;
      if (typeof form.requestSubmit === "function") form.requestSubmit();
      else form.submit();
      return true;
    }
    var primary = scope.node.querySelector(PRIMARY_SELECTOR + ", button");
    if (primary && !primary.disabled && typeof primary.click === "function") {
      primary.click();
      return true;
    }
    return false;
  }

  function onKeyDown(e) {
    if (e.key !== "Enter") return;
    if (e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
    var el = e.target;
    if (!isTextInput(el) || isExcluded(el)) return;
    if (runSubmit(el)) e.preventDefault();
  }

  function onInput(e) {
    var el = e.target;
    if (!isTextInput(el) || isExcluded(el)) return;
    var raw = el.getAttribute("data-autosubmit");
    if (!raw) return;
    var n = parseInt(raw, 10);
    if (!isNaN(n) && el.value && el.value.trim().length >= n) runSubmit(el);
  }

  function firstVisibleTextInput(root) {
    var inputs = root.querySelectorAll("input");
    for (var i = 0; i < inputs.length; i++) {
      var el = inputs[i];
      if (!isTextInput(el)) continue;
      var r = el.getBoundingClientRect();
      if (r.width > 0 && r.height > 0) return el;
    }
    return null;
  }

  function tryFocus(el) {
    try {
      if (el && document.activeElement !== el) el.focus();
    } catch (err) {
      /* ignore */
    }
  }

  // Don't steal focus while the user is typing elsewhere.
  function userIsTyping() {
    var a = document.activeElement;
    return !!(
      a &&
      (a.tagName === "INPUT" || a.tagName === "TEXTAREA" || a.isContentEditable)
    );
  }

  function focusTargets() {
    if (userIsTyping()) return;
    var direct = document.querySelector("[data-autofocus]");
    if (direct && isTextInput(direct)) {
      tryFocus(direct);
      return;
    }
    var containers = document.querySelectorAll("[data-autofocus-first]");
    for (var i = 0; i < containers.length; i++) {
      var input = firstVisibleTextInput(containers[i]);
      if (input) {
        tryFocus(input);
        return;
      }
    }
  }

  document.addEventListener("keydown", onKeyDown, true);
  document.addEventListener("input", onInput, true);

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", focusTargets);
  } else {
    focusTargets();
  }
  try {
    new MutationObserver(focusTargets).observe(document.documentElement, {
      childList: true,
      subtree: true,
    });
  } catch (err) {
    /* observer not available — autofocus still runs on load */
  }

  window.dotnetcloudFormDefaults = {
    focusFirst: focusTargets,
    submit: runSubmit,
  };
})();
