using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Calendar module.
/// Reports module status and basic metrics.
/// </summary>
public sealed class CalendarHealthCheck : IHealthCheck
{
    private readonly CalendarModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarHealthCheck"/> class.
    /// </summary>
    public CalendarHealthCheck(CalendarModule module)
    {
        _module = module;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
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
            return Task.FromResult(HealthCheckResult.Degraded(
                description: "Calendar module is not initialized",
                data: data));
        }

        if (!_module.IsRunning)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: "Calendar module is initialized but not running",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            description: "Calendar module is running",
            data: data));
    }
}
