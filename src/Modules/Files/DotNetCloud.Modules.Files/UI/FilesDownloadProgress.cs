namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Builds the text and timing for the modal shown while the server prepares a multi-item
/// ZIP download. Building the archive can take seconds, and the browser fetch gives no other
/// feedback, so the modal is the only indication that anything is happening.
/// </summary>
internal static class FilesDownloadProgress
{
    /// <summary>
    /// Shortest time the preparation modal stays on screen. Blazor Server coalesces renders, so a
    /// fast ZIP would otherwise never reach the browser as a visible modal — it would appear to
    /// open and close in the same frame.
    /// </summary>
    internal const int MinimumVisibleMs = 700;

    /// <summary>
    /// How long (in milliseconds) the preparation modal must be held back after the work finished
    /// so that it is actually seen. Returns <c>0</c> once preparation has already taken
    /// <see cref="MinimumVisibleMs"/> or longer, so slow downloads are never delayed.
    /// </summary>
    /// <param name="elapsedMs">Time preparation has taken so far, in milliseconds.</param>
    internal static int GetHoldTimeMs(int elapsedMs)
        => Math.Max(0, MinimumVisibleMs - elapsedMs);

    /// <summary>
    /// Describes what is being prepared, e.g. <c>"Preparing download…"</c> for a single item or
    /// <c>"Preparing 5 items for download…"</c> for a multi-item selection.
    /// </summary>
    /// <param name="count">Number of items included in the download.</param>
    internal static string BuildStatus(int count)
        => count <= 1
            ? "Preparing download…"
            : $"Preparing {count} items for download…";
}
