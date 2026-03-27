using Ganss.Xss;
using Markdig;

namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Renders Markdown to sanitized HTML using Markdig and HtmlSanitizer.
/// Strips script tags, event handlers, javascript: URLs, iframes, and other XSS vectors.
/// </summary>
public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownRenderer"/> class
    /// with a preconfigured Markdig pipeline and HtmlSanitizer policy.
    /// </summary>
    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmojiAndSmiley()
            .Build();

        _sanitizer = CreateSanitizer();
    }

    /// <inheritdoc />
    public string RenderToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var rawHtml = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(rawHtml);
    }

    /// <inheritdoc />
    public string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }

    /// <inheritdoc />
    public string GetPlainTextExcerpt(string markdown, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var plainText = Markdig.Markdown.ToPlainText(markdown, _pipeline);
        plainText = plainText.Trim();

        if (plainText.Length <= maxLength)
        {
            return plainText;
        }

        // Cut at the last word boundary before maxLength
        var cutoff = plainText.LastIndexOf(' ', maxLength);
        if (cutoff <= 0)
        {
            cutoff = maxLength;
        }

        return string.Concat(plainText.AsSpan(0, cutoff), "…");
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // ── Allowed tags ──────────────────────────────────────────────
        // Start clean and add only what Markdown output needs
        sanitizer.AllowedTags.Clear();

        // Block elements
        foreach (var tag in new[]
        {
            "p", "div", "br", "hr",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "blockquote", "pre", "code",
            "ul", "ol", "li",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td",
            "dl", "dt", "dd",
            "details", "summary",
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        // Inline elements
        foreach (var tag in new[]
        {
            "a", "img",
            "strong", "b", "em", "i", "u", "s", "del", "ins",
            "mark", "sub", "sup", "small",
            "span", "abbr", "kbd",
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        // Task-list checkboxes
        sanitizer.AllowedTags.Add("input");

        // ── Allowed attributes ────────────────────────────────────────
        sanitizer.AllowedAttributes.Clear();

        foreach (var attr in new[]
        {
            "class", "id", "title", "alt",
            "href", "src",
            "colspan", "rowspan", "scope",
            "start", "type",       // ol start number, list type
            "open",                // details element
            "width", "height",     // img dimensions
        })
        {
            sanitizer.AllowedAttributes.Add(attr);
        }

        // Task-list checkbox attributes
        sanitizer.AllowedAttributes.Add("checked");
        sanitizer.AllowedAttributes.Add("disabled");

        // ── Allowed URL schemes ───────────────────────────────────────
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        // ── Allowed CSS properties (minimal) ─────────────────────────
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("text-align");
        sanitizer.AllowedCssProperties.Add("color");
        sanitizer.AllowedCssProperties.Add("background-color");

        return sanitizer;
    }
}
