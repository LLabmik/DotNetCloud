using System.CommandLine;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.DTOs;

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

    private static async Task<int> CheckUpdateAsync(bool checkOnly)
    {
        ConsoleOutput.WriteHeader("DotNetCloud Updates");

        var currentVersion = GetCurrentVersion();
        ConsoleOutput.WriteDetail("Current Version", currentVersion);

        try
        {
            ConsoleOutput.WriteInfo("Checking for updates...");

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetCloud-CLI/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            http.Timeout = TimeSpan.FromSeconds(15);

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var releases = await http.GetFromJsonAsync<List<GitHubReleaseDto>>(
                "https://api.github.com/repos/LLabmik/DotNetCloud/releases", options);

            if (releases is null || releases.Count == 0)
            {
                ConsoleOutput.WriteInfo("No releases found.");
                return 0;
            }

            var latest = releases.FirstOrDefault(r => !r.Draft);
            if (latest is null)
            {
                ConsoleOutput.WriteInfo("No published releases found.");
                return 0;
            }

            var latestVersion = latest.TagName.StartsWith('v') ? latest.TagName[1..] : latest.TagName;
            ConsoleOutput.WriteDetail("Latest Version", latestVersion);

            if (IsNewerVersion(currentVersion, latestVersion))
            {
                Console.WriteLine();
                ConsoleOutput.WriteWarning($"Update available: {currentVersion} → {latestVersion}");

                if (latest.PublishedAt.HasValue)
                {
                    ConsoleOutput.WriteDetail("Released", latest.PublishedAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                }

                if (!string.IsNullOrWhiteSpace(latest.Body))
                {
                    Console.WriteLine();
                    ConsoleOutput.WriteInfo("Release Notes:");
                    // Show first 10 lines of release notes
                    var lines = latest.Body.Split('\n');
                    foreach (var line in lines.Take(10))
                    {
                        Console.WriteLine($"    {line.TrimEnd()}");
                    }

                    if (lines.Length > 10)
                    {
                        Console.WriteLine($"    ... ({lines.Length - 10} more lines)");
                    }
                }

                if (!string.IsNullOrEmpty(latest.HtmlUrl))
                {
                    Console.WriteLine();
                    ConsoleOutput.WriteDetail("Release URL", latest.HtmlUrl);
                }

                // List platform assets
                var platformAssets = latest.Assets
                    .Where(a => !a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (platformAssets.Count > 0)
                {
                    Console.WriteLine();
                    ConsoleOutput.WriteInfo("Available downloads:");
                    foreach (var asset in platformAssets)
                    {
                        var size = asset.Size > 1024 * 1024
                            ? $"{asset.Size / (1024.0 * 1024.0):F1} MB"
                            : $"{asset.Size / 1024.0:F0} KB";
                        ConsoleOutput.WriteDetail($"  {asset.Name}", size);
                    }
                }

                if (!checkOnly)
                {
                    Console.WriteLine();
                    ConsoleOutput.WriteInfo("To update manually:");
                    ConsoleOutput.WriteInfo("  1. Stop the server: dotnetcloud stop");
                    ConsoleOutput.WriteInfo("  2. Download the release from the URL above");
                    ConsoleOutput.WriteInfo("  3. Extract and replace installation files");
                    ConsoleOutput.WriteInfo("  4. Run database migrations if needed");
                    ConsoleOutput.WriteInfo("  5. Start the server: dotnetcloud serve");
                }

                return 1; // Exit code 1 = update available
            }
            else
            {
                Console.WriteLine();
                ConsoleOutput.WriteSuccess("You are running the latest version.");
                return 0;
            }
        }
        catch (HttpRequestException ex)
        {
            ConsoleOutput.WriteError($"Failed to check for updates: {ex.Message}");
            ConsoleOutput.WriteInfo("Check your internet connection or visit https://github.com/LLabmik/DotNetCloud/releases");
            return 2;
        }
        catch (TaskCanceledException)
        {
            ConsoleOutput.WriteError("Update check timed out.");
            return 2;
        }
    }

    private static string GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(infoVersion))
        {
            var plusIdx = infoVersion.IndexOf('+');
            return plusIdx >= 0 ? infoVersion[..plusIdx] : infoVersion;
        }

        var ver = asm.GetName().Version;
        return ver is not null ? ver.ToString(3) : "0.0.0";
    }

    /// <summary>
    /// Returns true if <paramref name="latest"/> is strictly newer than <paramref name="current"/>.
    /// </summary>
    internal static bool IsNewerVersion(string current, string latest)
    {
        var currentBase = current.Contains('-') ? current[..current.IndexOf('-')] : current;
        var latestBase = latest.Contains('-') ? latest[..latest.IndexOf('-')] : latest;

        if (!Version.TryParse(currentBase, out var currentVer) ||
            !Version.TryParse(latestBase, out var latestVer))
        {
            return false;
        }

        var cmp = latestVer.CompareTo(currentVer);
        if (cmp > 0) return true;
        if (cmp < 0) return false;

        // Same base version: a release is newer than a pre-release
        var currentIsPre = current.Contains('-');
        var latestIsPre = latest.Contains('-');

        return currentIsPre && !latestIsPre;
    }

    // Minimal DTOs for deserializing GitHub release responses
    private sealed class GitHubReleaseDto
    {
        public string TagName { get; set; } = null!;
        public string? Body { get; set; }
        public string? HtmlUrl { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public List<GitHubAssetDto> Assets { get; set; } = [];
    }

    private sealed class GitHubAssetDto
    {
        public string Name { get; set; } = null!;
        public string BrowserDownloadUrl { get; set; } = null!;
        public long Size { get; set; }
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
            if (CliConfiguration.TryLoad(out var config, out var errorMessage))
            {
                ConsoleOutput.WriteDetail("Database", config.DatabaseProvider);
                ConsoleOutput.WriteDetail("Config", CliConfiguration.GetConfigFilePath());
                if (config.SetupCompletedAt.HasValue)
                {
                    ConsoleOutput.WriteDetail("Setup Date", config.SetupCompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                }
            }
            else
            {
                ConsoleOutput.WriteDetail("Config", CliConfiguration.GetConfigFilePath());
                ConsoleOutput.WriteDetail("Config Access", errorMessage ?? "Unable to read configuration.");
            }
        }
    }
}
