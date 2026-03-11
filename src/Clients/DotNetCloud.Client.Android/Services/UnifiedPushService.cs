#if FDROID
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Push notification service backed by UnifiedPush (F-Droid flavor).
/// Supports any UnifiedPush-compatible distributor (e.g. ntfy, Gotify UP).
/// </summary>
internal sealed class UnifiedPushService : IPushNotificationService
{
    private readonly HttpClient _http;
    private readonly ILogger<UnifiedPushService> _logger;

    /// <summary>Initializes a new <see cref="UnifiedPushService"/>.</summary>
    public UnifiedPushService(HttpClient http, ILogger<UnifiedPushService> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        // UnifiedPush registration is handled in UnifiedPushReceiver (Android broadcast receiver).
        // The endpoint URL (provided by the distributor) is passed here once available.
        var endpoint = await UnifiedPushReceiver.GetEndpointAsync(ct).ConfigureAwait(false);
        if (endpoint is null)
        {
            _logger.LogWarning("No UnifiedPush distributor available.");
            return;
        }

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);

        var apiEndpoint = $"{serverBaseUrl.TrimEnd('/')}/api/v1/notifications/devices/register?userId={userId}";
        var body = new { DeviceToken = endpoint, Provider = "UnifiedPush", Endpoint = endpoint };

        using var response = await _http.PostAsJsonAsync(apiEndpoint, body, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Push registration returned {StatusCode}.", response.StatusCode);
        else
            _logger.LogInformation("UnifiedPush endpoint registered with {ServerBaseUrl}.", serverBaseUrl);
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);

        var endpointToken = await UnifiedPushReceiver.GetEndpointAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpointToken))
            return;

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/v1/notifications/devices/{Uri.EscapeDataString(endpointToken)}?userId={userId}";
        using var response = await _http.DeleteAsync(endpoint, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Push unregister returned {StatusCode}.", response.StatusCode);
    }
}
#endif
