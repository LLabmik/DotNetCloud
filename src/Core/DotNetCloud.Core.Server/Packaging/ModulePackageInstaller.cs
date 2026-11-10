using System.Runtime.InteropServices;
using System.Text.Json;
using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Installs, upgrades and rolls back <c>.dncpkg</c> packages.
/// </summary>
/// <remarks>
/// <para>
/// One state machine serves every entry point (CLI and admin API):
/// verify → review → provision → stage → swap → record → start → health-check → roll back.
/// Verification and extraction are the packer's own code, so the installer cannot accept a package
/// the packer would refuse to publish.
/// </para>
/// <para>
/// <b>Failure behaviour.</b> Everything before the swap leaves the installation untouched. A failure
/// after the swap (a module that never becomes healthy) restores the previous binaries and install
/// record, so the server is never left with a half-installed module. Module data stores are
/// provisioned before the swap and are <i>not</i> dropped by a rollback: modules ship expand/contract
/// migrations, so the older binaries keep working against the newer schema (F16).
/// </para>
/// </remarks>
public sealed class ModulePackageInstaller : IModulePackageInstaller
{
    /// <summary>The install receipt written into each installed module directory.</summary>
    public const string ReceiptFileName = "install-receipt.json";

    private static readonly string[] TrustIssueCodes =
    [
        PackageIssueCodes.SignatureMissing,
        PackageIssueCodes.SignatureUntrustedKey,
    ];

