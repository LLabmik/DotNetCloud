using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android background service that maintains the SignalR chat connection while the app is
/// alive. Does NOT hold a WakeLock — relies on FCM push notifications to wake the device for
/// real-time message delivery during Doze.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <b>not</b> a foreground service. It used to be promoted with a <c>dataSync</c>
/// type, but Android 15/16 caps <c>dataSync</c> foreground services to a rolling 24-hour budget;
/// once exhausted the platform throws <c>ForegroundServiceDidNotStopInTimeException</c>, which —
/// combined with the sticky restart below — crash-looped the app and burned the whole daily
/// allowance. Promotion is therefore not used at all, and no foreground-service type is declared
/// for this component, so the failure cannot recur.
/// </para>
/// <para>
/// Background message delivery does not depend on this service: FCM (googleplay flavour) and
/// UnifiedPush (fdroid flavour) already deliver notifications while the app is not running, so
/// losing the connection when the process is reclaimed is not a functional regression.
/// </para>
/// <para>Started via <see cref="ActionStart"/>; stopped via <see cref="ActionStop"/>.</para>
/// </remarks>
[Service(Name = "net.dotnetcloud.client.ChatConnectionService", Exported = false)]
public sealed class ChatConnectionService : Service
{
    /// <summary>Intent action that starts the foreground chat service.</summary>
    public const string ActionStart = "net.dotnetcloud.client.action.START_CHAT";

    /// <summary>Intent action that stops the foreground chat service.</summary>
    public const string ActionStop = "net.dotnetcloud.client.action.STOP_CHAT";

    internal const int NotificationId = 1001;
    internal const string ConnectionChannelId = "chat_connection";

    private ILogger<ChatConnectionService>? _logger;

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            Log.Info("DotNetCloud", "ChatConnectionService.OnStartCommand entered.");
            _logger = Ioc.Default.GetService<ILogger<ChatConnectionService>>();

            if (intent?.Action == ActionStop)
            {
                _logger?.LogInformation("ChatConnectionService stopping via intent.");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            // No foreground promotion — see the class remarks. Promoting this service with a
            // dataSync type would spend the Android 15/16 24-hour budget and can crash-loop the app.
            Log.Info("DotNetCloud", "ChatConnectionService: running without foreground promotion (push handles background delivery).");
            _logger?.LogInformation("ChatConnectionService started (no wake lock, no foreground promotion).");

            // Ensure the SignalR connection is live.
            _ = EnsureSignalRConnectedAsync();
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"ChatConnectionService.OnStartCommand crashed: {ex}");
        }

        return StartCommandResult.Sticky;
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        _logger?.LogInformation("ChatConnectionService destroyed.");
        base.OnDestroy();
    }

    private async Task EnsureSignalRConnectedAsync()
    {
        try
        {
            Log.Info("DotNetCloud", "ChatConnectionService.EnsureSignalRConnectedAsync: resolving services...");
            var signalR = Ioc.Default.GetService<IChatSignalRClient>();
            var serverStore = Ioc.Default.GetService<IServerConnectionStore>();
            var tokenStore = Ioc.Default.GetService<ISecureTokenStore>();

            Log.Info("DotNetCloud", $"EnsureSignalRConnectedAsync: signalR={(signalR is not null)}, serverStore={(serverStore is not null)}, tokenStore={(tokenStore is not null)}");

            if (signalR is null || serverStore is null || tokenStore is null)
            {
                Log.Warn("DotNetCloud", "EnsureSignalRConnectedAsync: one or more services null, cannot connect.");
                return;
            }

            var connection = serverStore.GetActive();
            Log.Info("DotNetCloud", $"EnsureSignalRConnectedAsync: connection={(connection is not null)}");
            if (connection is null)
                return;

            var token = await tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl).ConfigureAwait(false);
            Log.Info("DotNetCloud", $"EnsureSignalRConnectedAsync: token={(token is not null)}, length={(token?.Length ?? 0)}");
            if (token is null)
                return;

            Log.Info("DotNetCloud", $"EnsureSignalRConnectedAsync: connecting to SignalR at {connection.ServerBaseUrl}...");
            if (signalR is SignalRChatClient androidSignalR)
                await androidSignalR.ConnectAsync(connection.ServerBaseUrl).ConfigureAwait(false);
            else
                await signalR.ConnectAsync().ConfigureAwait(false);

            Log.Info("DotNetCloud", "EnsureSignalRConnectedAsync: SignalR connected successfully!");
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"EnsureSignalRConnectedAsync failed: {ex}");
            _logger?.LogWarning(ex, "Failed to ensure SignalR connection in chat service.");
        }
    }
}
