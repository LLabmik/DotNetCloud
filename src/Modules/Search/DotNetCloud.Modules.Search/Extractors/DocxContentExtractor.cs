using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from DOCX documents using Open XML SDK.
/// </summary>
public sealed class DocxContentExtractor : IContentExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-word.document.macroEnabled.12"
    };

    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return SupportedMimeTypes.Contains(mimeType);
    }

    /// <inheritdoc />
    public Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return Task.FromResult<ExtractedContent?>(null);
        }

        // Extract core properties
        var props = document.PackageProperties;
        if (props.Creator is { } author && !string.IsNullOrWhiteSpace(author))
            metadata["author"] = author;
        if (props.Title is { } title && !string.IsNullOrWhiteSpace(title))
            metadata["title"] = title;

        // Extract all paragraphs
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = paragraph.InnerText;
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
