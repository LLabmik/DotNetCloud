namespace DotNetCloud.Core.DTOs;

/// <summary>
/// A single finding produced while verifying a <c>.dncpkg</c> package.
/// </summary>
public class ModulePackageIssueDto
{
    /// <summary>Gets or sets the stable machine-readable code (for example <c>ARCHIVE_TRAVERSAL</c>).</summary>
    public string Code { get; set; } = null!;

    /// <summary>Gets or sets the human-readable explanation.</summary>
    public string Message { get; set; } = null!;

    /// <summary>Gets or sets the offending archive entry, when the finding is entry-specific.</summary>
    public string? EntryName { get; set; }
}

/// <summary>
/// The result of inspecting a package <em>without installing it</em>: verification findings plus the
/// manifest facts an administrator must review before approving the install.
/// </summary>
/// <remarks>
/// The installer is a two-step flow on purpose. The review shows the declared capabilities and
/// consent scopes, and the install call must echo them back
/// (<see cref="ModulePackageInstallRequest.AcknowledgedCapabilities"/> /
/// <see cref="ModulePackageInstallRequest.AcknowledgedScopes"/>) together with the hash shown here.
/// A package swapped between the two calls therefore cannot smuggle in a capability the
/// administrator never saw.
/// </remarks>
public class ModulePackageReviewDto
{
    /// <summary>Gets or sets whether every verification check passed.</summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets whether the archive, manifest and compatibility checks passed, ignoring whether
    /// a trusted key signed the package. An unverified install is only possible when this is true.
    /// </summary>
    public bool IsStructurallyValid { get; set; }

    /// <summary>Gets or sets the module id declared by the package.</summary>
    public string? ModuleId { get; set; }

    /// <summary>Gets or sets the display name declared by the manifest.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the module version.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the manifest description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the manifest schema version (1 or 2).</summary>
    public int ManifestVersion { get; set; }

    /// <summary>Gets or sets the publisher id declared by the manifest.</summary>
    public string? PublisherId { get; set; }

    /// <summary>Gets or sets the publisher display name declared by the manifest.</summary>
    public string? PublisherName { get; set; }

    /// <summary>Gets or sets whether a detached signature accompanied the package.</summary>
    public bool IsSigned { get; set; }

    /// <summary>Gets or sets whether the signature was verified against a trusted key id.</summary>
    public bool IsSignedByTrustedKey { get; set; }

    /// <summary>Gets or sets the signing key id, when the package is signed.</summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Gets or sets the store trust tier for this package: <c>official</c>,
    /// <c>verified-publisher</c>, <c>community</c> or <c>unverified</c>. Derived by the store when the
    /// package comes from the catalogue, and by this server (untrusted key → unverified, otherwise
    /// community) for a file or local-feed install.
    /// </summary>
    public string TrustTier { get; set; } = "unknown";

    /// <summary>Gets or sets the lower-case hex SHA-256 of the package bytes.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Gets or sets the package size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the number of archive entries.</summary>
    public int EntryCount { get; set; }

    /// <summary>Gets or sets the total uncompressed size of the archive.</summary>
    public long UncompressedBytes { get; set; }

    /// <summary>Gets or sets the manifest data-store mode (<c>None</c> or <c>Isolated</c>).</summary>
    public string DataStoreMode { get; set; } = "None";

    /// <summary>Gets or sets whether the module implements the user-erasure contract.</summary>
    public bool HandlesUserErasure { get; set; }

    /// <summary>Gets or sets the module API version the package was built against.</summary>
    public int? ModuleApiVersion { get; set; }

    /// <summary>Gets or sets the minimum core version the package requires.</summary>
    public string? MinCoreVersion { get; set; }

    /// <summary>Gets or sets the requested memory ceiling in megabytes.</summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>Gets or sets the requested database connection limit.</summary>
    public int? ConnectionLimit { get; set; }

    /// <summary>Gets or sets the capabilities the module asks the core to grant.</summary>
    public IReadOnlyList<string> Capabilities { get; set; } = [];

    /// <summary>Gets or sets the per-user consent scopes the module may request.</summary>
    public IReadOnlyList<string> UserConsentScopes { get; set; } = [];

    /// <summary>Gets or sets the event types the module publishes.</summary>
    public IReadOnlyList<string> PublishedEvents { get; set; } = [];

    /// <summary>Gets or sets the event types the module subscribes to.</summary>
    public IReadOnlyList<string> SubscribedEvents { get; set; } = [];

    /// <summary>Gets or sets the fatal findings; empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<ModulePackageIssueDto> Issues { get; set; } = [];

    /// <summary>Gets or sets the non-fatal findings (for example a legacy v1 manifest).</summary>
    public IReadOnlyList<ModulePackageIssueDto> Warnings { get; set; } = [];

