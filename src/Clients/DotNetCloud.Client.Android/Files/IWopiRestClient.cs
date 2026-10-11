namespace DotNetCloud.Client.Android.Files;

/// <summary>
/// REST client for the Collabora Online / WOPI endpoints exposed by the server. Uses the same
/// per-call bearer-credentials pattern as <see cref="IFileRestClient"/>.
/// </summary>
public interface IWopiRestClient
{
    /// <summary>
    /// Gets Collabora capability information from the server. Returns
    /// <see cref="WopiDiscovery.Unavailable"/> instead of throwing when the server cannot be reached,
    /// so a caller can simply fall back to today's behavior.
    /// </summary>
    Task<WopiDiscovery> GetDiscoveryAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Mints a WOPI access token and the matching editor URL for a file. The server encodes write
    /// capability in the chosen <c>edit</c>/<c>view</c> URL template — the client computes no
    /// permissions.
    /// </summary>
    /// <exception cref="HttpRequestException">The server rejected or failed the request.</exception>
    Task<WopiEditorSession> OpenEditorAsync(
        string serverBaseUrl, string accessToken, Guid nodeId, CancellationToken ct = default);

    /// <summary>Ends the editing session, releasing the caller's concurrent-session slot.</summary>
    Task CloseEditorAsync(
        string serverBaseUrl, string accessToken, Guid nodeId, CancellationToken ct = default);
}
