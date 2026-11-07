using System.CommandLine;
using System.Text.Json;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
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
        var moduleArg = new Argument<string>("module")
        {
            Description = "The module ID to register (must already exist under the modules directory)"
        };
        var command = new Command("install", "Register a module that is already present on disk") { moduleArg };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return InstallModuleAsync(moduleId);
        });
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

    private static async Task<int> InstallModuleAsync(string moduleId)
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
