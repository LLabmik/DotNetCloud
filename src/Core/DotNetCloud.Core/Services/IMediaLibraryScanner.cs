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
