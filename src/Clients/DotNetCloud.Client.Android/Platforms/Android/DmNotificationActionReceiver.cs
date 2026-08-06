using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Handles notification action intents for DM channel creation notifications.
/// Receives broadcast intents from notification action buttons (Accept, Ignore, DND)
/// and performs the corresponding API calls and navigation.
/// </summary>
[BroadcastReceiver(Exported = false)]
public sealed class DmNotificationActionReceiver : BroadcastReceiver
{
    /// <inheritdoc />
    public override async void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;

        var logger = Ioc.Default.GetService<ILogger<DmNotificationActionReceiver>>();
        var action = intent.Action;
        var channelId = intent.GetStringExtra("channelId") ?? string.Empty;

        if (!Guid.TryParse(channelId, out var channelGuid) || channelGuid == Guid.Empty)
        {
            logger?.LogWarning("DmNotificationActionReceiver: invalid channelId '{ChannelId}'", channelId);
            return;
        }

        // Cancel the notification
        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var notificationId = 6000 + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Cancel(notificationId);

        try
        {
            var serverStore = Ioc.Default.GetService<IServerConnectionStore>();
            var tokenStore = Ioc.Default.GetService<ISecureTokenStore>();
            var chatApi = Ioc.Default.GetService<IChatRestClient>();

            if (serverStore is null || tokenStore is null || chatApi is null)
            {
                logger?.LogWarning("DmNotificationActionReceiver: required services not available.");
                return;
            }

            var connection = serverStore.GetActive();
            if (connection is null)
                return;

            var token = await tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl);
            if (token is null)
                return;

            switch (action)
            {
                case "DOTNETCLOUD_DM_ACCEPT":
                    logger?.LogInformation("DM accept action for channel {ChannelId}", channelId);
                    await chatApi.AcceptDmAsync(connection.ServerBaseUrl, token, channelGuid, null, CancellationToken.None);
                    // Open the DM channel
                    OpenChatActivity(context, channelId);
                    break;

                case "DOTNETCLOUD_DM_IGNORE":
                    logger?.LogInformation("DM ignore action for channel {ChannelId}", channelId);
                    await chatApi.IgnoreDmAsync(connection.ServerBaseUrl, token, channelGuid, CancellationToken.None);
                    break;

                case "DOTNETCLOUD_DM_DND":
                    logger?.LogInformation("DM DND action for channel {ChannelId}", channelId);
                    // Set DoNotDisturb on the server
                    await chatApi.SetDoNotDisturbAsync(connection.ServerBaseUrl, token, enabled: true, CancellationToken.None);
                    break;

                default:
                    logger?.LogWarning("DmNotificationActionReceiver: unknown action '{Action}'", action);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "DmNotificationActionReceiver: action '{Action}' failed for channel {ChannelId}", action, channelId);
        }
    }

    private static void OpenChatActivity(Context context, string channelId)
    {
        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        openIntent.PutExtra("channelId", channelId);
        openIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        context.StartActivity(openIntent);
    }
}
