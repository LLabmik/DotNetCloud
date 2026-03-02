namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for permission information.
/// </summary>
public class PermissionDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the permission.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the permission code (e.g., "files.upload").
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission display name.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the module that owns this permission.
    /// </summary>
    public string Module { get; set; } = null!;
}

/// <summary>
/// Data transfer object for creating a new permission.
/// </summary>
public class CreatePermissionDto
{
    /// <summary>
    /// Gets or sets the permission code (required, e.g., "files.upload").
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission display name (required).
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the module that owns this permission (required).
    /// </summary>
    public string Module { get; set; } = null!;
}

/// <summary>
/// Data transfer object for role information.
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the role.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a system role.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Gets or sets the permissions assigned to this role.
    /// </summary>
    public ICollection<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();

    /// <summary>
    /// Gets or sets the count of users assigned this role.
    /// </summary>
    public int UserCount { get; set; }
}

/// <summary>
/// Data transfer object for creating a new role.
/// </summary>
public class CreateRoleDto
{
    /// <summary>
    /// Gets or sets the role name (required).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the role description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the permission IDs to assign to this role (optional).
    /// </summary>
    public ICollection<Guid> PermissionIds { get; set; } = new List<Guid>();
}

/// <summary>
/// Data transfer object for updating role information.
/// </summary>
public class UpdateRoleDto
{
    /// <summary>
    /// Gets or sets the role name (optional).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the role description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the permission IDs to assign to this role (optional).
    /// </summary>
    public ICollection<Guid>? PermissionIds { get; set; }
}
