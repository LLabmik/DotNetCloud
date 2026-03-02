using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.Core.Data.Entities.Identity;

/// <summary>
/// Represents a role in the DotNetCloud system.
/// Extends ASP.NET Core Identity's IdentityRole with application-specific properties.
/// Uses Guid as the primary key type.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>
    /// Gets or sets a description of the role's purpose and permissions.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this role is a system role.
    /// System roles cannot be deleted and have special privileges.
    /// Examples: "System Administrator", "Module Manager".
    /// </summary>
    public bool IsSystemRole { get; set; }
}
