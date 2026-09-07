namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Captured rich metadata for a single URL in a chat message.
/// Returned by <see cref="ILinkPreviewService"/> for the message service to persist.
/// </summary>
public sealed record LinkPreviewResult
{
    /// <summary>URL the preview was fetched from.</summary>
    public required string Url { get; init; }

    /// <summary>Resolved page title.</summary>
    public string? Title { get; init; }

    /// <summary>Resolved page description.</summary>
    public string? Description { get; init; }

    /// <summary>Preview image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Site name.</summary>
    public string? SiteName { get; init; }

    /// <summary>Favicon URL.</summary>
    public string? FaviconUrl { get; init; }
}

/// <summary>
/// SSRF-safe link preview unfurling for chat messages.
/// Detects the first URL in message content and fetches rich metadata from the page.
/// </summary>
public interface ILinkPreviewService
{
    /// <summary>
    /// Finds the first http(s) URL in the given message content, or <see langword="null"/>
    /// when the content contains no usable URL.
    /// </summary>
    Uri? FindFirstUrl(string? content);

    /// <summary>
    /// Fetches and parses a preview for the given URL. Returns <see langword="null"/> when the
    /// page cannot be fetched safely or contains no extractable metadata. Never throws.
    /// </summary>
    Task<LinkPreviewResult?> FetchPreviewAsync(Uri url, CancellationToken cancellationToken = default);
}
