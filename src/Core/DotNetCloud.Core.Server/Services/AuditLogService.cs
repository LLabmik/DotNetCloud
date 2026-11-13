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

        // Module-supplied text is untrusted: strip control characters/newlines (log-forging and
        // ambiguous single-line records) and cap the length so one module cannot blow up the table.
        var moduleId = AuditTextSanitizer.CleanRequired(entry.ModuleId, AuditTextSanitizer.MaxModuleIdLength, "unknown");
        var entityType = AuditTextSanitizer.CleanRequired(entry.EntityType, AuditTextSanitizer.MaxEntityTypeLength, "Unknown");
        var description = AuditTextSanitizer.Clean(entry.Description, AuditTextSanitizer.MaxDescriptionLength);

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
                ModuleId = moduleId,
                Action = (int)entry.Action,
                EntityType = entityType,
                EntityId = entry.EntityId,
                Description = description,
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit: {AuditAction} on {EntityType}/{EntityId} by {CallerType} in {ModuleId}",
                entry.Action, entityType, entry.EntityId, entry.Caller.Type, moduleId);
        }
        catch (Exception ex)
        {
            // Never break the calling operation because auditing failed, but do
            // not drop silently — surface it to the operator's logs.
            _logger.LogError(ex,
                "Failed to persist audit entry {AuditAction} on {EntityType}/{EntityId} in {ModuleId}",
                entry.Action, entityType, entry.EntityId, moduleId);
        }
    }
}

/// <summary>
/// Cleans module-supplied text before it reaches the audit table (finding F14).
/// </summary>
/// <remarks>
/// Audit descriptions are rendered in the admin UI and in log viewers. Unbounded or multi-line
/// values let a module forge log records or bloat the table, so every field is stripped of control
/// characters (including CR/LF) and truncated to a fixed maximum.
/// </remarks>
public static class AuditTextSanitizer
{
    /// <summary>Maximum persisted length of <c>AuditLog.ModuleId</c>.</summary>
    public const int MaxModuleIdLength = 200;

    /// <summary>Maximum persisted length of <c>AuditLog.EntityType</c>.</summary>
    public const int MaxEntityTypeLength = 100;

    /// <summary>Maximum persisted length of <c>AuditLog.Description</c>.</summary>
    public const int MaxDescriptionLength = 1000;

    /// <summary>
    /// Removes control characters (including newlines) and truncates the value.
    /// </summary>
    /// <param name="value">The untrusted value.</param>
    /// <param name="maxLength">The maximum length to keep.</param>
    /// <param name="defaultValue">Returned when the cleaned value is empty.</param>
    /// <returns>The cleaned value, or <see langword="null"/> when the input was null and no default was given.</returns>
    public static string? Clean(string? value, int maxLength, string? defaultValue = null)
    {
        if (value is null)
        {
            return defaultValue;
        }

        var buffer = new char[Math.Min(value.Length, maxLength)];
        var written = 0;

        foreach (var character in value)
        {
            if (written >= buffer.Length)
            {
                break;
            }

            // Drop C0/C1 control characters and the zero-width/BOM range; keep ordinary text.
            if (char.IsControl(character) || character is '\u200B' or '\uFEFF')
            {
                continue;
            }

            buffer[written++] = character;
        }

        var cleaned = new string(buffer, 0, written).Trim();
        return cleaned.Length == 0 ? defaultValue : cleaned;
    }

    /// <summary>
    /// Removes control characters and truncates the value, always returning a non-null result.
    /// </summary>
    /// <param name="value">The untrusted value.</param>
    /// <param name="maxLength">The maximum length to keep.</param>
    /// <param name="defaultValue">Returned when the input is null or cleans to an empty string.</param>
    /// <returns>The cleaned, non-null value.</returns>
    public static string CleanRequired(string? value, int maxLength, string defaultValue) =>
        Clean(value, maxLength, defaultValue) ?? defaultValue;
}
