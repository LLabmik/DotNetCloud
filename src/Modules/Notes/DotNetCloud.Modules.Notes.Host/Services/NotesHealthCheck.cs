using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Notes.Host.Services;

/// <summary>
/// Health check for the Notes module.
/// </summary>
public sealed class NotesHealthCheck : IHealthCheck
{
    private readonly NotesModule _module;
    private readonly NotesDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesHealthCheck"/> class.
    /// </summary>
    public NotesHealthCheck(NotesModule module, NotesDbContext db)
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
            return HealthCheckResult.Degraded("Notes module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Notes module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Notes module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Notes module is running", data);
    }
}
