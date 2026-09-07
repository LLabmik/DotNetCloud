namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Rich link preview metadata captured from the first URL in a chat message.
/// Fetched server-side at send time with SSRF-safe URL retrieval.
/// </summary>
public sealed class MessageLinkPreview
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Foreign key to the parent message.</summary>
    public Guid MessageId { get; set; }

    /// <summary>URL the preview was fetched from.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolved page title (OG/Twitter title, falling back to the HTML title).</summary>
    public string? Title { get; set; }

    /// <summary>Resolved page description (OG/Twitter description, falling back to meta description).</summary>
    public string? Description { get; set; }

    /// <summary>Preview image URL (OG image or Twitter card image).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Site name extracted from OG or Twitter card metadata.</summary>
    public string? SiteName { get; set; }

    /// <summary>Favicon URL for display next to the site name.</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>When the preview was fetched (UTC).</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent message navigation property.</summary>
    public Message? Message { get; set; }
}
