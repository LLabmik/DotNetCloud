using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Orchestrates content extraction from binary documents using registered <see cref="IContentExtractor"/> implementations.
/// Selects the appropriate extractor based on MIME type and enforces content size limits.
/// </summary>
public sealed class ContentExtractionService
{
    private readonly IEnumerable<IContentExtractor> _extractors;
    private readonly ILogger<ContentExtractionService> _logger;

    /// <summary>Maximum text length to index from extracted content (100KB).</summary>
    public const int MaxContentLength = 102400;

    /// <summary>Initializes a new instance of the <see cref="ContentExtractionService"/> class.</summary>
    public ContentExtractionService(IEnumerable<IContentExtractor> extractors, ILogger<ContentExtractionService> logger)
    {
        _extractors = extractors;
        _logger = logger;
    }

    /// <summary>
    /// Extracts text content from a document stream using the appropriate extractor.
    /// </summary>
    /// <param name="fileStream">The document content stream.</param>
    /// <param name="mimeType">The MIME type of the document.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The extracted content, or null if no extractor supports the MIME type or extraction fails.</returns>
    public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            _logger.LogDebug("No MIME type provided, skipping extraction");
            return null;
        }

        var extractor = _extractors.FirstOrDefault(e => e.CanExtract(mimeType));
        if (extractor is null)
        {
            _logger.LogDebug("No extractor found for MIME type {MimeType}", mimeType);
            return null;
        }

        try
        {
            var result = await extractor.ExtractAsync(fileStream, mimeType, cancellationToken);
            if (result is null)
                return null;

            // Truncate to max length
            var text = result.Text.Length > MaxContentLength
                ? result.Text[..MaxContentLength]
                : result.Text;

            return new ExtractedContent
            {
                Text = text,
                Metadata = result.Metadata
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Content extraction failed for MIME type {MimeType}", mimeType);
            return null;
        }
    }

    /// <summary>
    /// Checks whether any registered extractor supports the given MIME type.
    /// </summary>
    public bool CanExtract(string mimeType)
    {
        return _extractors.Any(e => e.CanExtract(mimeType));
    }
}
