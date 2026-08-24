using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Calendar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Calendar module.
/// Reports module status and basic metrics.
/// </summary>
public sealed class CalendarHealthCheck : IHealthCheck
{
    private readonly CalendarModule _module;
    private readonly CalendarDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarHealthCheck"/> class.
    /// </summary>
    public CalendarHealthCheck(CalendarModule module, CalendarDbContext db)
    {
        _module = module;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["module_id"] = _module.Manifest.Id,
            ["version"] = _module.Manifest.Version,
            ["initialized"] = _module.IsInitialized,
            ["running"] = _module.IsRunning
        };

        if (!_module.IsInitialized)
        {
            return HealthCheckResult.Degraded(
                description: "Calendar module is not initialized",
                data: data);
        }

        if (!_module.IsRunning)
        {
            return HealthCheckResult.Degraded(
                description: "Calendar module is initialized but not running",
                data: data);
        }

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Calendar module database is unreachable.", data: data);
        }

        return HealthCheckResult.Healthy(
            description: "Calendar module is running",
            data: data);
    }
}
