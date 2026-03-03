using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Component status and restart commands.
/// Components are the major subsystems: database, server, modules, signalr, grpc.
/// </summary>
internal static class ComponentCommands
{
    private static readonly string[] KnownComponents =
    [
        "database",
        "server",
        "modules",
        "signalr",
        "grpc"
    ];

    /// <summary>
    /// Creates the <c>component</c> parent command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("component", "Manage DotNetCloud components");
        command.Subcommands.Add(CreateStatus());
        command.Subcommands.Add(CreateRestart());
        return command;
    }

    private static Command CreateStatus()
    {
        var componentArg = new Argument<string>("component")
        {
            Description = "Component name (database, server, modules, signalr, grpc)"
        };
        var command = new Command("status", "Check component status") { componentArg };
        command.SetAction(parseResult =>
        {
            var component = parseResult.GetValue(componentArg)!;
            return ComponentStatusAsync(component);
        });
        return command;
    }

    private static Command CreateRestart()
    {
        var componentArg = new Argument<string>("component")
        {
            Description = "Component name to restart"
        };
        var command = new Command("restart", "Restart a component") { componentArg };
        command.SetAction(parseResult =>
        {
            var component = parseResult.GetValue(componentArg)!;
            return ComponentRestartAsync(component);
        });
        return command;
    }

    private static async Task<int> ComponentStatusAsync(string component)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        var normalized = component.ToLowerInvariant();
        if (!KnownComponents.Contains(normalized))
        {
            ConsoleOutput.WriteError($"Unknown component '{component}'. Known components: {string.Join(", ", KnownComponents)}");
            return 1;
        }

        ConsoleOutput.WriteHeader($"Component Status: {normalized}");

        switch (normalized)
        {
            case "database":
                return await CheckDatabaseStatusAsync();
            case "server":
                return CheckServerStatus();
            case "modules":
                return await CheckModulesStatusAsync();
            case "signalr":
                ConsoleOutput.WriteDetail("SignalR", "Status depends on server process. Use 'dotnetcloud status' for overall status.");
                return 0;
            case "grpc":
                ConsoleOutput.WriteDetail("gRPC", "Status depends on server process. Use 'dotnetcloud status' for overall status.");
                return 0;
            default:
                return 1;
        }
    }

    private static Task<int> ComponentRestartAsync(string component)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        var normalized = component.ToLowerInvariant();
        if (!KnownComponents.Contains(normalized))
        {
            ConsoleOutput.WriteError($"Unknown component '{component}'. Known components: {string.Join(", ", KnownComponents)}");
            return Task.FromResult(1);
        }

        // Components are subsystems of the server process — a full restart is needed
        ConsoleOutput.WriteInfo($"Component '{normalized}' is part of the server process.");
        ConsoleOutput.WriteInfo("Use 'dotnetcloud restart' to restart the entire server.");
        return Task.FromResult(0);
    }

    private static async Task<int> CheckDatabaseStatusAsync()
    {
        try
        {
            await using var provider = ServiceProviderFactory.CreateFromConfig();
            if (provider is null) return 1;

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
            {
                ConsoleOutput.WriteDetail("Database", ConsoleOutput.FormatStatus("Healthy"));
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                var pending = pendingMigrations.ToList();
                if (pending.Count > 0)
                {
                    ConsoleOutput.WriteWarning($"{pending.Count} pending migration(s).");
                }
                else
                {
                    ConsoleOutput.WriteSuccess("All migrations applied.");
                }
            }
            else
            {
                ConsoleOutput.WriteDetail("Database", ConsoleOutput.FormatStatus("Unhealthy"));
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Database check failed: {ex.Message}");
            return 1;
        }
    }

    private static int CheckServerStatus()
    {
        var pidFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "dotnetcloud.pid");

        if (!File.Exists(pidFile))
        {
            ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Stopped"));
            return 0;
        }

        if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Running"));
                    ConsoleOutput.WriteDetail("PID", pid.ToString());
                    return 0;
                }
            }
            catch (ArgumentException) { }
        }

        ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Stopped"));
        return 0;
    }

    private static async Task<int> CheckModulesStatusAsync()
    {
        try
        {
            await using var provider = ServiceProviderFactory.CreateFromConfig();
            if (provider is null) return 1;

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var modules = await db.InstalledModules
                .AsNoTracking()
                .OrderBy(m => m.ModuleId)
                .ToListAsync();

            if (modules.Count == 0)
            {
                ConsoleOutput.WriteInfo("No modules installed.");
                return 0;
            }

            var headers = new[] { "Module ID", "Status", "Version" };
            var rows = modules.Select(m => new[]
            {
                m.ModuleId,
                ConsoleOutput.FormatStatus(m.Status),
                m.Version.ToString()
            }).ToList();

            ConsoleOutput.WriteTable(headers, rows);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Module check failed: {ex.Message}");
            return 1;
        }
    }
}
