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
    /// <returns>The node ID of the uploaded file.</returns>
    Task<Guid> UploadAsync(
        Guid? existingNodeId,
        string localPath,
        Stream fileStream,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken = default);

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
