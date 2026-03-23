namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// Represents a sharing grant on a contact (user or team scoped).
/// </summary>
public sealed class ContactShare
{
    /// <summary>Unique identifier for this share.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The contact being shared.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the shared contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>User who created this share.</summary>
    public Guid SharedByUserId { get; set; }

    /// <summary>User receiving the share (null if team-scoped).</summary>
    public Guid? SharedWithUserId { get; set; }

    /// <summary>Team receiving the share (null if user-scoped).</summary>
    public Guid? SharedWithTeamId { get; set; }

    /// <summary>Permission level of the share.</summary>
    public ContactSharePermission Permission { get; set; } = ContactSharePermission.ReadOnly;

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional expiration for time-limited shares.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>User ID who last updated this share record (for audit).</summary>
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// Permission levels for contact sharing.
/// </summary>
public enum ContactSharePermission
{
    /// <summary>Read-only access to the contact.</summary>
    ReadOnly,

    /// <summary>Read and write access to the contact.</summary>
    ReadWrite
}
