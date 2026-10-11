namespace DotNetCloud.Client.Android.Files;

/// <summary>
/// Collabora Online capability information reported by the server's
/// <c>GET /api/v1/wopi/discovery</c> endpoint.
/// </summary>
/// <param name="Available">Whether Collabora is configured and reachable on the server.</param>
/// <param name="SupportedExtensions">
/// Dot-less, lowercase extensions Collabora can open. Empty when <paramref name="Available"/> is
/// <c>false</c>.
/// </param>
public sealed record WopiDiscovery(bool Available, IReadOnlyList<string> SupportedExtensions)
{
    /// <summary>The result used when the server is unreachable or reports no Collabora.</summary>
    public static WopiDiscovery Unavailable { get; } = new(false, []);
}

/// <summary>
/// A minted, self-authenticating Collabora editor session for a single file
/// (<c>POST /api/v1/wopi/token/{fileId}</c>).
/// </summary>
/// <param name="AccessToken">Opaque, short-lived, file-scoped WOPI access token.</param>
/// <param name="AccessTokenTtl">Token expiry as Unix milliseconds (not a duration).</param>
/// <param name="WopiSrc">The WOPI source URL Collabora calls back into.</param>
/// <param name="EditorUrl">
/// The full editor URL. It is loadable top-level in a WebView without any cookie: the server decides
/// editable vs view-only from the caller's effective permissions.
/// </param>
public sealed record WopiEditorSession(
    string AccessToken,
    long AccessTokenTtl,
    string WopiSrc,
    string EditorUrl);
