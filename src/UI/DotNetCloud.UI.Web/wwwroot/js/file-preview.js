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

    return { fetchTextContent: fetchTextContent, saveTextContent: saveTextContent };
})();
