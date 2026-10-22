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

    /// <summary>
    /// Immediately provisions administrator-managed default content for a newly created user.
    /// Failures are reported, never thrown: account creation must not depend on this call.
    /// </summary>
    /// <param name="userId">The new user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProvisionUserDefaultsResult> ProvisionUserDefaultsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a convergence pass for a batch of users. Used by the start-up sweep and daily pass.
    /// </summary>
    /// <param name="userIds">Users to converge (the call is chunked internally).</param>
    /// <param name="allowDeletions">Whether the pass may delete content (automatic passes never do).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<EnqueueProvisioningResult> EnqueueProvisioningAsync(
        IReadOnlyCollection<Guid> userIds,
        bool allowDeletions = false,
        CancellationToken cancellationToken = default);
}
