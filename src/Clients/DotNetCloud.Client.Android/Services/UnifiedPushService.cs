namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Push notification service backed by UnifiedPush.
/// </summary>
/// <remarks>
/// <para>
/// This is the only push transport in either flavour: Firebase was removed outright rather than
/// kept behind a flag, so no message metadata can reach Google infrastructure. The distributor
/// (for example the ntfy app) is chosen by the user and the push server is self-hosted.
/// </para>
/// <para>
/// The work lives in <see cref="IUnifiedPushConnector"/>; this type exists so that call sites
/// depend on the transport-agnostic <see cref="IPushNotificationService"/> rather than on the
/// Android connector.
/// </para>
/// </remarks>
internal sealed class UnifiedPushService : IPushNotificationService
{
    private readonly IUnifiedPushConnector _connector;

    /// <summary>Initializes a new <see cref="UnifiedPushService"/>.</summary>
    /// <param name="connector">The UnifiedPush connector.</param>
    public UnifiedPushService(IUnifiedPushConnector connector)
    {
        _connector = connector;
    }

    /// <inheritdoc />
    public Task<bool> RegisterAsync(string serverBaseUrl, CancellationToken ct = default) =>
        _connector.RegisterAsync(serverBaseUrl, ct);

    /// <inheritdoc />
    public Task<bool> UnregisterAsync(string serverBaseUrl, CancellationToken ct = default) =>
        _connector.UnregisterAsync(serverBaseUrl, ct);
}
