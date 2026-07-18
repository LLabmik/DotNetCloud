using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android foreground service that keeps the media auto-upload loop alive when the
/// app is backgrounded. Uses <c>dataSync</c> foreground service type.
/// Started via <see cref="ActionStart"/> intent; stopped via <see cref="ActionStop"/> intent.
/// </summary>
[Service(Name = "net.dotnetcloud.client.MediaUploadForegroundService",
         ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync,
         Exported = false)]
public sealed class MediaUploadForegroundService : Service
{
    /// <summary>Intent action that starts the foreground upload service.</summary>
    public const string ActionStart = "net.dotnetcloud.client.action.START_MEDIA_UPLOAD";

    /// <summary>Intent action that stops the foreground upload service.</summary>
    public const string ActionStop = "net.dotnetcloud.client.action.STOP_MEDIA_UPLOAD";

    internal const int NotificationId = 3002;

    private ILogger<MediaUploadForegroundService>? _logger;
    private IMediaAutoUploadService? _uploadService;

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            Log.Info("DotNetCloud", "MediaUploadForegroundService.OnStartCommand entered.");
            _logger = Ioc.Default.GetService<ILogger<MediaUploadForegroundService>>();

            if (intent?.Action == ActionStop)
            {
                _logger?.LogInformation("MediaUploadForegroundService stopping via intent.");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            // Show persistent notification required for foreground services.
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
#pragma warning disable CA1416
                    StartForeground(NotificationId, BuildIdleNotification(),
                        global::Android.Content.PM.ForegroundService.TypeDataSync);
#pragma warning restore CA1416
                }
                else
                {
                    StartForeground(NotificationId, BuildIdleNotification());
                }
                Log.Info("DotNetCloud", "MediaUploadForegroundService: StartForeground succeeded.");
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"MediaUploadForegroundService: StartForeground failed: {ex.Message}");
                _logger?.LogWarning(ex, "StartForeground failed; continuing without persistent notification.");
            }

            // Start the auto-upload background loop.
            _uploadService = Ioc.Default.GetService<IMediaAutoUploadService>();
            if (_uploadService is not null)
            {
                _ = _uploadService.StartAsync();
                _logger?.LogInformation("MediaUploadForegroundService: auto-upload started.");
            }
            else
            {
                Log.Warn("DotNetCloud", "MediaUploadForegroundService: IMediaAutoUploadService not found in DI.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"MediaUploadForegroundService.OnStartCommand crashed: {ex}");
        }

        return StartCommandResult.Sticky;
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        if (_uploadService is not null)
        {
            _ = _uploadService.StopAsync();
            _logger?.LogInformation("MediaUploadForegroundService destroyed; auto-upload stopped.");
        }
        base.OnDestroy();
    }

    private static Notification BuildIdleNotification()
    {
        var appContext = global::Android.App.Application.Context;
        var openIntent = new Intent(appContext, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            appContext, 0, openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new Notification.Builder(appContext, MainApplication.ChannelIdMediaUpload)
            .SetContentTitle("Auto-upload")
            .SetContentText("Watching for new photos...")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuUpload)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .Build();
    }
}
