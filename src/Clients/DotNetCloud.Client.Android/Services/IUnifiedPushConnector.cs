namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Drives the UnifiedPush registration lifecycle for this app: it owns the connection tokens,
/// talks to the user's distributor app, and reports endpoints to the DotNetCloud server.
/// </summary>
/// <remarks>
/// Implementations are Android-specific (they broadcast intents) but must never throw: every
/// entry point can be reached from a broadcast receiver or a background service.
/// </remarks>
public interface IUnifiedPushConnector
{
    /// <summary>
    /// Ensures this device is registered for push for a server connection and that the server
    /// knows the endpoint. Safe to call on every app start and after every login: it re-sends
    /// <c>REGISTER</c> (per the specification, to avoid inconsistent state) using the existing
    /// token, and re-reports a known endpoint.
    /// </summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when a registration is in place or a request was sent successfully.</returns>
    Task<bool> RegisterAsync(string serverBaseUrl, CancellationToken ct = default);

    /// <summary>
    /// Unregisters this device from the distributor and removes the endpoint from the server.
    /// </summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the unregistration request was handled.</returns>
    Task<bool> UnregisterAsync(string serverBaseUrl, CancellationToken ct = default);

    /// <summary>
    /// Lets the user pick a distributor with the system UI (the specification's
    /// <c>unifiedpush://link</c> activity, started for a result).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when a distributor was selected.</returns>
    Task<bool> SelectDistributorAsync(CancellationToken ct = default);

    /// <summary>Reports the current state for the Settings card.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The status snapshot.</returns>
    Task<UnifiedPushStatus> GetStatusAsync(string serverBaseUrl, CancellationToken ct = default);

    /// <summary>Acknowledges a distributor message or endpoint ping, when it carried an id.</summary>
    /// <param name="token">Connection token.</param>
    /// <param name="id">Identifier to acknowledge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the ack has been sent.</returns>
    Task AcknowledgeAsync(string token, string? id, CancellationToken ct = default);

    /// <summary>Handles <c>org.unifiedpush.android.connector.NEW_ENDPOINT</c>.</summary>
    /// <param name="token">Connection token.</param>
    /// <param name="endpoint">Endpoint URL.</param>
    /// <param name="id">Ping identifier to acknowledge, when present.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the endpoint has been handled.</returns>
    Task HandleNewEndpointAsync(string token, string endpoint, string? id, CancellationToken ct = default);

    /// <summary>Handles <c>org.unifiedpush.android.connector.REGISTRATION_FAILED</c>.</summary>
    /// <param name="token">Connection token.</param>
    /// <param name="reason">Failure reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the failure has been recorded.</returns>
    Task HandleRegistrationFailedAsync(string token, string? reason, CancellationToken ct = default);

    /// <summary>Handles <c>org.unifiedpush.android.connector.UNREGISTERED</c>.</summary>
    /// <param name="token">Connection token.</param>
    /// <param name="useDistributor">Distributor to switch to, when the distributor asks for one.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the removal has been processed.</returns>
    Task HandleUnregisteredAsync(string token, string? useDistributor, CancellationToken ct = default);

    /// <summary>Handles <c>org.unifiedpush.android.connector.TEMP_UNAVAILABLE</c>.</summary>
    /// <param name="token">Connection token.</param>
    /// <param name="useDistributor">Distributor to fall back to; ignored by this client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the state has been recorded.</returns>
    Task HandleTempUnavailableAsync(string token, string? useDistributor, CancellationToken ct = default);
}

/// <summary>Snapshot of UnifiedPush state for the Settings card.</summary>
/// <param name="State">Lifecycle state of the registration.</param>
/// <param name="DistributorPackage">Package name of the distributor in use, when known.</param>
/// <param name="DistributorLabel">Human-readable distributor name, when resolvable.</param>
/// <param name="EndpointHost">Host of the registered endpoint (never the full capability URL).</param>
/// <param name="LastReason">Last failure reason reported by a distributor, when any.</param>
/// <param name="InstalledDistributorCount">How many UnifiedPush distributor apps are installed.</param>
public sealed record UnifiedPushStatus(
    UnifiedPushRegistrationState State,
    string? DistributorPackage,
    string? DistributorLabel,
    string? EndpointHost,
    string? LastReason,
    int InstalledDistributorCount)
{
    /// <summary>Whether push delivery is believed to be working.</summary>
    public bool IsRegistered => State == UnifiedPushRegistrationState.Registered;

    /// <summary>Whether the device has no distributor app at all (so push cannot work yet).</summary>
    public bool HasNoDistributorInstalled => InstalledDistributorCount == 0;

    /// <summary>Whether the user still has to pick between several installed distributors.</summary>
    public bool NeedsDistributorChoice => !IsRegistered && InstalledDistributorCount > 1 && DistributorPackage is null;
}
