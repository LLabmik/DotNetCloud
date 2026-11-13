using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Quotas;

/// <summary>
/// Periodically measures each sandboxed module's database and data-directory footprint and
/// suspends modules that exceed their measured quota (F10, §4.11).
/// </summary>
/// <remarks>
/// <para>
/// Neither PostgreSQL nor SQL Server offers a per-role disk quota, and all module processes share
/// one OS account, so the guarantee here is <em>detection + suspension</em> (R22): an over-quota
/// module is stopped, its <see cref="InstalledModule.SandboxStatus"/> becomes
/// <see cref="ModuleSandboxStatus.QuotaExceeded"/>, an audit entry is written, and the supervisor
/// refuses to start it again until usage drops below 90% of the limit or an admin raises the quota.
/// </para>
/// <para>
/// Only dimensions with a configured limit are measured, and a failed measurement is treated as
/// "unknown" — never as a violation.
/// </para>
/// </remarks>
internal sealed class ModuleUsageMonitor : BackgroundService
{
    private const double RecoveryThreshold = 0.9;
    private const long BytesPerMegabyte = 1024L * 1024L;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleQuotaResolver _quotas;
    private readonly ModuleUsageMeasurer _measurer;
    private readonly IProcessSupervisor _supervisor;
    private readonly ModuleQuotasOptions _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ModuleUsageMonitor> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleUsageMonitor"/> class.</summary>
    /// <param name="scopeFactory">Scope factory for the core database context and audit logger.</param>
    /// <param name="quotas">Effective quota resolver.</param>
    /// <param name="measurer">Database and data-directory size measurer.</param>
    /// <param name="supervisor">Supervisor used to stop an over-quota module.</param>
    /// <param name="policy">Policy options (sweep interval).</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="logger">Logger.</param>
    public ModuleUsageMonitor(
        IServiceScopeFactory scopeFactory,
        ModuleQuotaResolver quotas,
        ModuleUsageMeasurer measurer,
        IProcessSupervisor supervisor,
        IOptions<ModuleQuotasOptions> policy,
        TimeProvider timeProvider,
        ILogger<ModuleUsageMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _quotas = quotas;
        _measurer = measurer;
        _supervisor = supervisor;
        _policy = policy.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _policy.UsageCheckIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Module usage sweep failed");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs one measurement sweep. Exposed for tests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var modules = await dbContext.InstalledModules
            .AsNoTracking()
            .Where(m => m.DataStoreMode == ModuleDataStoreMode.Isolated && !m.IsRequired)
            .ToListAsync(cancellationToken);

        if (modules.Count == 0)
        {
            return;
        }

        var stores = await dbContext.ModuleDataStores
            .AsNoTracking()
            .ToDictionaryAsync(d => d.ModuleId, cancellationToken);

        foreach (var module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var quota = await _quotas.ResolveAsync(module.ModuleId, new ModuleQuotaRequest(), cancellationToken);
            if (quota.DatabaseMb is null && quota.DataDirMb is null)
            {
                continue;
            }

            long? databaseBytes = null;
            if (quota.DatabaseMb is not null && stores.TryGetValue(module.ModuleId, out var store))
            {
                databaseBytes = await _measurer.MeasureDatabaseBytesAsync(store.SchemaName, cancellationToken);
            }

            long? dataDirectoryBytes = quota.DataDirMb is not null
                ? _measurer.MeasureDataDirectoryBytes(module.ModuleId)
                : null;

            var overLimit = IsOverLimit(databaseBytes, quota.DatabaseMb)
                || IsOverLimit(dataDirectoryBytes, quota.DataDirMb);

            if (overLimit && module.SandboxStatus != ModuleSandboxStatus.QuotaExceeded)
            {
                await SuspendAsync(
                    scope, dbContext, module.ModuleId, databaseBytes, dataDirectoryBytes, quota, cancellationToken);
            }
            else if (module.SandboxStatus == ModuleSandboxStatus.QuotaExceeded
                     && !overLimit
                     && IsWithinRecovery(databaseBytes, quota.DatabaseMb)
                     && IsWithinRecovery(dataDirectoryBytes, quota.DataDirMb))
            {
                await SetSandboxStatusAsync(dbContext, module.ModuleId, ModuleSandboxStatus.Active, cancellationToken);

                _logger.LogInformation(
                    "Module {ModuleId} is back under its quota; it may be started again",
                    LogSanitizer.Sanitize(module.ModuleId));
            }
        }
    }

    /// <summary>Whether a measured usage in bytes exceeds a limit in megabytes.</summary>
    /// <param name="usageBytes">Measured usage, or null when unmeasured.</param>
    /// <param name="limitMb">The limit in megabytes, or null when the dimension is unlimited.</param>
    /// <returns><see langword="true"/> only when the usage is known and over the limit.</returns>
    internal static bool IsOverLimit(long? usageBytes, int? limitMb) =>
        usageBytes is long usage && limitMb is > 0 && usage > (long)limitMb.Value * BytesPerMegabyte;

    /// <summary>Whether usage recovered far enough below the limit for a suspended module to restart.</summary>
    /// <param name="usageBytes">Measured usage, or null when unmeasured.</param>
    /// <param name="limitMb">The limit in megabytes, or null when the dimension is unlimited.</param>
    /// <returns><see langword="true"/> when unmeasured or at or below 90% of the limit.</returns>
    internal static bool IsWithinRecovery(long? usageBytes, int? limitMb) =>
        usageBytes is not long usage
        || limitMb is not > 0
        || usage <= (long)(limitMb.Value * BytesPerMegabyte * RecoveryThreshold);

    private async Task SuspendAsync(
        IServiceScope scope,
        CoreDbContext dbContext,
        string moduleId,
        long? databaseBytes,
        long? dataDirectoryBytes,
        EffectiveModuleQuota quota,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Module {ModuleId} exceeded its resource quota (database: {DatabaseBytes} bytes of {DatabaseLimitMb} MB, " +
            "data directory: {DataDirectoryBytes} bytes of {DataDirLimitMb} MB); stopping it",
            LogSanitizer.Sanitize(moduleId),
            databaseBytes,
            quota.DatabaseMb,
            dataDirectoryBytes,
            quota.DataDirMb);

        try
        {
            await _supervisor.StopModuleAsync(moduleId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not stop over-quota module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }

        await SetSandboxStatusAsync(dbContext, moduleId, ModuleSandboxStatus.QuotaExceeded, cancellationToken);

        try
        {
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = "dotnetcloud.core",
                    Action = AuditAction.Update,
                    EntityType = "ModuleQuota",
                    EntityId = Guid.CreateVersion7(),
                    Description = $"module-quota-exceeded:{moduleId}",
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not audit the quota suspension of module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }
    }

    private async Task SetSandboxStatusAsync(
        CoreDbContext dbContext,
        string moduleId,
        ModuleSandboxStatus status,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (row is null || row.SandboxStatus == status)
        {
            return;
        }

        row.SandboxStatus = status;
        row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
