using System.Globalization;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>
/// Renders a lightweight subset of Markdown into a <see cref="FormattedString"/>.
/// Supports fenced code blocks, inline code, bold, italic, and links. Deterministic
/// and pure so it can be unit-tested without a UI.
/// </summary>
public static class MarkdownFormatter
{
    private static readonly char[] SpecialChars = { '`', '*', '[' };

    /// <summary>Formats the given Markdown text into spans. Never returns <c>null</c>.</summary>
    public static FormattedString Format(string? markdown)
    {
        var result = new FormattedString();
        if (string.IsNullOrEmpty(markdown))
            return result;

        int i = 0;
        while (i < markdown.Length)
        {
            // Fenced code block: ```lang\n ... \n```
            if (i + 2 < markdown.Length && markdown[i] == '`' && markdown[i + 1] == '`' && markdown[i + 2] == '`')
            {
                var end = markdown.IndexOf("```", i + 3, StringComparison.Ordinal);
                if (end < 0)
                {
                    result.Spans.Add(CodeSpan(markdown[i..].Trim('\n', '\r')));
                    break;
                }

                // Skip the language tag on the opening fence line.
                var contentStart = markdown.IndexOf('\n', i + 3);
                var contentStartIdx = contentStart < 0 ? i + 3 : contentStart + 1;
                var code = markdown[contentStartIdx..end];
                result.Spans.Add(CodeSpan(code.Trim('\n', '\r')));
                i = end + 3;
                continue;
            }

            // Inline code: `code`
            if (markdown[i] == '`')
            {
                var end = markdown.IndexOf('`', i + 1);
                if (end < 0)
                {
                    result.Spans.Add(CodeSpan(markdown[i..]));
                    break;
                }

                result.Spans.Add(CodeSpan(markdown[(i + 1)..end]));
                i = end + 1;
                continue;
            }

            // Link: [text](url)
            if (markdown[i] == '[')
            {
                var close = markdown.IndexOf(']', i + 1);
                if (close > i + 1 && close + 1 < markdown.Length && markdown[close + 1] == '(')
                {
                    var parenClose = markdown.IndexOf(')', close + 2);
                    if (parenClose > close + 2)
                    {
                        var text = markdown[(i + 1)..close];
                        var url = markdown[(close + 2)..parenClose];
                        result.Spans.Add(LinkSpan(text, url));
                        i = parenClose + 1;
                        continue;
                    }
                }
            }

            // Bold: **text**
            if (markdown[i] == '*' && i + 1 < markdown.Length && markdown[i + 1] == '*')
            {
                var end = markdown.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    result.Spans.Add(BoldSpan(markdown[(i + 2)..end]));
                    i = end + 2;
                    continue;
                }
            }

            // Italic: *text*
            if (markdown[i] == '*')
            {
                var end = markdown.IndexOf('*', i + 1);
                if (end > i + 1)
                {
                    result.Spans.Add(ItalicSpan(markdown[(i + 1)..end]));
                    i = end + 1;
                    continue;
                }
            }

            // Plain run up to the next special character.
            var next = markdown.IndexOfAny(SpecialChars, i + 1);
            if (next < 0)
            {
                result.Spans.Add(PlainSpan(markdown[i..]));
                break;
            }

            result.Spans.Add(PlainSpan(markdown[i..next]));
            i = next;
        }

        return result;
    }

    private static Span PlainSpan(string text) => new() { Text = text };

    private static Span CodeSpan(string text) => new()
    {
        Text = text,
        FontFamily = "monospace",
        TextColor = Color.FromArgb("#E2E8F0"),
        BackgroundColor = Color.FromArgb("#1E293B")
    };

    private static Span BoldSpan(string text) => new() { Text = text, FontAttributes = FontAttributes.Bold };

    private static Span ItalicSpan(string text) => new() { Text = text, FontAttributes = FontAttributes.Italic };

    private static Span LinkSpan(string text, string url) => new()
    {
        Text = text,
        TextColor = Color.FromArgb("#0EA5E9"),
        TextDecorations = TextDecorations.Underline
    };
}

/// <summary>XAML value converter that renders Markdown via <see cref="MarkdownFormatter"/>.</summary>
public sealed class MarkdownConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        MarkdownFormatter.Format(value as string);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