    private readonly CoreDbContext _dbContext;
    private readonly IModuleInstallRunner _runner;
    private readonly IModuleDataStoreProvisioningService _dataStores;
    private readonly IModuleStoreService _store;
    private readonly IOptions<ModuleStoreOptions> _storeOptions;
    private readonly IOptions<ProcessSupervisorOptions> _supervisorOptions;
    private readonly IAuditLogger? _auditLogger;
    private readonly ILogger<ModulePackageInstaller> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModulePackageInstaller"/> class.</summary>
    /// <param name="dbContext">The core database context.</param>
    /// <param name="runner">Starts/stops module processes; unavailable when no supervisor exists.</param>
    /// <param name="dataStores">Isolated data-store provisioning.</param>
    /// <param name="store">The configured module store source (used for withdrawal checks).</param>
    /// <param name="storeOptions">The <c>Modules:Store</c> options.</param>
    /// <param name="supervisorOptions">Supervisor options, used to resolve the modules directory.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="auditLogger">Optional audit logger (absent in CLI-only process).</param>
    public ModulePackageInstaller(
        CoreDbContext dbContext,
        IModuleInstallRunner runner,
        IModuleDataStoreProvisioningService dataStores,
        IModuleStoreService store,
        IOptions<ModuleStoreOptions> storeOptions,
        IOptions<ProcessSupervisorOptions> supervisorOptions,
        ILogger<ModulePackageInstaller> logger,
        IAuditLogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(dataStores);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(storeOptions);
        ArgumentNullException.ThrowIfNull(supervisorOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _runner = runner;
        _dataStores = dataStores;
        _store = store;
        _storeOptions = storeOptions;
        _supervisorOptions = supervisorOptions;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ModulePackageReviewDto> InspectAsync(
        string packagePath,
        string? signaturePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath))
        {
            return new ModulePackageReviewDto
            {
                IsValid = false,
                Issues =
                [
                    new ModulePackageIssueDto
                    {
                        Code = PackageIssueCodes.PackageNotFound,
                        Message = "The package file does not exist.",
                    },
                ],
            };
        }

        var trustedKeys = _storeOptions.Value.GetEffectiveTrustedKeys();

        // Inspection always runs the strict policy: the review reports whether the package would
        // install without an explicit unverified override.
        var verification = await ModulePackageVerifier.VerifyAsync(
            fullPath,
            signaturePath,
            new ModulePackageVerificationOptions { TrustedKeys = trustedKeys },
            cancellationToken);

        var manifest = await ModulePackageManifest.TryReadAsync(fullPath, cancellationToken);

        var issues = verification.Issues.Select(MapIssue).ToList();
        var warnings = verification.Warnings.Select(MapIssue).ToList();

        // Scope-catalog and erasure-contract rules live in ModuleManifestLoader (F18), so they are
        // not repeated here: a package cannot be verified if the core loader would refuse its
        // manifest at start-up.

        var moduleId = manifest?.Id ?? verification.ModuleId;
        var version = manifest?.Version ?? verification.Version;

        var isRequired = moduleId is not null && RequiredModules.IsRequired(moduleId);
        if (isRequired)
        {
            issues.Add(new ModulePackageIssueDto
            {
                Code = "MODULE_REQUIRED",
                Message = $"'{moduleId}' ships with the core and cannot be installed from a package.",
            });
        }

        // One resolution serves both questions: is this version withdrawn, and what tier does the
        // store derive for it.
        var storeListing = moduleId is null || version is null
            ? null
            : await ResolveStoreListingAsync(moduleId, version, cancellationToken);

        var yanked = storeListing?.Yanked ?? false;
        var yankReason = storeListing?.YankReason;

        if (yanked)
        {
            issues.Add(new ModulePackageIssueDto
            {
                Code = "PACKAGE_YANKED",
                Message = $"The store has withdrawn this version: {yankReason ?? "no reason recorded"}.",
            });
        }

        string? installedVersion = null;
        var isDowngrade = false;

        if (moduleId is not null)
        {
            installedVersion = await _dbContext.InstalledModules
                .AsNoTracking()
                .Where(module => module.ModuleId == moduleId)
                .Select(module => module.Version)
                .FirstOrDefaultAsync(cancellationToken);

            isDowngrade = installedVersion is not null
                && version is not null
                && ModuleCompatibility.TryParseVersion(installedVersion, out var installed)
                && ModuleCompatibility.TryParseVersion(version, out var candidate)
                && candidate < installed;
        }

        if (isDowngrade)
        {
            warnings.Add(new ModulePackageIssueDto
            {
                Code = "PACKAGE_DOWNGRADE",
                Message = $"This installs {version}, older than the installed {installedVersion}.",
            });
        }

        var isSigned = verification.KeyId is not null;
        var isTrusted = isSigned
            && trustedKeys.ContainsKey(verification.KeyId!);

        // Store trust is derived, in this order: an untrusted signature means unverified whatever any
        // catalogue says; otherwise the catalogue's own derived tier applies; a package from a file or
        // a local feed (no catalogue entry) is community at best.
        var trustTier = !isTrusted
            ? ModuleStoreTrustTier.Unverified
            : storeListing?.TrustTier ?? ModuleStoreTrustTier.Community;

        return new ModulePackageReviewDto
        {
            IsValid = issues.Count == 0,
            IsStructurallyValid = issues.All(issue => TrustIssueCodes.Contains(issue.Code)),
            ModuleId = moduleId,
            Name = manifest?.Name,
            Version = version,
            Description = manifest?.Description,
            ManifestVersion = manifest?.ManifestVersion ?? 0,
            PublisherId = manifest?.PublisherId,
            PublisherName = manifest?.PublisherName,
            IsSigned = isSigned,
            IsSignedByTrustedKey = isTrusted,
            KeyId = verification.KeyId,
            TrustTier = trustTier.ToWireValue(),
            Sha256 = verification.Sha256,
            SizeBytes = new FileInfo(fullPath).Length,
            EntryCount = verification.EntryCount,
            UncompressedBytes = verification.UncompressedBytes,
            DataStoreMode = (manifest?.DataStoreMode ?? ModuleDataStoreMode.None).ToString(),
            HandlesUserErasure = manifest?.HandlesUserErasure ?? false,
            ModuleApiVersion = manifest?.ModuleApiVersion,
            MinCoreVersion = manifest?.MinCoreVersion,
            MemoryLimitMb = manifest?.MemoryLimitMb,
            ConnectionLimit = manifest?.ConnectionLimit,
            Capabilities = manifest?.Capabilities ?? [],
            UserConsentScopes = manifest?.UserConsentScopes ?? [],
            PublishedEvents = manifest?.PublishedEvents ?? [],
            SubscribedEvents = manifest?.SubscribedEvents ?? [],
            Issues = issues.AsReadOnly(),
            Warnings = warnings.AsReadOnly(),
            IsAlreadyInstalled = installedVersion is not null,
            InstalledVersion = installedVersion,
            IsDowngrade = isDowngrade,
            IsYanked = yanked,
            YankReason = yankReason,
            IsRequiredModule = isRequired,
        };
    }

