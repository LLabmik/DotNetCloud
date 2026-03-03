using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides administrative operations on installed modules (list, details, start/stop/restart, capability management).
/// </summary>
public interface IAdminModuleService
{
    /// <summary>
    /// Lists all installed modules with their status and capabilities.
    /// </summary>
    /// <returns>A read-only list of module DTOs.</returns>
    Task<IReadOnlyList<ModuleDto>> ListModulesAsync();

    /// <summary>
    /// Gets detailed information for a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier (e.g., "dotnetcloud.files").</param>
    /// <returns>The module DTO, or <see langword="null"/> if not found.</returns>
    Task<ModuleDto?> GetModuleAsync(string moduleId);

    /// <summary>
    /// Starts a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the module was found and started; otherwise <see langword="false"/>.</returns>
    Task<bool> StartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the module was found and stopped; otherwise <see langword="false"/>.</returns>
    Task<bool> StopModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the module was found and restarted; otherwise <see langword="false"/>.</returns>
    Task<bool> RestartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants a capability to a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="capabilityName">The capability name (e.g., "IStorageProvider").</param>
    /// <param name="grantedByUserId">The admin user who is granting the capability.</param>
    /// <returns>The grant DTO, or <see langword="null"/> if the module was not found.</returns>
    Task<ModuleCapabilityGrantDto?> GrantCapabilityAsync(string moduleId, string capabilityName, Guid grantedByUserId);

    /// <summary>
    /// Revokes a capability from a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="capabilityName">The capability name to revoke.</param>
    /// <returns><see langword="true"/> if the grant was found and removed; otherwise <see langword="false"/>.</returns>
    Task<bool> RevokeCapabilityAsync(string moduleId, string capabilityName);
}
