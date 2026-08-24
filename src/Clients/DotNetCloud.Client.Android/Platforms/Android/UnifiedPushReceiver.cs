#if FDROID
using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using UnifiedPush;
using static DotNetCloud.Client.Android.Services.AppBadgeManager;

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
[BroadcastReceiver(Name = "net.dotnetcloud.client.UnifiedPushReceiver", Exported = true)]
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
        // Calendar reminders use a dedicated notification channel and deep-link
        if (string.Equals(payload.Type, "calendar_reminder", StringComparison.OrdinalIgnoreCase))
        {
            ShowCalendarReminderNotification(context, payload);
            return;
        }

        // Calendar event changes (created/updated/deleted from Blazor UI) trigger a refresh
        if (string.Equals(payload.Type, "calendar_event", StringComparison.OrdinalIgnoreCase))
        {
            var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
            logger?.LogInformation("UnifiedPush calendar_event received — signaling calendar to refresh.");
            global::CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default
                .Send(new global::DotNetCloud.Client.Android.Messages.CalendarEventChangedMessage());
            return;
        }

        // DM channel created — high-priority notification with action buttons
        if (string.Equals(payload.Type, "dm_channel_created", StringComparison.OrdinalIgnoreCase))
        {
            ShowDmChannelNotification(context, payload);
            return;
        }

        // ── Foreground check: suppress if app is visible ──
        try
        {
            var foreground = Ioc.Default.GetService<IAppForegroundService>();
            if (foreground?.IsInForeground == true)
            {
                var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
                logger?.LogDebug("App in foreground; suppressing notification for channel {ChannelId}.", payload.ChannelId);
                return;
            }
        }
        catch { /* Best effort — post notification if we can't check */ }

        // ── Mute check: suppress if channel is muted ──
        try
        {
            if (Guid.TryParse(payload.ChannelId, out var chId) && chId != Guid.Empty)
            {
                var muteState = Ioc.Default.GetService<IChannelMuteStateService>();
                if (muteState?.IsMuted(chId) == true)
                {
                    var logger = Ioc.Default.GetService<ILogger<UnifiedPushReceiver>>();
                    logger?.LogDebug("Channel {ChannelId} is muted; suppressing notification.", payload.ChannelId);
                    return;
                }
            }
        }
        catch { /* Best effort */ }

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
            .WithBadgeCount(context)
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
        public string? EventId { get; init; }
    }

    private static void ShowCalendarReminderNotification(Context context, PushPayload payload)
    {
        var eventId = payload.EventId ?? payload.ChannelId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        openIntent.PutExtra("eventId", eventId);

        var pendingIntent = PendingIntent.GetActivity(
            context,
            eventId.GetHashCode(),
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = context.Resources!
            .GetIdentifier("ic_notification", "drawable", context.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(context, MainApplication.ChannelIdCalendarReminders)
            .SetContentTitle(payload.Title ?? "Calendar reminder")
            .SetContentText(payload.Body ?? string.Empty)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryAlarm)
            .Build();

        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var notificationId = 5000 + (eventId.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);

        // Cancel any local alarm for the same event to avoid duplicates
        try
        {
            if (Guid.TryParse(eventId, out var evtId))
            {
                var scheduler = Ioc.Default.GetService<ICalendarReminderScheduler>();
                scheduler?.CancelReminders(evtId);
            }
        }
        catch { /* Best effort */ }
    }

    private static void ShowDmChannelNotification(Context context, PushPayload payload)
    {
        var channelId = payload.ChannelId ?? string.Empty;
        var channelGuid = Guid.TryParse(channelId, out var g) ? g : Guid.Empty;
        var title = payload.Title ?? "DotNetCloud";
        var body = payload.Body ?? string.Empty;

        // Deep-link intent: open MainActivity and route to the DM channel.
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

        // Accept action
        var acceptIntent = new Intent(context, typeof(DmNotificationActionReceiver));
        acceptIntent.SetAction("DOTNETCLOUD_DM_ACCEPT");
        acceptIntent.PutExtra("channelId", channelId);
        var acceptPending = PendingIntent.GetBroadcast(
            context, channelGuid.GetHashCode() ^ 1, acceptIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Ignore action
        var ignoreIntent = new Intent(context, typeof(DmNotificationActionReceiver));
        ignoreIntent.SetAction("DOTNETCLOUD_DM_IGNORE");
        ignoreIntent.PutExtra("channelId", channelId);
        var ignorePending = PendingIntent.GetBroadcast(
            context, channelGuid.GetHashCode() ^ 2, ignoreIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // DND action
        var dndIntent = new Intent(context, typeof(DmNotificationActionReceiver));
        dndIntent.SetAction("DOTNETCLOUD_DM_DND");
        dndIntent.PutExtra("channelId", channelId);
        var dndPending = PendingIntent.GetBroadcast(
            context, channelGuid.GetHashCode() ^ 3, dndIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = context.Resources!
            .GetIdentifier("ic_notification", "drawable", context.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(context, MainApplication.ChannelIdDmNotifications)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .AddAction(new Notification.Action.Builder(
                null, "Reply & Join", acceptPending).Build())
            .AddAction(new Notification.Action.Builder(
                null, "Ignore", ignorePending).Build())
            .AddAction(new Notification.Action.Builder(
                null, "DND", dndPending).Build())
            .Build();

        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var notificationId = 6000 + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);
    }
}
#endif