    /// <inheritdoc/>
    public async Task<ModuleInstallResultDto> InstallAsync(
        ModulePackageInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var packagePath = Path.GetFullPath(request.PackagePath);
        var review = await InspectAsync(packagePath, request.SignaturePath, cancellationToken);

        var blocking = review.Issues.Where(issue => !TrustIssueCodes.Contains(issue.Code)).ToList();
        var trustIssues = review.Issues.Where(issue => TrustIssueCodes.Contains(issue.Code)).ToList();

        if (blocking.Count > 0)
        {
            await AuditAsync(
                AuditAction.ModulePackageVerificationFailed,
                review.ModuleId,
                request.InstalledByUserId,
                $"package-rejected:{review.Sha256}",
                cancellationToken);

            _logger.LogWarning(
                "Package {Sha256} was rejected: {Codes}",
                LogSanitizer.Sanitize(review.Sha256),
                string.Join(",", blocking.Select(issue => issue.Code)));

            return Failure(review, "PACKAGE_INVALID", "The package failed verification.", blocking);
        }

        if (trustIssues.Count > 0)
        {
            if (!request.AllowUnverified)
            {
                return Failure(
                    review,
                    "PACKAGE_UNTRUSTED",
                    "The package is not signed by a key this server trusts. Install it with "
                    + "--allow-unverified (audited) only if you trust its source.",
                    trustIssues);
            }

            if (!_storeOptions.Value.AllowUnverified)
            {
                return Failure(
                    review,
                    "UNVERIFIED_DISABLED",
                    "This server does not permit unverified installs (Modules:Store:AllowUnverified).",
                    trustIssues);
            }
        }

        if (string.IsNullOrWhiteSpace(review.ModuleId) || string.IsNullOrWhiteSpace(review.Version))
        {
            return Failure(review, "PACKAGE_IDENTITY_UNKNOWN", "The package identity could not be determined.", []);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedSha256)
            && !string.Equals(request.ExpectedSha256, review.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                review,
                "PACKAGE_CHANGED",
                "The package bytes changed since they were reviewed. Review and approve it again.",
                []);
        }

        var scopeIssue = CheckAcknowledgements(request, review);
        if (scopeIssue is not null)
        {
            return Failure(review, "PACKAGE_NOT_ACKNOWLEDGED", scopeIssue, []);
        }

        var moduleId = review.ModuleId;
        var version = review.Version;
        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var unverified = trustIssues.Count > 0;

        var existing = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(module => module.ModuleId == moduleId, cancellationToken);

        var previousVersion = existing?.Version;
        var isUpgrade = existing is not null;

        // What to restore if the new version never becomes healthy.
        var previousState = existing is null
            ? null
            : new InstallRecordSnapshot(
                existing.Version,
                existing.TrustTier,
                existing.PublisherId,
                existing.SignedByKeyId,
                existing.DataStoreMode,
                existing.SandboxStatus,
                existing.Status);

        // 1. Stop the running process before its files move.
        if (_runner.CanStartModules)
        {
            await _runner.StopAsync(moduleId, cancellationToken);
        }

        // 2. Provision the isolated data store before anything is swapped, so a provisioning
        //    failure leaves the previous installation intact.
        var dataStoreProvisioned = false;
        if (string.Equals(review.DataStoreMode, nameof(ModuleDataStoreMode.Isolated), StringComparison.Ordinal))
        {
            var provisioning = await _dataStores.EnsureProvisionedAsync(moduleId, cancellationToken: cancellationToken);
            if (!provisioning.Success)
            {
                await AuditAsync(
                    AuditAction.ModuleDataStoreError,
                    moduleId,
                    request.InstalledByUserId,
                    "install-provision-failed",
                    cancellationToken);

                return Failure(
                    review,
                    "DATASTORE_PROVISIONING_FAILED",
                    provisioning.Error ?? "Provisioning the module data store failed.",
                    []);
            }

            dataStoreProvisioned = true;
        }

        // 3. Stage the extracted package (RID selection + receipt) without touching the live install.
        string stagedRoot;
        try
        {
            stagedRoot = await StageAsync(
                packagePath,
                moduleId,
                version,
                review,
                unverified,
                ModuleStoreTrustTiers.Parse(review.TrustTier),
                cancellationToken);
        }
        catch (ModulePackageException ex)
        {
            _logger.LogWarning(
                "Staging package for {ModuleId} failed: {Error}",
                LogSanitizer.Sanitize(moduleId), ex.Message);

            return Failure(review, "PACKAGE_STAGING_FAILED", ex.Message, ex.Issues.Select(MapIssue).ToList());
        }

