using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android foreground service for music playback. Holds a <see cref="PowerManager.WakeLock"/>
/// to prevent Doze from interrupting audio, and displays a media-style notification with
/// play/pause, next, and previous buttons.
/// </summary>
/// <remarks>
/// Declared in AndroidManifest.xml with <c>android:foregroundServiceType="mediaPlayback"</c>.
/// Started via <see cref="ActionStart"/> intent; stopped via <see cref="ActionStop"/>.
/// </remarks>
[Service(Name = "net.dotnetcloud.client.MusicPlaybackService",
         ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback,
         Exported = false)]
public sealed class MusicPlaybackService : Service
{
    /// <summary>Intent action that starts the foreground music service.</summary>
    public const string ActionStart = "net.dotnetcloud.client.action.START_MUSIC";

    /// <summary>Intent action that stops the foreground music service.</summary>
    public const string ActionStop = "net.dotnetcloud.client.action.STOP_MUSIC";

    /// <summary>Intent action that updates the notification (e.g. play/pause icon toggle).</summary>
    public const string ActionUpdateNotification = "net.dotnetcloud.client.action.MUSIC_UPDATE_NOTIFICATION";

    /// <summary>Intent action to toggle play/pause from notification.</summary>
    public const string ActionPlayPause = "net.dotnetcloud.client.action.MUSIC_PLAYPAUSE";

    /// <summary>Intent action to skip to next track from notification.</summary>
    public const string ActionNext = "net.dotnetcloud.client.action.MUSIC_NEXT";

    /// <summary>Intent action to go to previous track from notification.</summary>
    public const string ActionPrevious = "net.dotnetcloud.client.action.MUSIC_PREVIOUS";

    internal const int NotificationId = 1002;
    internal const string ChannelId = "music_playback";

