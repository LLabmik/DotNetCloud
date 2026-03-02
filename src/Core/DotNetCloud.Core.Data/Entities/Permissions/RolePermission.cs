namespace DotNetCloud.Core.Data.Entities.Permissions;

/// <summary>
/// Represents the many-to-many relationship between roles and permissions.
/// This junction table enables flexible assignment of permissions to roles.
/// </summary>
/// <remarks>
/// Uses a composite primary key (RoleId, PermissionId) to ensure uniqueness.
/// Prevents the same permission from being assigned multiple times to the same role.
/// Supports cascading deletes: deleting a role or permission will automatically remove the association.
/// </remarks>
public class RolePermission
{
    /// <summary>
    /// Gets or sets the role identifier (part of composite primary key).
    /// </summary>
    /// <remarks>
    /// Foreign key to the Role entity.
    /// When the associated Role is deleted, this record is automatically deleted.
    /// </remarks>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier (part of composite primary key).
    /// </summary>
    /// <remarks>
    /// Foreign key to the Permission entity.
    /// When the associated Permission is deleted, this record is automatically deleted.
    /// </remarks>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the role associated with this permission assignment.
    /// </summary>
    /// <remarks>
    /// Navigation property for EF Core relationship management.
    /// Enables efficient loading of the related Role entity.
    /// </remarks>
    public virtual Role Role { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission associated with this role assignment.
    /// </summary>
    /// <remarks>
    /// Navigation property for EF Core relationship management.
    /// Enables efficient loading of the related Permission entity.
    /// </remarks>
    public virtual Permission Permission { get; set; } = null!;
}
