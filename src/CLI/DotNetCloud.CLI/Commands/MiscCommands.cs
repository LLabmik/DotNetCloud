using System.CommandLine;
using System.Reflection;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Miscellaneous commands: update check and help reference.
/// </summary>
internal static class MiscCommands
{
    /// <summary>
    /// Creates the <c>update</c> command — checks for and applies updates.
    /// </summary>
    public static Command CreateUpdate()
    {
        var command = new Command("update", "Check for and apply DotNetCloud updates");
        var checkOnlyOption = new Option<bool>("--check")
        {
            Description = "Only check for updates without applying",
            DefaultValueFactory = _ => false
        };
        command.Options.Add(checkOnlyOption);

        command.SetAction(parseResult =>
        {
            var checkOnly = parseResult.GetValue(checkOnlyOption);
            return CheckUpdateAsync(checkOnly);
        });

        return command;
    }

    /// <summary>
    /// Creates the <c>version</c> command — shows version information.
    /// </summary>
    public static Command CreateVersion()
    {
        var command = new Command("version", "Show DotNetCloud version information");
        command.SetAction(_ =>
        {
            ShowVersion();
            return Task.FromResult(0);
        });
        return command;
    }

    private static Task<int> CheckUpdateAsync(bool checkOnly)
    {
        ConsoleOutput.WriteHeader("DotNetCloud Updates");

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        ConsoleOutput.WriteDetail("Current Version", currentVersion.ToString(3));

        // In a future phase, this will query a remote server for available updates.
        // For now, report that the system is up to date.
        ConsoleOutput.WriteInfo("Update checking is not yet implemented.");
        ConsoleOutput.WriteInfo("To update manually:");
        ConsoleOutput.WriteInfo("  1. Stop the server: dotnetcloud stop");
        ConsoleOutput.WriteInfo("  2. Update the installation files");
        ConsoleOutput.WriteInfo("  3. Run database migrations if needed");
        ConsoleOutput.WriteInfo("  4. Start the server: dotnetcloud serve");

        return Task.FromResult(0);
    }

    private static void ShowVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(0, 0, 0);
        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        ConsoleOutput.WriteHeader("DotNetCloud");
        ConsoleOutput.WriteDetail("Version", version.ToString(3));
        ConsoleOutput.WriteDetail("Runtime", runtime);
        ConsoleOutput.WriteDetail("OS", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        ConsoleOutput.WriteDetail("Architecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString());

        if (CliConfiguration.ConfigExists())
        {
            var config = CliConfiguration.Load();
            ConsoleOutput.WriteDetail("Database", config.DatabaseProvider);
            ConsoleOutput.WriteDetail("Config", CliConfiguration.GetConfigFilePath());
            if (config.SetupCompletedAt.HasValue)
            {
                ConsoleOutput.WriteDetail("Setup Date", config.SetupCompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            }
        }
    }
}