    private PowerManager.WakeLock? _wakeLock;
    private ILogger<MusicPlaybackService>? _logger;

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            // CRITICAL: Call StartForeground IMMEDIATELY with a basic notification
            // to satisfy Android's requirement that startForegroundService() must be
            // followed by startForeground() within a few seconds. DI resolution and
            // other work happen AFTER this call to avoid the
            // ForegroundServiceDidNotStartInTimeException crash.
            try
            {
                StartForeground(NotificationId, BuildBasicNotification());
                Log.Info("DotNetCloud", "MusicPlaybackService: StartForeground succeeded.");
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"MusicPlaybackService: StartForeground failed: {ex.Message}");
            }

            _logger = SafeResolveLogger();

            if (intent?.Action == ActionStop)
            {
                _logger?.LogInformation("MusicPlaybackService stopping via intent.");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            // Handle media button actions
            var player = SafeResolvePlayer();
            switch (intent?.Action)
            {
                case ActionPlayPause:
                    if (player is null)
                        break;
                    if (player.IsPlaying)
                        player.Pause();
                    else
                        player.Resume();
                    break;
                case ActionNext:
                    player?.PlayNext();
                    break;
                case ActionPrevious:
                    player?.PlayPrevious();
                    break;
            }

            // Update the notification with full track info now that DI is available
            if (player is not null)
            {
                UpdateNotification();
            }

            AcquireWakeLock();
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"MusicPlaybackService.OnStartCommand crashed: {ex}");
        }

        return StartCommandResult.Sticky;
    }

    /// <summary>
    /// Safely resolves <see cref="ILogger{MusicPlaybackService}"/> from DI,
    /// returning null if the service provider is already disposed.
    /// </summary>
    private ILogger<MusicPlaybackService>? SafeResolveLogger()
    {
        try
        {
            return Ioc.Default.GetService<ILogger<MusicPlaybackService>>();
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Safely resolves <see cref="IMusicPlayerService"/> from DI,
    /// returning null if the service provider is already disposed.
    /// </summary>
    private static IMusicPlayerService? SafeResolvePlayer()
    {
        try
        {
            return Ioc.Default.GetService<IMusicPlayerService>();
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a minimal notification that can be constructed without any DI services.
    /// Used for the initial <c>StartForeground</c> call to satisfy the Android
    /// timeout requirement. The notification is later updated with full track info
    /// once DI services are available.
    /// </summary>
    private Notification BuildBasicNotification()
    {
        var iconRes = ApplicationContext?.Resources?.GetIdentifier(
            "ic_notification", "drawable", ApplicationContext?.PackageName) ?? 0;
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("DotNetCloud Music")
            .SetContentText("Loading...")
            .SetSmallIcon(iconRes)
            .SetOngoing(true)
            .SetShowWhen(false)
            .Build();
    }

    /// <summary>
    /// Rebuilds the notification with full track info from the music player service.
    /// Called after the initial StartForeground to upgrade the placeholder notification.
    /// </summary>
    private void UpdateNotification()
    {
        try
        {
            var player = SafeResolvePlayer();
            if (player is null)
                return;

            var notification = BuildNotification(player);
            var mgr = (NotificationManager?)GetSystemService(NotificationService);
            mgr?.Notify(NotificationId, notification);
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"MusicPlaybackService: UpdateNotification failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        _wakeLock?.Release();
        _wakeLock = null;
        _logger?.LogInformation("MusicPlaybackService destroyed; wake lock released.");
        base.OnDestroy();
    }

    private void AcquireWakeLock()
    {
        try
        {
            var pm = (PowerManager?)GetSystemService(PowerService);
            if (pm is not null)
            {
                _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "DotNetCloud::MusicWakeLock");
                _wakeLock?.Acquire();
                Log.Info("DotNetCloud", "MusicPlaybackService: wake lock acquired.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"MusicPlaybackService: wake lock failed: {ex.Message}");
        }
    }

    private Notification BuildNotification(IMusicPlayerService player)
    {
        var trackTitle = player.CurrentTrack?.Title ?? "DotNetCloud Music";
        var artistName = player.CurrentTrack?.ArtistName ?? "";

        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pendingOpenIntent = PendingIntent.GetActivity(
            this, 0, openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Previous button intent
        var prevIntent = new Intent(this, typeof(MusicPlaybackService));
        prevIntent.SetAction(ActionPrevious);
        var pendingPrev = PendingIntent.GetService(
            this, 1, prevIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Play/Pause button intent
        var ppIntent = new Intent(this, typeof(MusicPlaybackService));
        ppIntent.SetAction(ActionPlayPause);
        var pendingPp = PendingIntent.GetService(
            this, 2, ppIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // Next button intent
        var nextIntent = new Intent(this, typeof(MusicPlaybackService));
        nextIntent.SetAction(ActionNext);
        var pendingNext = PendingIntent.GetService(
            this, 3, nextIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = ApplicationContext?.Resources?.GetIdentifier(
            "ic_notification", "drawable", ApplicationContext?.PackageName) ?? 0;
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var playPauseIcon = player.IsPlaying
            ? global::Android.Resource.Drawable.IcMediaPause
            : global::Android.Resource.Drawable.IcMediaPlay;

        // CA1422: AddAction(int, string?, PendingIntent?) and Notification.Action(int, ...)
        // are deprecated from API 23 (icons on actions don't display on 23+). The APIs
        // still work correctly; the icons are simply ignored at runtime on API 23+.
#pragma warning disable CA1422
        var builder = new Notification.Builder(this, ChannelId);
        builder.SetContentTitle(trackTitle);
        builder.SetContentText(artistName);
        builder.SetSmallIcon(iconRes);
        builder.SetContentIntent(pendingOpenIntent);
        builder.SetOngoing(true);
        builder.SetShowWhen(false);
        builder.SetStyle(new Notification.MediaStyle()
            .SetShowActionsInCompactView(0, 1, 2));
        builder.AddAction(global::Android.Resource.Drawable.IcMediaPrevious, "Previous", pendingPrev);
        builder.AddAction(playPauseIcon, "Play/Pause", pendingPp);
        builder.AddAction(global::Android.Resource.Drawable.IcMediaNext, "Next", pendingNext);
        return builder.Build()!;
#pragma warning restore CA1422
    }
}
