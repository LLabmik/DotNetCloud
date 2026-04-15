using System.Reflection;
using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Checks for updates by querying the GitHub Releases API for the DotNetCloud repository.
/// Responses are cached in memory to avoid hitting GitHub's rate limits.
/// </summary>
internal sealed class GitHubUpdateService : IUpdateService
{
    private const string GitHubReleasesUrl = "https://api.github.com/repos/LLabmik/DotNetCloud/releases";
    private const string CacheKeyReleases = "github_releases";
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GitHubUpdateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubUpdateService"/> class.
    /// </summary>
    public GitHubUpdateService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<GitHubUpdateService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GitHubReleases");
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdateAsync(string? currentVersion = null, CancellationToken ct = default)
    {
        var current = currentVersion ?? GetServerVersion();
        var latest = await GetLatestReleaseAsync(ct);

        if (latest is null)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = current,
                LatestVersion = current,
            };
        }

        var isNewer = IsNewerVersion(current, latest.Version);

        return new UpdateCheckResult
        {
            IsUpdateAvailable = isNewer,
            CurrentVersion = current,
            LatestVersion = latest.Version,
            ReleaseUrl = latest.ReleaseUrl,
            ReleaseNotes = latest.ReleaseNotes,
            PublishedAt = latest.PublishedAt,
            Assets = latest.Assets,
        };
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        var releases = await FetchReleasesAsync(ct);
        return releases.Count > 0 ? releases[0] : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(int count = 5, CancellationToken ct = default)
    {
        var releases = await FetchReleasesAsync(ct);
        return releases.Count <= count ? releases : releases.Take(count).ToList().AsReadOnly();
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private async Task<IReadOnlyList<ReleaseInfo>> FetchReleasesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKeyReleases, out IReadOnlyList<ReleaseInfo>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            _logger.LogDebug("Fetching releases from GitHub API");

            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesUrl);
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var ghReleases = JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonOptions) ?? [];

            var releases = ghReleases
                .Where(r => !r.Draft)
                .Select(MapRelease)
                .ToList()
                .AsReadOnly();

            _cache.Set(CacheKeyReleases, (IReadOnlyList<ReleaseInfo>)releases, DefaultCacheDuration);

            _logger.LogInformation("Cached {Count} releases from GitHub", releases.Count);
            return releases;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch releases from GitHub; returning cached or empty");

            // Return stale cache if available
            if (_cache.TryGetValue(CacheKeyReleases, out IReadOnlyList<ReleaseInfo>? stale) && stale is not null)
            {
                return stale;
            }

            return [];
        }
    }

    private static ReleaseInfo MapRelease(GitHubRelease gh)
    {
        return new ReleaseInfo
        {
            Version = NormalizeVersion(gh.TagName),
            TagName = gh.TagName,
            ReleaseNotes = gh.Body,
            PublishedAt = gh.PublishedAt,
            IsPreRelease = gh.Prerelease,
            ReleaseUrl = gh.HtmlUrl,
            Assets = gh.Assets
                .Select(a => new ReleaseAsset
                {
                    Name = a.Name,
                    DownloadUrl = a.BrowserDownloadUrl,
                    Size = a.Size,
                    ContentType = a.ContentType,
                    Platform = InferPlatform(a.Name),
                })
                .ToList()
                .AsReadOnly(),
        };
    }

    /// <summary>
    /// Strips a leading "v" from the tag to get a clean version string.
    /// </summary>
    private static string NormalizeVersion(string tagName)
    {
        return tagName.StartsWith('v') ? tagName[1..] : tagName;
    }

    /// <summary>
    /// Compares <paramref name="current"/> against <paramref name="latest"/> and returns
    /// <see langword="true"/> when <paramref name="latest"/> is strictly newer.
    /// Pre-release suffixes are handled: a pre-release of the same base version is NOT
    /// considered newer than its release counterpart.
    /// </summary>
    internal static bool IsNewerVersion(string current, string latest)
    {
        // Strip pre-release suffix for base comparison
        var (currentBase, currentPre) = SplitPreRelease(current);
        var (latestBase, latestPre) = SplitPreRelease(latest);

        if (!Version.TryParse(NormalizeForSystemVersion(currentBase), out var currentVer))
        {
            return false;
        }

        if (!Version.TryParse(NormalizeForSystemVersion(latestBase), out var latestVer))
        {
            return false;
        }

        var cmp = latestVer.CompareTo(currentVer);
        if (cmp > 0)
        {
            return true;
        }

        if (cmp < 0)
        {
            return false;
        }

        // Same base version — pre-release is older than release
        if (!string.IsNullOrEmpty(currentPre) && string.IsNullOrEmpty(latestPre))
        {
            return true; // current is pre-release, latest is release → newer
        }

        return false;
    }

    private static (string baseVersion, string preRelease) SplitPreRelease(string version)
    {
        var idx = version.IndexOf('-');
        if (idx < 0)
        {
            return (version, string.Empty);
        }

        return (version[..idx], version[(idx + 1)..]);
    }

    /// <summary>
    /// Ensures version string has at least Major.Minor for <see cref="Version"/>.<c>TryParse</c>.
    /// </summary>
    private static string NormalizeForSystemVersion(string version)
    {
        // Strip leading v
        if (version.StartsWith('v'))
        {
            version = version[1..];
        }

        // System.Version needs at least Major.Minor
        if (!version.Contains('.'))
        {
            version += ".0";
        }

        return version;
    }

    /// <summary>
    /// Infers the target platform from an asset filename.
    /// </summary>
    internal static string? InferPlatform(string fileName)
    {
        var lower = fileName.ToLowerInvariant();

        if (lower.Contains("linux-x64")) return "linux-x64";
        if (lower.Contains("linux-arm64")) return "linux-arm64";
        if (lower.Contains("win-x64")) return "win-x64";
        if (lower.Contains("win-arm64")) return "win-arm64";
        if (lower.Contains("osx-x64") || lower.Contains("macos-x64")) return "osx-x64";
        if (lower.Contains("osx-arm64") || lower.Contains("macos-arm64")) return "osx-arm64";
        if (lower.Contains("android") || lower.EndsWith(".apk")) return "android";

        // Checksum files inherit the platform of the artifact they describe
        if (lower.EndsWith(".sha256"))
        {
            var baseName = lower[..^".sha256".Length];
            return InferPlatform(baseName);
        }

        return null;
    }

    private static string GetServerVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(infoVersion))
        {
            // Strip the +sha suffix if present (e.g., "0.1.7-alpha+abc123")
            var plusIdx = infoVersion.IndexOf('+');
            return plusIdx >= 0 ? infoVersion[..plusIdx] : infoVersion;
        }

        var ver = asm.GetName().Version;
        return ver is not null ? ver.ToString(3) : "0.0.0";
    }

    // -----------------------------------------------------------------------
    // GitHub API response models (private, deserialization only)
    // -----------------------------------------------------------------------

    private sealed class GitHubRelease
    {
        public string TagName { get; set; } = null!;
        public string? Name { get; set; }
        public string? Body { get; set; }
        public string? HtmlUrl { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        public string Name { get; set; } = null!;
        public string BrowserDownloadUrl { get; set; } = null!;
        public long Size { get; set; }
        public string? ContentType { get; set; }
    }
}
