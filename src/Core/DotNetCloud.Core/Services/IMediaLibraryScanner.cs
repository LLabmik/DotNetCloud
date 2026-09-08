using DotNetCloud.Core.DTOs.Media;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Scans directories or virtual folders for media files and triggers module indexing.
/// </summary>
public interface IMediaLibraryScanner
{
    /// <summary>
    /// Scans a local filesystem directory for media files and imports them.
    /// </summary>
    /// <param name="directoryPath">Absolute path to the directory to scan.</param>
    /// <param name="ownerId">User ID that will own the imported files.</param>
    /// <param name="mediaType">Type of media to scan for: "Photos", "Music", or "Video".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with counts of imported and skipped files.</returns>
    Task<MediaScanResult> ScanAsync(string directoryPath, Guid ownerId, string mediaType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans an existing DotNetCloud Files virtual folder for media files and triggers module indexing.
    /// Files are already stored in the Files module; this finds matching ones and publishes indexing events.
    /// </summary>
    /// <param name="folderId">The Files module folder ID to scan, or null for root.</param>
    /// <param name="ownerId">User ID whose files to scan.</param>
    /// <param name="mediaType">Type of media to scan for: "Photos", "Music", or "Video".</param>
    /// <param name="progress">Optional progress reporter for real-time scan updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with counts of found and indexed files.</returns>
    Task<MediaScanResult> ScanFolderAsync(Guid? folderId, Guid ownerId, string mediaType, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans one or more persisted media-library sources and triggers module indexing.
    /// </summary>
    /// <param name="sources">The enabled media-library sources to scan.</param>
    /// <param name="ownerId">User ID whose personal media library should be updated.</param>
    /// <param name="mediaType">Type of media to scan for: "Photos", "Music", or "Video".</param>
    /// <param name="progress">Optional progress reporter for real-time scan updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with counts of found and indexed files.</returns>
    Task<MediaScanResult> ScanSourcesAsync(IReadOnlyCollection<MediaLibrarySource> sources, Guid ownerId, string mediaType, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers new (not yet indexed) media files across the given sources without importing them.
    /// Read-only — no indexing, removal, or other mutation of module data is performed.
    /// </summary>
    /// <param name="sources">The enabled media-library sources to inspect.</param>
    /// <param name="ownerId">User ID whose personal media library should be checked.</param>
    /// <param name="mediaType">Type of media to look for: "Photos", "Music", or "Video".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery result with the count of new (unindexed) files found.</returns>
    Task<MediaDiscoveryResult> DiscoverNewMediaFilesAsync(IReadOnlyCollection<MediaLibrarySource> sources, Guid ownerId, string mediaType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts how many of the user's indexed library items would no longer be reachable if the
    /// library were limited to <paramref name="remainingSources"/> — i.e., items whose backing files
    /// exist only under sources that were removed. Used to show a confirmation count before the user
    /// deletes a media source. Read-only — no indexing or deletion is performed.
    /// </summary>
    /// <param name="remainingSources">The media-library sources that would remain after removal.</param>
    /// <param name="ownerId">User ID whose personal media library should be checked.</param>
    /// <param name="mediaType">Type of media to inspect: "Photos", "Music", or "Video".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of indexed library items that would become unreachable.</returns>
    Task<int> CountLibraryItemsNotInSourcesAsync(IReadOnlyCollection<MediaLibrarySource> remainingSources, Guid ownerId, string mediaType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes indexed library items whose backing files are no longer reachable under
    /// <paramref name="remainingSources"/> (they exist only under sources the user removed). This is
    /// the cleanup counterpart of a source removal — it mirrors the "deleted files" prune that a full
    /// scan performs, without re-indexing or importing anything. The actual media files are never
    /// affected; only indexed library metadata is removed.
    /// </summary>
    /// <param name="remainingSources">The media-library sources that remain after removal.</param>
    /// <param name="ownerId">User ID whose personal media library should be pruned.</param>
    /// <param name="mediaType">Type of media to prune: "Photos", "Music", or "Video".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of library items removed.</returns>
    Task<int> RemoveLibraryItemsNotInSourcesAsync(IReadOnlyCollection<MediaLibrarySource> remainingSources, Guid ownerId, string mediaType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a media library scan operation.
/// </summary>
public sealed class MediaScanResult
{
    /// <summary>Total media files found in the directory.</summary>
    public int TotalFound { get; set; }

    /// <summary>Files successfully imported.</summary>
    public int Imported { get; set; }

    /// <summary>Files skipped (already imported).</summary>
    public int Skipped { get; set; }

    /// <summary>Files that failed to import.</summary>
    public int Failed { get; set; }

    /// <summary>Tracks removed because their source files were deleted.</summary>
    public int Removed { get; set; }

    /// <summary>Error messages for failed imports.</summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Real-time progress update during a media library scan.
/// </summary>
public sealed class MediaScanProgress
{
    /// <summary>Current scan phase description.</summary>
    public string Phase { get; init; } = "Scanning";

    /// <summary>Name of the file currently being processed.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Number of files discovered so far during the discovery phase.</summary>
    public int FilesDiscovered { get; init; }

    /// <summary>Number of files processed so far.</summary>
    public int FilesProcessed { get; init; }

    /// <summary>Total number of files to process.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Number of files successfully imported.</summary>
    public int Imported { get; init; }

    /// <summary>Number of files that failed to import.</summary>
    public int Failed { get; init; }

    /// <summary>Number of tracks removed (source files deleted).</summary>
    public int Removed { get; init; }

    /// <summary>Percentage complete (0-100).</summary>
    public int PercentComplete { get; init; }
}
