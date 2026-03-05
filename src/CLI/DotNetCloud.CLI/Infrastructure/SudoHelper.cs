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
    /// If not running as root on Linux, re-launches the current command under <c>sudo</c>,
    /// waits for it to finish, and returns the exit code. Returns <c>null</c> if already root
    /// or on a non-Linux platform (caller should proceed normally).
    /// </summary>
    public static int? ReExecWithSudo(string[] args)
    {
        if (IsRunningAsRoot())
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

        psi.ArgumentList.Add(exe);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
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

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();
}
