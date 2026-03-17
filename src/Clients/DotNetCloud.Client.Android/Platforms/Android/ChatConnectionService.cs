using Android.App;
using Android.Content;
using Android.OS;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android foreground service that maintains the SignalR chat connection while the
/// app is backgrounded. Holds a <see cref="PowerManager.WakeLock"/> to prevent
/// Doze mode from killing the CPU and terminating the WebSocket.
/// </summary>
/// <remarks>
/// Declared in AndroidManifest.xml with <c>android:foregroundServiceType="dataSync"</c>.
/// Started via <see cref="ActionStart"/> intent; stopped via <see cref="ActionStop"/> intent
/// or when the app returns to the foreground.
/// </remarks>
[Service(Name = "net.dotnetcloud.client.ChatConnectionService",
         ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync,
         Exported = false)]
public sealed class ChatConnectionService : Service
{
    /// <summary>Intent action that starts the foreground chat service.</summary>
    public const string ActionStart = "net.dotnetcloud.client.action.START_CHAT";

    /// <summary>Intent action that stops the foreground chat service.</summary>
    public const string ActionStop = "net.dotnetcloud.client.action.STOP_CHAT";

    internal const int NotificationId = 1001;
    internal const string ConnectionChannelId = "chat_connection";

    private PowerManager.WakeLock? _wakeLock;
    private ILogger<ChatConnectionService>? _logger;

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        _logger = Ioc.Default.GetService<ILogger<ChatConnectionService>>();

        if (intent?.Action == ActionStop)
        {
            _logger?.LogInformation("ChatConnectionService stopping via intent.");
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        // Build and show the persistent notification required for foreground services.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
#pragma warning disable CA1416 // already guarded by runtime SDK check above
            StartForeground(NotificationId, BuildNotification(),
                global::Android.Content.PM.ForegroundService.TypeDataSync);
#pragma warning restore CA1416
        }
        else
        {
            StartForeground(NotificationId, BuildNotification());
        }

        // Acquire partial wake lock to prevent the CPU from sleeping while SignalR is active.
        var pm = (PowerManager?)GetSystemService(PowerService);
        if (pm is not null)
        {
            _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "DotNetCloud::ChatWakeLock");
            _wakeLock?.Acquire();
        }

        _logger?.LogInformation("ChatConnectionService started; wake lock acquired.");

        // Ensure the SignalR connection is live.
        _ = EnsureSignalRConnectedAsync();

        return StartCommandResult.Sticky;
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        _wakeLock?.Release();
        _wakeLock = null;
        _logger?.LogInformation("ChatConnectionService destroyed; wake lock released.");
        base.OnDestroy();
    }

    private async Task EnsureSignalRConnectedAsync()
    {
        try
        {
            var signalR = Ioc.Default.GetService<IChatSignalRClient>();
            var serverStore = Ioc.Default.GetService<IServerConnectionStore>();
            var tokenStore = Ioc.Default.GetService<ISecureTokenStore>();

            if (signalR is null || serverStore is null || tokenStore is null) return;

            var connection = serverStore.GetActive();
            if (connection is null) return;

            var token = await tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl).ConfigureAwait(false);
            if (token is null) return;

            if (signalR is SignalRChatClient androidSignalR)
                await androidSignalR.ConnectAsync(connection.ServerBaseUrl, token).ConfigureAwait(false);
            else
                await signalR.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure SignalR connection in foreground service.");
        }
    }

    private Notification BuildNotification()
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            this, 0, openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = ApplicationContext!.Resources!
            .GetIdentifier("ic_notification", "drawable", ApplicationContext.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        return new Notification.Builder(this, ConnectionChannelId)
            .SetContentTitle("DotNetCloud")
            .SetContentText("Chat is connected")
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetShowWhen(false)
            .Build();
    }
}
