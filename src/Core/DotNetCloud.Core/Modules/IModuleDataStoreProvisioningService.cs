namespace DotNetCloud.Core.Modules;

/// <summary>
/// Result of checking whether the core may provision module database principals.
/// </summary>
/// <param name="IsSatisfied">Whether every prerequisite is met.</param>
/// <param name="Errors">Operator-facing remediation messages when a prerequisite is missing.</param>
public sealed record ModuleDataStorePrerequisiteResult(bool IsSatisfied, IReadOnlyList<string> Errors)
{
    /// <summary>Gets a satisfied result.</summary>
    public static ModuleDataStorePrerequisiteResult Satisfied { get; } = new(true, []);
}

/// <summary>
/// Result of provisioning or rotating a module's isolated data store.
/// </summary>
/// <param name="Success">Whether the operation completed.</param>
/// <param name="SchemaName">The module schema, when provisioning succeeded.</param>
/// <param name="PrincipalName">The module principal, when provisioning succeeded.</param>
/// <param name="Error">A sanitized, admin-visible failure reason; never contains a secret.</param>
public sealed record ModuleDataStoreProvisioningResult(
    bool Success,
    string? SchemaName,
    string? PrincipalName,
    string? Error)
{
    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The sanitized failure reason.</param>
    public static ModuleDataStoreProvisioningResult Failed(string error) => new(false, null, null, error);
}

/// <summary>
/// Provisions and maintains the database principal + schema a sandboxed module exclusively owns.
/// </summary>
/// <remarks>
/// Implementations must be idempotent: <see cref="EnsureProvisionedAsync"/> is called on every
/// start of an <c>isolated</c> module and must not fail when the store already exists. Generated
/// secrets are written only to <c>ModuleDataStore.EncryptedConnectionString</c> and are never
/// logged, audited or persisted in <c>LastError</c>.
/// </remarks>
public interface IModuleDataStoreProvisioningService
{
    /// <summary>
    /// Checks whether the core has the privileges each engine needs to provision principals.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleDataStorePrerequisiteResult> CheckPrerequisitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the schema and principal for a module, or refreshes an existing store.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="connectionLimit">Maximum concurrent connections for the module.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleDataStoreProvisioningResult> EnsureProvisionedAsync(
        string moduleId,
        int connectionLimit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the module principal's password and re-encrypts the stored connection string.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="connectionLimit">Maximum concurrent connections for the module.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleDataStoreProvisioningResult> RotateCredentialsAsync(
        string moduleId,
        int connectionLimit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the module schema and principal. Used on uninstall with "destroy data".
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DestroyAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the decrypted, scoped connection string handed to the module process at launch.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection string, or <see langword="null"/> when the store is not provisioned.</returns>
    Task<string?> GetConnectionStringAsync(string moduleId, CancellationToken cancellationToken = default);
}
