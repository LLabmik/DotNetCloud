namespace DotNetCloud.Core.Data.Entities.Permissions;

/// <summary>
/// Represents a permission entity that defines a specific action or resource access within the system.
/// Permissions are granular rights that can be assigned to roles, which are then granted to users or teams.
/// </summary>
/// <remarks>
/// Permissions follow a hierarchical naming convention using dots (e.g., "files.upload", "users.delete").
/// This structure allows for efficient permission checking and hierarchical permission scoping.
/// System permissions are built-in and cannot be modified, while custom permissions can be created by administrators.
/// </remarks>
public class Permission
{
    /// <summary>
    /// Gets or sets the unique identifier for the permission.
    /// </summary>
    /// <remarks>
    /// Guid primary key for relational integrity across database providers.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the permission code that uniquely identifies this permission.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 255 characters. Uses a hierarchical naming convention with dots.
    /// Examples: "files.upload", "files.download", "users.create", "users.delete".
    /// Must be unique across all permissions in the system.
    /// </remarks>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the permission for UI representation.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Human-readable name for administrators.
    /// Example: "Upload Files", "Download Files", "Create Users".
    /// </remarks>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of what this permission allows.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 1000 characters. Provides detailed explanation of the permission's scope and implications.
    /// Helps administrators understand the impact of granting this permission.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the collection of RolePermission associations for this permission.
    /// </summary>
    /// <remarks>
    /// Navigation property representing the many-to-many relationship between permissions and roles.
    /// Used by EF Core for relationship tracking and lazy loading.
    /// </remarks>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