    /// <summary>Gets or sets whether a different version of this module is already installed.</summary>
    public bool IsAlreadyInstalled { get; set; }

    /// <summary>Gets or sets the currently installed version, when there is one.</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>Gets or sets whether installing the package would downgrade the module.</summary>
    public bool IsDowngrade { get; set; }

    /// <summary>Gets or sets whether the version is withdrawn in the configured feed.</summary>
    public bool IsYanked { get; set; }

    /// <summary>Gets or sets the withdrawal reason, when <see cref="IsYanked"/> is true.</summary>
    public string? YankReason { get; set; }

    /// <summary>Gets or sets whether the id belongs to a module that ships with the core.</summary>
    public bool IsRequiredModule { get; set; }

    /// <summary>
    /// Gets whether an administrator must acknowledge capabilities and scopes before installing.
    /// </summary>
    public bool RequiresAcknowledgement => !IsRequiredModule;
}

/// <summary>
/// Request body for installing an uploaded package through the admin API.
/// </summary>
/// <remarks>
/// The package itself arrives as multipart form data; this body carries the approval: the hash the
/// administrator reviewed and the exact capability and scope lists shown in the review. A package
/// that changed between review and install fails the hash check, and one that declares different
/// capabilities fails the acknowledgement check.
/// </remarks>
public class InstallModulePackageDto
{
    /// <summary>Gets or sets the SHA-256 shown by the review step.</summary>
    public string? ExpectedSha256 { get; set; }

    /// <summary>Gets or sets the capabilities the administrator approved.</summary>
    public IReadOnlyList<string> AcknowledgedCapabilities { get; set; } = [];

    /// <summary>Gets or sets the consent scopes the administrator approved.</summary>
    public IReadOnlyList<string> AcknowledgedScopes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether an unsigned or untrusted package may be installed. Requires
    /// <c>Modules:Store:AllowUnverified</c> on the server and is always audited.
    /// </summary>
    public bool AllowUnverified { get; set; }

    /// <summary>Gets or sets whether the module is started and health-checked after the swap.</summary>
    public bool StartAfterInstall { get; set; } = true;
}

/// <summary>
/// Request body for installing a package that the configured store source lists, after the
/// administrator reviewed it.
/// </summary>
/// <remarks>
/// Same approval contract as <see cref="InstallModulePackageDto"/> — the reviewed hash plus the
/// exact capability and scope lists — but the bytes come from the store source rather than an
/// upload, so the request names the package instead of carrying it.
/// </remarks>
public class InstallStorePackageDto
{
    /// <summary>Gets or sets the version to install; <see langword="null"/> means the highest listed.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the SHA-256 shown by the review step.</summary>
    public string? ExpectedSha256 { get; set; }

    /// <summary>Gets or sets the capabilities the administrator approved.</summary>
    public IReadOnlyList<string> AcknowledgedCapabilities { get; set; } = [];

    /// <summary>Gets or sets the consent scopes the administrator approved.</summary>
    public IReadOnlyList<string> AcknowledgedScopes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether an unsigned or untrusted package may be installed. Requires
    /// <c>Modules:Store:AllowUnverified</c> on the server and is always audited.
    /// </summary>
    public bool AllowUnverified { get; set; }

    /// <summary>Gets or sets whether the module is started and health-checked after the swap.</summary>
    public bool StartAfterInstall { get; set; } = true;
}

/// <summary>
/// Request to install a <c>.dncpkg</c> package from a local file.
/// </summary>
public class ModulePackageInstallRequest
{
    /// <summary>Gets or sets the path of the package to install.</summary>
    public string PackagePath { get; set; } = null!;

    /// <summary>Gets or sets an explicit detached signature path; defaults to <c>&lt;package&gt;.sig</c>.</summary>
    public string? SignaturePath { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 the caller reviewed. When supplied it must match the package bytes,
    /// which is what binds an approval to the exact file it was granted for.
    /// </summary>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// Gets or sets whether an unsigned package (or one signed by a key this server does not
    /// trust) may be installed. Never the default; audited.
    /// </summary>
    public bool AllowUnverified { get; set; }

    /// <summary>Gets or sets whether the module is started and health-checked after the swap.</summary>
    public bool StartAfterInstall { get; set; } = true;

    /// <summary>Gets or sets the capabilities the administrator reviewed and approved.</summary>
    public IReadOnlyList<string> AcknowledgedCapabilities { get; set; } = [];

    /// <summary>Gets or sets the consent scopes the administrator reviewed and approved.</summary>
    public IReadOnlyList<string> AcknowledgedScopes { get; set; } = [];

    /// <summary>Gets or sets the administrator performing the install, when known.</summary>
    public Guid? InstalledByUserId { get; set; }
}

/// <summary>
/// The outcome of an install or rollback.
/// </summary>
public class ModuleInstallResultDto
{
    /// <summary>Gets or sets whether the module is now installed at the requested version.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the module identifier.</summary>
    public string? ModuleId { get; set; }

