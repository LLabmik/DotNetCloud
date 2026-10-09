using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Detects whether the process is running with root/admin privileges on Linux
/// and re-executes with <c>sudo</c> when necessary.
/// </summary>
internal static class SudoHelper
{
    /// <summary>
    /// Returns <c>true</c> when the process is running as root (UID 0) on Linux.
    /// Always returns <c>true</c> on non-Linux platforms (no elevation needed from the CLI).
    /// </summary>
    public static bool IsRunningAsRoot()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return true;
        }

        return geteuid() == 0;
    }

    /// <summary>
    /// Returns <c>true</c> when the process was started by systemd as a service unit.
    /// systemd sets <c>INVOCATION_ID</c> for every service it manages.
    /// </summary>
    public static bool IsRunningUnderSystemd()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INVOCATION_ID"));
    }

    /// <summary>
    /// If not running as root on Linux, re-launches the current command under <c>sudo</c>,
    /// waits for it to finish, and returns the exit code. Returns <c>null</c> if already root,
    /// on a non-Linux platform, or running under systemd (caller should proceed normally).
    /// </summary>
    public static int? ReExecWithSudo(string[] args)
    {
        if (IsRunningAsRoot())
        {
            return null;
        }

        // When running as a systemd service, NoNewPrivileges=true prevents sudo.
        // The service unit is responsible for running with the correct user/permissions.
        if (IsRunningUnderSystemd())
        {
            return null;
        }

        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (exe is null)
        {
            ConsoleOutput.WriteError("Could not determine the executable path for sudo re-execution.");
            return 1;
        }

        ConsoleOutput.WriteInfo("Root privileges required. Re-running with sudo...");
        Console.WriteLine();

        var psi = new ProcessStartInfo("sudo")
        {
            UseShellExecute = false
        };

        foreach (var argument in BuildSudoArguments(
            exe, CliConfiguration.GetConfigDirectory(), args, GetEntryAssemblyArgument(exe)))
        {
            psi.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                ConsoleOutput.WriteError("Failed to start sudo process.");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to elevate with sudo: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Builds the arguments for the elevated process: the executable, then <c>--config-dir</c> with the
    /// directory this process resolved, then the original arguments.
    /// </summary>
    /// <remarks>
    /// <c>sudo</c> resets the environment by default, so the elevated process would not see
    /// <c>DOTNETCLOUD_CONFIG_DIR</c> and would silently fall back to <c>/etc/dotnetcloud</c> — running
    /// against a different configuration than the caller asked for. Passing the resolved directory on
    /// the command line avoids depending on sudo's environment policy (and on a site's sudoers rules)
    /// altogether. A <c>--config-dir</c> already present in the arguments is left alone so the option is
    /// never duplicated.
    /// </remarks>
    /// <param name="executable">Executable to run as root.</param>
    /// <param name="configDirectory">Configuration directory the caller resolved.</param>
    /// <param name="args">Original command-line arguments.</param>
    /// <param name="entryAssembly">
    /// Entry assembly to repeat on the elevated command line, when the CLI was launched through the
    /// <c>dotnet</c> muxer (see <see cref="GetEntryAssemblyArgument"/>).
    /// </param>
    internal static IReadOnlyList<string> BuildSudoArguments(
        string executable,
        string configDirectory,
        IReadOnlyList<string> args,
        string? entryAssembly = null)
    {
        var arguments = new List<string>(args.Count + 4) { executable };

        if (!string.IsNullOrWhiteSpace(entryAssembly))
        {
            arguments.Add(entryAssembly);
        }

        var hasConfigDir = args.Any(a =>
            a.Equals("--config-dir", StringComparison.OrdinalIgnoreCase)
            || a.StartsWith("--config-dir=", StringComparison.OrdinalIgnoreCase));

        if (!hasConfigDir && !string.IsNullOrWhiteSpace(configDirectory))
        {
            arguments.Add("--config-dir");
            arguments.Add(configDirectory);
        }

        arguments.AddRange(args);
        return arguments;
    }

    /// <summary>
    /// Returns the entry assembly to repeat on the elevated command line when the CLI was started through
    /// the <c>dotnet</c> muxer (<c>dotnet dotnetcloud.dll backup</c>), where
    /// <see cref="Environment.ProcessPath"/> is the muxer itself and the assembly is passed as an argument.
    /// Without this, the re-exec would run <c>dotnet backup</c>, which the muxer cannot resolve.
    /// </summary>
    /// <remarks>
    /// A system install uses the published apphost (<c>/usr/local/bin/dotnetcloud</c>), which carries the
    /// assembly itself, so no extra argument is needed there.
    /// </remarks>
    /// <param name="executablePath">Path of the running process.</param>
    /// <returns>The absolute assembly path, or <see langword="null"/> when it is not a muxer launch.</returns>
    internal static string? GetEntryAssemblyArgument(string executablePath)
    {
        if (!Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Length == 0
            || !commandLine[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFullPath(commandLine[0]);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();
}
