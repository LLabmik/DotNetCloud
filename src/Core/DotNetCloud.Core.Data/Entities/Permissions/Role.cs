namespace DotNetCloud.Core.Data.Entities.Permissions;

/// <summary>
/// Represents a role entity that groups permissions together for assignment to users or teams.
/// Roles provide a convenient way to manage access control by bundling related permissions.
/// </summary>
/// <remarks>
/// Roles can be either system-defined (immutable, provided by the platform) or custom (created by administrators).
/// System roles include Administrator, User, Guest, etc. Custom roles can be created for organization-specific needs.
/// Roles support hierarchical organization through implicit inheritance (a user can have multiple roles).
/// This entity is distinct from ASP.NET Core Identity's IdentityRole and represents application-level role definitions.
/// </remarks>
public class Role
{
    /// <summary>
    /// Gets or sets the unique identifier for the role.
    /// </summary>
    /// <remarks>
    /// Guid primary key for relational integrity across database providers.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Must be unique across all roles in the system.
    /// Examples: "Administrator", "Editor", "Viewer", "FileManager".
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of the role's purpose and scope.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 1000 characters. Helps administrators understand the role's intended use.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a system-defined role.
    /// </summary>
    /// <remarks>
    /// System roles are built-in and immutable. They cannot be deleted or significantly modified.
    /// Custom roles (IsSystemRole = false) can be created, modified, and deleted by administrators.
    /// Default: false for custom roles, true for system roles like Administrator and User.
    /// </remarks>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Gets or sets the collection of RolePermission associations for this role.
    /// </summary>
    /// <remarks>
    /// Navigation property representing the many-to-many relationship between roles and permissions.
    /// Used by EF Core for relationship tracking and lazy loading.
    /// Allows efficient querying of all permissions assigned to this role.
    /// </remarks>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
