using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Contacts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Contacts.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Contacts module.
/// Reports module status and basic metrics.
/// </summary>
public sealed class ContactsHealthCheck : IHealthCheck
{
    private readonly ContactsModule _module;
    private readonly ContactsDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsHealthCheck"/> class.
    /// </summary>
    public ContactsHealthCheck(ContactsModule module, ContactsDbContext db)
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
                description: "Contacts module is not initialized",
                data: data);
        }

        if (!_module.IsRunning)
        {
            return HealthCheckResult.Degraded(
                description: "Contacts module is initialized but not running",
                data: data);
        }

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Contacts module database is unreachable.", data: data);
        }

        return HealthCheckResult.Healthy(
            description: "Contacts module is running",
            data: data);
    }
}
