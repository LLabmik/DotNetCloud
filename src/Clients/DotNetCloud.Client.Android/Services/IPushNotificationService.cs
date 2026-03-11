namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Registers the device for push notifications and routes incoming push payloads.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Registers the device with the push notification provider and sends the token
    /// to the DotNetCloud server.
    /// </summary>
    /// <param name="serverBaseUrl">Server URL to register the push token with.</param>
    /// <param name="accessToken">OAuth2 access token for the server API.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RegisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>Unregisters the device from push notifications on the server.</summary>
    /// <param name="serverBaseUrl">Server URL.</param>
    /// <param name="accessToken">OAuth2 access token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnregisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);
}
