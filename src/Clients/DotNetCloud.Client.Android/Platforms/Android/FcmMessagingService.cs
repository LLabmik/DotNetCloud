#if GOOGLEPLAY
using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Services;
using Firebase.Messaging;
using Microsoft.Extensions.Logging;
using static DotNetCloud.Client.Android.Services.AppBadgeManager;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Receives incoming Firebase Cloud Messaging (FCM) push notifications and
/// translates them into Android system notifications with deep-link tap handlers.
/// </summary>
/// <remarks>
/// The server sends FCM data messages with the following keys:
/// <list type="bullet">
///   <item><c>type</c> — "message", "mention", or "announcement"</item>
///   <item><c>channelId</c> — target channel GUID</item>
///   <item><c>title</c> — notification title</item>
///   <item><c>body</c> — notification body text</item>
/// </list>
/// The service also updates the FCM registration token with the server whenever
/// Firebase rotates it.
/// </remarks>
[Service(Name = "net.dotnetcloud.client.FcmMessagingService", Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class FcmMessagingService : FirebaseMessagingService
{
    private const int BaseNotificationId = 2000;

    /// <inheritdoc />
    public override void OnMessageReceived(RemoteMessage message)
    {
        var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();

        var data = message.Data;
        if (data is null || data.Count == 0)
        {
            logger?.LogDebug("FCM message received with no data payload.");
            return;
        }

        data.TryGetValue("type", out var type);
        data.TryGetValue("channelId", out var channelId);
        data.TryGetValue("title", out var title);
        data.TryGetValue("body", out var body);

        logger?.LogInformation(
            "FCM push received: type={Type}, channelId={ChannelId}.",
            type, channelId);

        // Calendar reminders use a dedicated notification handler
        if (string.Equals(type, "calendar_reminder", StringComparison.OrdinalIgnoreCase))
        {
            ShowCalendarReminderNotification(
                eventId: channelId ?? string.Empty,
                title: title ?? "Calendar reminder",
                body: body ?? string.Empty);
            return;
        }

        // DM channel created — high-priority notification with action buttons
        if (string.Equals(type, "dm_channel_created", StringComparison.OrdinalIgnoreCase))
        {
            ShowDmChannelNotification(
                channelId ?? string.Empty,
                title ?? "DotNetCloud",
                body ?? string.Empty,
                data);
            return;
        }

        // Calendar event changes (created/updated/deleted from Blazor UI) trigger a refresh
        if (string.Equals(type, "calendar_event", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation("FCM calendar_event push received — signaling calendar to refresh.");
            WeakReferenceMessenger.Default.Send(new CalendarEventChangedMessage());
            return;
        }

        ShowChatNotification(
            type ?? "message",
            channelId,
            title ?? "DotNetCloud",
            body ?? string.Empty);
    }

    /// <inheritdoc />
    [Obsolete("Base FirebaseMessagingService.OnNewToken is deprecated by the SDK; still required to receive token refresh callbacks.")]
    public override async void OnNewToken(string token)
    {
        var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
        logger?.LogInformation("FCM token refreshed.");

        // Re-register with the server using the new token.
        try
        {
            var serverStore = Ioc.Default.GetService<IServerConnectionStore>();
            var tokenStore = Ioc.Default.GetService<ISecureTokenStore>();
            var pushService = Ioc.Default.GetService<IPushNotificationService>();

            if (serverStore is null || tokenStore is null || pushService is null)
                return;

            var connection = serverStore.GetActive();
            if (connection is null)
                return;

            var accessToken = await tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl).ConfigureAwait(false);
            if (accessToken is null)
                return;

            await pushService.RegisterAsync(connection.ServerBaseUrl, accessToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to re-register FCM token after rotation.");
        }
    }

    // ── Notification building ────────────────────────────────────────────────

    private void ShowChatNotification(string type, string? channelId, string title, string body)
    {
        // ── Foreground check: suppress if app is visible ──
        try
        {
            var foreground = Ioc.Default.GetService<IAppForegroundService>();
            if (foreground?.IsInForeground == true)
            {
                var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
                logger?.LogDebug("App in foreground; suppressing notification for channel {ChannelId}.", channelId);
                return;
            }
        }
        catch { /* Best effort — post notification if we can't check */ }

        // ── Mute check: suppress if channel is muted ──
        try
        {
            if (Guid.TryParse(channelId, out var chId) && chId != Guid.Empty)
            {
                var muteState = Ioc.Default.GetService<IChannelMuteStateService>();
                if (muteState?.IsMuted(chId) == true)
                {
                    var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
                    logger?.LogDebug("Channel {ChannelId} is muted; suppressing notification.", channelId);
                    return;
                }
            }
        }
        catch { /* Best effort */ }

        var channelGuid = Guid.TryParse(channelId, out var g) ? g : Guid.Empty;

        // Deep-link intent: open MainActivity and route to the specified channel.
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        if (channelGuid != Guid.Empty)
            openIntent.PutExtra("channelId", channelGuid.ToString());

        var pendingIntent = PendingIntent.GetActivity(
            this,
            channelGuid.GetHashCode(),
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var notificationChannelId = type switch
        {
            "mention" => MainApplication.ChannelIdMentions,
            "announcement" => MainApplication.ChannelIdAnnouncements,
            _ => MainApplication.ChannelIdMessages
        };

        var iconRes = ApplicationContext!.Resources!
            .GetIdentifier("ic_notification", "drawable", ApplicationContext.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(this, notificationChannelId)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetGroup($"dnc_chat_{channelId}")
            .WithBadgeCount(this)
            .Build();

        var nm = (NotificationManager?)GetSystemService(NotificationService);
        var notificationId = BaseNotificationId + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);
    }

    private void ShowDmChannelNotification(string channelId, string title, string body, IDictionary<string, string> data)
    {
        var channelGuid = Guid.TryParse(channelId, out var g) ? g : Guid.Empty;

        // Deep-link intent: open MainActivity and route to the DM channel.
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        if (channelGuid != Guid.Empty)
            openIntent.PutExtra("channelId", channelGuid.ToString());

        var pendingIntent = PendingIntent.GetActivity(
            this,
            channelGuid.GetHashCode(),
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Accept action: open chat directly
        var acceptIntent = new Intent(this, typeof(DmNotificationActionReceiver));
        acceptIntent.SetAction("DOTNETCLOUD_DM_ACCEPT");
        acceptIntent.PutExtra("channelId", channelId);
        acceptIntent.PutExtra("initiatorName", data.TryGetValue("initiatorName", out var iname) ? iname : "Someone");
        var acceptPending = PendingIntent.GetBroadcast(
            this, channelGuid.GetHashCode() ^ 1, acceptIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Ignore action
        var ignoreIntent = new Intent(this, typeof(DmNotificationActionReceiver));
        ignoreIntent.SetAction("DOTNETCLOUD_DM_IGNORE");
        ignoreIntent.PutExtra("channelId", channelId);
        var ignorePending = PendingIntent.GetBroadcast(
            this, channelGuid.GetHashCode() ^ 2, ignoreIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // DND action
        var dndIntent = new Intent(this, typeof(DmNotificationActionReceiver));
        dndIntent.SetAction("DOTNETCLOUD_DM_DND");
        dndIntent.PutExtra("channelId", channelId);
        var dndPending = PendingIntent.GetBroadcast(
            this, channelGuid.GetHashCode() ^ 3, dndIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = ApplicationContext!.Resources!
            .GetIdentifier("ic_notification", "drawable", ApplicationContext.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(this, MainApplication.ChannelIdDmNotifications)
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
            .WithBadgeCount(this)
            .Build();

        var nm = (NotificationManager?)GetSystemService(NotificationService);
        var notificationId = 6000 + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);
    }

    private void ShowCalendarReminderNotification(string eventId, string title, string body)
    {
        // Deep-link intent: open MainActivity with eventId for EventDetailPage
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        openIntent.PutExtra("eventId", eventId);

        var pendingIntent = PendingIntent.GetActivity(
            this,
            eventId.GetHashCode(),
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = ApplicationContext!.Resources!
            .GetIdentifier("ic_notification", "drawable", ApplicationContext.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new Notification.Builder(this, MainApplication.ChannelIdCalendarReminders)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryAlarm)
            .Build();

        var nm = (NotificationManager?)GetSystemService(NotificationService);
        var notificationId = 4000 + (eventId.GetHashCode() & 0x0FFF);
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
}
#endif
