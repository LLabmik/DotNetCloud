namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Configuration options for the Firebase Cloud Messaging provider.
/// </summary>
public sealed class FcmPushOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Chat:Push:Fcm";

    /// <summary>
    /// Gets or sets a value indicating whether FCM push delivery is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the FCM project identifier.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets credentials file path or secret reference.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Gets or sets optional dry-run mode for integration testing.
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// Configuration options for the UnifiedPush provider.
/// </summary>
public sealed class UnifiedPushOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Chat:Push:UnifiedPush";

    /// <summary>
    /// Gets or sets a value indicating whether UnifiedPush delivery is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets max retry attempts for transient delivery failures.
    /// </summary>
    public int MaxSendAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets retry delay in milliseconds between transient attempts.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the default distributor endpoint prefix.
    /// </summary>
    public string? DistributorBaseUrl { get; set; }
}
