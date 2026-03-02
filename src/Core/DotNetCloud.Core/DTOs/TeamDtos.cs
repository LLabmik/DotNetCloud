namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for team information.
/// </summary>
public class TeamDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the team.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the organization ID this team belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the team name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the team description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time the team was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the team is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time the team was deleted (if applicable).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the count of members in the team.
    /// </summary>
    public int MemberCount { get; set; }
}

/// <summary>
/// Data transfer object for creating a new team.
/// </summary>
public class CreateTeamDto
{
    /// <summary>
    /// Gets or sets the team name (required).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the team description (optional).
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for updating team information.
/// </summary>
public class UpdateTeamDto
{
    /// <summary>
    /// Gets or sets the team name (optional).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the team description (optional).
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for team member information.
/// </summary>
public class TeamMemberDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the team member relationship.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the team ID.
    /// </summary>
    public Guid TeamId { get; set; }

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
    /// Gets or sets the roles assigned to this member within the team.
    /// </summary>
    public ICollection<string> Roles { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the date and time the member was added to the team.
    /// </summary>
    public DateTime AddedAt { get; set; }
}

/// <summary>
/// Data transfer object for adding a member to a team.
/// </summary>
public class AddTeamMemberDto
{
    /// <summary>
    /// Gets or sets the user ID to add (required).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the roles to assign to the member (optional).
    /// </summary>
    public ICollection<string> Roles { get; set; } = new List<string>();
}
