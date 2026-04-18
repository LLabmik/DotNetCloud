using System.Text.RegularExpressions;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from Markdown files by stripping Markdown syntax.
/// </summary>
public sealed partial class MarkdownContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType, "text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var markdown = await reader.ReadToEndAsync(cancellationToken);

        var plainText = StripMarkdown(markdown);

        return new ExtractedContent
        {
            Text = plainText,
            Metadata = new Dictionary<string, string>
            {
                ["mimeType"] = mimeType
            }
        };
    }

    internal static string StripMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var text = markdown;

        // Remove code blocks
        text = CodeBlockRegex().Replace(text, " ");

        // Remove inline code
        text = InlineCodeRegex().Replace(text, "$1");

        // Remove images
        text = ImageRegex().Replace(text, "$1");

        // Remove links but keep text
        text = LinkRegex().Replace(text, "$1");

        // Remove headings markers
        text = HeadingRegex().Replace(text, "$1");

        // Remove bold/italic markers
        text = BoldItalicRegex().Replace(text, "$1");
        text = BoldRegex().Replace(text, "$1");
        text = ItalicRegex().Replace(text, "$1");

        // Remove strikethrough
        text = StrikethroughRegex().Replace(text, "$1");

        // Remove blockquotes markers
        text = BlockquoteRegex().Replace(text, "$1");

        // Remove list markers
        text = UnorderedListRegex().Replace(text, "$1");
        text = OrderedListRegex().Replace(text, "$1");

        // Remove horizontal rules
        text = HorizontalRuleRegex().Replace(text, " ");

        // Collapse whitespace
        text = WhitespaceRegex().Replace(text, " ");

        return text.Trim();
    }

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`([^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^\)]+\)", RegexOptions.Compiled)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^\)]+\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled)]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\*(.+?)\*", RegexOptions.Compiled)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"~~(.+?)~~", RegexOptions.Compiled)]
    private static partial Regex StrikethroughRegex();

    [GeneratedRegex(@"^>\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^[\*\-\+]\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex UnorderedListRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^[\-\*_]{3,}$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
