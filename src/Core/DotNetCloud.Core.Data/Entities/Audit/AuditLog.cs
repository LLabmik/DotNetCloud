using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Core.Data.Entities.Audit;

/// <summary>
/// A single persisted audit trail entry (SOC 2 CC4 / P7).
/// </summary>
/// <remarks>
/// <para>
/// Every security-relevant operation (login, MFA change, admin mutation, module
/// create/update/delete/share/export/import) is recorded here for attribution and
/// monitoring. Entries are written by <c>AuditLogService</c> in Core.Server, which
/// is reached directly by Core.Server controllers or via the <c>LogAudit</c> gRPC
/// capability from process-isolated module hosts.
/// </para>
/// <para>
/// The <see cref="CallerRoles"/> field stores a JSON array of role names; it is
/// kept as a string to avoid a separate table. Retention of rows is enforced by
/// <c>AuditLogPurgeHostedService</c> (see <c>core.AuditLogRetentionDays</c>).
/// </para>
/// </remarks>
public sealed class AuditLog
{
    /// <summary>
    /// Unique identifier for this audit entry (defaults to a Version 7 GUID).
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// When the audited action occurred (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of caller that performed the action: "User", "System", or "Module".
    /// </summary>
    /// <remarks>Required. Maximum 20 characters.</remarks>
    public string CallerType { get; set; } = "User";

    /// <summary>
    /// ID of the user who performed the action, or <c>null</c> for system callers.
    /// </summary>
    public Guid? CallerUserId { get; set; }

    /// <summary>
    /// JSON array of role names assigned to the caller at the time of the action.
    /// </summary>
    /// <remarks>Optional. Maximum 2000 characters.</remarks>
    public string? CallerRoles { get; set; }

    /// <summary>
    /// Module where the action occurred (e.g., "dotnetcloud.files").
    /// </summary>
    /// <remarks>Required. Maximum 100 characters.</remarks>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>
    /// The category of action performed, as an <see cref="AuditAction"/> enum value.
    /// </summary>
    public int Action { get; set; }

    /// <summary>
    /// The type of entity acted upon (e.g., "Contact", "CalendarEvent", "Note").
    /// </summary>
    /// <remarks>Required. Maximum 100 characters.</remarks>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the entity acted upon.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Optional human-readable description of the action.
    /// </summary>
    /// <remarks>Optional. Maximum 2000 characters.</remarks>
    public string? Description { get; set; }
}
