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
