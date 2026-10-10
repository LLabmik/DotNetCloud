namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Records the EXIF capture timestamp (<c>FileNode.CapturedAtUtc</c>) for media nodes, so photo
/// listings can show when a picture was taken rather than when it was uploaded.
/// </summary>
public interface IMediaCaptureTimeService
{
    /// <summary>
    /// Stores <paramref name="capturedAtUtc"/> on the node when it currently has no value.
    /// This is a no-op that returns <c>false</c> when the node does not exist or already has a
    /// capture timestamp — an existing value is never overwritten.
    /// </summary>
    /// <param name="fileNodeId">The file node to update.</param>
    /// <param name="capturedAtUtc">The EXIF capture timestamp, normalised to UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the column was filled; <c>false</c> when nothing changed.</returns>
    Task<bool> TrySetCaptureTimeAsync(
        Guid fileNodeId,
        DateTime capturedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the EXIF capture timestamp from image content and records it. A no-op (returning
    /// <c>false</c>) for non-images, when the node already has a capture timestamp, and when the
    /// content carries no usable EXIF date. Failures are logged and swallowed — capture time is a
    /// best-effort enhancement and must never break the caller's primary operation.
    /// </summary>
    /// <param name="fileNodeId">The file node to update.</param>
    /// <param name="content">A readable stream containing the image bytes.</param>
    /// <param name="mimeType">The node's MIME type, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a capture timestamp was recorded; otherwise <c>false</c>.</returns>
    Task<bool> TryCaptureFromStreamAsync(
        Guid fileNodeId,
        Stream content,
        string? mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload that reads the image from a node's stored whole-file blob. A no-op when
    /// the blob does not exist (e.g. media still stored as chunks).
    /// </summary>
    /// <param name="fileNodeId">The file node to update.</param>
    /// <param name="storagePath">The node's storage path (whole-file blob location), or <c>null</c>.</param>
    /// <param name="mimeType">The node's MIME type, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a capture timestamp was recorded; otherwise <c>false</c>.</returns>
    Task<bool> TryCaptureFromStorageAsync(
        Guid fileNodeId,
        string? storagePath,
        string? mimeType,
        CancellationToken cancellationToken = default);
}
