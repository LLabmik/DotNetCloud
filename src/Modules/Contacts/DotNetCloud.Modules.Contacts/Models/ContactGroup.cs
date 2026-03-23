namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// A named group of contacts (e.g., "Family", "Colleagues").
/// </summary>
public sealed class ContactGroup
{
    /// <summary>Unique identifier for this group.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this group.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Display name for the group.</summary>
    public required string Name { get; set; }

    /// <summary>Whether this group is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the group was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the group was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the group was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Members of this group (join table).</summary>
    public ICollection<ContactGroupMember> Members { get; set; } = [];
}
