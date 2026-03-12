#if FDROID
using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using UnifiedPush;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Broadcast receiver that handles UnifiedPush distributor callbacks for the F-Droid flavor.
/// </summary>
/// <remarks>
/// UnifiedPush distributes notifications through a user-chosen distributor app (e.g. ntfy, Gotify).
/// This receiver handles three intents:
/// <list type="bullet">
///   <item><c>UP_ENDPOINT</c> — a new push endpoint URL is available; register with the server.</item>
///   <item><c>UP_UNREGISTERED</c> — the distributor has unregistered this app.</item>
///   <item><c>UP_MESSAGE</c> — an incoming push notification payload.</item>
/// </list>
/// </remarks>
[BroadcastReceiver(Exported = true)]
[IntentFilter(["org.unifiedpush.android.connector.MESSAGE",
               "org.unifiedpush.android.connector.NEW_ENDPOINT",
               "org.unifiedpush.android.connector.UNREGISTERED"])]
public sealed class UnifiedPushReceiver : UnifiedPush.MessagingReceiver
{
    private static TaskCompletionSource<string?>? _endpointTcs;

    /// <summary>
    /// Waits for the distributor to supply a push endpoint URL.
    /// Called once during app start-up after <see cref="UnifiedPush.Connector.Register"/> is invoked.
    /// </summary>
    public static Task<string?> GetEndpointAsync(CancellationToken ct = default)
    {
        _endpointTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _endpointTcs.TrySetResult(null));
        // Return immediately if an endpoint was already cached in preferences.
        var cached = CachedEndpoint;
        if (!string.IsNullOrWhiteSpace(cached))
            _endpointTcs.TrySetResult(cached);
        return _endpointTcs.Task;
    }

    // Cached endpoint persisted across process restarts (read on demand).
    private static string? CachedEndpoint =>
        Preferences.Default.Get("up_endpoint", (string?)null);

    /// <inheritdoc />
    public override void OnNewEndpoint(Context? context, string endpoint, string instance)
    {
        Preferences.Default.Set("up_endpoint", endpoint);
        _endpointTcs?.TrySetResult(endpoint);

        // Re-register with the server in the background.
        _ = RegisterWithServerAsync(context, endpoint);
    }

    /// <inheritdoc />
    public override void OnRegistrationRefused(Context? context, string instance, string reason)
    {
        var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
        logger?.LogWarning("UnifiedPush registration refused: {Reason}.", reason);
        _endpointTcs?.TrySetResult(null);
    }

    /// <inheritdoc />
    public override void OnUnregistered(Context? context, string instance)
    {
        Preferences.Default.Remove("up_endpoint");
        var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
        logger?.LogInformation("UnifiedPush distributor unregistered the app.");
    }

    /// <inheritdoc />
    public override void OnMessage(Context? context, byte[] message, string instance)
    {
        if (context is null) return;

        var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(message);
            var payload = System.Text.Json.JsonSerializer.Deserialize<PushPayload>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null) return;

            logger?.LogInformation(
                "UnifiedPush message: type={Type}, channelId={ChannelId}.",
                payload.Type, payload.ChannelId);

            ShowNotification(context, payload);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to process UnifiedPush message.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task RegisterWithServerAsync(Context? context, string endpoint)
    {
        try
        {
            var serverStore = Ioc.Default.GetService<IServerConnectionStore>();
            var tokenStore  = Ioc.Default.GetService<ISecureTokenStore>();
            var pushService = Ioc.Default.GetService<IPushNotificationService>();

            if (serverStore is null || tokenStore is null || pushService is null) return;

            var connection = serverStore.GetActive();
            if (connection is null) return;

            var accessToken = await tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl).ConfigureAwait(false);
            if (accessToken is null) return;

            await pushService.RegisterAsync(connection.ServerBaseUrl, accessToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
            logger?.LogWarning(ex, "Failed to register UnifiedPush endpoint with server.");
        }
    }

    private static void ShowNotification(Context context, PushPayload payload)
    {
        var channelGuid = Guid.TryParse(payload.ChannelId, out var g) ? g : Guid.Empty;

        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        if (channelGuid != Guid.Empty)
            openIntent.PutExtra("channelId", channelGuid.ToString());

        var pendingIntent = PendingIntent.GetActivity(
            context,
            channelGuid.GetHashCode(),
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var notificationChannelId = payload.Type switch
        {
            "mention"      => MainApplication.ChannelIdMentions,
            "announcement" => MainApplication.ChannelIdAnnouncements,
            _              => MainApplication.ChannelIdMessages
        };

        var iconRes = context.Resources!
            .GetIdentifier("ic_notification", "drawable", context.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(context, notificationChannelId)
            .SetContentTitle(payload.Title ?? "DotNetCloud")
            .SetContentText(payload.Body ?? string.Empty)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .Build();

        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var notificationId = 2000 + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);
    }

    private sealed class PushPayload
    {
        public string? Type { get; init; }
        public string? ChannelId { get; init; }
        public string? Title { get; init; }
        public string? Body { get; init; }
    }
}
#endif
