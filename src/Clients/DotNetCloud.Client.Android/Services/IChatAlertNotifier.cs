namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Posts the (always generic) system notification for a poll decision.
/// </summary>
/// <remarks>
/// Kept as an interface so the poll logic stays free of Android types: the implementation lives under
/// <c>Platforms/Android</c> and reuses the same renderer and payload mapping the push path used, so both
/// transports produce identical, content-free notifications.
/// </remarks>
public interface IChatAlertNotifier
{
    /// <summary>
    /// Renders and posts a notification for a decision.
    /// </summary>
    /// <param name="decision">The decision to render.</param>
    /// <param name="serverBaseUrl">Server connection the alert belongs to, used for tap routing.</param>
    /// <returns>True when a notification was posted.</returns>
    bool Notify(ChatAlertDecision decision, string? serverBaseUrl);
}
