using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// HTTP implementation of <see cref="IPushEndpointRegistrar"/> against the Chat module's
/// <c>/api/v1/notifications/devices</c> API.
/// </summary>
public sealed class PushEndpointRegistrar : IPushEndpointRegistrar
{
    private const string RegisterPath = "/api/v1/notifications/devices/register";
    private const string DevicesPath = "/api/v1/notifications/devices";

    /// <summary>Value the server parses as <c>PushProvider.UnifiedPush</c>.</summary>
    public const string ProviderName = "UnifiedPush";

    private readonly HttpClient _http;
    private readonly ILogger<PushEndpointRegistrar> _logger;

    /// <summary>Initializes a new <see cref="PushEndpointRegistrar"/>.</summary>
    /// <param name="http">HTTP client carrying the authenticated handler.</param>
    /// <param name="logger">Logger.</param>
    public PushEndpointRegistrar(HttpClient http, ILogger<PushEndpointRegistrar> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> RegisterAsync(string serverBaseUrl, string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl) || string.IsNullOrWhiteSpace(endpoint))
            return false;

        try
        {
            var url = $"{serverBaseUrl.TrimEnd('/')}{RegisterPath}";
            var body = new
            {
                DeviceToken = endpoint,
                Provider = ProviderName,
                Endpoint = endpoint,
            };

            using var response = await _http.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Push endpoint registered with {Host} ({Provider}).",
                    UnifiedPushProtocol.SafeEndpointHost(serverBaseUrl) ?? serverBaseUrl,
                    ProviderName);
                return true;
            }

            _logger.LogWarning(
                "Push endpoint registration returned {StatusCode}.", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push endpoint registration failed.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnregisterAsync(string serverBaseUrl, string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl) || string.IsNullOrWhiteSpace(endpoint))
            return false;

        try
        {
            var url = $"{serverBaseUrl.TrimEnd('/')}{DevicesPath}/{Uri.EscapeDataString(endpoint)}";
            using var response = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Push endpoint removed from the server.");
                return true;
            }

            // A device the server has already forgotten is not an error worth retrying.
            _logger.LogWarning(
                "Push endpoint removal returned {StatusCode}.", (int)response.StatusCode);
            return response.StatusCode is HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push endpoint removal failed.");
            return false;
        }
    }
}
