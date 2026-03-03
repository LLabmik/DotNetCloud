using System.CommandLine;
using System.Diagnostics;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Service lifecycle commands: serve, stop, status, restart.
/// </summary>
internal static class ServiceCommands
{
    /// <summary>
    /// Creates the <c>serve</c> command — starts all DotNetCloud services.
    /// </summary>
    public static Command CreateServe()
    {
        var command = new Command("serve", "Start all DotNetCloud services");

        var foregroundOption = new Option<bool>("--foreground")
        {
            Description = "Run in foreground (don't detach)",
            DefaultValueFactory = _ => false
        };
        command.Options.Add(foregroundOption);

        command.SetAction(parseResult =>
        {
            var foreground = parseResult.GetValue(foregroundOption);
            return ServeAsync(foreground);
        });

        return command;
    }

    /// <summary>
    /// Creates the <c>stop</c> command — gracefully stops all services.
    /// </summary>
    public static Command CreateStop()
    {
        var command = new Command("stop", "Gracefully stop all DotNetCloud services");
        command.SetAction(_ => StopAsync());
        return command;
    }

    /// <summary>
    /// Creates the <c>status</c> command — shows service and module status.
    /// </summary>
    public static Command CreateStatus()
    {
        var command = new Command("status", "Show service and module status");
        command.SetAction(_ => StatusAsync());
        return command;
    }

    /// <summary>
    /// Creates the <c>restart</c> command — restarts all services.
    /// </summary>
    public static Command CreateRestart()
    {
        var command = new Command("restart", "Restart all DotNetCloud services");
        command.SetAction(_ => RestartAsync());
        return command;
    }

    private static Task<int> ServeAsync(bool foreground)
    {
        if (!CliConfiguration.ConfigExists())
        {
            ConsoleOutput.WriteError("DotNetCloud is not configured. Run 'dotnetcloud setup' first.");
            return Task.FromResult(1);
        }

        var config = CliConfiguration.Load();
        ConsoleOutput.WriteHeader("DotNetCloud Server");

        // Find the server executable
        var serverPath = FindServerExecutable();
        if (serverPath is null)
        {
            ConsoleOutput.WriteError("Could not find DotNetCloud.Core.Server executable.");
            ConsoleOutput.WriteInfo("Ensure the server is built: dotnet build src/Core/DotNetCloud.Core.Server");
            return Task.FromResult(1);
        }

        ConsoleOutput.WriteInfo($"Starting server from: {serverPath}");
        ConsoleOutput.WriteDetail("HTTP Port", config.HttpPort.ToString());
        if (config.EnableHttps)
        {
            ConsoleOutput.WriteDetail("HTTPS Port", config.HttpsPort.ToString());
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = serverPath,
                UseShellExecute = !foreground,
                RedirectStandardOutput = foreground,
                RedirectStandardError = foreground,
                Environment =
                {
                    ["ConnectionStrings__DefaultConnection"] = config.ConnectionString,
                    ["Kestrel__HttpPort"] = config.HttpPort.ToString(),
                    ["Kestrel__HttpsPort"] = config.HttpsPort.ToString(),
                    ["Kestrel__EnableHttps"] = config.EnableHttps.ToString()
                }
            };

            var process = Process.Start(psi);
            if (process is null)
            {
                ConsoleOutput.WriteError("Failed to start server process.");
                return Task.FromResult(1);
            }

            // Write PID file for stop/status commands
            var pidFile = GetPidFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
            File.WriteAllText(pidFile, process.Id.ToString());

            ConsoleOutput.WriteSuccess($"Server started (PID: {process.Id})");

            if (foreground)
            {
                ConsoleOutput.WriteInfo("Press Ctrl+C to stop...");
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    ConsoleOutput.WriteInfo("Shutting down...");
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                };
                process.WaitForExit();
                ConsoleOutput.WriteInfo($"Server exited with code {process.ExitCode}.");
                CleanupPidFile();
                return Task.FromResult(process.ExitCode);
            }

