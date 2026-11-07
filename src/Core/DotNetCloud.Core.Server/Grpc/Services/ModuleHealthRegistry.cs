using System.Collections.Concurrent;
using DotNetCloud.Core;

namespace DotNetCloud.Core.Server.Grpc.Services;

/// <summary>
/// Records the latest health report each module pushed through <c>ModuleApi.ReportHealth</c>.
/// </summary>
/// <remarks>
/// This is the module's own view; the supervisor's process/health probing stays authoritative for
/// liveness. The registry exists so readiness can distinguish "the process answers" from "the
/// module says it is usable" (for example while its own dependencies are down).
/// </remarks>
internal sealed class ModuleHealthRegistry
{
    private readonly ConcurrentDictionary<string, ModuleHealthReport> _reports =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ModuleHealthRegistry"/> class.</summary>
    /// <param name="timeProvider">Clock used to stamp reports.</param>
    public ModuleHealthRegistry(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>
    /// Records a report for a module, replacing any previous report.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="status">Normalized status (<c>healthy</c>, <c>degraded</c>, <c>unhealthy</c>).</param>
    /// <param name="detail">Optional detail; control characters are stripped and the value capped.</param>
    /// <param name="activeOperations">Operations the module reported as in flight.</param>
    public void Record(string moduleId, string status, string? detail, int activeOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        _reports[moduleId] = new ModuleHealthReport(
            moduleId,
            status,
            SanitizeDetail(detail),
            activeOperations < 0 ? 0 : activeOperations,
            _timeProvider.GetUtcNow());
    }

    /// <summary>Gets the latest reports, ordered by module id.</summary>
    /// <returns>A snapshot of the recorded reports.</returns>
    public IReadOnlyList<ModuleHealthReport> GetSnapshot() =>
        _reports.Values.OrderBy(report => report.ModuleId, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Gets the latest report for a module, when one was recorded.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The report, or <see langword="null"/>.</returns>
    public ModuleHealthReport? Get(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _reports.TryGetValue(moduleId, out var report)
            ? report
            : null;

    /// <summary>
    /// Normalizes a module-supplied status to <c>healthy</c>/<c>degraded</c>/<c>unhealthy</c>.
    /// </summary>
    /// <param name="status">The raw status from the module.</param>
    /// <param name="normalized">The normalized status when the value is recognized.</param>
    /// <returns><see langword="true"/> when the value is one of the three known statuses.</returns>
    public static bool TryNormalizeStatus(string? status, out string normalized)
    {
        normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "healthy" or "degraded" or "unhealthy";
    }

    private static string? SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        // Module-supplied text is untrusted: strip log-forging characters and cap the length.
        var sanitized = LogSanitizer.Sanitize(detail).Trim();
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }
}

/// <summary>A module's latest self-reported health.</summary>
/// <param name="ModuleId">The module id.</param>
/// <param name="Status">Normalized status.</param>
/// <param name="Detail">Sanitized detail, when supplied.</param>
/// <param name="ActiveOperations">Operations in flight at report time.</param>
/// <param name="ReportedAtUtc">When the report was received.</param>
internal sealed record ModuleHealthReport(
    string ModuleId,
    string Status,
    string? Detail,
    int ActiveOperations,
    DateTimeOffset ReportedAtUtc);
