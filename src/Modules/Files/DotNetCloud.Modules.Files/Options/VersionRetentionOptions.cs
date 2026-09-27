namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for file version retention policies.
/// Controls how many versions are kept per file and for how long.
/// </summary>
/// <remarks>
/// These are the <b>baseline</b> values bound from configuration (<c>Files:VersionRetention</c>).
/// The administrator settings on <c>/admin/files</c> (<c>VersionRetention:Enabled</c>,
/// <c>VersionRetention:MaxNumber</c>, <c>VersionRetention:MaxDays</c>) are layered on top by
/// <c>IFileVersioningSettingsProvider</c>, which is what every consumer resolves — so the admin page
/// is authoritative and a change applies without a restart.
/// </remarks>
public sealed class VersionRetentionOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:VersionRetention";

    /// <summary>
    /// Whether file version history is kept at all. When <see langword="false"/> only the current
    /// version of each file is retained: the newest version (the content actually served) is always
    /// kept and the retention pass removes every older version of that file.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of versions to retain per file.
    /// Oldest unlabeled versions are pruned when this limit is exceeded.
    /// Set to 0 for unlimited versions. Default: 50.
    /// </summary>
    public int MaxVersionCount { get; set; } = 50;

    /// <summary>
    /// Number of days to retain file versions.
    /// Unlabeled versions older than this threshold are automatically deleted,
    /// provided at least one version always remains.
    /// Set to 0 to disable time-based retention. Default: 0 (disabled).
    /// </summary>
    public int RetentionDays { get; set; } = 0;

    /// <summary>
    /// How often the version cleanup background service runs. Default: 24 hours.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);
}
