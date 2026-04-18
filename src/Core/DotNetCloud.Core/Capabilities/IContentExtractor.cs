using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Extracts plain text from binary document formats for full-text search indexing.
/// Implementations handle specific MIME types (PDF, DOCX, XLSX, etc.).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tier:</b> Restricted — used internally by the Search module.
/// </para>
/// <para>
/// The content extraction pipeline selects the appropriate extractor based on
/// <see cref="CanExtract"/>. Extracted text is stored in the search index alongside
/// the document's metadata.
/// </para>
/// </remarks>
public interface IContentExtractor : ICapabilityInterface
{
    /// <summary>
    /// Determines whether this extractor can handle the given MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type to check (e.g., "application/pdf").</param>
    /// <returns>True if this extractor supports the MIME type.</returns>
    bool CanExtract(string mimeType);

    /// <summary>
    /// Extracts plain text and metadata from a document stream.
    /// </summary>
    /// <param name="fileStream">The document content stream.</param>
    /// <param name="mimeType">The MIME type of the document.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The extracted content, or null if extraction fails.</returns>
    Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default);
}
