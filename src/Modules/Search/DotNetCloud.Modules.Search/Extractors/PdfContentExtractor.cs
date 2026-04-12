using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using UglyToad.PdfPig;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from PDF documents using PdfPig.
/// </summary>
public sealed class PdfContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        using var document = PdfDocument.Open(fileStream);

        metadata["pageCount"] = document.NumberOfPages.ToString();

        if (document.Information?.Author is { } author && !string.IsNullOrWhiteSpace(author))
            metadata["author"] = author;

        if (document.Information?.Title is { } title && !string.IsNullOrWhiteSpace(title))
            metadata["title"] = title;

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        var result = new ExtractedContent
        {
            Text = sb.ToString(),
            Metadata = metadata
        };

        return Task.FromResult<ExtractedContent?>(result);
    }
}
