namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Configuration options for LiveKit SFU integration.
/// </summary>
public sealed class LiveKitOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Chat:LiveKit";

    /// <summary>
    /// Whether LiveKit integration is enabled. Defaults to <c>false</c>.
    /// When disabled, <see cref="NullLiveKitService"/> is used and calls are limited to P2P mesh (max 3 participants).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The LiveKit server URL (e.g., "https://livekit.example.com" or "ws://localhost:7880").
    /// Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The LiveKit API key for authentication.
    /// Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The LiveKit API secret for JWT token generation.
    /// Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Default maximum participants per LiveKit room. Defaults to 50.
    /// </summary>
    public int DefaultMaxParticipants { get; set; } = 50;

    /// <summary>
    /// Token time-to-live in seconds. Defaults to 3600 (1 hour).
    /// </summary>
    public int TokenTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Maximum participants allowed in P2P mesh mode (without LiveKit). Defaults to 3.
    /// </summary>
    public int MaxP2PParticipants { get; set; } = 3;

    /// <summary>
    /// Room empty timeout in seconds — LiveKit auto-deletes rooms after this period of inactivity. Defaults to 300 (5 minutes).
    /// </summary>
    public int EmptyRoomTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Validates that required settings are configured when LiveKit is enabled.
    /// </summary>
    /// <returns><c>true</c> if the configuration is valid; otherwise <c>false</c>.</returns>
    public bool IsValid()
    {
        if (!Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ServerUrl)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(ApiSecret);
    }
}
