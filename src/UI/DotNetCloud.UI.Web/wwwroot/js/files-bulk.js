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
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ nodeIds: nodeIds }),
    });

    if (!response.ok) {
      let code = "DOWNLOAD_FAILED";
      let message = "The ZIP download failed.";
      try {
        const envelope = await response.json();
        code =
          envelope && envelope.error && envelope.error.code
            ? envelope.error.code
            : code;
        message =
          envelope && envelope.error && envelope.error.message
            ? envelope.error.message
            : message;
      } catch (e) {
        // Non-JSON error body — keep the default message.
      }
      return { ok: false, code: code, message: message };
    }

    const blob = await response.blob();
    const blobUrl = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = blobUrl;
    a.download = "download.zip";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(blobUrl);
    return { ok: true };
  } catch (err) {
    return { ok: false, code: "DOWNLOAD_FAILED", message: String(err) };
  }
};
