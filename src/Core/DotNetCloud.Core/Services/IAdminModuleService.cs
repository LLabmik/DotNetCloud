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

    /// <summary>
    /// Gets the isolated data store of a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data-store state, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleDataStoreDto?> GetDataStoreAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions (or repairs) the schema and database principal for an isolated module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation outcome, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleDataStoreOperationResultDto?> ProvisionDataStoreAsync(
        string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the module principal's password and re-encrypts the stored connection string.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation outcome, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleDataStoreOperationResultDto?> RotateDataStoreCredentialsAsync(
        string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the module schema and database principal. Irreversible; destroys all module data.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation outcome, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleDataStoreOperationResultDto?> DestroyDataStoreAsync(
        string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resource quota override and the effective quota for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The quota state, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleQuotaDto?> GetQuotaAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an admin quota override for a module. Null or zero values clear the override.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="dto">The replacement override values.</param>
    /// <param name="updatedByUserId">The administrator making the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleQuotaOperationResultDto?> UpdateQuotaAsync(
        string moduleId,
        UpdateModuleQuotaDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a module: stops it, revokes its launch credential, optionally drops or orphans its
    /// isolated data store, optionally removes its binaries, and deletes the installation record.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="dto">Whether to destroy the module's data and remove its files.</param>
    /// <param name="uninstalledByUserId">The administrator performing the uninstall.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleUninstallResultDto?> UninstallModuleAsync(
        string moduleId,
        UninstallModuleDto dto,
        Guid uninstalledByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the non-secret lifecycle record of a module's launch credential.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The credential record, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleCredentialDto?> GetCredentialAsync(string moduleId);

    /// <summary>
    /// Rotates a module's launch credential. A running module is restarted so it receives the new
    /// secret; the previous credential stops working immediately.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new credential record, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleCredentialDto?> RotateCredentialAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a module's launch credential. Every in-flight call from the running process is
    /// rejected until the module is restarted (which mints a new secret).
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The revoked credential record, or <see langword="null"/> when the module is not installed.</returns>
    Task<ModuleCredentialDto?> RevokeCredentialAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists data stores retained as <c>Orphaned</c> after a module was uninstalled with "keep data".
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The orphaned stores an administrator can inspect and drop.</returns>
    Task<IReadOnlyList<ModuleDataStoreDto>> ListOrphanedDataStoresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the packages available from the configured module store source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalogue entries, each annotated with the installed version when known.</returns>
    Task<IReadOnlyList<ModuleStorePackageDto>> ListStorePackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an uploaded package and returns the manifest facts an administrator must review.
    /// Nothing is installed.
    /// </summary>
    /// <param name="packagePath">Path of a package already written to a temporary location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The review, including every verification finding.</returns>
    Task<ModulePackageReviewDto> InspectPackageAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs an uploaded package after the administrator reviewed it.
    /// </summary>
    /// <param name="packagePath">Path of the package to install.</param>
    /// <param name="dto">
    /// The reviewed hash plus the acknowledged capabilities and scopes. Both lists must equal the
    /// manifest's declarations exactly, which is what binds the approval to this package.
    /// </param>
    /// <param name="installedByUserId">The administrator performing the install.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome; a failed health check is rolled back before returning.</returns>
    Task<ModuleInstallResultDto> InstallPackageAsync(
        string packagePath,
        InstallModulePackageDto dto,
        Guid installedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the previous version retained by the last install.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="rolledBackByUserId">The administrator performing the rollback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<ModuleInstallResultDto> RollbackModuleAsync(
        string moduleId,
        Guid rolledBackByUserId,
        CancellationToken cancellationToken = default);
}
