using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules.Supervisor;
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
    private readonly ILogger<AdminModuleService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminModuleService"/>.
    /// </summary>
    public AdminModuleService(
        CoreDbContext dbContext,
        IProcessSupervisor processSupervisor,
        ILogger<AdminModuleService> logger)
    {
        _dbContext = dbContext;
        _processSupervisor = processSupervisor;
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
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return false;
        }

        await _processSupervisor.StartModuleAsync(moduleId, cancellationToken);

        module.Status = "Enabled";
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Module {ModuleId} started", moduleId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> StopModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var module = await _dbContext.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (module is null)
        {
            return false;
        }

        await _processSupervisor.StopModuleAsync(moduleId, cancellationToken);

        module.Status = "Disabled";
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Module {ModuleId} stopped", moduleId);
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

        _logger.LogInformation("Module {ModuleId} restarted", moduleId);
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
                capabilityName, moduleId);
            return MapGrantToDto(existing);
        }

        var grant = new ModuleCapabilityGrant
        {
            Id = Guid.NewGuid(),
            ModuleId = moduleId,
            CapabilityName = capabilityName,
            GrantedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
        };

        _dbContext.ModuleCapabilityGrants.Add(grant);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Granted capability {Capability} to module {ModuleId} by user {UserId}",
            capabilityName, moduleId, grantedByUserId);

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

        _logger.LogInformation("Revoked capability {Capability} from module {ModuleId}",
            capabilityName, moduleId);
        return true;
    }

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
