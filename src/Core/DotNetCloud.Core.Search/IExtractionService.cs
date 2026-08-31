using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Search;

/// <summary>
/// Abstraction over the out-of-process content extraction worker.
/// </summary>
public interface IExtractionService
{
    /// <summary>
    /// Extracts plain text from binary document content.
    /// Returns null if no extractor supports the MIME type or extraction fails.
    /// </summary>
    Task<ExtractedContent?> ExtractAsync(byte[] content, string mimeType, CancellationToken cancellationToken = default);
}
