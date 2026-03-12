#if GOOGLEPLAY
using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
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
[Service(Exported = false)]
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

        ShowChatNotification(
            type ?? "message",
            channelId,
            title ?? "DotNetCloud",
            body ?? string.Empty);
    }

    /// <inheritdoc />
    public override async void OnNewToken(string token)
    {
        var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
        logger?.LogInformation("FCM token refreshed.");

        // Re-register with the server using the new token.
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
            logger?.LogWarning(ex, "Failed to re-register FCM token after rotation.");
        }
    }

    // ── Notification building ────────────────────────────────────────────────

    private void ShowChatNotification(string type, string? channelId, string title, string body)
    {
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
            "mention"      => MainApplication.ChannelIdMentions,
            "announcement" => MainApplication.ChannelIdAnnouncements,
            _              => MainApplication.ChannelIdMessages
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
}
#endif
