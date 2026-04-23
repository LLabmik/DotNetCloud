namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents a cross-team permission group within an organization.
/// Groups enable permission management across multiple teams and users.
/// </summary>
/// <remarks>
/// Groups differ from Teams in that:
/// - Teams are organizational units for collaboration and team-scoped roles
/// - Groups are permission containers that span multiple teams
/// 
/// Common use cases:
/// - "AllDevelopers" group containing members from multiple development teams
/// - "Administrators" group with organization-wide admin permissions
/// - "ContentEditors" group with specific content management permissions
/// 
/// Groups enable efficient permission assignment to multiple users at once.
/// </remarks>
public class Group
{
    /// <summary>
    /// The reserved display name for the built-in organization-wide group.
    /// </summary>
    public const string AllUsersGroupName = "All Users";

    /// <summary>
    /// Gets or sets the unique identifier for the group.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the organization that owns this group.
    /// </summary>
    /// <remarks>
    /// Required foreign key. Every group must belong to an organization.
    /// Groups cannot span multiple organizations.
    /// </remarks>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the name of the group.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Should be unique within the organization.
    /// Examples: "AllDevelopers", "ContentEditors", "ProjectManagers", "Administrators"
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of the group's purpose.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 1000 characters. Explains the group's purpose and who should be members.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the built-in group that includes all active organization members.
    /// </summary>
    /// <remarks>
    /// Built-in groups are system-managed and cannot be renamed, deleted, or have explicit membership edited.
    /// Membership is resolved implicitly from active <see cref="OrganizationMember"/> records.
    /// </remarks>
    public bool IsAllUsersGroup { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the group was created.
    /// </summary>
    /// <remarks>
    /// Automatically set when the entity is created. Cannot be modified.
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group has been soft-deleted.
    /// </summary>
    /// <remarks>
    /// Default is false. When true, the group is hidden from normal queries but preserved in database.
    /// Enables audit history and referential integrity preservation.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the group was soft-deleted.
    /// </summary>
    /// <remarks>
    /// Null when IsDeleted is false. Set automatically when group is soft-deleted.
    /// </remarks>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the organization that owns this group.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of group members.
    /// </summary>
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}
