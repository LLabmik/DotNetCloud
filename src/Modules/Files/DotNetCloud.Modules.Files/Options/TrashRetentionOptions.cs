namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for trash retention policies.
/// Controls how long items stay in the trash before automatic permanent deletion.
/// </summary>
public sealed class TrashRetentionOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:TrashRetention";

    /// <summary>
    /// Number of days to retain items in the trash before automatic permanent deletion.
    /// Set to 0 to disable auto-cleanup. Default: 30 days.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// How often the trash cleanup background service runs. Default: 6 hours.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);
}
