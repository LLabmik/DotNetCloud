using DotNetCloud.Core.DTOs.Media;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Extracts metadata from media files (photos, audio, video).
/// Implementations are registered per <see cref="MediaType"/> and invoked
/// when new media files are uploaded or during library scan operations.
/// </summary>
/// <remarks>
/// <para><b>Capability tier:</b> Public — automatically granted to all modules.</para>
/// <para>
/// The framework uses a provider pattern: multiple extractors can be registered,
/// keyed by the <see cref="MediaType"/> they handle. The correct extractor is
/// selected based on the file's MIME type.
/// </para>
/// </remarks>
public interface IMediaMetadataExtractor : ICapabilityInterface
{
    /// <summary>
    /// The media type this extractor handles.
    /// </summary>
    MediaType SupportedMediaType { get; }

    /// <summary>
    /// Returns <c>true</c> if this extractor can process the given MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type to check (e.g. "image/jpeg", "audio/flac").</param>
    bool CanExtract(string mimeType);

    /// <summary>
    /// Extracts metadata from the media file at the specified path.
    /// </summary>
    /// <param name="filePath">Absolute path to the media file on disk.</param>
    /// <param name="mimeType">MIME type of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Extracted metadata, or <c>null</c> if the file could not be parsed.
    /// </returns>
    Task<MediaMetadataDto?> ExtractAsync(
        string filePath,
        string mimeType,
        CancellationToken cancellationToken = default);
}
