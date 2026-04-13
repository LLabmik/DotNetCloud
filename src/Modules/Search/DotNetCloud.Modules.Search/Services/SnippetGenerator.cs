using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Generates search result snippets with highlighted matching terms using <c>&lt;mark&gt;</c> tags.
/// All output is HTML-encoded first to prevent XSS, then highlight marks are applied.
/// </summary>
public static partial class SnippetGenerator
{
    private const int DefaultSnippetLength = 200;
    private const int ContextChars = 60;

    /// <summary>
    /// Generates a snippet from content with matching terms highlighted using <c>&lt;mark&gt;</c> tags.
    /// </summary>
    /// <param name="content">The full content text to extract a snippet from.</param>
    /// <param name="parsedQuery">The parsed search query containing terms and phrases to highlight.</param>
    /// <param name="maxLength">Maximum length of the snippet (before HTML tags).</param>
    /// <returns>An HTML-safe snippet with <c>&lt;mark&gt;</c> highlighted terms.</returns>
    public static string Generate(string? content, ParsedSearchQuery? parsedQuery, int maxLength = DefaultSnippetLength)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        if (parsedQuery is null || !parsedQuery.HasSearchableContent)
        {
            var truncated = content.Length > maxLength ? content[..maxLength] + "..." : content;
            return HttpUtility.HtmlEncode(truncated);
        }

        // Collect all highlight terms (phrases first, then individual terms)
        var highlightTerms = new List<string>();
        highlightTerms.AddRange(parsedQuery.Phrases);
        highlightTerms.AddRange(parsedQuery.Terms);

        if (highlightTerms.Count == 0)
        {
            var truncated = content.Length > maxLength ? content[..maxLength] + "..." : content;
            return HttpUtility.HtmlEncode(truncated);
        }

        // Find the best window in the content centered around the first match
        var snippet = ExtractBestWindow(content, highlightTerms, maxLength);

        // HTML-encode the snippet first (XSS prevention)
        var encoded = HttpUtility.HtmlEncode(snippet);

        // Apply highlighting with <mark> tags on the encoded text
        encoded = ApplyHighlighting(encoded, highlightTerms);

        return encoded;
    }

    /// <summary>
    /// Generates a highlighted title with matching terms wrapped in <c>&lt;mark&gt;</c> tags.
    /// </summary>
    /// <param name="title">The original title text.</param>
    /// <param name="parsedQuery">The parsed search query containing terms and phrases to highlight.</param>
    /// <returns>An HTML-safe title with <c>&lt;mark&gt;</c> highlighted terms.</returns>
    public static string HighlightTitle(string? title, ParsedSearchQuery? parsedQuery)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        if (parsedQuery is null || !parsedQuery.HasSearchableContent)
            return HttpUtility.HtmlEncode(title);

        var highlightTerms = new List<string>();
        highlightTerms.AddRange(parsedQuery.Phrases);
        highlightTerms.AddRange(parsedQuery.Terms);

        if (highlightTerms.Count == 0)
            return HttpUtility.HtmlEncode(title);

        var encoded = HttpUtility.HtmlEncode(title);
        return ApplyHighlighting(encoded, highlightTerms);
    }

    private static string ExtractBestWindow(string content, List<string> terms, int maxLength)
    {
        // Find the position of the first matching term
        var bestIndex = -1;
        foreach (var term in terms)
        {
            var idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                bestIndex = idx;
                break;
            }
        }

        if (bestIndex < 0)
        {
            // No match found, return beginning of content
            return content.Length > maxLength ? content[..maxLength] + "..." : content;
        }

        // Center the window around the match
        var start = Math.Max(0, bestIndex - ContextChars);
        var end = Math.Min(content.Length, start + maxLength);

        // Adjust start to avoid cutting mid-word
        if (start > 0)
        {
            var spaceIdx = content.IndexOf(' ', start);
            if (spaceIdx >= 0 && spaceIdx < start + 20)
            {
                start = spaceIdx + 1;
            }
        }

        // Adjust end to avoid cutting mid-word
        if (end < content.Length)
        {
            var spaceIdx = content.LastIndexOf(' ', end);
            if (spaceIdx > start && spaceIdx > end - 20)
            {
                end = spaceIdx;
            }
        }

        var snippet = content[start..end];
        var sb = new StringBuilder();

        if (start > 0) sb.Append("...");
        sb.Append(snippet);
        if (end < content.Length) sb.Append("...");

        return sb.ToString();
    }

    private static string ApplyHighlighting(string encoded, List<string> terms)
    {
        foreach (var term in terms)
        {
            // HTML-encode the term for matching against encoded text
            var encodedTerm = HttpUtility.HtmlEncode(term);
            if (string.IsNullOrEmpty(encodedTerm)) continue;

            // Use regex for case-insensitive replace, escaping regex special chars
            var pattern = Regex.Escape(encodedTerm);
            encoded = Regex.Replace(
                encoded,
                pattern,
                m => $"<mark>{m.Value}</mark>",
                RegexOptions.IgnoreCase);
        }

        return encoded;
    }
}
