using System.CommandLine;
using System.Diagnostics;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Service lifecycle commands: start, stop, status, restart.
/// </summary>
internal static class ServiceCommands
{
    /// <summary>
    /// Creates the <c>start</c> command — starts all DotNetCloud services.
    /// </summary>
    public static Command CreateStart()
    {
        var command = new Command("start", "Start all DotNetCloud services");

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
        var serverInfo = FindServerExecutable();
        if (serverInfo is null)
        {
            ConsoleOutput.WriteError("Could not find DotNetCloud.Core.Server executable.");
            ConsoleOutput.WriteInfo("Ensure the server is built: dotnet build src/Core/DotNetCloud.Core.Server");
            return Task.FromResult(1);
        }

        var (serverFileName, serverArguments) = serverInfo.Value;
        ConsoleOutput.WriteInfo($"Starting server from: {serverFileName}");
        ConsoleOutput.WriteDetail("HTTP Port", config.HttpPort.ToString());
        if (config.EnableHttps)
        {
            ConsoleOutput.WriteDetail("HTTPS Port", config.HttpsPort.ToString());
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = serverFileName,
                Arguments = serverArguments ?? string.Empty,
                // On Linux, UseShellExecute = true invokes xdg-open which fails on
                // headless servers. Use false and let the child process inherit the
                // console (systemd captures stdout/stderr anyway). On Windows,
                // UseShellExecute = true detaches the child from the parent console.
                UseShellExecute = !foreground && OperatingSystem.IsWindows(),
                CreateNoWindow = !foreground,
                RedirectStandardOutput = foreground,
                RedirectStandardError = foreground,
                Environment =
                {
                    ["ConnectionStrings__DefaultConnection"] = config.ConnectionString,
                    ["Kestrel__HttpPort"] = config.HttpPort.ToString(),
                    ["Kestrel__HttpsPort"] = config.HttpsPort.ToString(),
                    ["Kestrel__EnableHttps"] = config.EnableHttps.ToString(),
                    ["DotNetCloud__AdminEmail"] = config.AdminEmail ?? ""
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

            // Send SIGTERM for graceful shutdown on Linux (lets the .NET host
            // drain connections and run IHostApplicationLifetime callbacks).
            // On Windows, CloseMainWindow is not applicable to console apps,
            // so Kill is used as a fallback.
            if (OperatingSystem.IsLinux())
            {
                Process.Start("kill", $"-TERM {pid}")?.WaitForExit(1000);
            }
            else
            {
                process.Kill(entireProcessTree: true);
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                ConsoleOutput.WriteWarning("Server did not exit gracefully, forcing kill...");
                process.Kill(entireProcessTree: true);
            }

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

    /// <summary>
    /// Locates the server executable, checking installed locations first, then
    /// development build output, and finally falling back to <c>dotnet run</c>.
    /// Returns <c>null</c> when the server cannot be found at all.
    /// </summary>
    private static (string FileName, string? Arguments)? FindServerExecutable()
    {
        // 1. Check common installed/published locations relative to the CLI binary
        var installedCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DotNetCloud.Core.Server"),
            Path.Combine(AppContext.BaseDirectory, "DotNetCloud.Core.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "server", "DotNetCloud.Core.Server"),
            Path.Combine(AppContext.BaseDirectory, "server", "DotNetCloud.Core.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "DotNetCloud.Core.Server", "DotNetCloud.Core.Server"),
            Path.Combine(AppContext.BaseDirectory, "..", "DotNetCloud.Core.Server", "DotNetCloud.Core.Server.exe"),
        };

        var found = installedCandidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return (Path.GetFullPath(found), null);
        }

        // 2. Development: find the server project and look for its build output
        var serverProject = FindServerProject();
        if (serverProject is not null)
        {
            var projectDir = Path.GetDirectoryName(serverProject)!;
            var tfm = $"net{Environment.Version.Major}.0";

            var buildOutputCandidates = new[]
            {
                Path.Combine(projectDir, "bin", "Debug", tfm, "DotNetCloud.Core.Server"),
                Path.Combine(projectDir, "bin", "Debug", tfm, "DotNetCloud.Core.Server.exe"),
                Path.Combine(projectDir, "bin", "Release", tfm, "DotNetCloud.Core.Server"),
                Path.Combine(projectDir, "bin", "Release", tfm, "DotNetCloud.Core.Server.exe"),
            };

            found = buildOutputCandidates.FirstOrDefault(File.Exists);
            if (found is not null)
            {
                return (Path.GetFullPath(found), null);
            }

            // 3. Fallback: use dotnet run (requires SDK, slower but always works)
            return ("dotnet", $"run --project \"{serverProject}\" --no-build");
        }

        return null;
    }

    /// <summary>
    /// Walks up from both the CLI binary directory and the current working directory
    /// to locate the server <c>.csproj</c> via the solution file.
    /// </summary>
    private static string? FindServerProject()
    {
        var searchRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var root in searchRoots)
        {
            var dir = new DirectoryInfo(root);
            while (dir is not null)
            {
                var slnFiles = dir.GetFiles("DotNetCloud.sln");
                if (slnFiles.Length > 0)
                {
                    var serverProj = Path.Combine(
                        dir.FullName, "src", "Core",
                        "DotNetCloud.Core.Server", "DotNetCloud.Core.Server.csproj");

                    if (File.Exists(serverProj))
                    {
                        return serverProj;
                    }
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static string GetPidFilePath()
    {
        // On Linux system installs, always use the FHS runtime directory
        // so the path matches the systemd PIDFile= directive.
        if (OperatingSystem.IsLinux() && Directory.Exists("/run"))
        {
            return "/run/dotnetcloud/dotnetcloud.pid";
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrEmpty(appData))
        {
            return Path.Combine(appData, "dotnetcloud", "dotnetcloud.pid");
        }

        return Path.Combine(Path.GetTempPath(), "dotnetcloud", "dotnetcloud.pid");
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
