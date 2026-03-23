using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Records audit log entries for security-sensitive operations across modules.
/// </summary>
/// <remarks>
/// <para><b>Capability tier:</b> Public — automatically granted to all modules.</para>
/// <para>
/// Every module uses this capability to log create, update, delete, share, and access
/// operations. Entries are associated with a <see cref="CallerContext"/> for attribution.
/// </para>
/// </remarks>
public interface IAuditLogger : ICapabilityInterface
{
    /// <summary>
    /// Records an audit entry.
    /// </summary>
    /// <param name="entry">The audit log details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single audit trail entry.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>
    /// Unique ID for this audit entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the action occurred (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The user or system context that performed the action.
    /// </summary>
    public required CallerContext Caller { get; init; }

    /// <summary>
    /// The module where the action occurred (e.g., "dotnetcloud.contacts").
    /// </summary>
    public required string ModuleId { get; init; }

    /// <summary>
    /// The category of action performed.
    /// </summary>
    public required AuditAction Action { get; init; }

    /// <summary>
    /// The type of entity acted upon (e.g., "Contact", "CalendarEvent", "Note").
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The ID of the entity acted upon.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Optional human-readable description of the action.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Categories of auditable actions.
/// </summary>
public enum AuditAction
{
    /// <summary>Entity created.</summary>
    Create,

    /// <summary>Entity read/accessed.</summary>
    Read,

    /// <summary>Entity updated.</summary>
    Update,

    /// <summary>Entity deleted (soft or hard).</summary>
    Delete,

    /// <summary>Entity shared with another user.</summary>
    Share,

    /// <summary>Share permission revoked.</summary>
    Unshare,

    /// <summary>Entity restored from deletion.</summary>
    Restore,

    /// <summary>Entity exported (vCard, iCal, etc.).</summary>
    Export,

    /// <summary>Entity imported.</summary>
    Import
}
