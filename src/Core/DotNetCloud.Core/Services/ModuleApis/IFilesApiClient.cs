using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Files module.
/// </summary>
public interface IFilesApiClient
{
    /// <summary>
    /// Lists one folder of a user's files.
    /// </summary>
    /// <param name="ownerId">The user whose files to list.</param>
    /// <param name="parentId">Folder to list; <see langword="null"/> lists the user's root.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Page size (the module caps it).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder's children, or why they could not be read.</returns>
    Task<FileListResult> ListChildrenAsync(
        Guid ownerId,
        Guid? parentId,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the current content of one of a user's files.
    /// </summary>
    /// <param name="ownerId">The user who owns the file.</param>
    /// <param name="nodeId">The file node.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content stream, or why it could not be read. The caller owns the stream.</returns>
    Task<FileReadResult> OpenReadAsync(
        Guid ownerId,
        Guid nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores content in a user's Files tree as a new file, or as a new version when a file with the
    /// same name already exists in the folder.
    /// </summary>
    /// <param name="ownerId">The user whose files receive the content.</param>
    /// <param name="parentId">Target folder; <see langword="null"/> stores in the user's root.</param>
    /// <param name="fileName">File name, already sanitized by the caller.</param>
    /// <param name="contentType">MIME type.</param>
    /// <param name="content">The bytes to store, read to the end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored node, or why it could not be stored.</returns>
    Task<FileWriteResult> UploadFileAsync(
        Guid ownerId,
        Guid? parentId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

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
