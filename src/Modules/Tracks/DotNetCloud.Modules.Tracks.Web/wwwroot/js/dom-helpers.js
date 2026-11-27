/**
 * CSP-safe DOM helpers for Blazor JS interop.
 *
 * The app's Content-Security-Policy does not allow 'unsafe-eval', so
 * `Js.InvokeVoidAsync("eval", "...")` throws an EvalError in the browser.
 * Every DOM action that used to be an eval() string lives here as a named
 * function instead.
 */
window.dotnetcloudDom = window.dotnetcloudDom || (function () {
    "use strict";

    /**
     * Clicks the first element matching a CSS selector, if any.
     * @param {string} selector - CSS selector.
     */
    function click(selector) {
        var el = document.querySelector(selector);
        if (el && typeof el.click === "function") {
            el.click();
        }
    }

    /**
     * Scrolls the first element matching a CSS selector into view.
     * @param {string} selector - CSS selector.
     * @param {string} [block] - Scroll alignment: "start", "center", "end" or "nearest".
     */
    function scrollIntoView(selector, block) {
        var el = document.querySelector(selector);
        if (el && typeof el.scrollIntoView === "function") {
            el.scrollIntoView({ block: block || "nearest" });
        }
    }

    /**
     * Triggers a browser download from a base64 payload without navigating away.
     * @param {string} base64 - Base64-encoded file contents.
     * @param {string} fileName - Suggested file name.
     * @param {string} [mimeType] - MIME type of the payload (default application/octet-stream).
     */
    function downloadData(base64, fileName, mimeType) {
        triggerDownload(
            "data:" + (mimeType || "application/octet-stream") + ";base64," + base64,
            fileName);
    }

    /**
     * Triggers a browser download of a URL without navigating away.
     * @param {string} url - Download URL or data URI.
     * @param {string} [fileName] - Suggested file name; omitted lets Content-Disposition decide.
     */
    function triggerDownload(url, fileName) {
        var a = document.createElement("a");
        a.href = url;
        a.download = fileName || "";
        a.style.display = "none";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }

    return {
        click: click,
        scrollIntoView: scrollIntoView,
        downloadData: downloadData,
        triggerDownload: triggerDownload
    };
})();
