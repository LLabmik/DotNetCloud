namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Represents the context of a caller making a request to the system.
/// Immutable record that captures caller identity, type, and role information.
/// </summary>
public sealed record CallerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallerContext"/> record.
    /// </summary>
    /// <param name="userId">The unique identifier of the caller.</param>
    /// <param name="roles">A read-only list of role names assigned to the caller.</param>
    /// <param name="type">The type of caller (User, System, or Module).</param>
    /// <exception cref="ArgumentException">Thrown when userId is empty or roles is null.</exception>
    public CallerContext(Guid userId, IReadOnlyList<string> roles, CallerType type)
    {
        Validate(userId, roles);

        UserId = userId;
        Roles = roles ?? Array.Empty<string>();
        Type = type;
    }

    /// <summary>
    /// Gets the unique identifier of the caller.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the read-only list of role names assigned to the caller.
    /// </summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Gets the type of caller (User, System, or Module).
    /// </summary>
    public CallerType Type { get; }

    /// <summary>
    /// Determines whether the caller has a specific role.
    /// </summary>
    /// <param name="role">The role to check for.</param>
    /// <returns>True if the caller has the specified role; otherwise false.</returns>
    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the caller has any of the specified roles.
    /// </summary>
    /// <param name="roles">The roles to check for.</param>
    /// <returns>True if the caller has at least one of the specified roles; otherwise false.</returns>
    public bool HasAnyRole(params string[] roles)
    {
        if (roles == null || roles.Length == 0)
            return false;

        return roles.Any(role => HasRole(role));
    }

    /// <summary>
    /// Determines whether the caller has all of the specified roles.
    /// </summary>
    /// <param name="roles">The roles to check for.</param>
    /// <returns>True if the caller has all of the specified roles; otherwise false.</returns>
    public bool HasAllRoles(params string[] roles)
    {
        if (roles == null || roles.Length == 0)
            return true;

        return roles.All(role => HasRole(role));
    }

    /// <summary>
    /// Creates a new <see cref="CallerContext"/> for a system process.
    /// </summary>
    /// <returns>A new CallerContext with System type and no roles.</returns>
    public static CallerContext CreateSystemContext()
    {
        return new CallerContext(Guid.Empty, Array.Empty<string>(), CallerType.System);
    }

    /// <summary>
    /// Creates a new <see cref="CallerContext"/> for a module.
    /// </summary>
    /// <param name="moduleId">The unique identifier of the module.</param>
    /// <param name="roles">Optional roles for the module context.</param>
    /// <returns>A new CallerContext with Module type.</returns>
    public static CallerContext CreateModuleContext(Guid moduleId, IReadOnlyList<string>? roles = null)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("Module ID cannot be empty.", nameof(moduleId));

        return new CallerContext(moduleId, roles ?? Array.Empty<string>(), CallerType.Module);
    }

    /// <summary>
    /// Validates the caller context inputs.
    /// </summary>
    /// <param name="userId">The user ID to validate.</param>
    /// <param name="roles">The roles list to validate.</param>
    /// <exception cref="ArgumentException">Thrown if validation fails.</exception>
    private static void Validate(Guid userId, IReadOnlyList<string>? roles)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (roles != null)
        {
            foreach (var role in roles)
            {
                if (string.IsNullOrWhiteSpace(role))
                    throw new ArgumentException("Role names cannot be null or whitespace.", nameof(roles));
            }
        }
    }
}
