using System.CommandLine;
using System.IO.Compression;
using System.Runtime.InteropServices;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// CLI command for downloading and installing Collabora CODE (the built-in document editor server).
/// Invoked as: <c>dotnetcloud install collabora</c>
/// </summary>
internal static class CollaboraInstallCommand
{
    // Collabora CODE download URLs by platform. These point to the official Collabora release page.
    // In production, the actual URLs should come from a manifest or configuration to stay up to date.
    private static readonly Dictionary<string, string> DownloadUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["linux-x64"] = "https://github.com/CollaboraOnline/online/releases/latest/download/collaboraoffice-linux-x64.tar.gz",
        ["linux-arm64"] = "https://github.com/CollaboraOnline/online/releases/latest/download/collaboraoffice-linux-arm64.tar.gz",
        ["win-x64"] = "https://www.collaboraoffice.com/wp-content/uploads/CODE-Windows-x64.zip"
    };

    /// <summary>
    /// Creates the <c>install collabora</c> command.
    /// </summary>
    public static Command Create()
    {
        var installCommand = new Command("install", "Install optional components");

        var collaboraCommand = new Command("collabora", "Download and install the built-in Collabora CODE document server");

        var dirOption = new Option<string?>("--dir")
        {
            Description = "Installation directory (overrides config)"
        };
        collaboraCommand.Options.Add(dirOption);

        var forceOption = new Option<bool>("--force")
        {
            Description = "Reinstall even if already installed"
        };
        collaboraCommand.Options.Add(forceOption);

        collaboraCommand.SetAction(async (parseResult, ct) =>
        {
            var dir = parseResult.GetValue(dirOption);
            var force = parseResult.GetValue(forceOption);
            return await RunAsync(dir, force, ct);
        });

        installCommand.Subcommands.Add(collaboraCommand);
        return installCommand;
    }

    private static async Task<int> RunAsync(string? installDir, bool force, CancellationToken ct)
    {
        ConsoleOutput.WriteHeader("Collabora CODE Installation");

        var config = CliConfiguration.Load();

        var targetDir = installDir
            ?? (string.IsNullOrWhiteSpace(config.CollaboraDirectory) ? null : config.CollaboraDirectory);

        if (targetDir is null)
        {
            targetDir = ConsoleOutput.Prompt(
                "Installation directory",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "dotnetcloud", "collabora"));
        }

        var platform = DetectPlatform();
        if (platform is null)
        {
            ConsoleOutput.WriteError("Unsupported platform. Collabora CODE auto-install supports Linux x64/arm64 and Windows x64.");
            ConsoleOutput.WriteInfo("Please install Collabora Online manually and set the URL in the admin settings.");
            return 1;
        }

        ConsoleOutput.WriteDetail("Platform", platform);
        ConsoleOutput.WriteDetail("Install directory", targetDir);

        // Check existing installation
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "coolwsd.exe" : "coolwsd";
        var candidatePaths = new[]
        {
            Path.Combine(targetDir, executableName),
            Path.Combine(targetDir, "bin", executableName)
        };

        var existing = candidatePaths.FirstOrDefault(File.Exists);
        if (existing is not null && !force)
        {
            ConsoleOutput.WriteSuccess($"Collabora CODE is already installed at: {existing}");
            ConsoleOutput.WriteInfo("Use --force to reinstall.");
            return 0;
        }

        if (!DownloadUrls.TryGetValue(platform, out var downloadUrl))
        {
            ConsoleOutput.WriteError($"No download URL configured for platform '{platform}'.");
            return 1;
        }

        ConsoleOutput.WriteInfo($"Downloading Collabora CODE from: {downloadUrl}");

        Directory.CreateDirectory(targetDir);

        var archiveName = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? "collabora.zip" : "collabora.tar.gz";
        var archivePath = Path.Combine(Path.GetTempPath(), archiveName);

        try
        {
            await DownloadFileAsync(downloadUrl, archivePath, ct);
            ConsoleOutput.WriteSuccess("Download complete.");

            ConsoleOutput.WriteInfo("Extracting...");
            await ExtractArchiveAsync(archivePath, targetDir, ct);
            ConsoleOutput.WriteSuccess($"Extracted to: {targetDir}");
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
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }

        // Verify installation
        var installed = candidatePaths.FirstOrDefault(File.Exists);
        if (installed is null)
        {
            ConsoleOutput.WriteWarning("Could not locate coolwsd executable after extraction.");
            ConsoleOutput.WriteInfo("You may need to set CollaboraExecutablePath manually in your configuration.");
        }
        else
        {
            ConsoleOutput.WriteSuccess($"Collabora CODE installed: {installed}");
        }

        // Persist the installation directory in config
        config.CollaboraMode = "BuiltIn";
        config.CollaboraDirectory = targetDir;
        CliConfiguration.Save(config);
        ConsoleOutput.WriteSuccess("Configuration updated.");

        Console.WriteLine();
        ConsoleOutput.WriteInfo("To start using Collabora, set the WOPI base URL and restart with: dotnetcloud serve");

        return 0;
    }

    private static async Task DownloadFileAsync(string url, string destination, CancellationToken ct)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var httpStream = await response.Content.ReadAsStreamAsync(ct);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;

            if (total.HasValue)
            {
                var pct = (int)(downloaded * 100 / total.Value);
                Console.Write($"\r  Downloading... {pct}% ({downloaded / 1024 / 1024} MB / {total.Value / 1024 / 1024} MB)");
            }
        }

        Console.WriteLine();
    }

    private static Task ExtractArchiveAsync(string archivePath, string destinationDir, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDir, overwriteFiles: true);
            }
            else
            {
                // For .tar.gz on Linux, use the system tar command
                // On Windows this path shouldn't be reached (we use .zip)
                var psi = new System.Diagnostics.ProcessStartInfo("tar")
                {
                    ArgumentList = { "-xzf", archivePath, "-C", destinationDir, "--strip-components=1" },
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();
            }
        }, ct);
    }

    private static string? DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64" : "linux-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "win-x64";
        }

        return null;
    }
}
