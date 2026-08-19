using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Audit;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Persists audit trail entries to the <c>AuditLog</c> table (SOC 2 CC4 / P7).
/// </summary>
/// <remarks>
/// <para>
/// This is the write-through implementation of <see cref="IAuditLogger"/> used by
/// Core.Server controllers directly. Process-isolated modules reach the same
/// persistence path through the <c>LogAudit</c> gRPC capability, whose handler
/// resolves this service from the request scope.
/// </para>
/// <para>
/// The service also mirrors each entry to the Serilog audit sink by logging with
/// an <c>Audit</c> context property (see <c>SerilogConfiguration.AuditFilePath</c>).
/// Failures to persist are logged but never thrown, so auditing can never break a
/// business operation.
/// </para>
/// </remarks>
public sealed class AuditLogService : IAuditLogger
{
    private readonly CoreDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="db">The core database context (scoped).</param>
    /// <param name="logger">The logger for this service.</param>
    public AuditLogService(CoreDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Caller);

        try
        {
            var log = new AuditLog
            {
                Id = entry.Id == Guid.Empty ? Guid.CreateVersion7() : entry.Id,
                TimestampUtc = entry.TimestampUtc,
                CallerType = entry.Caller.Type.ToString(),
                CallerUserId = entry.Caller.Type == CallerType.System ? null : entry.Caller.UserId,
                CallerRoles = entry.Caller.Roles is { Count: > 0 }
                    ? JsonSerializer.Serialize(entry.Caller.Roles)
                    : null,
                ModuleId = entry.ModuleId,
                Action = (int)entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Description = entry.Description,
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit: {AuditAction} on {EntityType}/{EntityId} by {CallerType} in {ModuleId}",
                entry.Action, entry.EntityType, entry.EntityId, entry.Caller.Type, entry.ModuleId);
        }
        catch (Exception ex)
        {
            // Never break the calling operation because auditing failed, but do
            // not drop silently — surface it to the operator's logs.
            _logger.LogError(ex,
                "Failed to persist audit entry {AuditAction} on {EntityType}/{EntityId} in {ModuleId}",
                entry.Action, entry.EntityType, entry.EntityId, entry.ModuleId);
        }
    }
}
