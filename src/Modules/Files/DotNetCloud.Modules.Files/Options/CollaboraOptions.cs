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
    /// Whether to allow insecure TLS certificates when DotNetCloud calls Collabora.
    /// Use only for local/testing environments with self-signed certificates.
    /// Default: <c>false</c>.
    /// </summary>
    public bool AllowInsecureTls { get; set; }

    /// <summary>
    /// Supported MIME types for Collabora editing. If empty, all types returned by
    /// Collabora discovery are accepted.
    /// </summary>
    public List<string> SupportedMimeTypes { get; set; } = [];

    /// <summary>
    /// Whether to use the built-in Collabora CODE instance managed by DotNetCloud.
    /// When <c>true</c>, DotNetCloud will start and supervise a local Collabora process.
    /// When <c>false</c>, <see cref="ServerUrl"/> must point to an externally-managed Collabora server.
    /// Default: <c>false</c>.
    /// </summary>
    public bool UseBuiltInCollabora { get; set; }

    /// <summary>
    /// Directory where Collabora CODE is installed (used when <see cref="UseBuiltInCollabora"/> is <c>true</c>).
    /// Example: "/opt/collaboraoffice" (Linux) or "C:\collabora" (Windows).
    /// </summary>
    public string CollaboraInstallDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the Collabora executable (coolwsd or coolwsd.exe).
    /// When empty and <see cref="UseBuiltInCollabora"/> is true, DotNetCloud will try to locate
    /// the executable within <see cref="CollaboraInstallDirectory"/>.
    /// </summary>
    public string CollaboraExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of restart attempts for the built-in Collabora process before giving up.
    /// Default: 5.
    /// </summary>
    public int CollaboraMaxRestartAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay in seconds for exponential backoff when restarting the Collabora process.
    /// Default: 5 seconds.
    /// </summary>
    public int CollaboraRestartBackoffSeconds { get; set; } = 5;
}
