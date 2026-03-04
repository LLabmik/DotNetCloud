namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Records that a user acknowledged an announcement.
/// </summary>
public sealed class AnnouncementAcknowledgement
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Announcement that was acknowledged.</summary>
    public Guid AnnouncementId { get; set; }

    /// <summary>Navigation property to the announcement.</summary>
    public Announcement? Announcement { get; set; }

    /// <summary>User who acknowledged.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the acknowledgement occurred (UTC).</summary>
    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
}
