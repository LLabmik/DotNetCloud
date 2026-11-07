using DotNetCloud.Core;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Implements <see cref="IAdminModuleService"/> using EF Core, <see cref="CoreDbContext"/>,
/// and <see cref="IProcessSupervisor"/> for runtime module lifecycle operations.
/// </summary>
internal sealed class AdminModuleService : IAdminModuleService
{
    private readonly CoreDbContext _dbContext;
    private readonly IProcessSupervisor _processSupervisor;
    private readonly IModuleDataStoreProvisioningService _dataStores;
    private readonly ModuleQuotaResolver _quotas;
    private readonly ModuleQuotaCache _quotaCache;
    private readonly IModuleAccessEvaluator _accessEvaluator;
    private readonly ILogger<AdminModuleService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminModuleService"/>.
    /// </summary>
    public AdminModuleService(
        CoreDbContext dbContext,
        IProcessSupervisor processSupervisor,
        IModuleDataStoreProvisioningService dataStores,
        ModuleQuotaResolver quotas,
        ModuleQuotaCache quotaCache,
        IModuleAccessEvaluator accessEvaluator,
        ILogger<AdminModuleService> logger)
    {
        _dbContext = dbContext;
        _processSupervisor = processSupervisor;
        _dataStores = dataStores;
        _quotas = quotas;
        _quotaCache = quotaCache;
        _accessEvaluator = accessEvaluator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleDto>> ListModulesAsync()
    {
        var modules = await _dbContext.InstalledModules
            .AsNoTracking()
            .Include(m => m.CapabilityGrants)
            .OrderBy(m => m.ModuleId)
            .ToListAsync();

        return modules.Select(MapToDto).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<ModuleDto?> GetModuleAsync(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var module = await _dbContext.InstalledModules
            .AsNoTracking()
            .Include(m => m.CapabilityGrants)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        return module is null ? null : MapToDto(module);
    }

    /// <inheritdoc/>
    public async Task<bool> StartModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return false;
        }

        await _processSupervisor.StartModuleAsync(moduleId, cancellationToken);

        module.Status = "Enabled";
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Module {ModuleId} started", LogSanitizer.Sanitize(moduleId));
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> StopModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return false;
        }

        if (module.IsRequired)
        {
            throw new InvalidOperationException(
                $"Module '{moduleId}' is architecturally required and cannot be stopped.");
        }

        await _processSupervisor.StopModuleAsync(moduleId, cancellationToken);