            ConsoleOutput.WriteInfo("Server running in background. Use 'dotnetcloud status' to check.");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to start server: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static Task<int> StopAsync()
    {
        ConsoleOutput.WriteHeader("Stopping DotNetCloud");

        var pidFile = GetPidFilePath();
        if (!File.Exists(pidFile))
        {
            ConsoleOutput.WriteWarning("No PID file found. Server may not be running.");
            return Task.FromResult(0);
        }

        if (!int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
        {
            ConsoleOutput.WriteError("Invalid PID file.");
            CleanupPidFile();
            return Task.FromResult(1);
        }

        try
        {
            var process = Process.GetProcessById(pid);
            ConsoleOutput.WriteInfo($"Stopping server (PID: {pid})...");
            process.Kill(entireProcessTree: true);
            process.WaitForExit(TimeSpan.FromSeconds(30));
            ConsoleOutput.WriteSuccess("Server stopped.");
        }
        catch (ArgumentException)
        {
            ConsoleOutput.WriteWarning($"Process {pid} is not running.");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to stop server: {ex.Message}");
            return Task.FromResult(1);
        }
        finally
        {
            CleanupPidFile();
        }

        return Task.FromResult(0);
    }

    private static Task<int> StatusAsync()
    {
        ConsoleOutput.WriteHeader("DotNetCloud Status");

        // Check server process
        var pidFile = GetPidFilePath();
        if (!File.Exists(pidFile))
        {
            ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Stopped"));
            ConsoleOutput.WriteInfo("Use 'dotnetcloud serve' to start the server.");
            return Task.FromResult(0);
        }

        if (!int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
        {
            ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Error"));
            ConsoleOutput.WriteError("Invalid PID file.");
            return Task.FromResult(1);
        }

        try
        {
            var process = Process.GetProcessById(pid);
            if (process.HasExited)
            {
                ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Stopped"));
                CleanupPidFile();
            }
            else
            {
                ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Running"));
                ConsoleOutput.WriteDetail("PID", pid.ToString());
                ConsoleOutput.WriteDetail("Memory", $"{process.WorkingSet64 / 1024 / 1024} MB");
                ConsoleOutput.WriteDetail("Uptime", (DateTime.Now - process.StartTime).ToString(@"d\.hh\:mm\:ss"));
            }
        }
        catch (ArgumentException)
        {
            ConsoleOutput.WriteDetail("Server", ConsoleOutput.FormatStatus("Stopped"));
            CleanupPidFile();
        }

        // Show configuration
        if (CliConfiguration.ConfigExists())
        {
            var config = CliConfiguration.Load();
            Console.WriteLine();
            ConsoleOutput.WriteDetail("Database", config.DatabaseProvider);
            ConsoleOutput.WriteDetail("HTTP Port", config.HttpPort.ToString());
            if (config.EnableHttps)
            {
                ConsoleOutput.WriteDetail("HTTPS Port", config.HttpsPort.ToString());
            }
            if (config.EnabledModules.Count > 0)
            {
                ConsoleOutput.WriteDetail("Modules", string.Join(", ", config.EnabledModules));
            }
        }
        else
        {
            ConsoleOutput.WriteWarning("No configuration found. Run 'dotnetcloud setup' first.");
        }

        return Task.FromResult(0);
    }

    private static async Task<int> RestartAsync()
    {
        ConsoleOutput.WriteHeader("Restarting DotNetCloud");

        var stopResult = await StopAsync();
        if (stopResult != 0)
        {
            ConsoleOutput.WriteWarning("Stop returned non-zero, attempting start anyway...");
        }

        // Brief delay between stop and start
        await Task.Delay(1000);

        return await ServeAsync(foreground: false);
    }

    private static string? FindServerExecutable()
    {
        // Look for the server in common locations
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DotNetCloud.Core.Server"),
            Path.Combine(AppContext.BaseDirectory, "DotNetCloud.Core.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "DotNetCloud.Core.Server", "DotNetCloud.Core.Server"),
            Path.Combine(AppContext.BaseDirectory, "..", "DotNetCloud.Core.Server", "DotNetCloud.Core.Server.exe"),
        };

        // Also check via dotnet run
        var serverProject = FindServerProject();
        if (serverProject is not null)
        {
            return $"dotnet run --project \"{serverProject}\" --no-build";
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindServerProject()
    {
        // Walk up from the CLI executable to find the solution directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var slnFiles = dir.GetFiles("DotNetCloud.sln");
            if (slnFiles.Length > 0)
            {
                var serverProj = Path.Combine(dir.FullName, "src", "Core", "DotNetCloud.Core.Server", "DotNetCloud.Core.Server.csproj");
                if (File.Exists(serverProj))
                {
                    return serverProj;
                }
            }
            dir = dir.Parent;
        }

        return null;
    }

    private static string GetPidFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "dotnetcloud.pid");
    }

    private static void CleanupPidFile()
    {
        var pidFile = GetPidFilePath();
        if (File.Exists(pidFile))
        {
            try { File.Delete(pidFile); }
            catch { /* best effort */ }
        }
    }
}
