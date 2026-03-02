using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents a user's membership in a permission group.
/// Junction entity that connects users to groups for cross-team permission management.
/// </summary>
/// <remarks>
/// Uses composite key (GroupId, UserId) to ensure each user appears only once per group.
/// Unlike TeamMember, GroupMember does not store roles - the group itself defines permissions.
/// Groups are used for efficient permission assignment across multiple teams and users.
/// </remarks>
public class GroupMember
{
    /// <summary>
    /// Gets or sets the unique identifier of the group.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the user was added to the group.
    /// </summary>
    /// <remarks>
    /// Automatically set when the membership is created.
    /// Used for auditing and tracking membership history.
    /// </remarks>
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user who added this member to the group.
    /// </summary>
    /// <remarks>
    /// Optional. Tracks who granted this permission for audit purposes.
    /// Null if added by system or during initial setup.
    /// </remarks>
    public Guid? AddedByUserId { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the group that this membership belongs to.
    /// </summary>
    public Group Group { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who is a member of this group.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who added this member to the group.
    /// </summary>
    /// <remarks>
    /// Null if added by system or during initial setup.
    /// </remarks>
    public ApplicationUser? AddedByUser { get; set; }
}
