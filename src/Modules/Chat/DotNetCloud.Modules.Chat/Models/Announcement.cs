namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents an organization-wide announcement.
/// </summary>
public sealed class Announcement
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Organization this announcement belongs to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>User who authored the announcement.</summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>Announcement title.</summary>
    public required string Title { get; set; }

    /// <summary>Announcement body content (Markdown).</summary>
    public required string Content { get; set; }

    /// <summary>Priority level.</summary>
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    /// <summary>When the announcement was published (UTC).</summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the announcement expires (UTC). Null for no expiry.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether the announcement is pinned at the top.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Whether users must acknowledge this announcement.</summary>
    public bool RequiresAcknowledgement { get; set; }

    /// <summary>Whether the announcement is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the announcement was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Acknowledgements for this announcement.</summary>
    public ICollection<AnnouncementAcknowledgement> Acknowledgements { get; set; } = [];
}

/// <summary>
/// Priority level for announcements.
/// </summary>
public enum AnnouncementPriority
{
    /// <summary>Regular priority.</summary>
    Normal,

    /// <summary>Important — highlighted in the UI.</summary>
    Important,

    /// <summary>Urgent — triggers visual/audio notification.</summary>
    Urgent
}