        string? previousDirectory = null;
        try
        {
            previousDirectory = layout.ArchiveCurrent(moduleId, previousVersion);
            layout.Promote(stagedRoot, moduleId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            layout.Restore(previousDirectory, moduleId);
            layout.DeleteStaging(stagedRoot);

            _logger.LogError(
                ex, "Swapping the install directory for {ModuleId} failed", LogSanitizer.Sanitize(moduleId));

            return Failure(review, "INSTALL_SWAP_FAILED", "Could not replace the module directory: " + ex.Message, []);
        }

        // 4. Record the installation.
        var row = existing ?? new InstalledModule
        {
            ModuleId = moduleId,
            InstalledAt = DateTime.UtcNow,
        };

        row.Version = version;
        row.UpdatedAt = DateTime.UtcNow;
        row.TrustTier = ModuleTrustTier.Store;
        row.PublisherId = review.PublisherId;
        row.SignedByKeyId = review.IsSignedByTrustedKey ? review.KeyId : null;
        row.DataStoreMode = string.Equals(
            review.DataStoreMode, nameof(ModuleDataStoreMode.Isolated), StringComparison.Ordinal)
            ? ModuleDataStoreMode.Isolated
            : ModuleDataStoreMode.None;
        row.SandboxStatus = ModuleSandboxStatus.Active;

        // A running supervisor reconciles "Enabled" by starting the module and
        // "RestartPending" by restarting it, so an upgrade never leaves old code running.
        row.Status = isUpgrade ? ModuleInstallStatus.RestartPending : ModuleInstallStatus.Enabled;

        if (existing is null)
        {
            _dbContext.InstalledModules.Add(row);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            isUpgrade ? AuditAction.ModuleUpdated : AuditAction.ModuleInstalled,
            moduleId,
            request.InstalledByUserId,
            $"package-install:{version}:{review.KeyId ?? "unverified"}",
            cancellationToken);

        if (unverified)
        {
            await AuditAsync(
                AuditAction.ModulePackageVerificationFailed,
                moduleId,
                request.InstalledByUserId,
                $"unverified-install:{review.Sha256}",
                cancellationToken);
        }

        _logger.LogInformation(
            "Installed module {ModuleId} {Version} (key={KeyId}, unverified={Unverified})",
            LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(version),
            LogSanitizer.Sanitize(review.KeyId ?? "none"), unverified);

        // 4b. Seed the manifest's default settings before the module starts. A sandboxed module reads
        //     its configuration from the settings plane, so the defaults have to exist for the first
        //     request rather than appear after the next core restart. Additive only: an upgrade never
        //     overwrites a value the administrator has set.
        try
        {
            await ModuleSettingDefaultsSeeder.SeedFromModuleDirectoryAsync(
                _dbContext,
                moduleId,
                layout.GetModuleDirectory(moduleId),
                _logger,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A manifest default that cannot be stored must not fail an otherwise valid install.
            _logger.LogWarning(
                ex, "Failed to seed default settings for module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }

        // 5. Start and health-check. A module that never comes up is rolled back.
        var started = false;
        var startReason = default(string);
        if (request.StartAfterInstall && _runner.CanStartModules)
        {
            var health = await _runner.StartAndAwaitHealthyAsync(moduleId, cancellationToken);
            started = health.IsHealthy;
            startReason = health.Reason;
        }

        if (request.StartAfterInstall && _runner.CanStartModules && !started)
        {
            var rollback = await RestoreAsync(
                layout, moduleId, version, previousVersion, previousDirectory, cancellationToken);

            if (isUpgrade && previousState is not null)
            {
                // The previous binaries are back; restore their identity in the record and leave the
                // module stopped so an operator can look at it instead of watching the supervisor
                // restart-loop a broken build.
                row.Version = previousState.Version;
                row.TrustTier = previousState.TrustTier;
                row.PublisherId = previousState.PublisherId;
                row.SignedByKeyId = previousState.SignedByKeyId;
                row.DataStoreMode = previousState.DataStoreMode;
                row.SandboxStatus = ModuleSandboxStatus.Error;
                row.Status = ModuleInstallStatus.Disabled;
            }
            else if (!rollback)
            {
                // A first install that never worked has nothing to keep: remove the record so the
                // module does not appear as a broken installation.
                _dbContext.InstalledModules.Remove(row);
            }
            else
            {
                row.Status = ModuleInstallStatus.Failed;
                row.SandboxStatus = ModuleSandboxStatus.Error;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "Module {ModuleId} {Version} failed its health check: {Reason}",
                LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(version),
                LogSanitizer.Sanitize(startReason ?? "no health response"));

            await AuditAsync(
                AuditAction.ModuleUpdated,
                moduleId,
                request.InstalledByUserId,
                $"install-rolled-back:{version}",
                cancellationToken);

            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Version = version,
                PreviousVersion = previousVersion,
                TrustTier = ModuleTrustTier.Store.ToString(),
                StoreTrustTier = review.TrustTier,
                KeyId = review.IsSignedByTrustedKey ? review.KeyId : null,
                Sha256 = review.Sha256,
                DataStoreProvisioned = dataStoreProvisioned,
                Started = false,
                RolledBack = rollback,
                RollbackReason = startReason,
                Error = "The module did not become healthy after install"
                        + (rollback ? "; the previous version was restored." : "."),
                Issues = [],
                Warnings = review.Warnings,
            };
        }

        return new ModuleInstallResultDto
        {
            Success = true,
            ModuleId = moduleId,
            Version = version,
            PreviousVersion = previousVersion,
            TrustTier = ModuleTrustTier.Store.ToString(),
            StoreTrustTier = review.TrustTier,
            KeyId = review.IsSignedByTrustedKey ? review.KeyId : null,
            Sha256 = review.Sha256,
            DataStoreProvisioned = dataStoreProvisioned,
            Started = started,
            Warnings = review.Warnings,
        };
    }

    /// <inheritdoc/>
    public async Task<ModuleInstallResultDto> RollbackAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var retained = layout.FindPrevious(moduleId);

        if (retained is null)
        {
            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Error = "There is no retained previous version to roll back to.",
            };
        }

        var retainedManifestPath = Path.Combine(retained.Value.Directory, "manifest.json");
        var manifest = ModulePackageManifest.TryReadFile(retainedManifestPath);

        if (manifest is null
            || !string.Equals(manifest.Id, moduleId, StringComparison.Ordinal))
        {
            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Error = "The retained version does not contain a valid manifest for this module.",
            };
        }

