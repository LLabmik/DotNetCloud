namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a shared video with another user or publicly.
/// </summary>
public sealed class VideoShare
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The video being shared.</summary>
    public Guid VideoId { get; set; }

    /// <summary>The user who shared the video.</summary>
    public Guid SharedByUserId { get; set; }

    /// <summary>The user the video is shared with (null for public link).</summary>
    public Guid? SharedWithUserId { get; set; }

    /// <summary>Permission level: "readonly" or "full".</summary>
    public required string Permission { get; set; }

    /// <summary>Optional share token for public links.</summary>
    public string? ShareToken { get; set; }

    /// <summary>When the share expires (null for no expiry).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
