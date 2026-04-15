namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Result of checking for available updates.
/// </summary>
public class UpdateCheckResult
{
    /// <summary>
    /// Gets or sets a value indicating whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>
    /// Gets or sets the currently running version.
    /// </summary>
    public string CurrentVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the latest available version.
    /// </summary>
    public string LatestVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the URL to the release page.
    /// </summary>
    public string? ReleaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the release notes (markdown).
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Gets or sets when the latest release was published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the downloadable assets for the release.
    /// </summary>
    public IReadOnlyList<ReleaseAsset> Assets { get; set; } = [];
}

/// <summary>
/// Information about a specific release.
/// </summary>
public class ReleaseInfo
{
    /// <summary>
    /// Gets or sets the semantic version string (e.g., "0.2.0").
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Git tag name (e.g., "v0.2.0").
    /// </summary>
    public string TagName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the release notes (markdown).
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Gets or sets when the release was published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a pre-release.
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    /// Gets or sets the URL to the release page on GitHub.
    /// </summary>
    public string? ReleaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the downloadable assets for this release.
    /// </summary>
    public IReadOnlyList<ReleaseAsset> Assets { get; set; } = [];
}

/// <summary>
/// A downloadable asset attached to a release.
/// </summary>
public class ReleaseAsset
{
    /// <summary>
    /// Gets or sets the filename of the asset.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    public string DownloadUrl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the target platform (e.g., "linux-x64", "win-x64", "android").
    /// </summary>
    public string? Platform { get; set; }
}
