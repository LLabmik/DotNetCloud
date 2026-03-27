namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Renders Markdown content to sanitized HTML safe for browser display.
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Converts Markdown to sanitized HTML, stripping dangerous elements and attributes.
    /// </summary>
    /// <param name="markdown">Raw Markdown content.</param>
    /// <returns>Sanitized HTML string safe for rendering in a browser.</returns>
    string RenderToHtml(string markdown);

    /// <summary>
    /// Sanitizes raw HTML content without Markdown processing.
    /// Useful when content is already HTML or mixed format.
    /// </summary>
    /// <param name="html">Raw HTML content.</param>
    /// <returns>Sanitized HTML string safe for rendering in a browser.</returns>
    string SanitizeHtml(string html);

    /// <summary>
    /// Generates a plain-text excerpt from Markdown content for previews and search results.
    /// </summary>
    /// <param name="markdown">Raw Markdown content.</param>
    /// <param name="maxLength">Maximum character length of the excerpt.</param>
    /// <returns>Plain-text excerpt stripped of all formatting.</returns>
    string GetPlainTextExcerpt(string markdown, int maxLength = 200);
}
