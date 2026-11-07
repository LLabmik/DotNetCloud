using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Quotas;

/// <summary>
/// Resource requests declared by a module (manifest <c>resources</c> block plus the legacy
/// <c>memoryLimitMb</c> field). Every value is a request, never an entitlement.
/// </summary>
public sealed record ModuleQuotaRequest(
    int? MemoryLimitMb = null,
    int? CpuPercent = null,
    int? DataDirMb = null,
    int? DatabaseMb = null,
    int? ConnectionLimit = null,
    int? RequestsPerMinute = null,
    int? EventsPerMinute = null,
    int? NotificationsPerMinute = null,
    int? AuditEntriesPerMinute = null);

/// <summary>
/// The effective quota for a module after applying <c>min(request, admin override, policy ceiling)</c>.
/// </summary>
/// <param name="MemoryLimitMb">Memory ceiling in MB, or <see langword="null"/> for none.</param>
/// <param name="CpuPercent">CPU share as a percentage of one core, or <see langword="null"/> for none.</param>
/// <param name="PidsLimit">Maximum tasks in the module cgroup, enforced for every module.</param>
/// <param name="DataDirMb">Data-directory ceiling in MB, or <see langword="null"/> when unmeasured.</param>
/// <param name="DatabaseMb">Database-schema ceiling in MB, or <see langword="null"/> when unmeasured.</param>
/// <param name="ConnectionLimit">Maximum concurrent database connections.</param>
/// <param name="RequestsPerMinute">Maximum capability RPCs per minute, or <see langword="null"/> for none.</param>
/// <param name="EventsPerMinute">Maximum published events per minute, or <see langword="null"/> for none.</param>
/// <param name="NotificationsPerMinute">Maximum notifications per minute, or <see langword="null"/> for none.</param>
/// <param name="AuditEntriesPerMinute">Maximum audit entries per minute, or <see langword="null"/> for none.</param>
public sealed record EffectiveModuleQuota(
    int? MemoryLimitMb,
    int? CpuPercent,
    int PidsLimit,
    int? DataDirMb,
    int? DatabaseMb,
    int ConnectionLimit,
    int? RequestsPerMinute,
    int? EventsPerMinute,
    int? NotificationsPerMinute,
    int? AuditEntriesPerMinute);

/// <summary>
/// Resolves the effective resource quota for a module from its manifest request, the admin
/// override row (<see cref="ModuleQuota"/>) and the <see cref="ModuleQuotasOptions"/> policy.
/// </summary>
public sealed class ModuleQuotaResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleQuotasOptions _policy;
    private readonly ProcessSupervisorOptions _supervisor;

    /// <summary>Initializes a new instance of the <see cref="ModuleQuotaResolver"/> class.</summary>
    /// <param name="scopeFactory">Scope factory for the core database context.</param>
    /// <param name="policy">Policy ceilings bound from <c>Modules:Quotas</c>.</param>
    /// <param name="supervisor">Supervisor options (default memory limit).</param>
    public ModuleQuotaResolver(
        IServiceScopeFactory scopeFactory,
        IOptions<ModuleQuotasOptions> policy,
        IOptions<ProcessSupervisorOptions> supervisor)
    {
        _scopeFactory = scopeFactory;
        _policy = policy.Value;
        _supervisor = supervisor.Value;
    }

    /// <summary>
    /// Resolves the effective quota for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="request">The module's requested resources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective quota. Never throws for a missing override row.</returns>
    public async Task<EffectiveModuleQuota> ResolveAsync(
        string moduleId,
        ModuleQuotaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(request);

        ModuleQuota? overrides = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            overrides = await dbContext.ModuleQuotas
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.ModuleId == moduleId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Quota resolution must never block a module start on a transient database problem:
            // fall back to the policy ceiling, which is the safe side of the min().
            overrides = null;
        }

        var defaultMemory = _supervisor.DefaultMemoryLimitMb > 0 ? _supervisor.DefaultMemoryLimitMb : (int?)null;

        return new EffectiveModuleQuota(
            MemoryLimitMb: Resolve(request.MemoryLimitMb, overrides?.MemoryLimitMb, _policy.MaxMemoryLimitMb, defaultMemory),
            CpuPercent: Resolve(request.CpuPercent, overrides?.CpuPercent, _policy.MaxCpuPercent),
            PidsLimit: Math.Max(1, _policy.MaxPids),
            DataDirMb: Resolve(request.DataDirMb, overrides?.DataDirMb, _policy.MaxDataDirMb),
            DatabaseMb: Resolve(request.DatabaseMb, overrides?.DatabaseMb, _policy.MaxDatabaseMb),
            ConnectionLimit: Math.Clamp(
                Resolve(
                    request.ConnectionLimit,
                    overrides?.ConnectionLimit,
                    _policy.MaxConnectionLimit,
                    _policy.DefaultConnectionLimit) ?? _policy.DefaultConnectionLimit,
                1,
                1000),
            RequestsPerMinute: Resolve(
                request.RequestsPerMinute, overrides?.RequestsPerMinute, _policy.MaxRequestsPerMinute),
            EventsPerMinute: Resolve(
                request.EventsPerMinute, overrides?.EventsPerMinute, _policy.MaxEventsPerMinute),
            NotificationsPerMinute: Resolve(
                request.NotificationsPerMinute, overrides?.NotificationsPerMinute, _policy.MaxNotificationsPerMinute),
            AuditEntriesPerMinute: Resolve(
                request.AuditEntriesPerMinute, overrides?.AuditEntriesPerMinute, _policy.MaxAuditEntriesPerMinute));
    }

    /// <summary>
    /// Applies the quota rule to one dimension: the lowest specified value among the module's
    /// request and the admin override wins, a dimension with neither falls back to the default,
    /// and the policy ceiling always caps the result.
    /// </summary>
    private static int? Resolve(int? request, int? overrideValue, int? ceiling, int? defaultValue = null)
    {
        var value = MinOf(request, overrideValue) ?? defaultValue;
        return value is int current ? MinOf(current, ceiling) : null;
    }

    private static int? MinOf(int? a, int? b, int? c = null)
    {
        int? result = null;
        foreach (var value in new[] { a, b, c })
        {
            if (value is not int current || current <= 0)
            {
                continue;
            }

            result = result is int existing ? Math.Min(existing, current) : current;
        }

        return result;
    }
}
