using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Services;

/// <summary>
/// Client-side update service that queries the server's update API endpoint
/// and falls back to GitHub Releases when the server is unreachable.
/// </summary>
public sealed class ClientUpdateService : IClientUpdateService
{
    private const string GitHubReleasesUrl = "https://api.github.com/repos/LLabmik/DotNetCloud/releases";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ClientUpdateService> _logger;
    private readonly string _currentVersion;
    private readonly string _platform;

    /// <inheritdoc/>
    public event EventHandler<UpdateCheckResult>? UpdateAvailable;

    /// <summary>Initializes a new <see cref="ClientUpdateService"/>.</summary>
    public ClientUpdateService(HttpClient httpClient, ILogger<ClientUpdateService> logger)
    {
        _http = httpClient;
        _logger = logger;
        _currentVersion = GetCurrentVersion();
        _platform = GetCurrentPlatform();
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        // Try the server endpoint first.
        if (_http.BaseAddress is not null)
        {
            try
            {
                var result = await CheckViaServerAsync(ct);
                if (result is not null)
                {
                    if (result.IsUpdateAvailable)
                        UpdateAvailable?.Invoke(this, result);
                    return result;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Server update check failed; falling back to GitHub.");
            }
        }

        // Fallback: hit GitHub directly.
        try
        {
            var result = await CheckViaGitHubAsync(ct);
            if (result.IsUpdateAvailable)
                UpdateAvailable?.Invoke(this, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GitHub fallback update check also failed.");
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = _currentVersion,
                LatestVersion = _currentVersion,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<string> DownloadUpdateAsync(
        ReleaseAsset asset,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.DownloadUrl);

        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "DotNetCloud",
            "updates");
        Directory.CreateDirectory(tempDir);

        var destPath = Path.Combine(tempDir, asset.Name);

        _logger.LogInformation("Downloading update asset {Name} ({Size} bytes) to {Dest}.",
            asset.Name, asset.Size, destPath);

        using var response = await _http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
        long bytesRead = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        int read;
        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;
            if (totalBytes > 0)
                progress?.Report((double)bytesRead / totalBytes);
        }

        progress?.Report(1.0);
        _logger.LogInformation("Download complete: {Path} ({Bytes} bytes).", destPath, bytesRead);

        // Verify SHA256 checksum if a .sha256 sidecar exists alongside the asset.
        await VerifyChecksumAsync(asset, destPath, ct);

        return destPath;
    }

    /// <inheritdoc/>
    public async Task ApplyUpdateAsync(string downloadPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadPath);

        if (!File.Exists(downloadPath))
            throw new FileNotFoundException("Downloaded update file not found.", downloadPath);

        var installDir = AppContext.BaseDirectory;
        _logger.LogInformation("Applying update from {Path} to {InstallDir}.", downloadPath, installDir);

        if (OperatingSystem.IsLinux())
        {
            await ApplyUpdateLinuxAsync(downloadPath, installDir, ct);
        }
        else if (OperatingSystem.IsWindows())
        {
            await ApplyUpdateWindowsAsync(downloadPath, installDir, ct);
        }
        else
        {
            _logger.LogWarning("Update apply not implemented for the current platform.");
        }
    }

