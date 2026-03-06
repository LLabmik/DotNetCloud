using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// CLI command for installing Collabora CODE (the built-in document editor server).
/// On Linux (Debian-based) this adds the official Collabora APT repository and installs
/// the <c>coolwsd</c> and <c>code-brand</c> packages.
/// Invoked as: <c>dotnetcloud install collabora</c>
/// </summary>
internal static class CollaboraInstallCommand
{
    private const string AptKeyUrl = "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0xD8915E456E7C440E";
    private const string AptKeyringPath = "/usr/share/keyrings/collaboraonline-release-keyring.gpg";
    private const string AptSourcePath = "/etc/apt/sources.list.d/collaboraonline.sources";

    /// <summary>
    /// Creates the <c>install collabora</c> command.
    /// </summary>
    public static Command Create()
    {
        var installCommand = new Command("install", "Install optional components");

        var collaboraCommand = new Command("collabora", "Install the built-in Collabora CODE document server");

        var forceOption = new Option<bool>("--force")
        {
            Description = "Reinstall even if already installed"
        };
        collaboraCommand.Options.Add(forceOption);

        collaboraCommand.SetAction(async (parseResult, ct) =>
        {
            var force = parseResult.GetValue(forceOption);
            return await RunAsync(force, ct);
        });

        installCommand.Subcommands.Add(collaboraCommand);
        return installCommand;
    }

    private static async Task<int> RunAsync(bool force, CancellationToken ct)
    {
        ConsoleOutput.WriteHeader("Collabora CODE Installation");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ConsoleOutput.WriteError("Automatic Collabora CODE installation is only supported on Linux.");
            ConsoleOutput.WriteInfo("On Windows, use Docker or install Collabora Online manually.");
            ConsoleOutput.WriteInfo("See: https://www.collaboraoffice.com/code/");
            return 1;
        }

        // Verify we have apt-get (Debian-based)
        if (!CommandExists("apt-get"))
        {
            ConsoleOutput.WriteError("This installer requires a Debian-based system with apt-get.");
            ConsoleOutput.WriteInfo("For other distributions, install Collabora CODE manually.");
            ConsoleOutput.WriteInfo("See: https://www.collaboraoffice.com/code/linux-packages/");
            return 1;
        }

        var alreadyInstalled = IsCollaboraInstalled();

        try
        {
            // Ensure the APT repository is configured
            if (!File.Exists(AptSourcePath))
            {
                ConsoleOutput.WriteInfo("Importing Collabora signing key...");
                var keyResult = await RunShellAsync(
                    $"curl -fsSL \"{AptKeyUrl}\" | gpg --dearmor -o {AptKeyringPath}", ct);

                if (keyResult != 0)
                {
                    ConsoleOutput.WriteError("Failed to import the Collabora signing key.");
                    return 1;
                }
                ConsoleOutput.WriteSuccess("Signing key imported.");

                ConsoleOutput.WriteInfo("Adding Collabora CODE repository...");
                var sourcesContent =
                    $"""
                    Types: deb
                    URIs: https://www.collaboraoffice.com/repos/CollaboraOnline/CODE-deb
                    Suites: ./
                    Signed-By: {AptKeyringPath}
                    """;

                await File.WriteAllTextAsync(AptSourcePath, sourcesContent, ct);
                ConsoleOutput.WriteSuccess("APT repository added.");
            }

            // Get current version before install/upgrade
            string? beforeVersion = null;
            if (alreadyInstalled)
            {
                beforeVersion = await GetPackageVersionAsync("coolwsd", ct);
            }

            // Update package lists
            ConsoleOutput.WriteInfo("Updating package lists...");
            var updateResult = await RunProcessAsync("apt-get", ["update", "-qq"], ct);
            if (updateResult != 0)
            {
                ConsoleOutput.WriteError("Failed to update package lists.");
                return 1;
            }

            // Install/upgrade Collabora CODE packages (apt-get install is idempotent)
            ConsoleOutput.WriteInfo(alreadyInstalled
                ? "Checking for Collabora CODE updates..."
                : "Installing coolwsd and code-brand packages...");

            var installResult = await RunProcessAsync(
                "apt-get", ["install", "-y", "-qq", "coolwsd", "code-brand"], ct);

            if (installResult != 0)
            {
                ConsoleOutput.WriteError("Failed to install Collabora CODE packages.");
                return 1;
            }

            var afterVersion = await GetPackageVersionAsync("coolwsd", ct);

            if (beforeVersion is null)
            {
                ConsoleOutput.WriteSuccess($"Collabora CODE v{afterVersion} installed.");
            }
            else if (beforeVersion == afterVersion && !force)
            {
                ConsoleOutput.WriteSuccess($"Collabora CODE v{afterVersion} is already the latest version.");
            }
            else
            {
                ConsoleOutput.WriteSuccess($"Collabora CODE upgraded: v{beforeVersion} → v{afterVersion}");
            }

            // Keep the OS service enabled so Collabora auto-starts after host reboots.
            await RunProcessAsync("systemctl", ["enable", "coolwsd"], ct);
            await RunProcessAsync("systemctl", ["start", "coolwsd"], ct);

            // Persist the mode in config
            var config = CliConfiguration.Load();
            config.CollaboraMode = "BuiltIn";
            CliConfiguration.Save(config);

            Console.WriteLine();
            ConsoleOutput.WriteInfo("Restart the server to activate: dotnetcloud serve");

            return 0;
        }
        catch (OperationCanceledException)
        {
            ConsoleOutput.WriteWarning("Installation cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Installation failed: {ex.Message}");
            return 1;
        }
    }

    private static bool IsCollaboraInstalled()
    {
        // Check if the coolwsd binary exists in typical locations
        return File.Exists("/usr/bin/coolwsd")
            || CommandExists("coolwsd");
    }

    private static async Task<string?> GetPackageVersionAsync(string packageName, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("dpkg-query")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-W");
            psi.ArgumentList.Add("-f=${Version}");
            psi.ArgumentList.Add(packageName);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var version = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 ? version.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("which", command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int> RunProcessAsync(string fileName, string[] arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return 1;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    private static async Task<int> RunShellAsync(string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("/bin/bash")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        using var process = Process.Start(psi);
        if (process is null)
        {
            return 1;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }
}
