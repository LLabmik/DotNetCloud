namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for file version retention policies.
/// Controls how many versions are kept per file and for how long.
/// </summary>
public sealed class VersionRetentionOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:VersionRetention";

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