    /// <summary>Gets or sets the version that is now installed.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the version that was replaced, when this was an upgrade.</summary>
    public string? PreviousVersion { get; set; }

    /// <summary>Gets or sets the persisted trust tier (<c>Store</c> for every package install).</summary>
    public string? TrustTier { get; set; }

    /// <summary>
    /// Gets or sets the store trust tier the package carried (<c>official</c>,
    /// <c>verified-publisher</c>, <c>community</c> or <c>unverified</c>). Informational: unlike
    /// <see cref="TrustTier"/> it does not change how capabilities are granted.
    /// </summary>
    public string? StoreTrustTier { get; set; }

    /// <summary>Gets or sets the signing key id, or null when the package was unverified.</summary>
    public string? KeyId { get; set; }

    /// <summary>Gets or sets the SHA-256 of the installed package bytes.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Gets or sets whether an isolated data store was provisioned by this install.</summary>
    public bool DataStoreProvisioned { get; set; }

    /// <summary>Gets or sets whether the module was started and reported healthy.</summary>
    public bool Started { get; set; }

    /// <summary>Gets or sets whether the install was rolled back after a failed health check.</summary>
    public bool RolledBack { get; set; }

    /// <summary>Gets or sets why the install was rolled back.</summary>
    public string? RollbackReason { get; set; }

    /// <summary>Gets or sets a sanitized failure reason when <see cref="Success"/> is false.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the fatal verification findings, when verification failed.</summary>
    public IReadOnlyList<ModulePackageIssueDto> Issues { get; set; } = [];

    /// <summary>Gets or sets the non-fatal findings.</summary>
    public IReadOnlyList<ModulePackageIssueDto> Warnings { get; set; } = [];
}

/// <summary>
/// One package listed by a module store source.
/// </summary>
public class ModuleStorePackageDto
{
    /// <summary>Gets or sets the module id.</summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the module version.</summary>
    public string Version { get; set; } = null!;

    /// <summary>Gets or sets the package file name relative to the store source.</summary>
    public string File { get; set; } = null!;

    /// <summary>Gets or sets the package size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the lower-case hex SHA-256 recorded by the index.</summary>
    public string Sha256 { get; set; } = null!;

    /// <summary>Gets or sets the publisher id from the manifest, when declared.</summary>
    public string? PublisherId { get; set; }

    /// <summary>Gets or sets the capabilities declared by the manifest.</summary>
    public IReadOnlyList<string> Capabilities { get; set; } = [];

    /// <summary>Gets or sets the consent scopes declared by the manifest.</summary>
    public IReadOnlyList<string> UserConsentScopes { get; set; } = [];

    /// <summary>Gets or sets the signing key id recorded by the index.</summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Gets or sets the trust tier the catalogue derives for this version (<c>official</c>,
    /// <c>verified-publisher</c> or <c>community</c>).
    /// </summary>
    public string? TrustTier { get; set; }

    /// <summary>Gets or sets whether the version is withdrawn.</summary>
    public bool Yanked { get; set; }

    /// <summary>Gets or sets the withdrawal reason, when <see cref="Yanked"/> is true.</summary>
    public string? YankReason { get; set; }

    /// <summary>Gets or sets the withdrawal severity (<c>deprecation</c>/<c>security</c>), when withdrawn.</summary>
    public string? YankSeverity { get; set; }

    /// <summary>Gets or sets the version currently installed on this server, when any.</summary>
    public string? InstalledVersion { get; set; }
}

/// <summary>
/// A warning about a version this server **already runs** whose status in the configured store has
/// changed since it was installed (phase B5).
/// </summary>
/// <remarks>
/// A yanked version keeps running — yanking never stops a module — but an administrator must know:
/// <c>security</c> means the version is unsafe to keep, <c>deprecation</c> means no further updates
/// will come from it, and <c>unlisted</c> means the store no longer publishes it at all (a takedown).
/// </remarks>
public class ModuleRevocationWarningDto
{
    /// <summary>Gets or sets the module id.</summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>Gets or sets the version that is installed here.</summary>
    public string InstalledVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the severity: <c>security</c>, <c>deprecation</c>, or <c>unlisted</c> when the
    /// catalogue no longer lists the installed version.
    /// </summary>
    public string Severity { get; set; } = null!;

    /// <summary>Gets or sets the store's reason, or an explanation for an unlisted version.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the newest version the store still lists for this module, when there is one; this
    /// is what an administrator is expected to upgrade to.
    /// </summary>
    public string? AvailableVersion { get; set; }
}
