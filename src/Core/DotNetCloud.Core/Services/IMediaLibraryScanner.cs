namespace DotNetCloud.Core.Services;

/// <summary>
/// Scans local directories for media files and imports them into the system.
/// </summary>
public interface IMediaLibraryScanner
{
    /// <summary>
    /// Scans a directory for media files and imports them.
    /// </summary>
    /// <param name="directoryPath">Absolute path to the directory to scan.</param>
    /// <param name="ownerId">User ID that will own the imported files.</param>
    /// <param name="mediaType">Type of media to scan for: "Photos", "Music", or "Video".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with counts of imported and skipped files.</returns>
    Task<MediaScanResult> ScanAsync(string directoryPath, Guid ownerId, string mediaType, CancellationToken cancellationToken = default);
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

    /// <summary>Error messages for failed imports.</summary>
    public List<string> Errors { get; set; } = [];
}
