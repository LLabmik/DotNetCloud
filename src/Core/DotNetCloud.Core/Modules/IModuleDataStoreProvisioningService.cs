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
    /// <param name="connectionLimit">
    /// Maximum concurrent connections for the module, or <see langword="null"/> to resolve the
    /// effective quota (admin override, then <c>Modules:Quotas</c> policy).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleDataStoreProvisioningResult> EnsureProvisionedAsync(
        string moduleId,
        int? connectionLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the module principal's password and re-encrypts the stored connection string.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="connectionLimit">
    /// Maximum concurrent connections for the module, or <see langword="null"/> to resolve the
    /// effective quota (admin override, then <c>Modules:Quotas</c> policy).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleDataStoreProvisioningResult> RotateCredentialsAsync(
        string moduleId,
        int? connectionLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adopts an existing, core-created schema for a module (upgrade path, §9): provisions the
    /// principal when needed and transfers schema/object ownership — including
    /// <c>__EFMigrationsHistory</c> — to it without moving any data.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome; idempotent on an already-adopted store.</returns>
    Task<ModuleDataStoreProvisioningResult> AdoptExistingStoreAsync(
        string moduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adopts a bundled module's data store <i>and</i> moves the objects it already had out of its
    /// pre-sandbox schema into the schema the module now owns.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="legacySchemaName">
    /// The schema the core used before the module became sandboxed (for example <c>bookmarks</c>),
    /// or <see langword="null"/> when the module never had one. The caller must derive this from the
    /// module id — never from a manifest or operator input.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome; idempotent, and a no-op once the legacy schema is empty.</returns>
    /// <remarks>
    /// Objects are moved in place (no rows are copied), ownership is transferred afterwards, and
    /// <c>__EFMigrationsHistory</c> travels with the data so the module's first self-migration sees
    /// the migrations the core already applied. A name that already exists in the target schema is
    /// never overwritten.
    /// </remarks>
    Task<ModuleDataStoreProvisioningResult> AdoptExistingStoreAsync(
        string moduleId,
        string? legacySchemaName,
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
