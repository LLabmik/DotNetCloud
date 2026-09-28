namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// The <b>effective</b> Collabora configuration currently in force: the server-configured
/// <c>Files:Collabora:*</c> values with any administrator edits layered on top.
/// Served by <c>GET /api/v1/files/admin/collabora/effective</c>.
/// </summary>
public sealed record CollaboraEffectiveSettingsDto
{
    /// <summary>Whether Collabora integration is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Public URL of the Collabora server.</summary>
    public string ServerUrl { get; init; } = string.Empty;

    /// <summary>Internal URL used for discovery/health checks.</summary>
    public string DiscoveryUrl { get; init; } = string.Empty;

    /// <summary>Public base URL of this DotNetCloud instance.</summary>
    public string WopiBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Whether a token signing key is in force. The key itself is never sent to the browser.
    /// </summary>
    public bool HasTokenSigningKey { get; init; }

    /// <summary>WOPI access token lifetime in minutes.</summary>
    public int TokenLifetimeMinutes { get; init; }

    /// <summary>Whether WOPI proof-key validation is enforced.</summary>
    public bool EnableProofKeyValidation { get; init; }

    /// <summary>Collabora auto-save interval in seconds.</summary>
    public int AutoSaveIntervalSeconds { get; init; }

    /// <summary>Maximum concurrent editing sessions (<c>0</c> = unlimited).</summary>
    public int MaxConcurrentSessions { get; init; }

    /// <summary>Comma-separated MIME type allow-list (empty = everything discovery reports).</summary>
    public string SupportedMimeTypes { get; init; } = string.Empty;

    /// <summary>Whether DotNetCloud supervises a local Collabora CODE process.</summary>
    public bool UseBuiltInCollabora { get; init; }

    /// <summary>Whether insecure TLS is allowed when calling Collabora.</summary>
    public bool AllowInsecureTls { get; init; }

    /// <summary>Directory the built-in Collabora CODE is installed in.</summary>
    public string CollaboraInstallDirectory { get; init; } = string.Empty;

    /// <summary>Explicit path to the Collabora executable.</summary>
    public string CollaboraExecutablePath { get; init; } = string.Empty;

    /// <summary>Maximum restart attempts for the built-in process.</summary>
    public int CollaboraMaxRestartAttempts { get; init; }

    /// <summary>Base backoff between built-in process restarts, in seconds.</summary>
    public int CollaboraRestartBackoffSeconds { get; init; }
}
