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
