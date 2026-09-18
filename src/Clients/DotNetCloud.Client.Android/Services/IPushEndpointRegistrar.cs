namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Reports a UnifiedPush endpoint to the DotNetCloud server, or removes it again.
/// </summary>
/// <remarks>
/// Deliberately does not take an access token: the implementation is wired to
/// <c>AuthenticatedHttpClientHandler</c>, which attaches (and proactively refreshes) the bearer
/// token for the server in the request URL. That is what fixes the old defect where this flow
/// tried to decode the user id out of a JWE access token and threw before reaching the network.
/// </remarks>
public interface IPushEndpointRegistrar
{
    /// <summary>Registers an endpoint with the server that owns the given connection.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="endpoint">Endpoint URL supplied by the distributor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the server accepted the endpoint.</returns>
    Task<bool> RegisterAsync(string serverBaseUrl, string endpoint, CancellationToken ct = default);

    /// <summary>Removes an endpoint from the server.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="endpoint">Endpoint URL to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the server accepted (or no longer knows) the endpoint.</returns>
    Task<bool> UnregisterAsync(string serverBaseUrl, string endpoint, CancellationToken ct = default);
}

