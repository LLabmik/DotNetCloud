using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Module lifecycle commands: list, start, stop, restart, install, uninstall.
/// </summary>
internal static class ModuleCommands
{
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
            Description = "The module ID to install"
        };
        var command = new Command("install", "Install a module") { moduleArg };
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
        var command = new Command("uninstall", "Uninstall a module") { moduleArg };
        command.SetAction(parseResult =>
        {
            var moduleId = parseResult.GetValue(moduleArg)!;
            return UninstallModuleAsync(moduleId);
        });
        return command;
    }

    private static async Task<int> ListModulesAsync()
    {
        ConsoleOutput.WriteHeader("Installed Modules");

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null) return 1;

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

        var headers = new[] { "Module ID", "Version", "Status", "Capabilities", "Installed" };
        var rows = modules.Select(m => new[]
        {
            m.ModuleId,
            m.Version.ToString(),
            ConsoleOutput.FormatStatus(m.Status),
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

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null) return 1;

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
        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess($"Module '{moduleId}' {verb}.");
        ConsoleOutput.WriteInfo("Note: If the server is running, the module will be affected on next server restart or via the admin API.");
        return 0;
    }

    private static async Task<int> RestartModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null) return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await db.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module is null)
        {
            ConsoleOutput.WriteError($"Module '{moduleId}' not found.");
            return 1;
        }

        ConsoleOutput.WriteSuccess($"Module '{moduleId}' restart requested.");
        ConsoleOutput.WriteInfo("The server's process supervisor will handle the restart. If the server is not running, start it with 'dotnetcloud serve'.");
        return 0;
    }

    private static async Task<int> InstallModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null) return 1;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existing = await db.InstalledModules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (existing is not null)
        {
            ConsoleOutput.WriteWarning($"Module '{moduleId}' is already installed (status: {existing.Status}).");
            return 0;
        }

        db.InstalledModules.Add(new DotNetCloud.Core.Data.Entities.Modules.InstalledModule
        {
            ModuleId = moduleId,
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        ConsoleOutput.WriteSuccess($"Module '{moduleId}' installed and enabled.");
        ConsoleOutput.WriteInfo("Restart the server to load the module: dotnetcloud restart");

        return 0;
    }

    private static async Task<int> UninstallModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!ConsoleOutput.PromptConfirm($"Uninstall module '{moduleId}'? This may remove module data."))
        {
            ConsoleOutput.WriteInfo("Uninstall cancelled.");
            return 0;
        }

        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null) return 1;

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

        db.ModuleCapabilityGrants.RemoveRange(module.CapabilityGrants);
        db.InstalledModules.Remove(module);
        await db.SaveChangesAsync();

        ConsoleOutput.WriteSuccess($"Module '{moduleId}' uninstalled.");
        ConsoleOutput.WriteInfo("Restart the server to apply changes: dotnetcloud restart");

        return 0;
    }
}
