namespace DotNetCloud.Core.Modules.Store;

/// <summary>
/// Where a module store source gets its packages from.
/// </summary>
public enum ModuleStoreSourceKind
{
    /// <summary>A directory holding an <c>index.json</c> next to the package files (first release).</summary>
    LocalFeed,

    /// <summary>A remote signed index fetched over HTTPS (deferred to a later release, decision D6).</summary>
    RemoteIndex,
}

/// <summary>
/// A package as listed by a store source.
/// </summary>
/// <param name="ModuleId">Module id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Module version.</param>
/// <param name="File">Package file name relative to the source root.</param>
/// <param name="SizeBytes">Package size in bytes.</param>
/// <param name="Sha256">Lower-case hex SHA-256 recorded by the index.</param>
/// <param name="PublisherId">Publisher id from the manifest, when declared.</param>
/// <param name="Capabilities">Capabilities declared by the manifest.</param>
/// <param name="UserConsentScopes">Consent scopes declared by the manifest.</param>
/// <param name="KeyId">Signing key id recorded by the index, when signed.</param>
/// <param name="Yanked">Whether the version has been withdrawn.</param>
/// <param name="YankReason">Why the version was withdrawn, when it was.</param>
public sealed record ModuleStorePackage(
    string ModuleId,
    string Name,
    string Version,
    string File,
    long SizeBytes,
    string Sha256,
    string? PublisherId,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> UserConsentScopes,
    string? KeyId,
    bool Yanked,
    string? YankReason);

/// <summary>
/// A resolved package: the catalogue entry plus the verified-on-disk location of its bytes.
/// </summary>
/// <param name="Package">The catalogue entry.</param>
/// <param name="PackagePath">The absolute path of the package file.</param>
/// <param name="SignaturePath">The absolute path of the detached signature, when present.</param>
public sealed record ModuleStoreResolution(
    ModuleStorePackage Package,
    string PackagePath,
    string? SignaturePath);

/// <summary>
/// A source of installable module packages (the store client seam, decision D6).
/// </summary>
/// <remarks>
/// The first release ships only <see cref="ModuleStoreSourceKind.LocalFeed"/>: a directory holding
/// an <c>index.json</c> beside the signed packages. Remote index fetch, caching, mirrors and
/// automatic update checks are deferred, but the core installer is written against this interface
/// so adding them later does not change the verification or install path.
/// </remarks>
public interface IModuleStoreService
{
    /// <summary>Gets a human-readable identifier for the configured source (for logs and the UI).</summary>
    string SourceId { get; }

    /// <summary>Gets the kind of source backing this instance.</summary>
    ModuleStoreSourceKind SourceKind { get; }

    /// <summary>
    /// Lists every package the source advertises.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalogue entries, ordered by module id then version.</returns>
    Task<IReadOnlyList<ModuleStorePackage>> ListPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a package to a file on disk, applying the source's containment rules.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="version">The version, or <see langword="null"/> for the highest listed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved package, or <see langword="null"/> when the source does not list it.</returns>
    Task<ModuleStoreResolution?> ResolveAsync(
        string moduleId,
        string? version = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Installs, upgrades and rolls back <c>.dncpkg</c> packages (decision D6: local file or local feed).
/// </summary>
/// <remarks>
/// Implementations run the same state machine for every entry point — verify, review, provision,
/// stage, swap, record, start, health-check, roll back — so the CLI and the admin API cannot drift
/// apart in what they accept.
/// </remarks>
public interface IModulePackageInstaller
{
    /// <summary>
    /// Verifies a package and returns the manifest facts an administrator must review. Does not
    /// change anything on disk or in the database.
    /// </summary>
    /// <param name="packagePath">The package to inspect.</param>
    /// <param name="signaturePath">An explicit signature path, or null for the default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The review, including every verification finding.</returns>
    Task<DTOs.ModulePackageReviewDto> InspectAsync(
        string packagePath,
        string? signaturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs (or upgrades) a package.
    /// </summary>
    /// <param name="request">The install request, including the reviewed hash and acknowledgements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome; a failed health check is rolled back before returning.</returns>
    Task<DTOs.ModuleInstallResultDto> InstallAsync(
        DTOs.ModulePackageInstallRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the running version with the version retained by the last install.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome; a withdrawn (yanked) retained version is refused.</returns>
    Task<DTOs.ModuleInstallResultDto> RollbackAsync(
        string moduleId,
        CancellationToken cancellationToken = default);
}
