using DotNetCloud.Core.Data.Entities.Settings;

namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents an organization entity in the DotNetCloud system.
/// Organizations are the top-level hierarchical unit that contains teams, groups, and members.
/// </summary>
/// <remarks>
/// Supports multi-tenancy by separating users into distinct organizations.
/// Each organization can have its own settings, teams, and permission structures.
/// Organizations support soft-deletion to preserve referential integrity and audit history.
/// </remarks>
public class Organization
{
    /// <summary>
    /// Gets or sets the unique identifier for the organization.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the organization.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Must be unique within the system.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of the organization.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 1000 characters. Provides additional context about the organization.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the organization was created.
    /// </summary>
    /// <remarks>
    /// Automatically set when the entity is created. Cannot be modified.
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the organization has been soft-deleted.
    /// </summary>
    /// <remarks>
    /// Default is false. When true, the organization is hidden from normal queries but preserved in database.
    /// Enables audit history and referential integrity preservation.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the organization was soft-deleted.
    /// </summary>
    /// <remarks>
    /// Null when IsDeleted is false. Set automatically when organization is soft-deleted.
    /// </remarks>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the collection of teams belonging to this organization.
    /// </summary>
    public ICollection<Team> Teams { get; set; } = new List<Team>();

    /// <summary>
    /// Gets or sets the collection of groups belonging to this organization.
    /// </summary>
    public ICollection<Group> Groups { get; set; } = new List<Group>();

    /// <summary>
    /// Gets or sets the collection of organization members.
    /// </summary>
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();

    /// <summary>
    /// Gets or sets the collection of organization-specific settings.
    /// </summary>
    public ICollection<OrganizationSetting> Settings { get; set; } = new List<OrganizationSetting>();
}
