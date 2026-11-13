using DotNetCloud.Core;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Services;
using DotNetCloud.ModulePackaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ModuleDiscoveryService _discovery;
    private readonly ModuleCredentialStore _credentials;
    private readonly IModulePackageInstaller _installer;
    private readonly IModuleStoreService _store;
    private readonly IModuleSettingsStore _settings;
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
        ModuleDiscoveryService discovery,
        ModuleCredentialStore credentials,
        IModulePackageInstaller installer,
        IModuleStoreService store,
        IModuleSettingsStore settings,
        ILogger<AdminModuleService> logger)
    {
        _dbContext = dbContext;
        _processSupervisor = processSupervisor;
        _dataStores = dataStores;
        _quotas = quotas;
        _quotaCache = quotaCache;
        _accessEvaluator = accessEvaluator;
        _discovery = discovery;
        _credentials = credentials;
        _installer = installer;
        _store = store;
        _settings = settings;
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

        if (module is null)
        {
            return null;
        }

        var dto = MapToDto(module);
        var manifest = TryLoadManifest(moduleId);
        EnrichFromManifest(dto, manifest, module.CapabilityGrants?.ToList() ?? []);
        return dto;
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

        var existing = await _dbContext.ModuleCapabilityGrants
            .AsTracking()
            .FirstOrDefaultAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName);

        if (existing is not null && ModuleCapabilityGrantStatus.IsGranted(existing.Status))
        {
            _logger.LogInformation("Capability {Capability} already granted to module {ModuleId}",
                LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId));
            return MapGrantToDto(existing);
        }

        if (existing is not null)
        {
            // Approving a pending or previously rejected request flips the decision rather than
            // adding a second row: the unique (module, capability) constraint is the decision.
            existing.Status = ModuleCapabilityGrantStatus.Granted;
            existing.GrantedAt = DateTime.UtcNow;
            existing.GrantedByUserId = grantedByUserId;
            existing.RejectedAtUtc = null;
            existing.RejectedByUserId = null;

            await _dbContext.SaveChangesAsync();
            _accessEvaluator.Invalidate(moduleId);

            _logger.LogInformation("Granted capability {Capability} to module {ModuleId} by user {UserId}",
                LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId), grantedByUserId);

            return MapGrantToDto(existing);
        }

        var grant = new ModuleCapabilityGrant
        {
            Id = Guid.CreateVersion7(),
            ModuleId = moduleId,
            CapabilityName = capabilityName,
            GrantedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
            Status = ModuleCapabilityGrantStatus.Granted,
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
    public async Task<ModuleCapabilityGrantDto?> RejectCapabilityAsync(
        string moduleId,
        string capabilityName,
        Guid rejectedByUserId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(capabilityName);

        var module = await _dbContext.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            return null;
        }

        var existing = await _dbContext.ModuleCapabilityGrants
            .AsTracking()
            .FirstOrDefaultAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName);

        if (existing is null)
        {
            // Rejecting a request the module never made is still worth recording: it is the
            // administrator's answer for this capability and stops a later reconcile from raising it
            // again as pending.
            existing = new ModuleCapabilityGrant
            {
                Id = Guid.CreateVersion7(),
                ModuleId = moduleId,
                CapabilityName = capabilityName,
                GrantedAt = DateTime.UtcNow,
            };

            _dbContext.ModuleCapabilityGrants.Add(existing);
        }

        existing.Status = ModuleCapabilityGrantStatus.Rejected;
        existing.GrantedByUserId = null;
        existing.RejectedAtUtc = DateTime.UtcNow;
        existing.RejectedByUserId = rejectedByUserId;

        await _dbContext.SaveChangesAsync();

        // Rejection must take effect immediately, not after the cache TTL expires.
        _accessEvaluator.Invalidate(moduleId);

        _logger.LogInformation("Rejected capability {Capability} for module {ModuleId} by user {UserId}",
            LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId), rejectedByUserId);

        return MapGrantToDto(existing);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeCapabilityAsync(string moduleId, string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(capabilityName);

        var grant = await _dbContext.ModuleCapabilityGrants
            .AsTracking()
            .FirstOrDefaultAsync(g => g.ModuleId == moduleId && g.CapabilityName == capabilityName);

        if (grant is null || !ModuleCapabilityGrantStatus.IsGranted(grant.Status))
        {
            return false;
        }

        // Revoking records a decision instead of deleting the row: deleting would let the next
        // start-up reconciliation raise the capability again as pending, which reads as "the core
        // keeps asking" rather than as the operator's answer.
        grant.Status = ModuleCapabilityGrantStatus.Rejected;
        grant.GrantedByUserId = null;
        grant.RejectedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Revocation must take effect immediately, not after the cache TTL expires.
        _accessEvaluator.Invalidate(moduleId);

        _logger.LogInformation("Revoked capability {Capability} from module {ModuleId}",
            LogSanitizer.Sanitize(capabilityName), LogSanitizer.Sanitize(moduleId));
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleSettingDto>?> ListSettingsAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return null;
        }

        var entries = await _settings.ListAsync(moduleId, cancellationToken);
        return entries.Select(entry => new ModuleSettingDto
        {
            ModuleId = entry.ModuleId,
            Key = entry.Key,
            IsSecret = entry.IsSecret,
            HasValue = entry.HasValue,
            Value = entry.Value,
            UpdatedAtUtc = entry.UpdatedAtUtc,
            UpdatedByUserId = entry.UpdatedByUserId,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<ModuleSettingDto?> SetSettingAsync(
        string moduleId,
        string key,
        UpsertModuleSettingDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(dto);

        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return null;
        }

        await _settings.SetAsync(moduleId, key, dto.Value, dto.IsSecret, updatedByUserId, cancellationToken);

        _logger.LogInformation(
            "Module setting {ModuleId}:{Key} updated by admin (secret: {IsSecret})",
            LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(key), dto.IsSecret);

        // The value is deliberately not echoed back: a secret must not round-trip through a response.
        return new ModuleSettingDto
        {
            ModuleId = moduleId,
            Key = key,
            IsSecret = dto.IsSecret,
            HasValue = true,
            Value = dto.IsSecret ? null : dto.Value,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = updatedByUserId,
        };
    }

    /// <inheritdoc/>
    public async Task<bool?> DeleteSettingAsync(
        string moduleId, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return null;
        }

        var deleted = await _settings.DeleteAsync(moduleId, key, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation(
                "Module setting {ModuleId}:{Key} deleted by admin",
                LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(key));
        }

        return deleted;
    }

    private async Task<bool> ModuleExistsAsync(string moduleId, CancellationToken cancellationToken) =>
        await _dbContext.InstalledModules
            .AsNoTracking()
            .AnyAsync(m => m.ModuleId == moduleId, cancellationToken);

    /// <inheritdoc/>
    public async Task<ModuleUninstallResultDto?> UninstallModuleAsync(
        string moduleId,
        UninstallModuleDto dto,
        Guid uninstalledByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(dto);

        var module = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        if (module.IsRequired || RequiredModules.IsRequired(moduleId))
        {
            return Failed(moduleId, $"Module '{moduleId}' is architecturally required and cannot be uninstalled.");
        }

        var result = new ModuleUninstallResultDto { Success = true, ModuleId = moduleId };

        // 1. Stop the process. A stop failure must not strand the installation record, so it is
        //    logged and the uninstall continues.
        try
        {
            await _processSupervisor.StopModuleAsync(moduleId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Stopping module {ModuleId} during uninstall failed: {Error}",
                LogSanitizer.Sanitize(moduleId), ex.Message);
        }

        // 2. The launch credential dies with the module; any lingering process can no longer call core.
        _credentials.Revoke(moduleId);

        // 3. Data store: destroy, or keep it as Orphaned for the admin cleanup list.
        var store = await _dbContext.ModuleDataStores
            .AsTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);

        if (store is not null)
        {
            if (dto.DestroyData)
            {
                try
                {
                    await _dataStores.DestroyAsync(moduleId, cancellationToken);
                    result.DataStoreDestroyed = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var error = ModuleDataStoreProvisioningService.SanitizeError(ex.Message);
                    _logger.LogError(
                        "Destroying the data store for module {ModuleId} during uninstall failed: {Error}",
                        LogSanitizer.Sanitize(moduleId), error);

                    // Keep the installation intact so the operator can retry instead of ending up
                    // with a half-uninstalled module and an unmanaged schema.
                    return Failed(moduleId, error);
                }
            }
            else
            {
                store.Status = ModuleDataStoreStatus.Orphaned;
                store.EncryptedConnectionString = null;
                store.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                result.DataStoreRetained = true;
            }
        }

        // 4. Count the consent grants the cascade is about to remove (reported back to the caller).
        result.ConsentsRevoked = await _dbContext.ModuleUserConsents
            .AsNoTracking()
            .CountAsync(c => c.ModuleId == moduleId, cancellationToken);

        // 5. Binaries. Path containment and the id check live in the discovery service.
        if (dto.RemoveFiles)
        {
            result.FilesRemoved = _discovery.RemoveModuleDirectory(moduleId);
        }

        // 6. Installation record. Capability grants, quotas, settings and consents cascade.
        _dbContext.InstalledModules.Remove(module);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _accessEvaluator.Invalidate(moduleId);
        _quotaCache.Invalidate(moduleId);

        _logger.LogInformation(
            "Module {ModuleId} uninstalled by user {UserId} (dataDestroyed={DataDestroyed}, dataRetained={DataRetained}, filesRemoved={FilesRemoved})",
            LogSanitizer.Sanitize(moduleId), uninstalledByUserId,
            result.DataStoreDestroyed, result.DataStoreRetained, result.FilesRemoved);

        return result;
    }

    /// <inheritdoc/>
    public async Task<ModuleCredentialDto?> GetCredentialAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var installed = await _dbContext.InstalledModules
            .AsNoTracking()
            .AnyAsync(m => m.ModuleId == moduleId);

        return installed ? MapCredential(moduleId) : null;
    }

    /// <inheritdoc/>
    public async Task<ModuleCredentialDto?> RotateCredentialAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var installed = await _dbContext.InstalledModules
            .AsNoTracking()
            .AnyAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (!installed)
        {
            return null;
        }

        // Mark the rotation, then hand the module a brand-new secret by restarting it. The restart's
        // fresh issue preserves the rotated-at stamp but replaces the token, so the old value is dead.
        _credentials.Rotate(moduleId);

        if (_processSupervisor.IsModuleRunning(moduleId))
        {
            await _processSupervisor.RestartModuleAsync(moduleId, cancellationToken);
        }

        _logger.LogInformation(
            "Launch credential for module {ModuleId} rotated by admin", LogSanitizer.Sanitize(moduleId));

        return MapCredential(moduleId);
    }

    /// <inheritdoc/>
    public async Task<ModuleCredentialDto?> RevokeCredentialAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var installed = await _dbContext.InstalledModules
            .AsNoTracking()
            .AnyAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (!installed)
        {
            return null;
        }

        _credentials.Revoke(moduleId);

        _logger.LogWarning(
            "Launch credential for module {ModuleId} revoked by admin; the running process must be restarted",
            LogSanitizer.Sanitize(moduleId));

        return MapCredential(moduleId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleDataStoreDto>> ListOrphanedDataStoresAsync(
        CancellationToken cancellationToken = default)
    {
        var stores = await _dbContext.ModuleDataStores
            .AsNoTracking()
            .Where(d => d.Status == ModuleDataStoreStatus.Orphaned)
            .OrderBy(d => d.ModuleId)
            .ToListAsync(cancellationToken);

        return stores.Select(store => new ModuleDataStoreDto
        {
            ModuleId = store.ModuleId,
            DataStoreMode = nameof(ModuleDataStoreMode.Isolated),
            Provisioned = true,
            Provider = store.Provider,
            SchemaName = store.SchemaName,
            PrincipalName = store.PrincipalName,
            Status = store.Status.ToString(),
            LastError = store.LastError,
            CreatedAtUtc = store.CreatedAtUtc,
            UpdatedAtUtc = store.UpdatedAtUtc,
            CredentialsReadable = false,
        }).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleStorePackageDto>> ListStorePackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var packages = await _store.ListPackagesAsync(cancellationToken);

        var installed = await _dbContext.InstalledModules
            .AsNoTracking()
            .Select(module => new { module.ModuleId, module.Version })
            .ToDictionaryAsync(row => row.ModuleId, row => row.Version, cancellationToken);

        return packages
            .Select(package => new ModuleStorePackageDto
            {
                ModuleId = package.ModuleId,
                Name = package.Name,
                Version = package.Version,
                File = package.File,
                SizeBytes = package.SizeBytes,
                Sha256 = package.Sha256,
                PublisherId = package.PublisherId,
                Capabilities = package.Capabilities,
                UserConsentScopes = package.UserConsentScopes,
                KeyId = package.KeyId,
                TrustTier = package.TrustTier.ToWireValue(),
                Yanked = package.Yanked,
                YankReason = package.YankReason,
                YankSeverity = package.YankSeverity,
                InstalledVersion = installed.GetValueOrDefault(package.ModuleId),
            })
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleRevocationWarningDto>> ListRevocationWarningsAsync(
        CancellationToken cancellationToken = default)
    {
        var installed = await _dbContext.InstalledModules
            .AsNoTracking()
            .Where(module => module.TrustTier == ModuleTrustTier.Store)
            .Select(module => new { module.ModuleId, module.Version })
            .ToListAsync(cancellationToken);

        if (installed.Count == 0)
        {
            return [];
        }

        IReadOnlyList<ModuleStorePackage> catalogue;
        try
        {
            catalogue = await _store.ListPackagesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is ModulePackageException or IOException
                                       or UnauthorizedAccessException or HttpRequestException
                                       or TaskCanceledException)
        {
            // A store that cannot be reached produces no warnings: "cannot check" is not "all clear",
            // and an offline store must not break the admin page.
            _logger.LogWarning(
                "Module store status check skipped: {Error}", LogSanitizer.Sanitize(ex.Message));
            return [];
        }

        var warnings = new List<ModuleRevocationWarningDto>();

        foreach (var module in installed)
        {
            var listed = catalogue
                .Where(package => string.Equals(package.ModuleId, module.ModuleId, StringComparison.Ordinal))
                .ToList();

            // The newest version an operator can actually upgrade to: a withdrawn version is never a
            // destination, which is exactly why rollback refuses one.
            var available = listed
                .Where(package => !package.Yanked)
                .OrderByDescending(package => package.Version, ModuleStoreVersionComparer.Instance)
                .FirstOrDefault();

            var entry = listed.FirstOrDefault(package =>
                string.Equals(package.Version, module.Version, StringComparison.Ordinal));

            if (entry is null)
            {
                warnings.Add(new ModuleRevocationWarningDto
                {
                    ModuleId = module.ModuleId,
                    InstalledVersion = module.Version,
                    Severity = "unlisted",
                    Reason = "The configured store no longer lists this version. It may have been "
                             + "taken down; check the store's security advisory.",
                    AvailableVersion = available?.Version,
                });
            }
            else if (entry.Yanked)
            {
                warnings.Add(new ModuleRevocationWarningDto
                {
                    ModuleId = module.ModuleId,
                    InstalledVersion = module.Version,
                    Severity = entry.YankSeverity ?? "deprecation",
                    Reason = entry.YankReason,
                    AvailableVersion = available?.Version,
                });
            }
        }

        return warnings
            .OrderBy(warning => warning.Severity switch
            {
                "security" => 0,
                "unlisted" => 1,
                _ => 2,
            })
            .ThenBy(warning => warning.ModuleId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public Task<ModulePackageReviewDto> InspectPackageAsync(
        string packagePath, CancellationToken cancellationToken = default) =>
        _installer.InspectAsync(packagePath, signaturePath: null, cancellationToken);

    /// <inheritdoc/>
    public Task<ModuleInstallResultDto> InstallPackageAsync(
        string packagePath,
        InstallModulePackageDto dto,
        Guid installedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(dto);

        return _installer.InstallAsync(
            new ModulePackageInstallRequest
            {
                PackagePath = packagePath,
                ExpectedSha256 = dto.ExpectedSha256,
                AllowUnverified = dto.AllowUnverified,
                StartAfterInstall = dto.StartAfterInstall,
                AcknowledgedCapabilities = dto.AcknowledgedCapabilities,
                AcknowledgedScopes = dto.AcknowledgedScopes,
                InstalledByUserId = installedByUserId,
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ModuleInstallResultDto> RollbackModuleAsync(
        string moduleId,
        Guid rolledBackByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        _logger.LogInformation(
            "Module {ModuleId} rollback requested by user {UserId}",
            LogSanitizer.Sanitize(moduleId), rolledBackByUserId);

        return _installer.RollbackAsync(moduleId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ModulePackageReviewDto> InspectStorePackageAsync(
        string moduleId,
        string? version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var resolved = await _store.ResolveAsync(moduleId, version, cancellationToken)
            ?? throw new ModulePackageException(
                $"'{moduleId}' is not listed by the configured module store source.");

        return await _installer.InspectAsync(
            resolved.PackagePath, resolved.SignaturePath, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ModuleInstallResultDto> InstallStorePackageAsync(
        string moduleId,
        InstallStorePackageDto dto,
        Guid installedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(dto);

        var resolved = await _store.ResolveAsync(moduleId, dto.Version, cancellationToken)
            ?? throw new ModulePackageException(
                $"'{moduleId}'{(dto.Version is null ? string.Empty : " " + dto.Version)} "
                + "is not listed by the configured module store source.");

        if (resolved.Package.Yanked)
        {
            throw new ModulePackageException(
                $"'{moduleId}' {resolved.Package.Version} has been withdrawn: "
                + (resolved.Package.YankReason ?? "no reason given"));
        }

        return await _installer.InstallAsync(
            new ModulePackageInstallRequest
            {
                PackagePath = resolved.PackagePath,
                SignaturePath = resolved.SignaturePath,
                ExpectedSha256 = dto.ExpectedSha256,
                AllowUnverified = dto.AllowUnverified,
                StartAfterInstall = dto.StartAfterInstall,
                AcknowledgedCapabilities = dto.AcknowledgedCapabilities,
                AcknowledgedScopes = dto.AcknowledgedScopes,
                InstalledByUserId = installedByUserId,
            },
            cancellationToken);
    }

    private ModuleCredentialDto MapCredential(string moduleId)
    {
        var status = _credentials.GetStatus(moduleId);

        return new ModuleCredentialDto
        {
            ModuleId = moduleId,
            Active = status?.IsActive ?? false,
            Hint = status?.Hint,
            IssuedAtUtc = status?.IssuedAtUtc,
            RotatedAtUtc = status?.RotatedAtUtc,
            RevokedAtUtc = status?.RevokedAtUtc,
        };
    }

    private static ModuleUninstallResultDto Failed(string moduleId, string error) => new()
    {
        Success = false,
        ModuleId = moduleId,
        Error = error,
    };

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
            PublisherId = entity.PublisherId,
            DataStoreMode = entity.DataStoreMode.ToString(),
            SandboxStatus = entity.SandboxStatus.ToString(),
            GrantedCapabilities = entity.CapabilityGrants?
                .Where(grant => ModuleCapabilityGrantStatus.IsGranted(grant.Status))
                .Select(MapGrantToDto)
                .ToList() ?? [],
        };
    }

    /// <summary>
    /// Reads the module's manifest from disk, when it can be found and is valid.
    /// </summary>
    /// <remarks>
    /// The manifest is what the module *asks for*; the database holds what the administrator
    /// *decided*. The detail page needs both to show a pending request at all. A manifest that has
    /// disappeared must not break the page, so failures degrade to "no declaration" rather than throw.
    /// </remarks>
    private ModuleManifestData? TryLoadManifest(string moduleId)
    {
        try
        {
            var discovered = _discovery.DiscoverModule(moduleId);
            if (discovered?.ManifestPath is null)
            {
                return null;
            }

            var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
            var result = loader.LoadAndValidate(discovered.ManifestPath, moduleId);
            return result.IsValid ? result.Manifest : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read the manifest of module {ModuleId}",
                LogSanitizer.Sanitize(moduleId));
            return null;
        }
    }

    /// <summary>
    /// Fills the manifest-derived fields of a module DTO, including the per-capability decisions.
    /// </summary>
    /// <param name="dto">The DTO to enrich.</param>
    /// <param name="manifest">The module's manifest, when it could be read.</param>
    /// <param name="decisions">Every stored capability decision for the module, including rejected ones.</param>
    private static void EnrichFromManifest(
        ModuleDto dto,
        ModuleManifestData? manifest,
        IReadOnlyCollection<ModuleCapabilityGrant> decisions)
    {
        if (manifest is null)
        {
            return;
        }

        dto.Name = manifest.Name;
        dto.Description = manifest.Description;
        dto.RequiredCapabilities = manifest.EffectiveCapabilities.ToList();
        dto.PublishedEvents = manifest.EffectivePublishedEvents.ToList();
        dto.SubscribedEvents = manifest.EffectiveSubscribedEvents.ToList();
        dto.CapabilityRequests = BuildCapabilityRequests(manifest, decisions);
    }

    private static List<ModuleCapabilityRequestDto> BuildCapabilityRequests(
        ModuleManifestData manifest,
        IReadOnlyCollection<ModuleCapabilityGrant> decisions)
    {
        var byCapability = decisions.ToDictionary(
            grant => grant.CapabilityName, StringComparer.OrdinalIgnoreCase);

        var requests = new List<ModuleCapabilityRequestDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in manifest.EffectiveCapabilities)
        {
            if (!seen.Add(capability))
            {
                continue;
            }

            var tier = CapabilityCatalog.GetCapabilityTier(capability);

            // Public capabilities need no decision; the admin page only shows what is gated.
            if (tier is null or CapabilityTier.Public or CapabilityTier.Forbidden)
            {
                continue;
            }

            byCapability.TryGetValue(capability, out var decision);
            var status = decision?.Status ?? ModuleCapabilityGrantStatus.Pending;
            var rejected = string.Equals(status, ModuleCapabilityGrantStatus.Rejected, StringComparison.OrdinalIgnoreCase);

            requests.Add(new ModuleCapabilityRequestDto
            {
                CapabilityName = capability,
                Tier = tier.ToString()!,
                Status = status,
                DecidedAtUtc = rejected ? decision?.RejectedAtUtc : decision?.GrantedAt,
                DecidedByUserId = rejected ? decision?.RejectedByUserId : decision?.GrantedByUserId,
            });
        }

        return requests;
    }

    private static ModuleCapabilityGrantDto MapGrantToDto(ModuleCapabilityGrant grant)
    {
        return new ModuleCapabilityGrantDto
        {
            ModuleId = grant.ModuleId,
            CapabilityName = grant.CapabilityName,
            Status = grant.Status,
            GrantedAt = grant.GrantedAt,
            GrantedByUserId = grant.GrantedByUserId,
            RejectedAtUtc = grant.RejectedAtUtc,
        };
    }
}
