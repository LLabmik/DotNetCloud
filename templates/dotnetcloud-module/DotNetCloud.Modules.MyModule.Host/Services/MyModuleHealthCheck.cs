using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.MyModule.Host.Services;

/// <summary>
/// The module's own ASP.NET Core health check, served at <c>/health</c>.
/// </summary>
/// <remarks>
/// The supervisor's liveness probe and the core's readiness aggregate both read this. Report
/// <see cref="HealthCheckResult.Unhealthy"/> only for a condition the process cannot recover from on
/// its own, and <see cref="HealthCheckResult.Degraded"/> for a dependency that is down but may return
/// — the supervisor keeps a degraded process running and restarts an unhealthy one.
/// </remarks>
public sealed class MyModuleHealthCheck : IHealthCheck
{
    private readonly MyModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyModuleHealthCheck"/> class.
    /// </summary>
    public MyModuleHealthCheck(MyModule module)
    {
        _module = module;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["module_id"] = _module.Manifest.Id,
            ["version"] = _module.Manifest.Version,
        };

        // A dependency probe belongs here, returning Degraded when it cannot answer.
        return Task.FromResult(HealthCheckResult.Healthy(
            _module.IsRunning ? "Running" : "Process up, lifecycle not started",
            data));
    }
}
