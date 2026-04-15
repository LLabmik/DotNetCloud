using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from plain text files (text/plain, text/csv).
/// </summary>
public sealed class PlainTextExtractor : IContentExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
        "application/json",
        "application/xml",
        "text/yaml",
        "text/css",
        "text/javascript",
        "text/x-python",
        "text/x-shellscript",
        "text/x-csharp",
        "text/x-sql",
        "text/toml",
        "text/typescript"
    };

    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return SupportedMimeTypes.Contains(mimeType);
    }

    /// <inheritdoc />
    public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return new ExtractedContent
        {
            Text = text,
            Metadata = new Dictionary<string, string>
            {
                ["mimeType"] = mimeType
            }
        };
    }
}