        // F16: a rollback must not restore a build the store has withdrawn.
        var retainedListing = await ResolveStoreListingAsync(moduleId, retained.Value.Version, cancellationToken);
        if (retainedListing is { Yanked: true })
        {
            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Version = retained.Value.Version,
                Error = $"Refusing to roll back to a withdrawn version: {retainedListing.YankReason ?? "no reason recorded"}.",
            };
        }

        var existing = await _dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(module => module.ModuleId == moduleId, cancellationToken);

        if (existing is null)
        {
            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Error = $"Module '{moduleId}' is not installed.",
            };
        }

        var currentVersion = existing.Version;

        if (_runner.CanStartModules)
        {
            await _runner.StopAsync(moduleId, cancellationToken);
        }

        var displaced = layout.ArchiveCurrent(moduleId, currentVersion, keepVersionIdentity: false);

        try
        {
            // The retained directory becomes the live install; drop the "previous" slot.
            layout.PromotePrevious(moduleId, retained.Value.Directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            layout.Restore(displaced, moduleId);

            return new ModuleInstallResultDto
            {
                Success = false,
                ModuleId = moduleId,
                Error = "Could not restore the retained version: " + ex.Message,
            };
        }

        existing.Version = retained.Value.Version;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Status = ModuleInstallStatus.RestartPending;
        existing.SandboxStatus = ModuleSandboxStatus.Active;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            AuditAction.ModuleUpdated,
            moduleId,
            userId: null,
            $"rollback:{currentVersion}->{retained.Value.Version}",
            cancellationToken);

        _logger.LogInformation(
            "Rolled module {ModuleId} back from {CurrentVersion} to {RetainedVersion}",
            LogSanitizer.Sanitize(moduleId),
            LogSanitizer.Sanitize(currentVersion),
            LogSanitizer.Sanitize(retained.Value.Version));

        var started = false;
        if (_runner.CanStartModules)
        {
            var health = await _runner.StartAndAwaitHealthyAsync(moduleId, cancellationToken);
            started = health.IsHealthy;

            if (!started)
            {
                return new ModuleInstallResultDto
                {
                    Success = true,
                    ModuleId = moduleId,
                    Version = retained.Value.Version,
                    PreviousVersion = currentVersion,
                    StoreTrustTier = retainedListing?.TrustTier.ToWireValue(),
                    Started = false,
                    Error = "Rolled back, but the restored version did not report healthy: "
                            + (health.Reason ?? "no health response"),
                };
            }
        }

        // The displaced (newer) build is no longer needed; drop it so only one generation remains.
        layout.DeleteStaging(displaced);

        return new ModuleInstallResultDto
        {
            Success = true,
            ModuleId = moduleId,
            Version = retained.Value.Version,
            PreviousVersion = currentVersion,
            TrustTier = ModuleTrustTier.Store.ToString(),
            StoreTrustTier = retainedListing?.TrustTier.ToWireValue(),
            Started = started,
        };
    }

    /// <summary>
    /// Extracts the package, selects the host runtime's binaries and writes the install receipt.
    /// </summary>
    /// <returns>The staged install root, ready to be promoted.</returns>
    private async Task<string> StageAsync(
        string packagePath,
        string moduleId,
        string version,
        ModulePackageReviewDto review,
        bool unverified,
        ModuleStoreTrustTier trustTier,
        CancellationToken cancellationToken)
    {
        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var stagingRoot = layout.CreateStagingRoot(moduleId);
        var extracted = Path.Combine(stagingRoot, "package");
        var installRoot = Path.Combine(stagingRoot, "root");

        Directory.CreateDirectory(extracted);
        Directory.CreateDirectory(installRoot);

        await ModulePackageVerifier.ExtractAsync(packagePath, extracted, cancellationToken);

        var runtimeDirectory = SelectRuntimeDirectory(extracted);
        CopyDirectory(runtimeDirectory, installRoot, recursive: true);

        // Metadata files travel alongside the binaries; discovery reads manifest.json from the
        // install directory itself.
        foreach (var name in new[]
                 {
                     ModulePackageFormat.ManifestEntryName,
                     ModulePackageFormat.LicenseEntryName,
                     ModulePackageFormat.ReadmeEntryName,
                     ModulePackageFormat.ChangelogEntryName,
                     ModulePackageFormat.IconEntryName,
                 })
        {
            var source = Path.Combine(extracted, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(installRoot, name), overwrite: true);
            }
        }

        // The extracted manifest must be the one that was reviewed: same id, same version.
        var stagedManifest = ModulePackageManifest.TryReadFile(
            Path.Combine(installRoot, ModulePackageFormat.ManifestEntryName));

        if (stagedManifest is null
            || !string.Equals(stagedManifest.Id, moduleId, StringComparison.Ordinal)
            || !string.Equals(stagedManifest.Version, version, StringComparison.Ordinal))
        {
            layout.DeleteStaging(stagingRoot);

            throw new ModulePackageException(
                "The extracted manifest does not match the package that was verified.",
                [new PackageIssue(PackageIssueCodes.ManifestMismatch, "Staged manifest identity mismatch.")]);
        }

        var receipt = new InstallReceipt
        {
            ModuleId = moduleId,
            Version = version,
            PackageFile = Path.GetFileName(packagePath),
            Sha256 = review.Sha256,
            KeyId = review.IsSignedByTrustedKey ? review.KeyId : null,
            Unverified = unverified,
            TrustTier = trustTier.ToWireValue(),
            PublisherId = review.PublisherId,
            EntryPoint = $"{moduleId}.dll",
            InstalledAtUtc = DateTime.UtcNow,
        };

        await File.WriteAllTextAsync(
            Path.Combine(installRoot, ReceiptFileName),
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + Environment.NewLine,
            cancellationToken);

        return installRoot;
    }

    /// <summary>
    /// Selects the <c>files/&lt;rid&gt;</c> directory that matches this host.
    /// </summary>
    /// <param name="extractedRoot">The extracted package root.</param>
    /// <returns>The selected runtime directory.</returns>
    /// <exception cref="ModulePackageException">
    /// No directory matches and the choice cannot be made unambiguously.
    /// </exception>
    private static string SelectRuntimeDirectory(string extractedRoot) =>
        SelectRuntimeDirectory(extractedRoot, RuntimeInformation.RuntimeIdentifier);

    /// <summary>
    /// Selects the <c>files/&lt;rid&gt;</c> directory that matches <paramref name="hostRuntime"/>.
    /// </summary>
    /// <param name="extractedRoot">The extracted package root.</param>
    /// <param name="hostRuntime">The host RID to match against.</param>
    /// <returns>The selected runtime directory.</returns>
    /// <exception cref="ModulePackageException">
    /// No directory matches and the choice cannot be made unambiguously.
    /// </exception>
    internal static string SelectRuntimeDirectory(string extractedRoot, string hostRuntime)
    {
        var filesRoot = Path.Combine(extractedRoot, ModulePackageFormat.FilesDirectoryName);

        if (!Directory.Exists(filesRoot))
        {
            throw new ModulePackageException(
                "The package contains no 'files/' directory.",
                [new PackageIssue(PackageIssueCodes.MissingEntry, "files/ is missing.")]);
        }

        var matching = Path.Combine(filesRoot, hostRuntime);

        if (Directory.Exists(matching))
        {
            return matching;
        }

        var candidates = Directory.GetDirectories(filesRoot);
        var compatible = candidates
            .Where(candidate => IsSamePlatform(Path.GetFileName(candidate), hostRuntime))
            .ToList();

        // One compatible runtime is unambiguous even when the RID spelling differs (linux-x64 vs
        // linux-musl-x64); a foreign OS or architecture must never be installed silently.
        if (compatible.Count == 1)
        {
            return compatible[0];
        }

        if (candidates.Length == 0 && Directory.GetFiles(filesRoot).Length > 0)
        {
            // Portable layout: binaries directly under files/.
            return filesRoot;
        }

        var available = candidates.Length == 0
            ? "none"
            : string.Join(", ", candidates.Select(Path.GetFileName).Order(StringComparer.Ordinal));

        throw new ModulePackageException(
            $"The package has no binaries for this runtime ('{hostRuntime}'). Available: {available}.",
            [new PackageIssue(
                PackageIssueCodes.NoMatchingRuntime,
                $"No 'files/{hostRuntime}' directory; available: {available}.")]);
    }

    /// <summary>
    /// Linux distro RIDs that ship glibc assets and therefore fall back to the portable
    /// <c>linux-&lt;arch&gt;</c> runtime, exactly as the .NET RID fallback graph does.
    /// </summary>
    private static readonly HashSet<string> GlibcLinuxRids = new(StringComparer.OrdinalIgnoreCase)
    {
        "amzn", "arch", "centos", "debian", "fedora", "linux", "ol", "opensuse", "rhel",
        "rocky", "sles", "tizen", "ubuntu",
    };

    /// <summary>
    /// Whether a package RID targets the same operating system and CPU architecture as the host.
    /// </summary>
    /// <remarks>
    /// A distro RID such as <c>ubuntu.24.04-x64</c> is compatible with the portable
    /// <c>linux-x64</c> assets, and <c>alpine.3.20-x64</c> with <c>linux-musl-x64</c>; comparing the
    /// literal first segment (as an earlier version did) matched neither, so a portable package
    /// looked "foreign" on its own platform. Anything else must match the RID OS exactly.
    /// </remarks>
    /// <param name="candidateRid">The RID directory name from the package.</param>
    /// <param name="hostRid">The RID of the machine installing the package.</param>
    /// <returns><see langword="true"/> when the candidate targets the host platform.</returns>
    internal static bool IsSamePlatform(string candidateRid, string hostRid)
    {
        var candidate = candidateRid.Split('-');
        var host = hostRid.Split('-');

        if (candidate.Length < 2 || host.Length < 2)
        {
            return string.Equals(candidateRid, hostRid, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(GetRuntimeOsFamily(candidateRid), GetRuntimeOsFamily(hostRid), StringComparison.Ordinal)
               && string.Equals(candidate[^1], host[^1], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reduces a RID to the OS family used for compatibility: <c>windows</c>, <c>osx</c>,
    /// <c>linux</c> (any glibc distro), <c>linux-musl</c> (musl and Alpine), or the literal OS
    /// segment for everything else.
    /// </summary>
    /// <param name="rid">The runtime identifier, for example <c>ubuntu.24.04-x64</c>.</param>
    /// <returns>The OS family token.</returns>
    private static string GetRuntimeOsFamily(string rid)
    {
        var parts = rid.Split('-');

        // RID syntax is <os>-[<os version>]-<architecture>: the architecture is the last segment.
        var os = parts.Length > 1 ? string.Join('-', parts[..^1]) : rid;

        if (os.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            return "windows";
        }

        if (os.StartsWith("osx", StringComparison.OrdinalIgnoreCase)
            || os.StartsWith("macos", StringComparison.OrdinalIgnoreCase))
        {
            return "osx";
        }

        var distro = os.Split('.')[0];

        if (os.Contains("musl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(distro, "alpine", StringComparison.OrdinalIgnoreCase))
        {
            return "linux-musl";
        }

        return GlibcLinuxRids.Contains(distro) ? "linux" : os.ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string destination, bool recursive)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        if (!recursive)
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), recursive: true);
        }
    }

    private static string? CheckAcknowledgements(
        ModulePackageInstallRequest request, ModulePackageReviewDto review)
    {
        var expectedCapabilities = Normalize(review.Capabilities);
        var expectedScopes = Normalize(review.UserConsentScopes);

        if (!expectedCapabilities.SetEquals(Normalize(request.AcknowledgedCapabilities)))
        {
            return "The approved capabilities do not match the package manifest. Review it again: "
                   + Describe(expectedCapabilities);
        }

        if (!expectedScopes.SetEquals(Normalize(request.AcknowledgedScopes)))
        {
            return "The approved consent scopes do not match the package manifest. Review it again: "
                   + Describe(expectedScopes);
        }

        return null;
    }

    private static HashSet<string> Normalize(IEnumerable<string>? values) =>
        new(values ?? [], StringComparer.Ordinal);

    private static string Describe(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? "none requested" : string.Join(", ", values.Order(StringComparer.Ordinal));

    private async Task<bool> RestoreAsync(
        ModuleInstallLayout layout,
        string moduleId,
        string failedVersion,
        string? previousVersion,
        string? previousDirectory,
        CancellationToken cancellationToken)
    {
        if (previousDirectory is null)
        {
            // A first install that never worked leaves nothing to restore.
            layout.DeleteLive(moduleId);
            return false;
        }

        try
        {
            await _runner.StopAsync(moduleId, cancellationToken);
            layout.Restore(previousDirectory, moduleId);

            if (previousVersion is not null && _runner.CanStartModules)
            {
                await _runner.StartAndAwaitHealthyAsync(moduleId, cancellationToken);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogError(
                ex,
                "Restoring the previous version of {ModuleId} after a failed {FailedVersion} install failed",
                LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(failedVersion));

            return false;
        }
    }

    /// <summary>
    /// Resolves the configured store's entry for a package version, when it lists one.
    /// </summary>
    /// <remarks>
    /// This is the only place the installer learns a store-provided trust tier: it comes from the
    /// verified catalogue entry, never from the package. A missing feed, an unreadable one or an
    /// unlisted version is not an error — it only means there is nothing to withdraw and no curated
    /// tier to report.
    /// </remarks>
    private async Task<StoreListing?> ResolveStoreListingAsync(
        string moduleId, string version, CancellationToken cancellationToken)
    {
        try
        {
            var resolved = await _store.ResolveAsync(moduleId, version, cancellationToken);

            return resolved is null
                ? null
                : new StoreListing(
                    resolved.Package.Yanked,
                    resolved.Package.YankReason,
                    resolved.Package.TrustTier);
        }
        catch (Exception ex) when (ex is ModulePackageException or IOException or UnauthorizedAccessException)
        {
            // No feed configured (or an unreadable one) withdraws nothing by itself.
            _logger.LogDebug(
                "Store lookup for {ModuleId} {Version} skipped: {Error}",
                LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(version), ex.Message);

            return null;
        }
    }

    /// <summary>What the configured store says about a version, when it lists one.</summary>
    private sealed record StoreListing(bool Yanked, string? YankReason, ModuleStoreTrustTier TrustTier);

    private async Task AuditAsync(
        AuditAction action,
        string? moduleId,
        Guid? userId,
        string description,
        CancellationToken cancellationToken)
    {
        if (_auditLogger is null)
        {
            return;
        }

        try
        {
            await _auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = "dotnetcloud.core",
                    Action = action,
                    EntityType = "Module",
                    EntityId = userId ?? Guid.Empty,
                    Description = moduleId is null
                        ? description
                        : $"{description}:{moduleId}",
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Auditing {Action} failed: {Error}", action, ex.Message);
        }
    }

    private static ModulePackageIssueDto MapIssue(PackageIssue issue) => new()
    {
        Code = issue.Code,
        Message = issue.Message,
        EntryName = issue.EntryName,
    };

    private static ModuleInstallResultDto Failure(
        ModulePackageReviewDto review,
        string code,
        string error,
        IReadOnlyList<ModulePackageIssueDto> issues) => new()
        {
            Success = false,
            ModuleId = review.ModuleId,
            Version = review.Version,
            TrustTier = ModuleTrustTier.Store.ToString(),
            StoreTrustTier = review.TrustTier,
            KeyId = review.KeyId,
            Sha256 = review.Sha256,
            Error = error,
            Issues = issues.Count == 0
            ? [new ModulePackageIssueDto { Code = code, Message = error }]
            : issues,
            Warnings = review.Warnings,
        };

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>The record written next to an installed module's binaries.</summary>
    private sealed class InstallReceipt
    {
        public required string ModuleId { get; init; }

        public required string Version { get; init; }

        public required string PackageFile { get; init; }

        public string? Sha256 { get; init; }

        public string? KeyId { get; init; }

        public bool Unverified { get; init; }

        /// <summary>The store trust tier the version carried at install time, or <c>unverified</c>.</summary>
        public string? TrustTier { get; init; }

        public string? PublisherId { get; init; }

        public required string EntryPoint { get; init; }

        public required DateTime InstalledAtUtc { get; init; }
    }

    /// <summary>The installation-record fields an upgrade may change and a rollback must restore.</summary>
    private sealed record InstallRecordSnapshot(
        string Version,
        ModuleTrustTier TrustTier,
        string? PublisherId,
        string? SignedByKeyId,
        ModuleDataStoreMode DataStoreMode,
        ModuleSandboxStatus SandboxStatus,
        string Status);
}
