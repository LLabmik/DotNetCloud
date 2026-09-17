#if GOOGLEPLAY
using System.Net.Http.Json;
using Android.Util;
using Microsoft.Extensions.Logging;
using GmsIOnCompleteListener = Android.Gms.Tasks.IOnCompleteListener;
using GmsTask = Android.Gms.Tasks.Task;

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
            // Without a token there is no push transport at all: while the app process is alive
            // messages still arrive over SignalR, but once Android reclaims/freezes the process
            // nothing can wake the device. The usual cause is a build with no Firebase
            // configuration — the googleplay flavour needs a google-services.json whose
            // google_app_id/gcm_defaultSenderId resources match the Firebase project the server
            // signs its sends with.
            _logger.LogWarning(
                "FCM token not available — this device cannot receive push notifications. "
                + "Background chat alerts require a google-services.json (Firebase project) in the googleplay build.");

            Log.Warn("DotNetCloud", "FCM token unavailable: push notifications are disabled on this device (no Firebase configuration in the build).");
            return;
        }

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/v1/notifications/devices/register?userId={userId}";
        var body = new { DeviceToken = fcmToken, Provider = "Fcm", Endpoint = (string?)null };

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
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);

        var fcmToken = await GetFcmTokenAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fcmToken))
            return;

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/v1/notifications/devices/{Uri.EscapeDataString(fcmToken)}?userId={userId}";
        using var response = await _http.DeleteAsync(endpoint, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Push unregister returned {StatusCode}.", response.StatusCode);
    }

    private static async Task<string?> GetFcmTokenAsync(CancellationToken ct)
    {
        // Retrieve the current FCM registration token using the Firebase Android SDK.
        var tcs = new TaskCompletionSource<string?>();
        ct.Register(() => tcs.TrySetCanceled(ct));

        // The Firebase SDK marks GetToken() as deprecated but offers no replacement
        // instance API in this binding; the behavior is still correct.
#pragma warning disable CS0618
        Firebase.Messaging.FirebaseMessaging.Instance
            .GetToken()
            .AddOnCompleteListener(new FcmTokenListener(tcs));
#pragma warning restore CS0618

        return await tcs.Task.ConfigureAwait(false);
    }

    private sealed class FcmTokenListener(TaskCompletionSource<string?> tcs)
        : Java.Lang.Object, GmsIOnCompleteListener
    {
        public void OnComplete(GmsTask task)
        {
            if (task.IsSuccessful)
                tcs.TrySetResult(task.Result?.ToString());
            else
                tcs.TrySetResult(null);
        }
    }
}
#endif
