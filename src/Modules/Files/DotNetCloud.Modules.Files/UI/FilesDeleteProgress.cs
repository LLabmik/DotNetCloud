namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Builds the progress text shown next to the spinner while files are being moved to the trash
/// (file browser) or permanently deleted (trash bin).
/// </summary>
internal static class FilesDeleteProgress
{
    /// <summary>
    /// Shortest time the progress state stays on screen for a delete. Blazor Server coalesces
    /// renders, so without a floor a delete that finishes in a few milliseconds never reaches the
    /// browser at all — the dialog would appear to close with no spinner.
    /// </summary>
    internal const int MinimumVisibleMs = 1000;

    /// <summary>
    /// How long (in milliseconds) the progress state must be held back after the work finished so
    /// that it is actually seen. Returns <c>0</c> once the delete has already taken
    /// <see cref="MinimumVisibleMs"/> or longer, so slow deletes are never delayed.
    /// </summary>
    /// <param name="elapsedMs">Time the delete has taken so far, in milliseconds.</param>
    internal static int GetHoldTimeMs(int elapsedMs)
        => Math.Max(0, MinimumVisibleMs - elapsedMs);

    /// <summary>
    /// Describes the item currently being deleted, e.g. <c>"Deleting report.pdf…"</c> for a single
    /// item or <c>"Deleting 2 of 5: report.pdf…"</c> for a multi-item delete.
    /// The name is optional — when it cannot be resolved, the text only counts the items.
    /// </summary>
    /// <param name="index">Zero-based index of the item currently being processed.</param>
    /// <param name="count">Total number of items in the operation.</param>
    /// <param name="name">Display name of the current item, if known.</param>
    internal static string BuildStatus(int index, int count, string? name)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);

        if (count <= 1)
        {
            return hasName ? $"Deleting {name}…" : "Deleting…";
        }

        return hasName
            ? $"Deleting {index + 1} of {count}: {name}…"
            : $"Deleting {index + 1} of {count}…";
    }
}
