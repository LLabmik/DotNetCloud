namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Registers this device for push notifications with a DotNetCloud server connection.
/// </summary>
/// <remarks>
/// The implementation is UnifiedPush-based (a user-installed distributor plus a self-hosted push
/// server); there is no Google/Firebase path. The abstraction stays because the transport is
/// provider-specific while the call sites (app start, login) are not.
/// </remarks>
public interface IPushNotificationService
{
    /// <summary>
    /// Ensures this device is registered for push with the given server connection. Safe to call
    /// on every app start: it re-registers with the distributor and re-reports the endpoint.
    /// </summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when a registration is in place or a request was sent.</returns>
    Task<bool> RegisterAsync(string serverBaseUrl, CancellationToken ct = default);

    /// <summary>Unregisters this device from push for the given server connection.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the unregistration request was handled.</returns>
    Task<bool> UnregisterAsync(string serverBaseUrl, CancellationToken ct = default);
}
