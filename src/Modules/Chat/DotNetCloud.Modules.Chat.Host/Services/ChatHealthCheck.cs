using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Chat.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Chat.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Chat module.
/// Reports module status and basic metrics.
/// </summary>
public sealed class ChatHealthCheck : IHealthCheck
{
    private readonly ChatModule _module;
    private readonly ChatDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatHealthCheck"/> class.
    /// </summary>
    public ChatHealthCheck(ChatModule module, ChatDbContext db)
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
                description: "Chat module is not initialized",
                data: data);
        }

        if (!_module.IsRunning)
        {
            return HealthCheckResult.Degraded(
                description: "Chat module is initialized but not running",
                data: data);
        }

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Chat module database is unreachable.", data: data);
        }

        return HealthCheckResult.Healthy(
            description: "Chat module is running",
            data: data);
    }
}
