namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Defines the type of caller making a request to the system.
/// 
/// Distinguishes between human users, system processes, and module-to-module calls.
/// Enables appropriate authorization policies and auditing behavior for each caller type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Caller Type Decision Tree:</b>
/// 
/// <code>
/// Does a human user make this request?
///     → CallerType.User
/// 
/// Is this a system process (background job, cleanup, migration)?
///     → CallerType.System
/// 
/// Is this a module calling another module?
///     → CallerType.Module
/// </code>
/// </para>
/// 
/// <para>
/// <b>Authorization by Caller Type:</b>
/// 
/// Different authorization policies apply to each type:
/// 
/// <list type="table">
///   <listheader>
///     <term>Type</term>
///     <description>Authorization</description>
///     <description>Auditing</description>
///   </listheader>
///   <item>
///     <term>User</term>
///     <description>
///       Role-based (admin, editor, viewer, etc.).
///       Restricted to user's organization/team.
///       Subject to rate limiting.
///     </description>
///     <description>
///       Audit logs include user identity.
///       User notified of significant changes.
///     </description>
///   </item>
///   <item>
///     <term>System</term>
///     <description>
///       Full permissions (executes admin operations).
///       No organization/team restrictions.
///       Not subject to rate limiting.
///     </description>
///     <description>
///       Audit logs show "system" as actor.
///       Users not notified (background operations).
///     </description>
///   </item>
///   <item>
///     <term>Module</term>
///     <description>
///       Restricted by granted capabilities.
///       May be restricted to its own org/team.
///       Not subject to rate limiting.
///     </description>
///     <description>
///       Audit logs show module ID as actor.
///       May trigger module-specific notifications.
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Usage Examples:</b>
/// 
/// <code>
/// // ===== User Type (HTTP request from browser/app) =====
/// var userCaller = new CallerContext(
///     userId: Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
///     roles: new[] { "user", "editor" },
///     type: CallerType.User);
/// 
/// // User can only modify their own documents and team documents
/// // Subject to rate limiting: 100 requests/minute
/// 
/// 
/// // ===== System Type (Background job) =====
/// var systemCaller = CallerContext.CreateSystemContext();
/// // or
/// var systemCaller = new CallerContext(
///     userId: Guid.Empty,
///     roles: Array.Empty&lt;string&gt;(),
///     type: CallerType.System);
/// 
/// // System has full permissions (cleanup, migrations, maintenance)
/// // Not subject to rate limiting
/// 
/// 
/// // ===== Module Type (Inter-module call) =====
/// var moduleCaller = CallerContext.CreateModuleContext(
///     moduleId: Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
///     roles: new[] { "dotnetcloud.chat" });
/// 
/// // Module restricted to its granted capabilities
/// // Can access shared resources through events
/// </code>
/// </para>
/// 
/// <para>
/// <b>Authorization Pattern:</b>
/// 
/// <code>
/// public async Task DeleteDocumentAsync(Guid documentId, CallerContext caller, CancellationToken ct)
/// {
///     // Get the document
///     var doc = await _db.Documents.FindAsync(documentId, cancellationToken: ct);
///     if (doc == null)
///         throw new NotFoundException("Document not found");
///     
///     // Authorization varies by caller type
///     switch (caller.Type)
///     {
///         case CallerType.User:
///             // Users can only delete their own documents or team documents
///             if (doc.OwnerId != caller.UserId)
///             {
///                 var inTeam = await UserInDocumentTeam(caller, doc);
///                 if (!inTeam)
///                     throw new UnauthorizedException("Cannot delete document");
///             }
///             break;
///         
///         case CallerType.System:
///             // System can delete anything (cleanup)
///             break;
///         
///         case CallerType.Module:
///             // Modules restricted to their capabilities
///             if (!await HasCapabilityAsync(caller.UserId, "delete_documents"))
///                 throw new UnauthorizedException("Module lacks capability");
///             break;
///     }
///     
///     // Delete and audit
///     _db.Documents.Remove(doc);
///     await _db.SaveChangesAsync(ct);
///     
///     // Audit log shows caller type
///     await _auditLog.LogAsync(
///         action: "document_deleted",
///         resourceId: documentId,
///         caller: caller);
/// }
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="CallerContext"/>
public enum CallerType
{
    /// <summary>
    /// A regular authenticated user making a request.
    /// 
    /// Examples: User logging in via browser, API key for user application
    /// 
    /// Characteristics:
    /// <list type="bullet">
    ///   <item><description>Authenticated via credentials (password, MFA, OAuth, etc.)</description></item>
    ///   <item><description>Has roles like "admin", "editor", "user", "viewer"</description></item>
    ///   <item><description>Restricted to their own organization/team</description></item>
    ///   <item><description>Subject to rate limiting</description></item>
    ///   <item><description>All actions audited and potentially notified to user</description></item>
    /// </list>
    /// </summary>
    User = 0,

    /// <summary>
    /// A system-level process performing administrative tasks.
    /// 
    /// Examples: Background job, database migration, scheduled cleanup, cron task
    /// 
    /// Characteristics:
    /// <list type="bullet">
    ///   <item><description>No specific user identity (UserId = Guid.Empty)</description></item>
    ///   <item><description>Full permissions (executes admin operations)</description></item>
    ///   <item><description>Not subject to rate limiting</description></item>
    ///   <item><description>Actions audited as "system" actor</description></item>
    ///   <item><description>Users typically not notified (background operation)</description></item>
    /// </list>
    /// </summary>
    System = 1,

    /// <summary>
    /// A loaded module making inter-module calls or module-initiated operations.
    /// 
    /// Examples: Files module calling Chat module, Chat module sending notifications
    /// 
    /// Characteristics:
    /// <list type="bullet">
    ///   <item><description>ModuleId in UserId field identifies which module</description></item>
    ///   <item><description>Permissions restricted to granted capabilities</description></item>
    ///   <item><description>Not subject to rate limiting</description></item>
    ///   <item><description>Actions audited with module ID as actor</description></item>
    ///   <item><description>Can trigger module-specific notifications/events</description></item>
    /// </list>
    /// </summary>
    Module = 2
}
