namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// Result of extracting text content from a binary document (PDF, DOCX, XLSX, etc.).
/// </summary>
public sealed record ExtractedContent
{
    /// <summary>The extracted plain-text content.</summary>
    public required string Text { get; init; }

    /// <summary>Optional metadata extracted from the document (author, title, page count, etc.).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
