namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for organization information.
/// </summary>
public class OrganizationDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the organization.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the organization name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the organization description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time the organization was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the organization is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time the organization was deleted (if applicable).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the count of members in the organization.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the count of teams in the organization.
    /// </summary>
    public int TeamCount { get; set; }
}

/// <summary>
/// Data transfer object for creating a new organization.
/// </summary>
public class CreateOrganizationDto
{
    /// <summary>
    /// Gets or sets the organization name (required).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the organization description (optional).
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for updating organization information.
/// </summary>
public class UpdateOrganizationDto
{
    /// <summary>
    /// Gets or sets the organization name (optional).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the organization description (optional).
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for an organization member.
/// </summary>
public class OrganizationMemberDto
{
    /// <summary>
    /// Gets or sets the user's unique identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the user joined the organization.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the membership is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the organization-scoped role IDs assigned to this member.
    /// </summary>
    public IReadOnlyList<Guid> RoleIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Gets or sets the human-readable role names for display.
    /// </summary>
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Data transfer object for adding a user to an organization.
/// </summary>
public class AddOrganizationMemberDto
{
    /// <summary>
    /// Gets or sets the user ID to add.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets optional org role IDs to assign. Defaults to [OrgMember] if empty.
    /// </summary>
    public List<Guid>? RoleIds { get; set; }
}

/// <summary>
/// Data transfer object for setting an organization member's roles.
/// </summary>
public class SetOrgMemberRolesDto
{
    /// <summary>
    /// Gets or sets the complete list of role IDs to assign.
    /// </summary>
    public List<Guid> RoleIds { get; set; } = new();
}
