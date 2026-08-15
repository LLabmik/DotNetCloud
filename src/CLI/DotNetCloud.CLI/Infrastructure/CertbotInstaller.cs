using System.Diagnostics;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Detects and installs the certbot Let's Encrypt client.
/// certbot handles certificate issuance and automatic renewal on Linux.
/// On Windows it is unavailable (the EFF discontinued Windows support in 2024),
/// so the built-in ACME client continues to be used there.
/// </summary>
internal static class CertbotInstaller
{
    /// <summary>
    /// Returns <c>true</c> if the <c>certbot</c> executable is already available.
    /// </summary>
    public static bool IsInstalled()
    {
        return IsCommandAvailable("certbot")
            || File.Exists("/usr/bin/certbot")
            || File.Exists("/usr/local/bin/certbot")
            || File.Exists("/snap/bin/certbot");
    }

    /// <summary>
    /// Ensures certbot is available for Let's Encrypt certificate management.
    /// On Linux it installs certbot via apt-get when missing. On Windows it is a
    /// no-op because certbot is no longer supported there. The return value does
    /// not block certificate provisioning — the built-in ACME client is always
    /// available as a fallback.
    /// </summary>
    public static bool EnsureInstalled()
    {
        if (OperatingSystem.IsWindows())
        {
            ConsoleOutput.WriteInfo("certbot is not supported on Windows — using the built-in Let's Encrypt client.");
            return true;
        }

        if (!OperatingSystem.IsLinux())
        {
            ConsoleOutput.WriteWarning("certbot installation is not supported on this platform.");
            ConsoleOutput.WriteInfo("Install certbot manually if you want automated Let's Encrypt renewal.");
            return false;
        }

        if (IsInstalled())
        {
            ConsoleOutput.WriteInfo("certbot is already installed.");
            return true;
        }

        return InstallOnLinux();
    }

    private static bool InstallOnLinux()
    {
        if (!IsCommandAvailable("apt-get"))
        {
            ConsoleOutput.WriteWarning("apt-get was not found. Install certbot manually for automatic Let's Encrypt renewal.");
            return false;
        }

        ConsoleOutput.WriteInfo("Installing certbot (Let's Encrypt client)...");

        if (!RunCommand("apt-get", "update -qq"))
        {
            ConsoleOutput.WriteError("Failed to update the package list.");
            return false;
        }

        if (!RunCommand("apt-get", "install -y -qq certbot"))
        {
            ConsoleOutput.WriteError("Failed to install certbot.");
            return false;
        }

        ConsoleOutput.WriteSuccess("certbot installed.");
        return true;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("which", command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
