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

    /**
     * Escapes a value for safe insertion into HTML text or an attribute.
     * @param {string} value - Raw value.
     * @returns {string} HTML-escaped value.
     */
    function escapeHtml(value) {
        return String(value === undefined || value === null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    /** Print-only typography. Inline styles are allowed by the app CSP (style-src 'unsafe-inline'). */
    var PRINT_STYLES = [
        "@page { margin: 18mm; }",
        "html, body { margin: 0; padding: 0; background: #fff; color: #111; }",
        "body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 11.5pt; line-height: 1.55; }",
        ".print-title { font-size: 20pt; line-height: 1.25; margin: 0 0 10pt; padding-bottom: 6pt; border-bottom: 1px solid #d4d4d8; }",
        ".print-content h1, .print-content h2, .print-content h3, .print-content h4, .print-content h5, .print-content h6 { line-height: 1.25; margin: 16pt 0 6pt; page-break-after: avoid; }",
        ".print-content h1 { font-size: 16pt; } .print-content h2 { font-size: 14pt; } .print-content h3 { font-size: 12.5pt; }",
        ".print-content p, .print-content ul, .print-content ol, .print-content blockquote, .print-content pre, .print-content table { margin: 0 0 8pt; }",
        ".print-content ul, .print-content ol { padding-left: 18pt; }",
        ".print-content li { margin: 2pt 0; }",
        ".print-content blockquote { border-left: 3pt solid #d4d4d8; padding: 2pt 0 2pt 10pt; color: #3f3f46; }",
        ".print-content code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 10pt; background: #f4f4f5; padding: 1pt 3pt; border-radius: 2pt; }",
        ".print-content pre { background: #f4f4f5; padding: 8pt; border-radius: 3pt; white-space: pre-wrap; word-wrap: break-word; page-break-inside: avoid; }",
        ".print-content pre code { background: none; padding: 0; font-size: 10pt; }",
        ".print-content table { border-collapse: collapse; width: 100%; }",
        ".print-content th, .print-content td { border: 1px solid #d4d4d8; padding: 4pt 6pt; text-align: left; }",
        ".print-content img { max-width: 100%; height: auto; }",
        ".print-content a { color: #1d4ed8; text-decoration: underline; }",
        ".print-content input[type=checkbox] { margin-right: 4pt; }",
        ".print-content .print-plaintext { white-space: pre-wrap; word-wrap: break-word; }",
        ".print-content hr { border: none; border-top: 1px solid #d4d4d8; margin: 12pt 0; }"
    ].join("\n");

    /**
     * Prints a sanitized HTML fragment (with a title) through a hidden iframe, so only the
     * supplied markup reaches the printer and the app page itself is never reprinted.
     * @param {string} html - Sanitized HTML body to print.
     * @param {string} [title] - Document title, also printed as the heading.
     */
    function printHtml(html, title) {
        var docTitle = (title === undefined || title === null || title === "") ? "Document" : String(title);

        var frame = document.createElement("iframe");
        frame.setAttribute("aria-hidden", "true");
        frame.setAttribute("tabindex", "-1");
        frame.style.position = "fixed";
        frame.style.right = "0";
        frame.style.bottom = "0";
        frame.style.width = "0";
        frame.style.height = "0";
        frame.style.border = "0";
        frame.style.visibility = "hidden";
        document.body.appendChild(frame);

        var cleaned = false;
        function cleanup() {
            if (cleaned) {
                return;
            }
            cleaned = true;
            if (frame.parentNode) {
                frame.parentNode.removeChild(frame);
            }
        }

        var doc = frame.contentWindow.document;
        doc.open();
        doc.write(
            "<!doctype html><html><head><meta charset=\"utf-8\">" +
            "<title>" + escapeHtml(docTitle) + "</title>" +
            "<style>" + PRINT_STYLES + "</style>" +
            "</head><body>" +
            "<h1 class=\"print-title\">" + escapeHtml(docTitle) + "</h1>" +
            "<div class=\"print-content\">" + (html || "") + "</div>" +
            "</body></html>");
        doc.close();

        frame.contentWindow.addEventListener("afterprint", cleanup);

        window.setTimeout(function () {
            try {
                frame.contentWindow.focus();
                frame.contentWindow.print();
            } catch (e) {
                cleanup();
                throw e;
            }
            // Some browsers never fire afterprint inside a hidden frame; clean up eventually.
            window.setTimeout(cleanup, 60000);
        }, 200);
    }

    return {
        click: click,
        scrollIntoView: scrollIntoView,
        downloadData: downloadData,
        triggerDownload: triggerDownload,
        escapeHtml: escapeHtml,
        printHtml: printHtml
    };
})();
