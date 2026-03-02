namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents a team within an organization.
/// Teams are organizational units that group users together for collaboration and permission management.
/// </summary>
/// <remarks>
/// Teams belong to exactly one organization and can have multiple members.
/// Teams support soft-deletion to preserve audit history and referential integrity.
/// Common use cases: Development Team, Marketing Team, Support Team, etc.
/// </remarks>
public class Team
{
    /// <summary>
    /// Gets or sets the unique identifier for the team.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the organization that owns this team.
    /// </summary>
    /// <remarks>
    /// Required foreign key. Every team must belong to an organization.
    /// </remarks>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the name of the team.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Should be unique within the organization.
    /// Examples: "Engineering", "Sales", "Support", "Marketing"
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of the team.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 1000 characters. Provides additional context about the team's purpose.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the team was created.
    /// </summary>
    /// <remarks>
    /// Automatically set when the entity is created. Cannot be modified.
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the team has been soft-deleted.
    /// </summary>
    /// <remarks>
    /// Default is false. When true, the team is hidden from normal queries but preserved in database.
    /// Enables audit history and referential integrity preservation.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the team was soft-deleted.
    /// </summary>
    /// <remarks>
    /// Null when IsDeleted is false. Set automatically when team is soft-deleted.
    /// </remarks>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the organization that owns this team.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of team members.
    /// </summary>
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
