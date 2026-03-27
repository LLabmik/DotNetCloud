window.dotnetcloudFilePreview = window.dotnetcloudFilePreview || (function () {
    "use strict";

    async function fetchTextContent(url) {
        const response = await fetch(url, { credentials: "include" });
        if (!response.ok) {
            throw new Error("Failed to fetch: " + response.status);
        }
        return await response.text();
    }

    async function saveTextContent(url, text) {
        const response = await fetch(url, {
            method: "PUT",
            credentials: "include",
            headers: { "Content-Type": "text/plain; charset=utf-8" },
            body: text
        });
        return response.ok;
    }

    function highlightCode(codeElement) {
        if (!codeElement || !(codeElement instanceof HTMLElement)) {
            return;
        }

        if (typeof hljs !== "undefined") {
            // Reset any previous highlighting so hljs re-processes.
            if (codeElement.dataset) {
                delete codeElement.dataset.highlighted;
            }
            hljs.highlightElement(codeElement);
        }
    }

    return {
        fetchTextContent: fetchTextContent,
        saveTextContent: saveTextContent,
        highlightCode: highlightCode
    };
})();
