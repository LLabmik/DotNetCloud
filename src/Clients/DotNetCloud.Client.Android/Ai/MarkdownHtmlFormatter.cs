using System.Text.RegularExpressions;
using Markdig;

namespace DotNetCloud.Client.Android.Ai;

/// <summary>
/// Renders Markdown to a self-contained, dark-themed HTML document for display in a
/// <see cref="Microsoft.Maui.Controls.WebView"/>. Pure and deterministic so it can be
/// unit-tested without a UI. Raw HTML in the source is escaped (never passed through).
/// </summary>
public static class MarkdownHtmlFormatter
{
    /// <summary>
    /// CommonMark superset with tables, task lists, autolinks, etc. Raw HTML is escaped by
    /// the <c>DisableHtml()</c> pipeline extension — no sanitizer dependency needed.
    /// </summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    // Block constructs that the lightweight FormattedString renderer cannot display.
    private static readonly Regex[] BlockPatterns =
    [
        // Fenced code block.
        new(@"^```", RegexOptions.Compiled | RegexOptions.Multiline),
        // ATX heading.
        new(@"^#{1,6}\s", RegexOptions.Compiled | RegexOptions.Multiline),
        // Unordered list (also covers task lists).
        new(@"^\s*[-*+]\s", RegexOptions.Compiled | RegexOptions.Multiline),
        // Ordered list.
        new(@"^\s*\d{1,3}[.)]\s", RegexOptions.Compiled | RegexOptions.Multiline),
        // Blockquote.
        new(@"^\s*>\s?", RegexOptions.Compiled | RegexOptions.Multiline),
        // Table row.
        new(@"^\s*\|.*\|", RegexOptions.Compiled | RegexOptions.Multiline),
        // Horizontal rule.
        new(@"^\s*(---|\*\*\*|___)\s*$", RegexOptions.Compiled | RegexOptions.Multiline),
    ];

    /// <summary>Renders <paramref name="markdown"/> as a full HTML document. Never returns <c>null</c>.</summary>
    public static string ToHtmlDocument(string? markdown)
    {
        var body = string.IsNullOrEmpty(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline);
        return HtmlTemplate.Replace("{content}", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether <paramref name="markdown"/> contains block-level constructs (headings, lists,
    /// tables, quotes, code fences, rules) that require the HTML/WebView renderer. Inline-only
    /// text stays on the lightweight <see cref="FormattedString"/> path.
    /// </summary>
    public static bool NeedsHtmlRendering(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return false;

        foreach (var pattern in BlockPatterns)
        {
            if (pattern.IsMatch(markdown))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Dark theme that mirrors the AI chat bubbles (<c>#1E293B</c> background, <c>#F1F5F9</c>
    /// text, <c>#0EA5E9</c> accent). CSP blocks all external resource loading; only inline
    /// styles are allowed. The <c>{content}</c> placeholder is replaced by the rendered body.
    /// </summary>
    private const string HtmlTemplate =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
        <style>
        body{margin:0;padding:0;background:#1E293B;color:#F1F5F9;font-family:-apple-system,Roboto,'Segoe UI',sans-serif;font-size:14px;line-height:1.55;overflow-wrap:break-word}
        h1,h2,h3,h4,h5,h6{color:#F1F5F9;margin:.7em 0 .35em;line-height:1.25}
        h1{font-size:1.45em}h2{font-size:1.3em}h3{font-size:1.18em}h4,h5,h6{font-size:1.05em}
        p{margin:.5em 0}
        ul,ol{margin:.5em 0;padding-left:1.35em}
        li{margin:.2em 0}
        blockquote{margin:.6em 0;padding:.3em .9em;border-left:3px solid #0EA5E9;color:#CBD5E1;background:#16213A;border-radius:4px}
        code{font-family:monospace,'Courier New',monospace;background:#0F172A;padding:.1em .35em;border-radius:4px;font-size:.9em;color:#E2E8F0}
        pre{background:#0F172A;padding:.7em .9em;border-radius:8px;overflow-x:auto;margin:.6em 0}
        pre code{background:transparent;padding:0;font-size:.88em}
        a{color:#38BDF8;text-decoration:underline}
        table{border-collapse:collapse;margin:.6em 0;width:100%}
        th,td{border:1px solid #334155;padding:.35em .6em;text-align:left}
        th{background:#16213A;color:#E2E8F0}
        tr:nth-child(even) td{background:#16213A}
        hr{border:none;border-top:1px solid #334155;margin:.8em 0}
        input[type="checkbox"]{margin-right:.4em}
        strong{color:#FFFFFF}
        </style>
        </head>
        <body>
        {content}
        </body>
        </html>
        """;
}
