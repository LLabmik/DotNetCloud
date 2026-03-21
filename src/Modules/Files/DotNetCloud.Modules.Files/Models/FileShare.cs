namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a share of a file or folder with another user, team, or via public link.
/// Supports read-only, read-write, and custom permission levels.
/// </summary>
public sealed class FileShare
{
    /// <summary>Unique identifier for this share.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The file or folder being shared.</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Navigation property to the shared node.</summary>
    public FileNode? FileNode { get; set; }

    /// <summary>Type of share (User, Team, Group, PublicLink).</summary>
    public ShareType ShareType { get; set; }

    /// <summary>
    /// Target user ID (for User shares).
    /// Null for public link or team/group shares.
    /// </summary>
    public Guid? SharedWithUserId { get; set; }

    /// <summary>
    /// Target team ID (for Team shares).
    /// Null for user or public link shares.
    /// </summary>
    public Guid? SharedWithTeamId { get; set; }

    /// <summary>
    /// Target group ID (for Group shares).
    /// Null for user, team, or public link shares.
    /// </summary>
    public Guid? SharedWithGroupId { get; set; }

    /// <summary>Permission level granted by this share.</summary>
    public SharePermission Permission { get; set; } = SharePermission.Read;

    /// <summary>
    /// Public link token (for PublicLink shares).
    /// Used to construct the shareable URL.
    /// </summary>
    public string? LinkToken { get; set; }

    /// <summary>Optional password for public link shares (hashed).</summary>
    public string? LinkPasswordHash { get; set; }

    /// <summary>Maximum number of downloads allowed (null = unlimited).</summary>
    public int? MaxDownloads { get; set; }

    /// <summary>Current download count for public link shares.</summary>
    public int DownloadCount { get; set; }

    /// <summary>Expiration date for the share (null = never expires).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the expiry notification was sent (null = not yet sent).</summary>
    public DateTime? ExpiryNotificationSentAt { get; set; }

    /// <summary>User who created this share.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional note attached to the share.</summary>
    public string? Note { get; set; }
}
