using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Module data-store lifecycle commands: status, provision, rotate, reset-credentials, destroy.
/// </summary>
internal static class DataStoreCommands
{
    /// <summary>
    /// Creates the <c>datastore</c> parent command with all subcommands.
    /// </summary>
    public static Command Create()
    {
        var command = new Command(
            "datastore",
            "Manage isolated module data stores (dedicated schemas and database principals)");
        command.Subcommands.Add(CreateStatus());
        command.Subcommands.Add(CreatePreflight());
        command.Subcommands.Add(CreateProvision());
        command.Subcommands.Add(CreateRotate());
        command.Subcommands.Add(CreateResetCredentials());
        command.Subcommands.Add(CreateDestroy());
        return command;
    }

    private static Argument<string?> CreateOptionalModuleArgument(string description) =>
        new("module")
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        };

    private static Argument<string> CreateModuleArgument(string description) =>
        new("module")
        {
            Description = description
        };

    private static Option<int?> CreateConnectionLimitOption() =>
        new("--connection-limit")
        {
            Description = "Maximum concurrent connections for the module principal (defaults to the module quota, then 10)"
        };

    private static Command CreateStatus()
    {
        var moduleArg = CreateOptionalModuleArgument(
            "The module ID to inspect (omit to list every isolated data store)");
        var command = new Command("status", "Show provisioned module data stores and their credentials state")
        {
            moduleArg
        };
        command.SetAction(parseResult => StatusAsync(parseResult.GetValue(moduleArg)));
        return command;
    }

    private static Command CreatePreflight()
    {
        var command = new Command(
            "preflight",
            "Report which installed modules can adopt their existing schema, and why any cannot");
        command.SetAction(_ => PreflightAsync());
        return command;
    }

    private static Command CreateProvision()
    {
        var moduleArg = CreateModuleArgument("The module ID to provision");
        var limitOption = CreateConnectionLimitOption();
        var command = new Command("provision", "Provision the schema and database principal for an isolated module")
        {
            moduleArg,
            limitOption
        };
        command.SetAction(parseResult => ProvisionAsync(
            parseResult.GetValue(moduleArg)!,
            parseResult.GetValue(limitOption)));
        return command;
    }

    private static Command CreateRotate()
    {
        var moduleArg = CreateModuleArgument("The module ID whose credentials should be rotated");
        var limitOption = CreateConnectionLimitOption();
        var command = new Command("rotate", "Rotate the module principal's password and re-encrypt the connection string")
        {
            moduleArg,
            limitOption
        };
        command.SetAction(parseResult => RotateAsync(
            parseResult.GetValue(moduleArg)!,
            parseResult.GetValue(limitOption),
            recovery: false));
        return command;
    }

    private static Command CreateResetCredentials()
    {
        var moduleArg = CreateModuleArgument("The module ID whose credentials should be reset");
        var limitOption = CreateConnectionLimitOption();
        var command = new Command(
            "reset-credentials",
            "Recovery: reset the module principal's password when the stored credentials cannot be decrypted")
        {
            moduleArg,
            limitOption
        };
        command.SetAction(parseResult => RotateAsync(
            parseResult.GetValue(moduleArg)!,
            parseResult.GetValue(limitOption),
            recovery: true));
        return command;
    }

    private static Command CreateDestroy()
    {
        var moduleArg = CreateModuleArgument("The module ID whose data store should be destroyed");
        var yesOption = new Option<bool>("--yes")
        {
            Description = "Skip the confirmation prompt (for automation)"
        };
        var command = new Command("destroy", "Drop the module schema and principal (irreversible)")
        {
            moduleArg,
            yesOption
        };
        command.SetAction(parseResult => DestroyAsync(
            parseResult.GetValue(moduleArg)!,
            parseResult.GetValue(yesOption)));
        return command;
    }

    private static async Task<int> StatusAsync(string? moduleId)
    {
        ConsoleOutput.WriteHeader("Module Data Stores");

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var query = db.ModuleDataStores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(moduleId))
            query = query.Where(d => d.ModuleId == moduleId);

        var stores = await query.OrderBy(d => d.ModuleId).ToListAsync();

        if (stores.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                var installed = await db.InstalledModules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

                if (installed is null)
                {
                    ConsoleOutput.WriteError($"Module '{moduleId}' is not installed.");
                    return 1;
                }

                ConsoleOutput.WriteInfo(
                    $"Module '{moduleId}' has no isolated data store (data store mode: {installed.DataStoreMode}).");
                return 0;
            }

            ConsoleOutput.WriteInfo("No isolated module data stores are provisioned.");
            return 0;
        }

        var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

        var headers = new[] { "Module ID", "Provider", "Schema", "Principal", "Status", "Updated" };
        var rows = new List<string[]>(stores.Count);

        foreach (var store in stores)
        {
            rows.Add(
            [
                store.ModuleId,
                store.Provider,
                store.SchemaName,
                store.PrincipalName,
                store.Status.ToString(),
                store.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm")
            ]);

            if (!string.IsNullOrEmpty(store.EncryptedConnectionString)
                && await provisioning.GetConnectionStringAsync(store.ModuleId) is null)
            {
                ConsoleOutput.WriteWarning(
                    $"{store.ModuleId}: stored credentials cannot be decrypted with the current key ring. " +
                    $"Run 'dotnetcloud datastore reset-credentials {store.ModuleId}'.");
            }

            if (!string.IsNullOrWhiteSpace(store.LastError))
            {
                ConsoleOutput.WriteWarning($"{store.ModuleId}: {store.LastError}");
            }
        }

        ConsoleOutput.WriteTable(headers, rows);
        Console.WriteLine();
        ConsoleOutput.WriteInfo($"Total: {stores.Count} data store(s)");
        return 0;
    }

    private static async Task<int> PreflightAsync()
    {
        ConsoleOutput.WriteHeader("Module Data Store Preflight");

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

        var modules = await db.InstalledModules
            .AsNoTracking()
            .OrderBy(m => m.ModuleId)
            .ToListAsync();

        if (modules.Count == 0)
        {
            ConsoleOutput.WriteInfo("No modules installed.");
            return 0;
        }

        var stores = await db.ModuleDataStores
            .AsNoTracking()
            .ToDictionaryAsync(d => d.ModuleId);

        ModuleDataStorePrerequisiteResult? prerequisites = null;
        var rows = new List<string[]>(modules.Count);
        var blocked = 0;

        foreach (var module in modules)
        {
            string outcome;
            string detail;

            if (module.IsRequired)
            {
                outcome = "Skipped";
                detail = "required module (core-managed schema)";
            }
            else if (module.DataStoreMode != ModuleDataStoreMode.Isolated)
            {
                outcome = "Skipped";
                detail = "dataStore: none";
            }
            else if (stores.TryGetValue(module.ModuleId, out var store)
                     && store.Status == ModuleDataStoreStatus.Active
                     && !string.IsNullOrEmpty(store.EncryptedConnectionString))
            {
                outcome = "Ready";
                detail = $"schema {store.SchemaName}";
            }
            else
            {
                prerequisites ??= await provisioning.CheckPrerequisitesAsync();

                if (prerequisites.IsSatisfied)
                {
                    outcome = "Adoptable";
                    detail = "existing schema will be adopted at startup";
                }
                else
                {
                    outcome = "Blocked";
                    detail = string.Join(" ", prerequisites.Errors);
                    blocked++;
                }
            }

            rows.Add([module.ModuleId, module.TrustTier.ToString(), outcome, detail]);
        }

        ConsoleOutput.WriteTable(["Module ID", "Tier", "Outcome", "Detail"], rows);
        Console.WriteLine();

        if (blocked > 0)
        {
            ConsoleOutput.WriteWarning(
                $"{blocked} module(s) will be held with SandboxStatus=Error until the prerequisites above are fixed.");
            return 1;
        }

        ConsoleOutput.WriteInfo("No blocking problems found.");
        return 0;
    }

    private static async Task<int> ProvisionAsync(string moduleId, int? connectionLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        ConsoleOutput.WriteHeader("Provision Module Data Store");

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is not installed.");
            return 1;
        }

        var rejection = DescribeNonIsolatedModule(module);
        if (rejection is not null)
        {
            ConsoleOutput.WriteError(rejection);
            return 1;
        }

        var limit = connectionLimit;
        var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

        var result = await provisioning.EnsureProvisionedAsync(moduleId, limit);
        await ApplySandboxStatusAsync(
            db, moduleId, result.Success ? ModuleSandboxStatus.Active : ModuleSandboxStatus.Error);

        if (!result.Success)
        {
            ConsoleOutput.WriteError($"Provisioning failed: {result.Error}");
            return 1;
        }

        ConsoleOutput.WriteSuccess(
            $"Data store ready for '{moduleId}' (schema: {result.SchemaName}, principal: {result.PrincipalName}).");
        ConsoleOutput.WriteInfo("Restart the module or the server so it picks up the new connection string.");
        return 0;
    }

    private static async Task<int> RotateAsync(string moduleId, int? connectionLimit, bool recovery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        ConsoleOutput.WriteHeader(recovery ? "Reset Module Data Store Credentials" : "Rotate Module Data Store Credentials");

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is not installed.");
            return 1;
        }

        var rejection = DescribeNonIsolatedModule(module);
        if (rejection is not null)
        {
            ConsoleOutput.WriteError(rejection);
            return 1;
        }

        if (recovery)
        {
            ConsoleOutput.WriteInfo(
                "Use this after a restore without the DataProtection key ring. " +
                "The password is regenerated and re-encrypted with the current key ring.");
        }

        if (!ConsoleOutput.PromptConfirm(
                $"Rotate credentials for '{moduleId}'? The module must be restarted to use the new password.",
                defaultValue: true))
        {
            ConsoleOutput.WriteInfo("Credential rotation cancelled.");
            return 0;
        }

        var limit = connectionLimit;
        var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

        var result = await provisioning.RotateCredentialsAsync(moduleId, limit);
        await ApplySandboxStatusAsync(
            db, moduleId, result.Success ? ModuleSandboxStatus.Active : ModuleSandboxStatus.Error);

        if (!result.Success)
        {
            ConsoleOutput.WriteError($"Credential rotation failed: {result.Error}");
            return 1;
        }

        ConsoleOutput.WriteSuccess($"Credentials rotated for '{moduleId}'.");
        ConsoleOutput.WriteInfo("Restart the module or the server so it picks up the new connection string.");
        return 0;
    }

    private static async Task<int> DestroyAsync(string moduleId, bool skipConfirmation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        ConsoleOutput.WriteHeader("Destroy Module Data Store");

        await using var provider = ServiceProviderFactory.CreateForDataStore();
        if (provider is null)
            return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' is not installed.");
            return 1;
        }

        if (module.IsRequired)
        {
            ConsoleOutput.WriteError(
                $"Module '{moduleId}' is architecturally required and has no isolated data store.");
            return 1;
        }

        var store = await db.ModuleDataStores
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId);

        if (store is null)
        {
            ConsoleOutput.WriteInfo($"Module '{moduleId}' has no isolated data store; nothing to destroy.");
            return 0;
        }

        if (!skipConfirmation && !ConsoleOutput.PromptConfirm(
                $"Permanently destroy schema '{store.SchemaName}' and principal '{store.PrincipalName}' for '{moduleId}'?"))
        {
            ConsoleOutput.WriteInfo("Destroy cancelled.");
            return 0;
        }

        var provisioning = scope.ServiceProvider.GetRequiredService<IModuleDataStoreProvisioningService>();

        try
        {
            await provisioning.DestroyAsync(moduleId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ApplySandboxStatusAsync(db, moduleId, ModuleSandboxStatus.Error);
            ConsoleOutput.WriteError($"Destroy failed: {ex.Message}");
            ConsoleOutput.WriteInfo("Stop the module first and retry; the admin API returns the same sanitized error.");
            return 1;
        }

        await ApplySandboxStatusAsync(db, moduleId, ModuleSandboxStatus.PendingProvisioning);

        ConsoleOutput.WriteSuccess($"Data store for '{moduleId}' destroyed.");
        return 0;
    }

    private static string? DescribeNonIsolatedModule(InstalledModule module)
    {
        if (module.IsRequired)
        {
            return $"Module '{module.ModuleId}' is architecturally required and uses the core schema.";
        }

        return module.DataStoreMode == ModuleDataStoreMode.Isolated
            ? null
            : $"Module '{module.ModuleId}' does not declare dataStore: \"isolated\".";
    }

    private static async Task ApplySandboxStatusAsync(
        CoreDbContext db, string moduleId, ModuleSandboxStatus status)
    {
        var module = await db.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null || module.SandboxStatus == status)
            return;

        module.SandboxStatus = status;
        await db.SaveChangesAsync();
    }
}
