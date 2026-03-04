namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for Collabora Online/CODE integration via WOPI protocol.
/// </summary>
public sealed class CollaboraOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:Collabora";

    /// <summary>
    /// URL of the Collabora Online server (e.g., "https://collabora.example.com").
    /// When empty, Collabora integration is disabled.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether Collabora integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Auto-save interval in seconds. Collabora will trigger PutFile at this interval.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of concurrent document editing sessions allowed.
    /// Set to 0 for unlimited. Default: 20.
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 20;

    /// <summary>
    /// WOPI access token lifetime in minutes.
    /// Tokens expire after this duration and must be refreshed. Default: 480 (8 hours).
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 480;

    /// <summary>
    /// Secret key used to sign WOPI access tokens (HMAC-SHA256).
    /// Must be at least 32 characters. Generated automatically if not set.
    /// </summary>
    public string TokenSigningKey { get; set; } = string.Empty;

    /// <summary>
    /// The public-facing base URL of this DotNetCloud instance.
    /// Used to construct WOPI source URLs that Collabora calls back to.
    /// Example: "https://cloud.example.com"
    /// </summary>
    public string WopiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable WOPI proof key validation for incoming Collabora requests.
    /// Should be enabled in production. Default: true.
    /// </summary>
    public bool EnableProofKeyValidation { get; set; } = true;

    /// <summary>
    /// Supported MIME types for Collabora editing. If empty, all types returned by
    /// Collabora discovery are accepted.
    /// </summary>
    public List<string> SupportedMimeTypes { get; set; } = [];
}