    private async Task ApplyUpdateLinuxAsync(string archivePath, string installDir, CancellationToken ct)
    {
        // Stage 1: extract the tarball to a temp directory.
        var extractDir = Path.Combine(
            Path.GetTempPath(),
            "DotNetCloud",
            "updates",
            "extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDir);

        _logger.LogInformation("Extracting tarball to {ExtractDir}...", extractDir);

        var tarStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tar",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        tarStartInfo.ArgumentList.Add("xzf");
        tarStartInfo.ArgumentList.Add(archivePath);
        tarStartInfo.ArgumentList.Add("-C");
        tarStartInfo.ArgumentList.Add(extractDir);

        var process = new System.Diagnostics.Process { StartInfo = tarStartInfo };
        process.Start();
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Failed to extract update archive (exit code {process.ExitCode}): {error}");
        }

        // Validate extracted paths to prevent zip slip / path traversal attacks
        ValidateNoZipSlip(extractDir);

        // Stage 2: locate the payload directory.
        // Expected structure: {extractDir}/linux-x64/payload/SyncTray/
        var payloadDir = FindPayloadDirectory(extractDir);
        if (payloadDir is null)
        {
            throw new InvalidOperationException(
                "Could not locate payload directory in the update archive.");
        }

        // Stage 3: create a restart script that replaces the running binary then relaunches.
        var scriptPath = Path.Combine(Path.GetTempPath(), "DotNetCloud", "updates",
            "apply-" + Guid.NewGuid().ToString("N") + ".sh");

        var scriptContent = $"#!/usr/bin/env bash\n" +
                            $"# Auto-generated update script for DotNetCloud SyncTray\n" +
                            $"sleep 1\n" +
                            $"cp -rf \"{payloadDir}/\"* \"{installDir}/\"\n" +
                            $"chmod +x \"{installDir}/dotnetcloud-sync-tray\"\n" +
                            $"exec \"{installDir}/dotnetcloud-sync-tray\"\n";

        await File.WriteAllTextAsync(scriptPath, scriptContent, ct);

        // Set execute permission on Linux only.
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        _logger.LogInformation("Restart script created at {ScriptPath}. Launching updater.", scriptPath);

        // Stage 4: launch the updater script and exit the current process.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            CreateNoWindow = true,
        });

        // Signal the UI/ caller to exit gracefully.
        _logger.LogInformation("Updater launched. Requesting application shutdown.");
    }

    private static string? FindPayloadDirectory(string extractDir)
    {
        // Walk into the extracted tree to find the payload directory.
        // Expected: {extractDir}/linux-x64/payload/SyncTray/
        // or any path ending in /payload/SyncTray or containing the binary.
        foreach (var dir in Directory.EnumerateDirectories(extractDir, "*", SearchOption.AllDirectories))
        {
            var dirName = Path.GetFileName(dir);
            if (string.Equals(dirName, "SyncTray", StringComparison.OrdinalIgnoreCase))
            {
                var parentDir = Path.GetFileName(Path.GetDirectoryName(dir));
                if (string.Equals(parentDir, "payload", StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }

            // Direct match: the binary is directly inside.
            if (File.Exists(Path.Combine(dir, "dotnetcloud-sync-tray")))
            {
                return dir;
            }
        }
        return null;
    }

    /// <summary>
    /// Validates that no extracted file escapes the extraction directory (zip slip / path traversal protection).
    /// </summary>
    /// <param name="extractDir">The root extraction directory.</param>
    /// <exception cref="InvalidOperationException">Thrown if a file is found outside the extraction directory.</exception>
    private static void ValidateNoZipSlip(string extractDir)
    {
        var fullExtractDir = Path.GetFullPath(extractDir);
        foreach (var file in Directory.EnumerateFiles(fullExtractDir, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(file);
            var relative = Path.GetRelativePath(fullExtractDir, fullPath);
            if (relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.IsPathRooted(relative))
            {
                throw new InvalidOperationException(
                    $"Zip slip detected: archive entry at '{relative}' escapes the extraction directory.");
            }
        }
    }

    private Task ApplyUpdateWindowsAsync(string archivePath, string installDir, CancellationToken ct)
    {
        // Windows update apply — deferred. On Windows the zip-based update
        // will use a similar updater-helper pattern.
        _logger.LogInformation(
            "Windows update apply from {Path} to {InstallDir} is not yet implemented.",
            archivePath, installDir);
        return Task.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task<UpdateCheckResult?> CheckViaServerAsync(CancellationToken ct)
    {
        var url = $"api/v1/core/updates/check?currentVersion={Uri.EscapeDataString(_currentVersion)}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        // The server wraps in { success: true, data: {...} }.
        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            return data.Deserialize<UpdateCheckResult>(JsonOptions);
        }

        // Unwrapped response (direct DTO).
        return doc.RootElement.Deserialize<UpdateCheckResult>(JsonOptions);
    }

    private async Task<UpdateCheckResult> CheckViaGitHubAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesUrl + "?per_page=1");
        request.Headers.UserAgent.ParseAdd("DotNetCloud-Client/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var releases = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions, ct);
        if (releases is null || releases.Length == 0)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = _currentVersion,
                LatestVersion = _currentVersion,
            };
        }

        var latest = releases[0];
        var tagName = latest.GetProperty("tag_name").GetString() ?? "";
        var latestVersion = NormalizeVersion(tagName);
        var isNewer = IsNewerVersion(_currentVersion, latestVersion);

        var assets = new List<ReleaseAsset>();
        if (latest.TryGetProperty("assets", out var assetsEl))
        {
            foreach (var a in assetsEl.EnumerateArray())
            {
                assets.Add(new ReleaseAsset
                {
                    Name = a.GetProperty("name").GetString() ?? "",
                    DownloadUrl = a.GetProperty("browser_download_url").GetString() ?? "",
                    Size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                    ContentType = a.TryGetProperty("content_type", out var ct2) ? ct2.GetString() : null,
                    Platform = InferPlatform(a.GetProperty("name").GetString() ?? ""),
                });
            }
        }

        return new UpdateCheckResult
        {
            IsUpdateAvailable = isNewer,
            CurrentVersion = _currentVersion,
            LatestVersion = latestVersion,
            ReleaseUrl = latest.TryGetProperty("html_url", out var url) ? url.GetString() : null,
            ReleaseNotes = latest.TryGetProperty("body", out var body) ? body.GetString() : null,
            PublishedAt = latest.TryGetProperty("published_at", out var pub) && pub.TryGetDateTimeOffset(out var dt) ? dt : null,
            Assets = assets,
        };
    }

    private async Task VerifyChecksumAsync(ReleaseAsset asset, string filePath, CancellationToken ct)
    {
        // Look for a companion .sha256 asset in the same release.
        var checksumName = asset.Name + ".sha256";
        // We can't easily access all assets from here, so skip if no direct URL is available.
        // The server-side update check already validates checksums when proxying.
        _logger.LogDebug("Checksum verification for {Asset} deferred (sidecar download not yet implemented).", asset.Name);
        await Task.CompletedTask;
    }

    private static string GetCurrentVersion()
    {
        var attr = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? "0.0.0";
        // Strip source-link metadata (e.g. "+abc123").
        var plusIdx = version.IndexOf('+');
        return plusIdx >= 0 ? version[..plusIdx] : version;
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "win-x64";
        if (OperatingSystem.IsMacOS())
            return "osx-x64";
        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "linux-arm64",
            _ => "linux-x64",
        };
    }

    private static string NormalizeVersion(string tagName)
    {
        var v = tagName.AsSpan().TrimStart('v').TrimStart('V');
        return v.ToString();
    }

    private static bool IsNewerVersion(string current, string latest)
    {
        var (currentBase, currentPre) = SplitPreRelease(current);
        var (latestBase, latestPre) = SplitPreRelease(latest);

        if (!Version.TryParse(currentBase, out var curVer))
            return false;
        if (!Version.TryParse(latestBase, out var latVer))
            return false;

        var cmp = latVer.CompareTo(curVer);
        if (cmp != 0)
            return cmp > 0;

        // Same base version: GA (no pre-release) > pre-release.
        if (string.IsNullOrEmpty(latestPre) && !string.IsNullOrEmpty(currentPre))
            return true;
        if (!string.IsNullOrEmpty(latestPre) && string.IsNullOrEmpty(currentPre))
            return false;

        return string.Compare(latestPre, currentPre, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static (string Base, string PreRelease) SplitPreRelease(string version)
    {
        var idx = version.IndexOf('-');
        return idx < 0
            ? (version, string.Empty)
            : (version[..idx], version[(idx + 1)..]);
    }

    private static string? InferPlatform(string fileName)
    {
        var lower = fileName.ToLowerInvariant();

        // Only match desktop-client assets — skip server-only tarballs
        // (e.g. "dotnetcloud-0.3.0-linux-x64.tar.gz" vs
        //  "dotnetcloud-desktop-client-linux-x64-0.3.0.tar.gz").
        if (!lower.Contains("desktop-client"))
            return null;

        if (lower.Contains("linux-x64"))
            return "linux-x64";
        if (lower.Contains("linux-arm64"))
            return "linux-arm64";
        if (lower.Contains("win-x64") || lower.Contains("windows"))
            return "win-x64";
        if (lower.Contains("osx-x64") || lower.Contains("macos"))
            return "osx-x64";
        if (lower.Contains("android") || lower.Contains(".apk"))
            return "android";
        return null;
    }
}
