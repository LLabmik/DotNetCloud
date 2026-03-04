namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for user storage quota policies.
/// Controls default limits, notification thresholds, and whether trashed items count against quota.
/// </summary>
public sealed class QuotaOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:Quota";

    /// <summary>
    /// Default storage quota for new users in bytes.
    /// Set to 0 for unlimited. Default: 10 GB.
    /// </summary>
    public long DefaultQuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    /// <summary>
    /// Whether to exclude soft-deleted (trashed) items from quota calculations.
    /// When <see langword="true"/>, files in the trash do not count against the user's quota.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool ExcludeTrashedFromQuota { get; set; } = false;

    /// <summary>
    /// Usage percentage at which a warning notification is published (0–100).
    /// Default: 80.0.
    /// </summary>
    public double WarnAtPercent { get; set; } = 80.0;

    /// <summary>
    /// Usage percentage at which a critical notification is published (0–100).
    /// Default: 95.0.
    /// </summary>
    public double CriticalAtPercent { get; set; } = 95.0;

    /// <summary>
    /// How often the background quota recalculation service runs. Default: 24 hours.
    /// </summary>
    public TimeSpan RecalculationInterval { get; set; } = TimeSpan.FromHours(24);
}
