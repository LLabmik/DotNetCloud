using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages whole-file storage for immutable media. Media (photos, music, video) is stored as a
/// single blob at the version's <see cref="FileVersion.StoragePath"/> instead of content-addressed
/// chunks, so the read path can hand out the blob's stream directly with no reassembly copy.
/// </summary>
public interface IWholeFileStorageService
{
    /// <summary>
    /// Returns <see langword="true"/> when a file with the given MIME type / name should be stored as
    /// a whole-file blob, honouring the <c>WholeFileMediaStorage</c> option gate.
    /// </summary>
    /// <param name="mimeType">MIME type reported by the client, if any.</param>
    /// <param name="fileName">File name, used for extension-based fallback.</param>
    bool ShouldStoreWholeFile(string? mimeType, string? fileName);

    /// <summary>
    /// Converts a chunked version into a whole-file blob: reassembles its chunks into a single blob,
    /// atomically flips <see cref="FileVersion.IsChunked"/> to <see langword="false"/>, and removes
    /// the version's chunk mappings while decrementing their reference counts.
    /// </summary>
    /// <remarks>
    /// Idempotent and safe to call concurrently (including from separate processes): the DB-gated
    /// flag flip ensures only one caller performs the cleanup, and the blob write is atomic. Returns
    /// <see langword="false"/> — leaving the version chunked and readable — when the version is
    /// missing, already whole-file, not eligible media, or the write fails.
    /// </remarks>
    /// <param name="versionId">The version to convert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when this call performed the conversion.</returns>
    Task<bool> ConvertVersionToWholeFileAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the whole-file blob for a version that has already been converted.
    /// </summary>
    /// <param name="version">The whole-file version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A seekable read stream, or <see langword="null"/> when the blob is missing.</returns>
    Task<Stream?> OpenWholeFileStreamAsync(FileVersion version, CancellationToken cancellationToken = default);
}
