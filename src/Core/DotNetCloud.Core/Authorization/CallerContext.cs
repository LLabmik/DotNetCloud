namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Represents the context of a caller making a request to the system.
/// 
/// An immutable record that captures caller identity, type, and role information.
/// Every operation in DotNetCloud is associated with a CallerContext to enable
/// authorization, auditing, and proper operation attribution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose:</b>
/// 
/// CallerContext serves multiple functions:
/// <list type="bullet">
///   <item><description>Identifies who/what is performing an operation (UserId, Type)</description></item>
///   <item><description>Provides authorization information (Roles for permission checks)</description></item>
///   <item><description>Enables auditing (know who changed what)</description></item>
///   <item><description>Supports capability-based security (enforce capability requirements)</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Caller Types:</b>
/// 
/// CallerContext identifies three types of callers:
/// 
/// <list type="table">
///   <listheader>
///     <term>Type</term>
///     <description>Purpose</description>
///     <description>Examples</description>
///   </listheader>
///   <item>
///     <term><see cref="CallerType.User"/></term>
///     <description>A human user authenticated via login (OAuth, MFA, etc.)</description>
///     <description>User logging in via browser, API key authentication</description>
///   </item>
///   <item>
///     <term><see cref="CallerType.System"/></term>
///     <description>System-level process performing administrative tasks</description>
///     <description>Background jobs, cleanup tasks, database migrations</description>
///   </item>
///   <item>
///     <term><see cref="CallerType.Module"/></term>
///     <description>A loaded module making inter-module calls</description>
///     <description>Files module calling Chat module, Chat posting notifications</description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Usage Pattern:</b>
/// 
/// <code>
/// // 1. In API controller - extract from HTTP request
/// public class DocumentController
/// {
///     [HttpPost("documents")]
///     public async Task&lt;IActionResult&gt; CreateDocumentAsync(
///         [FromBody] CreateDocumentDto dto,
///         CancellationToken cancellationToken)
///     {
///         // Extract caller from HttpContext.User
///         var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
///         var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
///         var caller = new CallerContext(userId, roles, CallerType.User);
///         
///         // Pass to service
///         var doc = await _documentService.CreateAsync(dto, caller, cancellationToken);
///         return Ok(doc);
///     }
/// }
/// 
/// // 2. In service - use for authorization and auditing
/// public class DocumentService
/// {
///     public async Task&lt;DocumentDto&gt; CreateAsync(
///         CreateDocumentDto dto,
///         CallerContext caller,
///         CancellationToken cancellationToken)
///     {
///         // Verify caller has permission
///         if (!caller.HasRole("user"))
///             throw new UnauthorizedException("Only users can create documents");
///         
///         if (caller.Type != CallerType.User)
///             throw new InvalidOperationException("Documents can only be created by users");
///         
///         // Create document with caller context
///         var doc = new Document
///         {
///             Id = Guid.NewGuid(),
///             OwnerId = caller.UserId,
///             Title = dto.Title,
///             CreatedAt = DateTime.UtcNow
///         };
///         
///         // Audit log includes caller
///         await _auditLog.LogAsync($"Document created by {caller.UserId}", doc.Id);
///         
///         // Publish event with caller context
///         await _eventBus.PublishAsync(
///             new DocumentCreatedEvent { /* ... */ },
///             caller,
///             cancellationToken);
///         
///         return new DocumentDto { /* ... */ };
///     }
/// }
/// 
/// // 3. For background tasks - use system caller
/// public async Task CleanupAsync(CancellationToken cancellationToken)
/// {
///     // System caller for background operations
///     var systemCaller = CallerContext.CreateSystemContext();
///     
///     // Perform cleanup
///     await _documentService.DeleteOrphanedAsync(systemCaller, cancellationToken);
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Role-Based Authorization Pattern:</b>
/// 
/// <code>
/// // Check specific role
/// if (!caller.HasRole("admin"))
///     throw new UnauthorizedException("Admin access required");
/// 
/// // Check multiple roles (any)
/// if (!caller.HasAnyRole("editor", "admin"))
///     throw new UnauthorizedException("Editor or Admin access required");
/// 
/// // Check multiple roles (all)
/// if (!caller.HasAllRoles("user", "verified"))
///     throw new UnauthorizedException("Must be verified user");
/// </code>
/// </para>
/// 
/// <para>
/// <b>Validation Rules:</b>
/// 
/// <list type="bullet">
///   <item><description>UserId must not be Guid.Empty for User/Module types (System is Guid.Empty)</description></item>
///   <item><description>Roles collection is never null (defaults to empty array)</description></item>
///   <item><description>Role comparison is case-insensitive</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="CallerType"/>
public sealed record CallerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallerContext"/> record.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the caller.
    /// For User/Module types: must not be Guid.Empty.
    /// For System type: should be Guid.Empty.
    /// </param>
    /// <param name="roles">
    /// A read-only list of role names assigned to the caller.
    /// If null, defaults to empty array. Case-insensitive.
    /// </param>
    /// <param name="type">
    /// The type of caller: User (human), System (background process), or Module (inter-module).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when UserId is Guid.Empty for User/Module types.
    /// </exception>
    public CallerContext(Guid userId, IReadOnlyList<string> roles, CallerType type)
    {
        Validate(userId, roles);

        UserId = userId;
        Roles = roles ?? Array.Empty<string>();
        Type = type;
    }

    /// <summary>
    /// Gets the unique identifier of the caller.
    /// 
    /// For User: the user's account ID.
    /// For Module: the module's ID.
    /// For System: Guid.Empty (no specific identity).
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the read-only list of role names assigned to the caller.
    /// 
    /// Role names are case-insensitive. Examples: "admin", "editor", "user", "verified".
    /// </summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Gets the type of caller (User, System, or Module).
    /// 
    /// Affects authorization policies and auditing behavior.
    /// </summary>
    public CallerType Type { get; }

    /// <summary>
    /// Determines whether the caller has a specific role.
    /// 
    /// Role check is case-insensitive.
    /// </summary>
    /// <param name="role">The role to check for (e.g., "admin", "editor").</param>
    /// <returns>True if the caller has the specified role; otherwise false.</returns>
    /// <remarks>
    /// <code>
    /// if (caller.HasRole("admin"))
    ///     // Perform admin action
    /// </code>
    /// </remarks>
    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the caller has any of the specified roles.
    /// 
    /// Useful for checking if caller has at least one required role.
    /// </summary>
    /// <param name="roles">The roles to check for (e.g., "editor", "admin").</param>
    /// <returns>True if the caller has at least one of the specified roles; otherwise false.</returns>
    /// <remarks>
    /// <code>
    /// if (caller.HasAnyRole("editor", "admin"))
    ///     // Either editor or admin
    /// </code>
    /// </remarks>
    public bool HasAnyRole(params string[] roles)
    {
        if (roles == null || roles.Length == 0)
            return false;

        return roles.Any(role => HasRole(role));
    }

    /// <summary>
    /// Determines whether the caller has all of the specified roles.
    /// 
    /// Useful for checking if caller has multiple required roles.
    /// </summary>
    /// <param name="roles">The roles to check for (e.g., "user", "verified").</param>
    /// <returns>True if the caller has all of the specified roles; otherwise false.</returns>
    /// <remarks>
    /// <code>
    /// if (caller.HasAllRoles("user", "verified"))
    ///     // Must have both roles
    /// </code>
    /// </remarks>
    public bool HasAllRoles(params string[] roles)
    {
        if (roles == null || roles.Length == 0)
            return true;

        return roles.All(role => HasRole(role));
    }

    /// <summary>
    /// Creates a new <see cref="CallerContext"/> for a system process.
    /// 
    /// System context is used for background operations, cleanup tasks, and system-level changes.
    /// </summary>
    /// <returns>A new CallerContext with System type, no roles, and empty UserId.</returns>
    /// <remarks>
    /// <code>
    /// // Background cleanup task
    /// var systemCaller = CallerContext.CreateSystemContext();
    /// await _service.CleanupAsync(systemCaller, cancellationToken);
    /// </code>
    /// </remarks>
    public static CallerContext CreateSystemContext()
    {
        return new CallerContext(Guid.Empty, Array.Empty<string>(), CallerType.System);
    }

    /// <summary>
    /// Creates a new <see cref="CallerContext"/> for a module.
    /// 
    /// Module context is used for inter-module calls and module-to-module communication.
    /// </summary>
    /// <param name="moduleId">
    /// The unique identifier of the module.
    /// Must not be Guid.Empty.
    /// </param>
    /// <param name="roles">
    /// Optional roles for the module context (e.g., "system-module", "trusted").
    /// If null, defaults to empty array.
    /// </param>
    /// <returns>A new CallerContext with Module type.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if moduleId is Guid.Empty.
    /// </exception>
    /// <remarks>
    /// <code>
    /// // Inter-module call
    /// var moduleCaller = CallerContext.CreateModuleContext(
    ///     moduleId: Guid.Parse("12345678-1234-1234-1234-123456789012"),
    ///     roles: new[] { "system-module" });
    /// 
    /// await _service.ProcessAsync(moduleCaller, cancellationToken);
    /// </code>
    /// </remarks>
    public static CallerContext CreateModuleContext(Guid moduleId, IReadOnlyList<string>? roles = null)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("Module ID cannot be empty.", nameof(moduleId));

        return new CallerContext(moduleId, roles ?? Array.Empty<string>(), CallerType.Module);
    }

    /// <summary>
    /// Validates the CallerContext arguments.
    /// </summary>
    /// <param name="userId">The caller's user ID.</param>
    /// <param name="roles">The caller's roles.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if validation fails.
    /// </exception>
    private static void Validate(Guid userId, IReadOnlyList<string>? roles)
    {
        // UserId must not be empty for explicit caller identification
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
    }
}