        module.Status = "Disabled";
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Module {ModuleId} stopped", LogSanitizer.Sanitize(moduleId));
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RestartModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var module = await _dbContext.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return false;
        }

        await _processSupervisor.RestartModuleAsync(moduleId, cancellationToken);

        _logger.LogInformation("Module {ModuleId} restarted", LogSanitizer.Sanitize(moduleId));
        return true;
    }

    /// <inheritdoc/>
    public async Task<ModuleCapabilityGrantDto?> GrantCapabilityAsync(
        string moduleId,
        string capabilityName,
        Guid grantedByUserId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(capabilityName);

        var module = await _dbContext.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            return null;
        }

        // Check if already granted
        var existing = await _dbContext.ModuleCapabilityGrants
            .FirstOrDefaultAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName);

        if (existing is not null)
        {
            _logger.LogInformation("Capability {Capability} already granted to module {ModuleId}",
                LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId));
            return MapGrantToDto(existing);
        }

        var grant = new ModuleCapabilityGrant
        {
            Id = Guid.CreateVersion7(),
            ModuleId = moduleId,
            CapabilityName = capabilityName,
            GrantedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
        };

        _dbContext.ModuleCapabilityGrants.Add(grant);
        await _dbContext.SaveChangesAsync();

        // Enforcement caches the module's grant set; a grant must apply on the next call.
        _accessEvaluator.Invalidate(moduleId);

        _logger.LogInformation("Granted capability {Capability} to module {ModuleId} by user {UserId}",
            LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId), grantedByUserId);

        return MapGrantToDto(grant);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeCapabilityAsync(string moduleId, string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(capabilityName);

        var grant = await _dbContext.ModuleCapabilityGrants
            .FirstOrDefaultAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName);

        if (grant is null)
        {
            return false;
        }

        _dbContext.ModuleCapabilityGrants.Remove(grant);
        await _dbContext.SaveChangesAsync();

        // Revocation must take effect immediately, not after the cache TTL expires.
        _accessEvaluator.Invalidate(moduleId);

        _logger.LogInformation("Revoked capability {Capability} from module {ModuleId}",
            capabilityName, moduleId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ModuleDataStoreDto?> GetDataStoreAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var module = await _dbContext.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        var store = await _dbContext.ModuleDataStores
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);

        var connectionLimit = await GetConnectionLimitAsync(moduleId, cancellationToken);

        if (store is null)
        {
            return new ModuleDataStoreDto
            {
                ModuleId = moduleId,
                DataStoreMode = module.DataStoreMode.ToString(),
                Provisioned = false,
                Status = "NotProvisioned",
                ConnectionLimit = connectionLimit,
            };
        }

        var credentialsReadable = store.EncryptedConnectionString is not null
            && await _dataStores.GetConnectionStringAsync(moduleId, cancellationToken) is not null;

        return new ModuleDataStoreDto
        {
            ModuleId = moduleId,
            DataStoreMode = module.DataStoreMode.ToString(),
            Provisioned = true,
            Provider = store.Provider,
            SchemaName = store.SchemaName,
            PrincipalName = store.PrincipalName,
            Status = store.Status.ToString(),
            LastError = store.LastError,
            CreatedAtUtc = store.CreatedAtUtc,
            UpdatedAtUtc = store.UpdatedAtUtc,
            CredentialsReadable = credentialsReadable,
            ConnectionLimit = connectionLimit,
        };
    }

    /// <inheritdoc/>
    public async Task<ModuleDataStoreOperationResultDto?> ProvisionDataStoreAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        var rejection = RejectNonIsolatedModule(module);
        if (rejection is not null)
        {
            return rejection;
        }

        var connectionLimit = await GetConnectionLimitAsync(moduleId, cancellationToken);
        var result = await _dataStores.EnsureProvisionedAsync(moduleId, connectionLimit, cancellationToken);

        await PersistSandboxStatusAsync(
            module,
            result.Success ? ModuleSandboxStatus.Active : ModuleSandboxStatus.Error,
            cancellationToken);

        _logger.LogInformation(
            "Data store provisioning for module {ModuleId} completed with success={Success}",
            LogSanitizer.Sanitize(moduleId), result.Success);

        return MapDataStoreResult(result);
    }

    /// <inheritdoc/>
    public async Task<ModuleDataStoreOperationResultDto?> RotateDataStoreCredentialsAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        var rejection = RejectNonIsolatedModule(module);
        if (rejection is not null)
        {
            return rejection;
        }

        var connectionLimit = await GetConnectionLimitAsync(moduleId, cancellationToken);
        var result = await _dataStores.RotateCredentialsAsync(moduleId, connectionLimit, cancellationToken);

        await PersistSandboxStatusAsync(
            module,
            result.Success ? ModuleSandboxStatus.Active : ModuleSandboxStatus.Error,
            cancellationToken);

        _logger.LogInformation(
            "Credential rotation for module {ModuleId} completed with success={Success}",
            LogSanitizer.Sanitize(moduleId), result.Success);

        return MapDataStoreResult(result);
    }

    /// <inheritdoc/>
    public async Task<ModuleDataStoreOperationResultDto?> DestroyDataStoreAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        if (module.IsRequired)
        {
            return new ModuleDataStoreOperationResultDto
            {
                Success = false,
                Error = $"Module '{moduleId}' is architecturally required and has no isolated data store.",
            };
        }

        var store = await _dbContext.ModuleDataStores
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);

        if (store is null)
        {
            return new ModuleDataStoreOperationResultDto { Success = true };
        }

        try
        {
            await _dataStores.DestroyAsync(moduleId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = ModuleDataStoreProvisioningService.SanitizeError(ex.Message);
            await PersistSandboxStatusAsync(module, ModuleSandboxStatus.Error, cancellationToken);
            _logger.LogError(
                "Destroying the data store for module {ModuleId} failed: {Error}",
                LogSanitizer.Sanitize(moduleId), error);
            return new ModuleDataStoreOperationResultDto { Success = false, Error = error };
        }

        await PersistSandboxStatusAsync(module, ModuleSandboxStatus.PendingProvisioning, cancellationToken);

        _logger.LogInformation(
            "Data store for module {ModuleId} destroyed by admin", LogSanitizer.Sanitize(moduleId));

        return new ModuleDataStoreOperationResultDto
        {
            Success = true,
            SchemaName = store.SchemaName,
            PrincipalName = store.PrincipalName,
        };
    }

    private static ModuleDataStoreOperationResultDto? RejectNonIsolatedModule(InstalledModule module)
    {
        if (module.IsRequired)
        {
            return new ModuleDataStoreOperationResultDto
            {
                Success = false,
                Error = $"Module '{module.ModuleId}' is architecturally required and uses the core schema.",
            };
        }

        if (module.DataStoreMode != ModuleDataStoreMode.Isolated)
        {
            return new ModuleDataStoreOperationResultDto
            {
                Success = false,
                Error = $"Module '{module.ModuleId}' does not declare dataStore: \"isolated\".",
            };
        }

        return null;
    }

    private async Task<int> GetConnectionLimitAsync(string moduleId, CancellationToken cancellationToken)
    {
        var effective = await _quotas.ResolveAsync(moduleId, new ModuleQuotaRequest(), cancellationToken);
        return effective.ConnectionLimit;
    }

    private async Task PersistSandboxStatusAsync(
        InstalledModule module, ModuleSandboxStatus status, CancellationToken cancellationToken)
    {
        module.SandboxStatus = status;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ModuleDataStoreOperationResultDto MapDataStoreResult(
        ModuleDataStoreProvisioningResult result) => new()
        {
            Success = result.Success,
            SchemaName = result.SchemaName,
            PrincipalName = result.PrincipalName,
            Error = result.Error,
        };

    /// <inheritdoc/>
    public async Task<ModuleQuotaDto?> GetQuotaAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var module = await _dbContext.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        var overrideRow = await _dbContext.ModuleQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.ModuleId == moduleId, cancellationToken);

        var effective = await _quotas.ResolveAsync(moduleId, new ModuleQuotaRequest(), cancellationToken);
        return MapQuotaToDto(moduleId, overrideRow, effective);
    }

    /// <inheritdoc/>
    public async Task<ModuleQuotaOperationResultDto?> UpdateQuotaAsync(
        string moduleId,
        UpdateModuleQuotaDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(dto);

        var module = await _dbContext.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        var invalid = FirstInvalidQuotaValue(dto);
        if (invalid is not null)
        {
            return new ModuleQuotaOperationResultDto { Success = false, Error = invalid };
        }

        var row = await _dbContext.ModuleQuotas
            .AsTracking()
            .FirstOrDefaultAsync(q => q.ModuleId == moduleId, cancellationToken);

        if (row is null)
        {
            row = new ModuleQuota { ModuleId = moduleId };
            _dbContext.ModuleQuotas.Add(row);
        }

        row.MemoryLimitMb = NormalizeQuotaValue(dto.MemoryLimitMb);
        row.CpuPercent = NormalizeQuotaValue(dto.CpuPercent);
        row.DataDirMb = NormalizeQuotaValue(dto.DataDirMb);
        row.DatabaseMb = NormalizeQuotaValue(dto.DatabaseMb);
        row.ConnectionLimit = NormalizeQuotaValue(dto.ConnectionLimit);
        row.RequestsPerMinute = NormalizeQuotaValue(dto.RequestsPerMinute);
        row.EventsPerMinute = NormalizeQuotaValue(dto.EventsPerMinute);
        row.NotificationsPerMinute = NormalizeQuotaValue(dto.NotificationsPerMinute);
        row.AuditEntriesPerMinute = NormalizeQuotaValue(dto.AuditEntriesPerMinute);
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = updatedByUserId == Guid.Empty ? null : updatedByUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Apply the new limits to the rate limiter immediately instead of waiting for the TTL.
        _quotaCache.Invalidate(moduleId);

        var effective = await _quotas.ResolveAsync(moduleId, new ModuleQuotaRequest(), cancellationToken);

        _logger.LogInformation(
            "Resource quota for module {ModuleId} updated by user {UserId}",
            LogSanitizer.Sanitize(moduleId), updatedByUserId);

        return new ModuleQuotaOperationResultDto
        {
            Success = true,
            Quota = MapQuotaToDto(moduleId, row, effective),
        };
    }

    private static int? NormalizeQuotaValue(int? value) => value is > 0 ? value : null;

    private static string? FirstInvalidQuotaValue(UpdateModuleQuotaDto dto)
    {
        (string Name, int? Value)[] values =
        [
            (nameof(dto.MemoryLimitMb), dto.MemoryLimitMb),
            (nameof(dto.CpuPercent), dto.CpuPercent),
            (nameof(dto.DataDirMb), dto.DataDirMb),
            (nameof(dto.DatabaseMb), dto.DatabaseMb),
            (nameof(dto.ConnectionLimit), dto.ConnectionLimit),
            (nameof(dto.RequestsPerMinute), dto.RequestsPerMinute),
            (nameof(dto.EventsPerMinute), dto.EventsPerMinute),
            (nameof(dto.NotificationsPerMinute), dto.NotificationsPerMinute),
            (nameof(dto.AuditEntriesPerMinute), dto.AuditEntriesPerMinute),
        ];

        var invalid = values.FirstOrDefault(v => v.Value is < 0);
        return invalid.Name is null
            ? null
            : $"{invalid.Name} must be positive; use 0 or null to inherit the policy default.";
    }

    private static ModuleQuotaDto MapQuotaToDto(
        string moduleId, ModuleQuota? row, EffectiveModuleQuota effective) => new()
        {
            ModuleId = moduleId,
            MemoryLimitMb = row?.MemoryLimitMb,
            CpuPercent = row?.CpuPercent,
            DataDirMb = row?.DataDirMb,
            DatabaseMb = row?.DatabaseMb,
            ConnectionLimit = row?.ConnectionLimit,
            RequestsPerMinute = row?.RequestsPerMinute,
            EventsPerMinute = row?.EventsPerMinute,
            NotificationsPerMinute = row?.NotificationsPerMinute,
            AuditEntriesPerMinute = row?.AuditEntriesPerMinute,
            UpdatedAtUtc = row?.UpdatedAtUtc,
            UpdatedByUserId = row?.UpdatedByUserId,
            EffectiveMemoryLimitMb = effective.MemoryLimitMb,
            EffectiveCpuPercent = effective.CpuPercent,
            EffectivePidsLimit = effective.PidsLimit,
            EffectiveDataDirMb = effective.DataDirMb,
            EffectiveDatabaseMb = effective.DatabaseMb,
            EffectiveConnectionLimit = effective.ConnectionLimit,
            EffectiveRequestsPerMinute = effective.RequestsPerMinute,
            EffectiveEventsPerMinute = effective.EventsPerMinute,
            EffectiveNotificationsPerMinute = effective.NotificationsPerMinute,
            EffectiveAuditEntriesPerMinute = effective.AuditEntriesPerMinute,
        };

    private static ModuleDto MapToDto(InstalledModule entity)
    {
        return new ModuleDto
        {
            Id = entity.ModuleId,
            Name = entity.ModuleId,
            Version = entity.Version,
            Status = entity.Status,
            InstalledAt = entity.InstalledAt,
            IsRequired = entity.IsRequired,
            TrustTier = entity.TrustTier.ToString(),
            DataStoreMode = entity.DataStoreMode.ToString(),
            SandboxStatus = entity.SandboxStatus.ToString(),
            GrantedCapabilities = entity.CapabilityGrants?
                .Select(MapGrantToDto)
                .ToList() ?? [],
        };
    }

    private static ModuleCapabilityGrantDto MapGrantToDto(ModuleCapabilityGrant grant)
    {
        return new ModuleCapabilityGrantDto
        {
            ModuleId = grant.ModuleId,
            CapabilityName = grant.CapabilityName,
            GrantedAt = grant.GrantedAt,
            GrantedByUserId = grant.GrantedByUserId,
        };
    }
}
