using System.IO.Compression;
using System.Text;
using System.Xml;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from OpenDocument Format (ODF) files.
/// ODF files are ZIP archives containing XML; text is extracted from content.xml.
/// Supports .odt (text), .ods (spreadsheet), .odp (presentation), and .odg (graphics).
/// </summary>
public sealed class OdfContentExtractor : IContentExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.spreadsheet",
        "application/vnd.oasis.opendocument.presentation",
        "application/vnd.oasis.opendocument.graphics"
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

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);

        // Extract metadata from meta.xml
        var metaEntry = archive.GetEntry("meta.xml");
        if (metaEntry is not null)
        {
            ExtractMetadata(metaEntry, metadata, cancellationToken);
        }

        // Extract text from content.xml
        var contentEntry = archive.GetEntry("content.xml");
        if (contentEntry is null)
        {
            return Task.FromResult<ExtractedContent?>(null);
        }

        ExtractContentText(contentEntry, sb, cancellationToken);

        var result = new ExtractedContent
        {
            Text = sb.ToString(),
            Metadata = metadata
        };

        return Task.FromResult<ExtractedContent?>(result);
    }

    private static void ExtractMetadata(ZipArchiveEntry metaEntry, Dictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        using var stream = metaEntry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType != XmlNodeType.Element) continue;

            switch (reader.LocalName)
            {
                case "initial-creator" or "creator":
                    var author = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(author))
                        metadata["author"] = author;
                    break;

                case "title":
                    var title = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(title))
                        metadata["title"] = title;
                    break;

                case "page-count":
                    var pageCount = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(pageCount))
                        metadata["pageCount"] = pageCount;
                    break;
            }
        }
    }

    private static void ExtractContentText(ZipArchiveEntry contentEntry, StringBuilder sb, CancellationToken cancellationToken)
    {
        using var stream = contentEntry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

        // Track whether we're inside text-bearing elements.
        // ODF uses namespaces like text:p, text:h, table:table-cell, draw:text-box.
        // We extract all text nodes and add paragraph breaks at element boundaries.
        var insideTextElement = false;
        var currentParagraph = new StringBuilder();

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    // Paragraph-level elements that should produce line breaks
                    if (reader.LocalName is "p" or "h" or "title" or "subtitle")
                    {
                        insideTextElement = true;
                        currentParagraph.Clear();
                    }
                    // Tab/space elements within paragraphs
                    else if (reader.LocalName is "tab" or "s" && insideTextElement)
                    {
                        currentParagraph.Append(' ');
                    }
                    // Line break
                    else if (reader.LocalName == "line-break" && insideTextElement)
                    {
                        currentParagraph.Append(' ');
                    }
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.SignificantWhitespace:
                    if (insideTextElement)
                    {
                        currentParagraph.Append(reader.Value);
                    }
                    break;

                case XmlNodeType.EndElement:
                    if (reader.LocalName is "p" or "h" or "title" or "subtitle")
                    {
                        insideTextElement = false;
                        var text = currentParagraph.ToString().Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            sb.AppendLine(text);
                        }
                    }
                    break;
            }
        }
    }
}
