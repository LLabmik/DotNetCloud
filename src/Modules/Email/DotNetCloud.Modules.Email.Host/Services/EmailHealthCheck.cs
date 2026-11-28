using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Email.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// Health check for the Email module.
/// </summary>
public sealed class EmailHealthCheck : IHealthCheck
{
    private readonly EmailModule _module;
    private readonly EmailDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailHealthCheck"/> class.
    /// </summary>
    public EmailHealthCheck(EmailModule module, EmailDbContext db)
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
            return HealthCheckResult.Degraded("Email module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Email module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Email module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Email module is running", data);
    }
}
