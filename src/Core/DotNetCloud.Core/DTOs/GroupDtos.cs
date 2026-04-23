namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for group information.
/// </summary>
public class GroupDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the group.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the organization ID this group belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the group description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time the group was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time the group was deleted, if applicable.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group is the built-in organization-wide group.
    /// </summary>
    public bool IsAllUsersGroup { get; set; }

    /// <summary>
    /// Gets or sets the count of members in the group.
    /// </summary>
    public int MemberCount { get; set; }
}

/// <summary>
/// Data transfer object for creating a new group.
/// </summary>
public class CreateGroupDto
{
    /// <summary>
    /// Gets or sets the organization ID the group should belong to.
    /// Use <see cref="Guid.Empty"/> to target the default organization.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the optional group description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for updating group information.
/// </summary>
public class UpdateGroupDto
{
    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the group description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for group member information.
/// </summary>
public class GroupMemberDto
{
    /// <summary>
    /// Gets or sets the group ID.
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string UserDisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's email.
    /// </summary>
    public string UserEmail { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the member was added to the group.
    /// </summary>
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who added this member, if recorded.
    /// </summary>
    public Guid? AddedByUserId { get; set; }
}

/// <summary>
/// Data transfer object for adding a member to a group.
/// </summary>
public class AddGroupMemberDto
{
    /// <summary>
    /// Gets or sets the user ID to add.
    /// </summary>
    public Guid UserId { get; set; }
}