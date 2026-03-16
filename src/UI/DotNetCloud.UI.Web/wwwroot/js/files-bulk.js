window.dotnetcloudFiles = window.dotnetcloudFiles || {};

/**
 * Downloads multiple files/folders as a ZIP by POSTing node IDs to the server.
 * Uses fetch + Blob to trigger a browser download.
 * @param {string} url - The download-zip endpoint URL.
 * @param {string[]} nodeIds - Array of node ID GUIDs to include.
 */
window.dotnetcloudFiles.downloadZip = async function (url, nodeIds) {
    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ nodeIds: nodeIds })
        });

        if (!response.ok) {
            console.error('ZIP download failed:', response.status, response.statusText);
            return;
        }

        const blob = await response.blob();
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = blobUrl;
        a.download = 'download.zip';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(blobUrl);
    } catch (err) {
        console.error('ZIP download error:', err);
    }
};
