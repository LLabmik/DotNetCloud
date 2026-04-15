namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Handles chunked upload and download with SHA-256 deduplication and transfer resumption.
/// </summary>
public interface IChunkedTransferClient
{
    /// <summary>
    /// Uploads a file using the chunked upload protocol.
    /// Only uploads chunks not already present on the server (deduplication).
    /// </summary>
    /// <param name="existingNodeId">Existing node ID for updates; null for new files.</param>
    /// <param name="localPath">Full path to the local file.</param>
    /// <param name="fileStream">Open, readable stream of the file.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="stateDatabasePath">Path to the local state database for crash-resilient session persistence. Null disables persistence.</param>
    /// <param name="posixMode">POSIX permission bitmask to send with the upload. Null on Windows or non-Linux systems.</param>
    /// <param name="posixOwnerHint">POSIX owner/group hint ("user:group"). Null if not known.</param>
    /// <param name="parentFolderId">Server-side parent folder ID. Null places file at root.</param>
    /// <returns>The upload result containing the node ID and content hash of the uploaded file.</returns>
    Task<UploadResult> UploadAsync(
        Guid? existingNodeId,
        string localPath,
        Stream fileStream,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken = default,
        string? stateDatabasePath = null,
        int? posixMode = null,
        string? posixOwnerHint = null,
        Guid? parentFolderId = null);

    /// <summary>
    /// Downloads a file using chunk-level delta sync.
    /// Only downloads chunks that differ from the local version.
    /// </summary>
    /// <param name="nodeId">Server node ID to download.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream with the file content.</returns>
    Task<Stream> DownloadAsync(
        Guid nodeId,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific version of a file.
    /// </summary>
    Task<Stream> DownloadVersionAsync(
        Guid nodeId,
        int versionNumber,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
