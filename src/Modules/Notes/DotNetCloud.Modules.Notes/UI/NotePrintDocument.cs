using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.Modules.Notes.UI;

/// <summary>
/// Everything the browser needs to print a single note: a plain-text title and the note's
/// Markdown rendered to sanitized HTML.
/// </summary>
/// <param name="Title">Plain-text note title. The printer HTML-escapes it before writing it out.</param>
/// <param name="Html">Sanitized HTML rendered from the note's Markdown body.</param>
public sealed record NotePrintPayload(string Title, string Html);

/// <summary>
/// Builds the print payload for a note. Kept separate from the page so the rendering rules
/// (title fallback, Markdown → sanitized HTML) can be unit tested without a browser.
/// </summary>
public static class NotePrintDocument
{
    /// <summary>JavaScript entry point (<c>dom-helpers.js</c>) that prints the payload.</summary>
    public const string JavaScriptFunction = "dotnetcloudDom.printHtml";

    /// <summary>Title used when a note has no title, so the printed page is never headerless.</summary>
    public const string FallbackTitle = "Note";

    /// <summary>
    /// Renders a note for printing. The body is produced by <see cref="IMarkdownRenderer"/>,
    /// which sanitizes the HTML, so only the note's own (safe) markup reaches the printer.
    /// </summary>
    /// <param name="note">Note to print.</param>
    /// <param name="renderer">Markdown renderer used to format the note body.</param>
    /// <returns>The title and formatted HTML body to print.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static NotePrintPayload Create(NoteDto note, IMarkdownRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(renderer);

        var title = string.IsNullOrWhiteSpace(note.Title) ? FallbackTitle : note.Title.Trim();
        var content = note.Content ?? string.Empty;

        // Plain-text notes are escaped verbatim (the printer CSS keeps their line breaks);
        // Markdown goes through the sanitizing renderer so formatting survives the print.
        var html = note.Format == NoteContentFormat.PlainText
            ? $"<div class=\"print-plaintext\">{System.Net.WebUtility.HtmlEncode(content)}</div>"
            : renderer.RenderToHtml(content);

        return new NotePrintPayload(title, html);
    }
}
