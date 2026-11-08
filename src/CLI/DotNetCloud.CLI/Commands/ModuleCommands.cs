using System.CommandLine;
using System.Text.Json;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.ModulePackaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Module lifecycle commands: list, start, stop, restart, install, uninstall.
/// </summary>
/// <remarks>
/// Lifecycle changes are written to the database; a running supervisor reconciles them within
/// 30 seconds (start/stop/restart), so the CLI never needs an IPC channel.
/// </remarks>
internal static class ModuleCommands
{
    private const string ModulesDirectoryEnvironmentVariable = "DOTNETCLOUD_MODULES_DIR";
    /// <summary>
    /// Creates the <c>module</c> parent command with all subcommands.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("module", "Manage DotNetCloud modules");
        command.Subcommands.Add(CreateList());
        command.Subcommands.Add(CreateStart());
        command.Subcommands.Add(CreateStop());
        command.Subcommands.Add(CreateRestart());
        command.Subcommands.Add(CreateInstall());
        command.Subcommands.Add(CreateVerify());
        command.Subcommands.Add(CreateInfo());
        command.Subcommands.Add(CreateRollback());
        command.Subcommands.Add(CreateUninstall());
        return command;
    }

    private static Command CreateList()
    {
        var command = new Command("list", "List all installed modules");
        command.SetAction(_ => ListModulesAsync());
        return command;
    }

    private static Command CreateStart()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to start (e.g., dotnetcloud.files)"
        };
        var command = new Command("start", "Start a specific module") { moduleArg };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return ChangeModuleStatusAsync(moduleId, "Enabled", "started");
        });
        return command;
    }

    private static Command CreateStop()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to stop"
        };
        var command = new Command("stop", "Stop a specific module") { moduleArg };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return ChangeModuleStatusAsync(moduleId, "Disabled", "stopped");
        });
        return command;
    }

    private static Command CreateRestart()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to restart"
        };
        var command = new Command("restart", "Restart a specific module") { moduleArg };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return RestartModuleAsync(moduleId);
        });
        return command;
    }

    private static Command CreateInstall()
    {
        var moduleArg = new Argument<string?>("module")
        {
            Description = "The module ID (with --from-feed or --from-store), or the ID of a module "
                          + "already extracted under the modules directory",
            DefaultValueFactory = _ => null,
        };
        var fileOption = new Option<string?>("--file")
        {
            Description = "Install a signed .dncpkg package file",
        };
        var feedOption = new Option<string?>("--from-feed")
        {
            Description = "Install a module from a local feed directory (index.json + packages)",
        };
        var storeOption = new Option<bool>("--from-store")
        {
            Description = "Install a module from the configured store source "
                          + "(Modules:Store:IndexUrl, or the local feed when unset)",
        };
        var versionOption = new Option<string?>("--version")
        {
            Description = "Version to install from the feed or store (default: the highest listed)",
        };
        var allowUnverifiedOption = new Option<bool>("--allow-unverified")
        {
            Description = "Install a package that is unsigned or signed by an untrusted key (audited)",
        };
        var yesOption = new Option<bool>("--yes", "-y")
        {
            Description = "Skip the interactive review confirmation",
        };

        var command = new Command("install", "Install a module from a package, a feed, the store, or the disk")
        {
            moduleArg,
            fileOption,
            feedOption,
            storeOption,
            versionOption,
            allowUnverifiedOption,
            yesOption,
        };

        command.SetAction(parseResult => InstallAsync(
            parseResult.GetValue(moduleArg),
            parseResult.GetValue(fileOption),
            parseResult.GetValue(feedOption),
            parseResult.GetValue(storeOption),
            parseResult.GetValue(versionOption),
            parseResult.GetValue(allowUnverifiedOption),
            parseResult.GetValue(yesOption)));

        return command;
    }

    private static Command CreateVerify()
    {
        var packageArg = new Argument<string>("package")
        {
            Description = "The .dncpkg package to verify",
        };
        var command = new Command("verify", "Verify a package's integrity, signature and manifest") { packageArg };
        command.SetAction(parseResult => VerifyPackageAsync(parseResult.GetValue(packageArg)!));
        return command;
    }

    private static Command CreateInfo()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to describe"
        };
        var command = new Command("info", "Show an installed module's manifest, tier and identity") { moduleArg };
        command.SetAction(parseResult => ShowModuleInfoAsync(parseResult.GetValue(moduleArg)!));
        return command;
    }

    private static Command CreateRollback()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to roll back"
        };
        var command = new Command("rollback", "Restore the previous version retained by the last install")
        {
            moduleArg
        };
        command.SetAction(parseResult => RollbackModuleAsync(parseResult.GetValue(moduleArg)!));
        return command;
    }

    private static Command CreateUninstall()
    {
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to uninstall"
        };
        var destroyDataOption = new Option<bool>("--destroy-data")
        {
            Description = "Drop the module's isolated schema and principal (irreversible). Default: keep the data as orphaned."
        };
        var keepFilesOption = new Option<bool>("--keep-files")
        {
            Description = "Keep the module binaries on disk (package-managed deployments)"
        };
        var command = new Command("uninstall", "Uninstall a module, optionally destroying its data") { moduleArg, destroyDataOption, keepFilesOption };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return UninstallModuleAsync(
                moduleId,
                parseResult.GetValue(destroyDataOption),
                parseResult.GetValue(keepFilesOption));
        });
        return command;
    }

    private static async Task<int> ListModulesAsync()
    {
        ConsoleOutput.WriteHeader("Installed Modules");

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var modules = await db.InstalledModules
            .AsNoTracking()
            .Include(m => m.CapabilityGrants)
            .OrderBy(m => m.ModuleId)
            .ToListAsync();

        if (modules.Count == 0)
        {
            ConsoleOutput.WriteInfo("No modules installed.");
            return 0;
        }

        var headers = new[] { "Module ID", "Version", "Status", "Trust", "Sandbox", "Capabilities", "Installed" };
        var rows = modules.Select(m => new[]
        {
            m.ModuleId,
            m.Version.ToString(),
            ConsoleOutput.FormatStatus(m.Status),
            m.TrustTier.ToString(),
            m.SandboxStatus.ToString(),
            m.CapabilityGrants.Count.ToString(),
            m.InstalledAt.ToString("yyyy-MM-dd")
        }).ToList();

        ConsoleOutput.WriteTable(headers, rows);
        Console.WriteLine();
        ConsoleOutput.WriteInfo($"Total: {modules.Count} module(s)");

        return 0;
    }

    private static async Task<int> ChangeModuleStatusAsync(string moduleId, string newStatus, string verb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (newStatus == "Disabled" && DotNetCloud.Core.Modules.RequiredModules.IsRequired(moduleId))
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is architecturally required and cannot be stopped or disabled.");
            return 1;
        }

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' not found.");
            return 1;
        }

        module.Status = newStatus;
        module.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess($"Module '{moduleId}' {verb}.");
        ConsoleOutput.WriteInfo("A running server applies this within 30 seconds; no restart is required.");
        return 0;
    }

    private static async Task<int> RestartModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' not found.");
            return 1;
        }

        // The supervisor reconciles this flag: it restarts the process (minting a fresh launch
        // credential) and resets the row to Enabled.
        module.Status = ModuleInstallStatus.RestartPending;
        module.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess($"Restart of module '{moduleId}' requested.");
        ConsoleOutput.WriteInfo("A running server applies this within 30 seconds. If it is not running, start it with 'dotnetcloud serve'.");
        return 0;
    }

    /// <summary>
    /// Dispatches <c>module install</c>: a package file, a local feed, the configured store source,
    /// or a module already on disk.
    /// </summary>
    private static Task<int> InstallAsync(
        string? moduleId,
        string? packageFile,
        string? feedDirectory,
        bool fromStore,
        string? version,
        bool allowUnverified,
        bool assumeYes)
    {
        var sources = new[] { packageFile is not null, feedDirectory is not null, fromStore };
        if (sources.Count(isSet => isSet) > 1)
        {
            ConsoleOutput.WriteError("Specify only one of --file, --from-feed or --from-store.");
            return Task.FromResult(1);
        }

        if (!string.IsNullOrWhiteSpace(packageFile))
        {
            return InstallFromPackageAsync(Path.GetFullPath(packageFile), allowUnverified, assumeYes);
        }

        if (!string.IsNullOrWhiteSpace(feedDirectory))
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                ConsoleOutput.WriteError("--from-feed requires a module id, for example: dotnetcloud module install --from-feed ./feed org.example.receipts");
                return Task.FromResult(1);
            }

            return InstallFromFeedAsync(Path.GetFullPath(feedDirectory), moduleId, version, allowUnverified, assumeYes);
        }

        if (fromStore)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                ConsoleOutput.WriteError("--from-store requires a module id, for example: dotnetcloud module install --from-store org.example.receipts");
                return Task.FromResult(1);
            }

            return InstallFromStoreAsync(moduleId, version, allowUnverified, assumeYes);
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            ConsoleOutput.WriteError(
                "Specify a module id, --file <package.dncpkg>, --from-feed <directory> or --from-store.");
            return Task.FromResult(1);
        }

        return RegisterInstalledModuleAsync(moduleId);
    }

    /// <summary>
    /// Resolves a module from a local feed and installs it. The feed's <c>yanked.json</c> is
    /// authoritative for this directory: a withdrawn version is never installed.
    /// </summary>
    private static async Task<int> InstallFromFeedAsync(
        string feedDirectory,
        string moduleId,
        string? version,
        bool allowUnverified,
        bool assumeYes)
    {
        if (!IsValidModuleId(moduleId))
        {
            ConsoleOutput.WriteError($"'{moduleId}' is not a valid module id.");
            return 1;
        }

        ModuleFeedIndex index;
        try
        {
            index = await ModuleFeed.ReadAsync(feedDirectory);
        }
        catch (ModulePackageException ex)
        {
            ConsoleOutput.WriteError(ex.Message);
            return 1;
        }

        var candidates = index.Packages
            .Where(entry => string.Equals(entry.Id, moduleId, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            ConsoleOutput.WriteError($"'{moduleId}' is not listed in the feed at '{feedDirectory}'.");
            return 1;
        }

        ModuleFeedEntry? entry;
        if (string.IsNullOrWhiteSpace(version))
        {
            entry = candidates
                .OrderByDescending(candidate => candidate.Version, StringComparer.Ordinal)
                .First();
        }
        else
        {
            entry = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Version, version, StringComparison.Ordinal));

            if (entry is null)
            {
                ConsoleOutput.WriteError($"'{moduleId}' {version} is not listed in the feed at '{feedDirectory}'.");
                return 1;
            }
        }

        var packagePath = Path.GetFullPath(Path.Combine(feedDirectory, entry.File));

        // The index is data, not authority.
        var relative = Path.GetRelativePath(feedDirectory, packagePath);
        var firstSegment = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
        if (Path.IsPathRooted(relative) || string.Equals(firstSegment, "..", StringComparison.Ordinal))
        {
            ConsoleOutput.WriteError($"Feed entry '{entry.File}' resolves outside the feed directory.");
            return 1;
        }

        if (!File.Exists(packagePath))
        {
            ConsoleOutput.WriteError($"The feed lists '{entry.File}' but the file does not exist.");
            return 1;
        }

        var yanked = ModuleFeed.FindYank(
            await ModuleFeed.ReadYankedAsync(feedDirectory), entry.Id, entry.Version);

        if (yanked is not null)
        {
            ConsoleOutput.WriteError(
                $"'{entry.Id}' {entry.Version} has been withdrawn ({yanked.Severity}): {yanked.Reason}");
            return 1;
        }

        return await InstallFromPackageAsync(packagePath, allowUnverified, assumeYes);
    }

    /// <summary>
    /// Resolves a module through the configured store source (a published index, or the local feed
    /// when <c>Modules:Store:IndexUrl</c> is unset), then verifies and installs it.
    /// </summary>
    /// <remarks>
    /// The remote client verifies the index signature, the downloaded hash and the package signature
    /// before the installer sees the file, so this path adds no trust of its own.
    /// </remarks>
    private static async Task<int> InstallFromStoreAsync(
        string moduleId,
        string? version,
        bool allowUnverified,
        bool assumeYes)
    {
        if (!IsValidModuleId(moduleId))
        {
            ConsoleOutput.WriteError($"'{moduleId}' is not a valid module id.");
            return 1;
        }

        await using var provider = ServiceProviderFactory.CreateForPackageInstall();
        if (provider is null)
            return 1;

        var store = provider.GetRequiredService<IModuleStoreService>();

        ModuleStoreResolution? resolved;
        try
        {
            resolved = await store.ResolveAsync(moduleId, version);
        }
        catch (ModulePackageException ex)
        {
            ConsoleOutput.WriteError(ex.Message);
            return 1;
        }

        if (resolved is null)
        {
            ConsoleOutput.WriteError(
                $"'{moduleId}'{(version is null ? string.Empty : " " + version)} "
                + $"is not listed by the configured store source ({store.SourceId}).");
            return 1;
        }

        if (resolved.Package.Yanked)
        {
            ConsoleOutput.WriteError(
                $"'{resolved.Package.ModuleId}' {resolved.Package.Version} has been withdrawn: "
                + (resolved.Package.YankReason ?? "no reason given"));
            return 1;
        }

        return await InstallFromPackageAsync(
            resolved.PackagePath, allowUnverified, assumeYes, resolved.SignaturePath);
    }

    /// <summary>
    /// Verifies, reviews and installs one package.
    /// </summary>
    private static async Task<int> InstallFromPackageAsync(
        string packagePath, bool allowUnverified, bool assumeYes, string? signaturePath = null)
    {
        if (!File.Exists(packagePath))
        {
            ConsoleOutput.WriteError($"Package not found: {packagePath}");
            return 1;
        }

        await using var provider = ServiceProviderFactory.CreateForPackageInstall();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var installer = scope.ServiceProvider.GetRequiredService<IModulePackageInstaller>();

        var review = await installer.InspectAsync(packagePath, signaturePath);
        WriteReview(review);

        var blocking = review.Issues
            .Where(issue => issue.Code is not (PackageIssueCodes.SignatureMissing or PackageIssueCodes.SignatureUntrustedKey))
            .ToList();

        if (blocking.Count > 0)
        {
            ConsoleOutput.WriteError("The package failed verification and was not installed.");
            return 1;
        }

        var unverified = review.Issues.Any(issue =>
            issue.Code is PackageIssueCodes.SignatureMissing or PackageIssueCodes.SignatureUntrustedKey);

        if (unverified && !allowUnverified)
        {
            ConsoleOutput.WriteError(
                "The package is not signed by a key this server trusts. "
                + "Re-run with --allow-unverified only if you trust its source.");
            return 1;
        }

        if (unverified)
        {
            ConsoleOutput.WriteWarning(
                $"UNVERIFIED INSTALL. Nobody vouches for these bytes: SHA-256 {review.Sha256}");
        }

        if (!assumeYes
            && !ConsoleOutput.PromptConfirm(
                $"Install {review.ModuleId} {review.Version}"
                + (review.IsAlreadyInstalled ? $" (replacing {review.InstalledVersion})" : string.Empty)
                + "?", defaultValue: false))
        {
            ConsoleOutput.WriteInfo("Install cancelled.");
            return 0;
        }

        var result = await installer.InstallAsync(new ModulePackageInstallRequest
        {
            PackagePath = packagePath,
            SignaturePath = signaturePath,
            ExpectedSha256 = review.Sha256,
            AllowUnverified = allowUnverified,
            AcknowledgedCapabilities = review.Capabilities,
            AcknowledgedScopes = review.UserConsentScopes,
        });

        if (!result.Success)
        {
            ConsoleOutput.WriteError(result.Error ?? "The install failed.");

            foreach (var issue in result.Issues)
            {
                ConsoleOutput.WriteError($"  [{issue.Code}] {issue.Message}");
            }

            return 1;
        }

        ConsoleOutput.WriteSuccess(
            $"Installed {result.ModuleId} {result.Version}"
            + (result.PreviousVersion is null ? string.Empty : $" (replacing {result.PreviousVersion})"));

        if (result.DataStoreProvisioned)
        {
            ConsoleOutput.WriteInfo("Provisioned the module's isolated data store.");
        }

        ConsoleOutput.WriteInfo("A running server starts (or restarts) the module within 30 seconds.");
        return 0;
    }

    /// <summary>
    /// Prints the review: identity, integrity, and the capabilities and scopes an operator approves.
    /// </summary>
    private static void WriteReview(ModulePackageReviewDto review)
    {
        ConsoleOutput.WriteHeader("Package Review");

        ConsoleOutput.WriteDetail("Module", review.ModuleId ?? "(unknown)");
        ConsoleOutput.WriteDetail("Name", review.Name ?? "(none)");
        ConsoleOutput.WriteDetail("Version", review.Version ?? "(unknown)");
        ConsoleOutput.WriteDetail("Manifest", review.ManifestVersion == 0 ? "unreadable" : $"v{review.ManifestVersion}");
        ConsoleOutput.WriteDetail("Publisher", review.PublisherId ?? "(none)");
        ConsoleOutput.WriteDetail("Data store", review.DataStoreMode);
        ConsoleOutput.WriteDetail("Erasure support", review.HandlesUserErasure ? "yes" : "no");
        ConsoleOutput.WriteDetail("SHA-256", review.Sha256 ?? "(not computed)");

        var signature = review.IsSigned
            ? $"{review.KeyId} ({(review.IsSignedByTrustedKey ? "trusted" : "UNTRUSTED")})"
            : "unsigned";
        ConsoleOutput.WriteDetail("Signature", signature);

        if (review.IsAlreadyInstalled)
        {
            ConsoleOutput.WriteDetail("Installed", review.InstalledVersion ?? "(unknown)");
        }

        ConsoleOutput.WriteDetail(
            "Capabilities",
            review.Capabilities.Count == 0 ? "(none requested)" : string.Join(", ", review.Capabilities));

        ConsoleOutput.WriteDetail(
            "Consent scopes",
            review.UserConsentScopes.Count == 0 ? "(none requested)" : string.Join(", ", review.UserConsentScopes));

        foreach (var warning in review.Warnings)
        {
            ConsoleOutput.WriteWarning($"[{warning.Code}] {warning.Message}");
        }

        foreach (var issue in review.Issues)
        {
            ConsoleOutput.WriteError($"[{issue.Code}] {issue.Message}");
        }
    }

    /// <summary>
    /// Verifies a package without touching the installation.
    /// </summary>
    private static async Task<int> VerifyPackageAsync(string packagePath)
    {
        var path = Path.GetFullPath(packagePath);
        if (!File.Exists(path))
        {
            ConsoleOutput.WriteError($"Package not found: {path}");
            return 1;
        }

        var configuration = ServiceProviderFactory.BuildCliConfiguration();
        var options = configuration.GetSection(ModuleStoreOptions.SectionName).Get<ModuleStoreOptions>()
                      ?? new ModuleStoreOptions();

        var verification = await ModulePackageVerifier.VerifyAsync(
            path,
            options: new ModulePackageVerificationOptions
            {
                TrustedKeys = options.GetEffectiveTrustedKeys(),
            });

        ConsoleOutput.WriteHeader("Package Verification");
        ConsoleOutput.WriteDetail("Package", Path.GetFileName(path));
        ConsoleOutput.WriteDetail("Module", verification.ModuleId ?? "(unknown)");
        ConsoleOutput.WriteDetail("Version", verification.Version ?? "(unknown)");
        ConsoleOutput.WriteDetail("SHA-256", verification.Sha256 ?? "(not computed)");
        ConsoleOutput.WriteDetail(
            "Signature",
            verification.KeyId is null ? "unsigned (not accepted without --allow-unverified)" : verification.KeyId);
        ConsoleOutput.WriteDetail("Entries", verification.EntryCount.ToString());
        ConsoleOutput.WriteDetail("Uncompressed", $"{verification.UncompressedBytes:N0} bytes");

        foreach (var warning in verification.Warnings)
        {
            ConsoleOutput.WriteWarning($"[{warning.Code}] {warning.Message}");
        }

        foreach (var issue in verification.Issues)
        {
            ConsoleOutput.WriteError($"[{issue.Code}] {issue.Message}");
        }

        if (verification.IsValid)
        {
            ConsoleOutput.WriteSuccess("The package is valid and signed by a trusted key.");
            return 0;
        }

        ConsoleOutput.WriteError("The package did not pass verification.");
        return 1;
    }

    /// <summary>
    /// Shows the persisted installation record next to the installed manifest and install receipt.
    /// </summary>
    private static async Task<int> ShowModuleInfoAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .AsNoTracking()
            .Include(m => m.CapabilityGrants)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is not installed.");
            return 1;
        }

        var moduleDirectory = Path.Combine(ResolveModulesDirectory(), moduleId);
        var manifest = ReadManifestIfPresent(Path.Combine(moduleDirectory, "manifest.json"));
        var receipt = ReadReceipt(Path.Combine(moduleDirectory, ModulePackageInstaller.ReceiptFileName));

        ConsoleOutput.WriteHeader($"Module {moduleId}");

        ConsoleOutput.WriteDetail("Version", module.Version);
        ConsoleOutput.WriteDetail("Status", ConsoleOutput.FormatStatus(module.Status));
        ConsoleOutput.WriteDetail("Trust tier", module.TrustTier.ToString());
        ConsoleOutput.WriteDetail("Data store", module.DataStoreMode.ToString());
        ConsoleOutput.WriteDetail("Sandbox", module.SandboxStatus.ToString());
        ConsoleOutput.WriteDetail("Publisher", module.PublisherId ?? "(none)");
        ConsoleOutput.WriteDetail("Signed by", module.SignedByKeyId ?? "(unverified / bundled)");
        ConsoleOutput.WriteDetail("Installed", module.InstalledAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        ConsoleOutput.WriteDetail("Directory", moduleDirectory);

        if (manifest is not null)
        {
            ConsoleOutput.WriteDetail("Manifest", $"v{manifest.ManifestVersion}");
            ConsoleOutput.WriteDetail(
                "Capabilities",
                manifest.Capabilities.Count == 0 ? "(none)" : string.Join(", ", manifest.Capabilities));
        }

        if (receipt is not null)
        {
            ConsoleOutput.WriteDetail("Package", receipt.PackageFile ?? "(unknown)");
            ConsoleOutput.WriteDetail("Package hash", receipt.Sha256 ?? "(none)");
            ConsoleOutput.WriteDetail("Install receipt", receipt.Unverified ? "UNVERIFIED" : "verified");
        }

        ConsoleOutput.WriteDetail(
            "Grants",
            module.CapabilityGrants.Count == 0
                ? "(none)"
                : string.Join(", ", module.CapabilityGrants.Select(grant => grant.CapabilityName)));

        return 0;
    }

    /// <summary>
    /// Restores the previous version retained by the last install.
    /// </summary>
    private static async Task<int> RollbackModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        await using var provider = ServiceProviderFactory.CreateForPackageInstall();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var installer = scope.ServiceProvider.GetRequiredService<IModulePackageInstaller>();

        var result = await installer.RollbackAsync(moduleId);

        if (!result.Success)
        {
            ConsoleOutput.WriteError(result.Error ?? "The rollback failed.");
            return 1;
        }

        ConsoleOutput.WriteSuccess(
            $"Rolled {moduleId} back to {result.Version} (was {result.PreviousVersion}).");
        ConsoleOutput.WriteInfo("A running server restarts the module within 30 seconds.");
        return 0;
    }

    private static ModuleManifestSnapshot? ReadManifestIfPresent(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            return ReadManifestSnapshot(manifestPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static ModuleManifestSnapshot ReadManifestSnapshot(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = document.RootElement;

        var manifestVersion = root.TryGetProperty("manifestVersion", out var versionElement)
                              && versionElement.TryGetInt32(out var parsedVersion)
            ? parsedVersion
            : 1;

        var capabilities = new List<string>();
        foreach (var property in new[] { "capabilities", "requiredCapabilities" })
        {
            if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            capabilities.AddRange(array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(value => value.Length > 0));

            if (capabilities.Count > 0)
            {
                break;
            }
        }

        return new ModuleManifestSnapshot(manifestVersion, capabilities);
    }

    private static InstallReceiptSnapshot? ReadReceipt(string receiptPath)
    {
        if (!File.Exists(receiptPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(receiptPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return new InstallReceiptSnapshot(
                root.TryGetProperty("packageFile", out var file) ? file.GetString() : null,
                root.TryGetProperty("sha256", out var hash) ? hash.GetString() : null,
                root.TryGetProperty("unverified", out var unverified)
                && unverified.ValueKind == JsonValueKind.True);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private sealed record ModuleManifestSnapshot(int ManifestVersion, IReadOnlyList<string> Capabilities);

    private sealed record InstallReceiptSnapshot(string? PackageFile, string? Sha256, bool Unverified);

    private static async Task<int> RegisterInstalledModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!IsValidModuleId(moduleId))
        {
            ConsoleOutput.WriteError(
                $"'{moduleId}' is not a valid module id. Expected reverse-DNS form, e.g. org.example.receipts.");
            return 1;
        }

        var modulesRoot = ResolveModulesDirectory();
        var manifestPath = Path.Combine(modulesRoot, moduleId, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            ConsoleOutput.WriteError(
                $"No manifest found at '{manifestPath}'. Copy the module into the modules directory first " +
                "(package installation arrives with the module store), then re-run this command.");
            return 1;
        }

        ModuleManifestInfo manifest;
        try
        {
            manifest = ReadManifest(manifestPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            ConsoleOutput.WriteError($"Could not read the module manifest: {ex.Message}");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(manifest.Id)
            && !string.Equals(manifest.Id, moduleId, StringComparison.Ordinal))
        {
            ConsoleOutput.WriteError(
                $"The manifest declares id '{manifest.Id}' but the directory is '{moduleId}'. " +
                "The manifest id must match the install directory.");
            return 1;
        }

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existing = await db.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        var isRequired = RequiredModules.IsRequired(moduleId);
        var trustTier = isRequired
            ? ModuleTrustTier.Required
            : manifest.TrustTier;

        if (existing is not null)
        {
            var previousVersion = existing.Version;
            existing.Version = manifest.Version;
            existing.TrustTier = trustTier;
            existing.DataStoreMode = manifest.DataStoreMode;
            existing.PublisherId = manifest.PublisherId ?? existing.PublisherId;
            existing.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            ConsoleOutput.WriteSuccess(
                $"Module '{moduleId}' reconciled at version {manifest.Version} (previously {previousVersion}).");
            ConsoleOutput.WriteInfo("Version changes are recorded as ModuleUpdated in the audit trail.");
            return 0;
        }

        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = moduleId,
            Version = manifest.Version,
            Status = ModuleInstallStatus.Enabled,
            InstalledAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsRequired = isRequired,
            TrustTier = trustTier,
            PublisherId = manifest.PublisherId,
            DataStoreMode = manifest.DataStoreMode,
            SandboxStatus = isRequired
                ? ModuleSandboxStatus.Active
                : ModuleSandboxStatus.PendingProvisioning,
        });

        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess(
            $"Module '{moduleId}' v{manifest.Version} registered (tier: {trustTier}, data store: {manifest.DataStoreMode}).");

        if (manifest.DataStoreMode == ModuleDataStoreMode.Isolated)
        {
            ConsoleOutput.WriteInfo($"Provision its isolated schema with: dotnetcloud datastore provision {moduleId}");
        }

        ConsoleOutput.WriteInfo("A running server starts the module within 30 seconds.");
        return 0;
    }

    private static async Task<int> UninstallModuleAsync(string moduleId, bool destroyData, bool keepFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (RequiredModules.IsRequired(moduleId))
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is architecturally required and cannot be uninstalled.");
            return 1;
        }

        if (!IsValidModuleId(moduleId))
        {
            ConsoleOutput.WriteError($"'{moduleId}' is not a valid module id.");
            return 1;
        }

        var dataChoice = destroyData ? "its data store is DESTROYED" : "its data store is kept as orphaned";
        if (!ConsoleOutput.PromptConfirm($"Uninstall module '{moduleId}'? {dataChoice}."))
        {
            ConsoleOutput.WriteInfo("Uninstall cancelled.");
            return 0;
        }

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .Include(m => m.CapabilityGrants)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' not found.");
            return 1;
        }

        if (module.IsRequired)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is architecturally required and cannot be uninstalled.");
            return 1;
        }

        // 1. Data store.
        var store = await db.ModuleDataStores
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId);

        if (store is not null)
        {
            if (destroyData)
            {
                var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

                try
                {
                    await provisioning.DestroyAsync(moduleId);
                    ConsoleOutput.WriteSuccess($"Data store for '{moduleId}' destroyed.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Do not delete the installation record: the operator must be able to retry.
                    ConsoleOutput.WriteError($"Destroying the data store failed: {ex.Message}");
                    ConsoleOutput.WriteInfo("Stop the module first (dotnetcloud module stop) and retry.");
                    return 1;
                }
            }
            else
            {
                store.Status = ModuleDataStoreStatus.Orphaned;
                store.EncryptedConnectionString = null;
                store.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                ConsoleOutput.WriteInfo(
                    $"Data store '{store.SchemaName}' kept and marked Orphaned. " +
                    $"Drop it later with: dotnetcloud datastore destroy {moduleId}");
            }
        }

        // 2. Binaries (containment-checked).
        if (!keepFiles)
        {
            if (TryRemoveModuleDirectory(moduleId, out var removedPath))
            {
                ConsoleOutput.WriteInfo($"Removed module binaries from '{removedPath}'.");
            }
            else
            {
                ConsoleOutput.WriteWarning(
                    "No module directory was removed (not present, or outside the modules directory).");
            }
        }

        // 3. Installation record; grants, quotas, settings and consents cascade.
        db.ModuleCapabilityGrants.RemoveRange(module.CapabilityGrants);
        db.InstalledModules.Remove(module);
        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess($"Module '{moduleId}' uninstalled.");
        ConsoleOutput.WriteInfo("A running server stops the module within 30 seconds.");
        return 0;
    }

    /// <summary>
    /// Resolves the modules directory the supervisor uses: an explicit
    /// <c>DOTNETCLOUD_MODULES_DIR</c>, otherwise <c>modules</c> next to this executable.
    /// </summary>
    private static string ResolveModulesDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(ModulesDirectoryEnvironmentVariable);
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "modules")
            : configured;

        return Path.GetFullPath(root);
    }

    /// <summary>
    /// Reads the fields the CLI needs from a module manifest. Unknown fields are ignored, so both
    /// manifest v1 and the strict v2 schema work here.
    /// </summary>
    private static ModuleManifestInfo ReadManifest(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = document.RootElement;

        var id = ReadString(root, "id");
        var version = ReadString(root, "version") ?? "0.0.0";

        var dataStore = ModuleDataStoreMode.None;
        if (string.Equals(ReadString(root, "dataStore"), "isolated", StringComparison.OrdinalIgnoreCase))
        {
            dataStore = ModuleDataStoreMode.Isolated;
        }

        string? publisherId = null;
        if (root.TryGetProperty("publisher", out var publisher) && publisher.ValueKind == JsonValueKind.Object)
        {
            publisherId = ReadString(publisher, "id");
        }
        else
        {
            publisherId = ReadString(root, "publisherId");
        }

        // A v2 manifest is the sandbox contract; a v1 manifest is legacy and bundled-only by
        // definition (it cannot declare a data store, consent scopes or UI).
        var manifestVersion = 1;
        if (root.TryGetProperty("manifestVersion", out var versionElement)
            && versionElement.TryGetInt32(out var parsedVersion))
        {
            manifestVersion = parsedVersion;
        }

        return new ModuleManifestInfo(
            id,
            version,
            manifestVersion >= 2 ? ModuleTrustTier.Store : ModuleTrustTier.Bundled,
            dataStore,
            publisherId);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    /// <summary>
    /// Deletes a module's directory, refusing anything that resolves outside the modules root.
    /// </summary>
    private static bool TryRemoveModuleDirectory(string moduleId, out string removedPath)
    {
        removedPath = string.Empty;

        var modulesRoot = ResolveModulesDirectory();
        var moduleDirectory = Path.GetFullPath(Path.Combine(modulesRoot, moduleId));

        // Strong containment guard: the relative path must not be rooted, must not be the root
        // itself, and its first segment must not be "..".
        var relative = Path.GetRelativePath(modulesRoot, moduleDirectory);
        var firstSegment = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
        if (string.Equals(relative, ".", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || string.Equals(firstSegment, "..", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Directory.Exists(moduleDirectory))
        {
            return false;
        }

        try
        {
            Directory.Delete(moduleDirectory, recursive: true);
            removedPath = moduleDirectory;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConsoleOutput.WriteWarning($"Could not delete '{moduleDirectory}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reverse-DNS module id: at least one dot, lowercase alphanumerics and hyphens per segment.
    /// </summary>
    private static bool IsValidModuleId(string moduleId) =>
        ModuleIdPattern.IsMatch(moduleId);

    private static readonly System.Text.RegularExpressions.Regex ModuleIdPattern = new(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private sealed record ModuleManifestInfo(
        string? Id,
        string Version,
        ModuleTrustTier TrustTier,
        ModuleDataStoreMode DataStoreMode,
        string? PublisherId);
}
