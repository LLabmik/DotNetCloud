using DotNetCloud.Core.DTOs.Media;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Files module.
/// </summary>
public interface IFilesApiClient
{
    /// <summary>
    /// Scans virtual file nodes for media files matching the given media type.
    /// Returns discovered file candidates without performing indexing.
    /// </summary>
    /// <param name="sources">The media library sources to scan.</param>
    /// <param name="ownerId">User ID whose files to scan.</param>
    /// <param name="mediaType">Type of media to scan for: "Photos", "Music", "Video", or "All".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with discovered file candidates.</returns>
    Task<MediaScanCandidatesResult> ScanMediaFoldersAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default);
}
