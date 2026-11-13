using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>
/// Code-behind for the open thread: message bodies and attachments.
/// </summary>
/// <remarks>
/// An email's HTML is untrusted, so it is never placed in this document. Each message body becomes the
/// <c>srcdoc</c> of a nested frame with <c>sandbox=""</c>, which blocks scripts, forms and same-origin
/// access — that frame is the sanitizer. A message without HTML falls back to its plain-text preview,
/// escaped into paragraphs.
/// </remarks>
public partial class ThreadDetailPanel : EmailPanelBase
{
    private void Back()
    {
        State.SelectedThread = null;
        State.Messages.Clear();
        State.View = EmailView.Mailbox;
    }

    private static string BuildBodyDocument(MessageEntry entry)
    {
        var inner = string.IsNullOrWhiteSpace(entry.BodyHtml)
            ? PlainTextToHtml(entry.Message.BodyPreview)
            : entry.BodyHtml;

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><base target=\"_blank\">"
            + "<style>body{margin:0;padding:8px;font:14px/1.5 system-ui,sans-serif;color:#1f2933;"
            + "background:#fff;overflow-wrap:anywhere}img{max-width:100%;height:auto}"
            + "blockquote{margin:0 0 0 12px;padding-left:10px;border-left:2px solid #dfe3e8;color:#5f6b7a}"
            + "a{color:#2563eb}</style></head><body>"
            + inner
            + "</body></html>";
    }

    /// <summary>Converts an escaped plain-text body into paragraphs, keeping <c>&gt;</c> quotes.</summary>
    /// <param name="text">The plain-text preview.</param>
    /// <returns>Minimal HTML.</returns>
    private static string PlainTextToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var output = new List<string>();
        var depth = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var quoteDepth = 0;
            var content = line;

            while (content.StartsWith('>'))
            {
                quoteDepth++;
                content = content[1..].TrimStart();
            }

            if (quoteDepth > 0)
            {
                while (depth > quoteDepth)
                {
                    output.Add("</blockquote>");
                    depth--;
                }

                while (depth < quoteDepth)
                {
                    output.Add("<blockquote>");
                    depth++;
                }

                output.Add("<p>" + WebUtility.HtmlEncode(content) + "</p>");
                continue;
            }

            while (depth > 0)
            {
                output.Add("</blockquote>");
                depth--;
            }

            if (Regex.IsMatch(line, "^[-=_]{3,}$"))
            {
                output.Add("<hr>");
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                output.Add("<br>");
            }
            else
            {
                output.Add("<p>" + WebUtility.HtmlEncode(line) + "</p>");
            }
        }

        while (depth > 0)
        {
            output.Add("</blockquote>");
            depth--;
        }

        return string.Join(string.Empty, output);
    }

    private static string FormatDate(DateTime? value)
    {
        if (value is not { } date || date == default)
        {
            return string.Empty;
        }

        var local = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime()
            : date.ToLocalTime();

        return local.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        return bytes < 1024L * 1024 * 1024
            ? $"{bytes / (1024d * 1024):F1} MB"
            : $"{bytes / (1024d * 1024 * 1024):F2} GB";
    }

    private static string FileTypeIcon(string? contentType)
    {
        var type = (contentType ?? string.Empty).ToLowerInvariant();

        return type switch
        {
            _ when type.Length == 0 => "attach_file",
            _ when type.StartsWith("image/", StringComparison.Ordinal) => "image",
            "application/pdf" => "picture_as_pdf",
            _ when type.StartsWith("text/", StringComparison.Ordinal) => "text_snippet",
            _ when type.StartsWith("audio/", StringComparison.Ordinal) => "music_note",
            _ when type.StartsWith("video/", StringComparison.Ordinal) => "movie",
            _ when type.Contains("word", StringComparison.Ordinal) || type.Contains("document", StringComparison.Ordinal) => "article",
            _ when type.Contains("sheet", StringComparison.Ordinal) || type.Contains("excel", StringComparison.Ordinal) || type.Contains("csv", StringComparison.Ordinal) => "table_chart",
            _ when type.Contains("presentation", StringComparison.Ordinal) || type.Contains("powerpoint", StringComparison.Ordinal) => "slideshow",
            _ when type.Contains("zip", StringComparison.Ordinal) || type.Contains("gzip", StringComparison.Ordinal) || type.Contains("tar", StringComparison.Ordinal) => "folder",
            _ => "attach_file",
        };
    }
}
