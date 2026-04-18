using System.Text.RegularExpressions;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from HTML files by stripping HTML tags and decoding entities.
/// </summary>
public sealed partial class HtmlContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var html = await reader.ReadToEndAsync(cancellationToken);

        var plainText = StripHtml(html);

        return new ExtractedContent
        {
            Text = plainText,
            Metadata = new Dictionary<string, string>
            {
                ["mimeType"] = mimeType
            }
        };
    }

    internal static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var text = html;

        // Remove script and style blocks entirely
        text = ScriptBlockRegex().Replace(text, " ");
        text = StyleBlockRegex().Replace(text, " ");

        // Remove HTML comments
        text = HtmlCommentRegex().Replace(text, " ");

        // Replace block-level tags with newlines for readability
        text = BlockTagRegex().Replace(text, "\n");

        // Remove remaining HTML tags
        text = HtmlTagRegex().Replace(text, " ");

        // Decode common HTML entities
        text = text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&apos;", "'")
            .Replace("&nbsp;", " ");

        // Decode numeric entities
        text = NumericEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, out var code) && code is > 0 and < 0x10FFFF)
                return char.ConvertFromUtf32(code);
            return m.Value;
        });

        text = HexEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var code) && code is > 0 and < 0x10FFFF)
                return char.ConvertFromUtf32(code);
            return m.Value;
        });

        // Collapse multiple whitespace
        text = MultipleWhitespaceRegex().Replace(text, " ");
        text = MultipleNewlinesRegex().Replace(text, "\n");

        return text.Trim();
    }

    [GeneratedRegex(@"<script\b[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex ScriptBlockRegex();

    [GeneratedRegex(@"<style\b[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"<\s*/?\s*(?:div|p|br|h[1-6]|li|tr|td|th|blockquote|pre|hr|section|article|header|footer|nav|main)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"<\/?[a-zA-Z][^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"&#(\d+);", RegexOptions.Compiled)]
    private static partial Regex NumericEntityRegex();

    [GeneratedRegex(@"&#x([0-9a-fA-F]+);", RegexOptions.Compiled)]
    private static partial Regex HexEntityRegex();

    [GeneratedRegex(@"[^\S\n]+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultipleNewlinesRegex();
}
