using DotNetCloud.Core.DTOs.Media;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides HTTP range-request streaming for media files (photos, audio, video).
/// Modules use this capability to serve media content with proper partial-content (206)
/// support, enabling seeking in audio/video players and efficient image loading.
/// </summary>
/// <remarks>
/// <para><b>Capability tier:</b> Restricted — requires administrator approval.</para>
/// <para>
/// This interface abstracts the range-request parsing, content-type detection,
/// and partial-content response generation so that individual media modules
/// (Photos, Music, Video) do not need to duplicate streaming logic.
/// </para>
/// </remarks>
public interface IMediaStreamingService : ICapabilityInterface
{
    /// <summary>
    /// Opens a read-only stream to the specified file, positioned for the requested byte range.
    /// Returns metadata needed to build the HTTP 206 Partial Content response.
    /// </summary>
    /// <param name="fileNodeId">The <c>FileNode</c> ID of the media file to stream.</param>
    /// <param name="rangeStart">
    /// Start byte offset (inclusive). <c>null</c> for the beginning of the file.
    /// </param>
    /// <param name="rangeEnd">
    /// End byte offset (inclusive). <c>null</c> to stream to the end of the file.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="MediaStreamResult"/> containing the data stream and response metadata,
    /// or <c>null</c> if the file was not found or the range was unsatisfiable.
    /// </returns>
    Task<MediaStreamResult?> OpenStreamAsync(
        Guid fileNodeId,
        long? rangeStart,
        long? rangeEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the MIME content type for the specified file.
    /// </summary>
    /// <param name="fileName">Original file name with extension.</param>
    /// <returns>The detected MIME type, or <c>"application/octet-stream"</c> as fallback.</returns>
    string DetectContentType(string fileName);
}

/// <summary>
/// Contains the data stream and HTTP response metadata for a range-request response.
/// </summary>
public sealed record MediaStreamResult : IDisposable
{
    /// <summary>
    /// The data stream positioned at <see cref="RangeStart"/>.
    /// The caller is responsible for disposing this stream.
    /// </summary>
    public required Stream Stream { get; init; }

    /// <summary>
    /// MIME content type for the <c>Content-Type</c> response header.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Total file size in bytes (for the <c>Content-Range</c> header).
    /// </summary>
    public required long TotalLength { get; init; }

    /// <summary>
    /// Start byte offset of the range being served (inclusive).
    /// </summary>
    public required long RangeStart { get; init; }

    /// <summary>
    /// End byte offset of the range being served (inclusive).
    /// </summary>
    public required long RangeEnd { get; init; }

    /// <summary>
    /// Whether this is a partial-content response (HTTP 206) as opposed to a full response (HTTP 200).
    /// </summary>
    public bool IsPartial => RangeStart > 0 || RangeEnd < TotalLength - 1;

    /// <summary>
    /// The number of bytes in this range segment.
    /// </summary>
    public long ContentLength => RangeEnd - RangeStart + 1;

    /// <inheritdoc />
    public void Dispose()
    {
        Stream.Dispose();
    }
}
