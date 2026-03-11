#if GOOGLEPLAY
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Push notification service backed by Firebase Cloud Messaging (Google Play flavor).
/// Retrieves the FCM registration token and registers it with the DotNetCloud server.
/// </summary>
internal sealed class FcmPushService : IPushNotificationService
{
    private readonly HttpClient _http;
    private readonly ILogger<FcmPushService> _logger;

    /// <summary>Initializes a new <see cref="FcmPushService"/>.</summary>
    public FcmPushService(HttpClient http, ILogger<FcmPushService> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        // Retrieve the FCM token from Firebase. The token is obtained via
        // Firebase.Messaging.FirebaseMessaging.Instance.GetTokenAsync() on Android.
        // The actual Java interop is done in FcmMessagingService (see Platforms/Android/).
        var fcmToken = await GetFcmTokenAsync(ct).ConfigureAwait(false);
        if (fcmToken is null)
        {
            _logger.LogWarning("FCM token not available, skipping push registration.");
            return;
        }

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/push/register";
        var body = new { Token = fcmToken, Provider = "fcm", Platform = "android" };

        using var response = await _http.PostAsJsonAsync(endpoint, body, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Push registration returned {StatusCode}.", response.StatusCode);
        else
            _logger.LogInformation("FCM push token registered with {ServerBaseUrl}.", serverBaseUrl);
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/push/unregister";
        using var response = await _http.PostAsync(endpoint, null, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Push unregister returned {StatusCode}.", response.StatusCode);
    }

    private static async Task<string?> GetFcmTokenAsync(CancellationToken ct)
    {
        // Retrieve the current FCM registration token using the Firebase Android SDK.
        var tcs = new TaskCompletionSource<string?>();
        ct.Register(() => tcs.TrySetCanceled(ct));

        Firebase.Messaging.FirebaseMessaging.Instance
            .GetToken()
            .AddOnCompleteListener(new FcmTokenListener(tcs));

        return await tcs.Task.ConfigureAwait(false);
    }

    private sealed class FcmTokenListener(TaskCompletionSource<string?> tcs)
        : Java.Lang.Object, Google.Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(Google.Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
                tcs.TrySetResult(task.Result?.ToString());
            else
                tcs.TrySetResult(null);
        }
    }
}
#endif
