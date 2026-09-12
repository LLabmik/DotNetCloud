using Android.App;
using Android.Content;
using Android.Media;
using Android.Runtime;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android application entry point. Creates the MAUI app via <see cref="MauiProgram.CreateMauiApp"/>
/// and creates the Android notification channels required by the app on API 26+.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
    // ── Notification channel IDs ─────────────────────────────────────────────
    internal const string ChannelIdConnection = "chat_connection";
    internal const string ChannelIdMessages = "chat_messages";
    internal const string ChannelIdMentions = "chat_mentions";
    internal const string ChannelIdAnnouncements = "chat_announcements";
    internal const string ChannelIdUpload = "photo_upload";
    internal const string ChannelIdMediaUpload = "media_upload";
    internal const string ChannelIdCalendarReminders = "calendar_reminders";
    internal const string ChannelIdDmNotifications = "dm_notifications";

    /// <summary>
    /// Initializes a new <see cref="MainApplication"/> and registers notification channels.
    /// </summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannels();
        StartMediaAutoUploadWatcher();
        ScheduleBackgroundMediaSync();
    }

    /// <summary>
    /// Starts the in-process media auto-upload watcher as soon as the process starts.
    /// </summary>
    /// <remarks>
    /// The watcher must not depend on the UI lifecycle: <c>App.OnStart</c> only runs when an
    /// Activity starts, so a process created in the background (push message, calendar alarm)
    /// previously left auto-upload dormant until the user next opened the app — a large part of
    /// why a kill during a long upload stopped all progress for days.
    /// <c>IMediaAutoUploadService.StartAsync</c> is idempotent, so starting it here and from the
    /// navigation path is safe.
    /// </remarks>
    private void StartMediaAutoUploadWatcher()
    {
        try
        {
            if (!Preferences.Default.Get("media_upload_enabled", false))
                return;

            var watcher = Ioc.Default.GetService<IMediaAutoUploadService>();
            if (watcher is null)
                return;

            _ = watcher.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Media auto-upload start failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers the periodic <c>JobScheduler</c> job that wakes the app to
    /// back up new media while it is closed, or removes it when auto-upload is switched off.
    /// </summary>
    /// <remarks>
    /// The in-process watcher only lives as long as the process, so without a trigger nothing would
    /// run while the app is closed. This job supplies that trigger and uses <b>no</b> foreground
    /// service, so it never consumes the Android 15/16 <c>dataSync</c> 24-hour budget.
    /// Scheduling is idempotent, so calling it on every process start is safe.
    /// </remarks>
    private void ScheduleBackgroundMediaSync()
    {
        var sync = Ioc.Default.GetService<IBackgroundMediaSync>();
        if (sync is null)
            return;

        if (Preferences.Default.Get("media_upload_enabled", false))
            sync.Schedule();
        else
            sync.Cancel();
    }

    /// <inheritdoc />
    public override void OnTrimMemory(TrimMemory level)
    {
        base.OnTrimMemory(level);

        // Google Play efficiency (Feb 2027): release cached image sources when the system
        // is under memory pressure so their ImageSource references can be reclaimed. The
        // entries are disk-backed, so the next lookup re-reads from disk instead of
        // re-downloading. Best-effort — a failure here must never crash the process.
        if (level >= TrimMemory.RunningLow)
        {
            try
            {
                Ioc.Default.GetService<IThumbnailCache>()?.TrimMemory();
                Ioc.Default.GetService<IAlbumArtCache>()?.TrimMemory();
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"OnTrimMemory cache cleanup failed: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // ── Notification channel setup ───────────────────────────────────────────

    private void CreateNotificationChannels()
    {
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm is null)
            return;

        // Persistent connection indicator — low importance, no sound.
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdConnection,
            "Chat connection",
            NotificationImportance.Low)
        {
            Description = "Shown while DotNetCloud chat is connected in the background.",
            LockscreenVisibility = NotificationVisibility.Secret
        });

        // Incoming chat messages — default importance (makes sound).
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdMessages,
            "Messages",
            NotificationImportance.Default)
        {
            Description = "Notifications for new chat messages."
        });

        // @mention alerts — high importance (makes sound, shown as heads-up).
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdMentions,
            "Mentions",
            NotificationImportance.High)
        {
            Description = "Notifications when you are @mentioned in a channel."
        });

        // Announcements — high importance.
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdAnnouncements,
            "Announcements",
            NotificationImportance.High)
        {
            Description = "Important announcements from your server."
        });

        // Photo auto-upload progress — low importance, no sound.
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdUpload,
            "Photo upload",
            NotificationImportance.Low)
        {
            Description = "Progress updates for automatic photo uploads."
        });

        // Media (photo + video) auto-upload progress — low importance, no sound.
        nm.CreateNotificationChannel(new NotificationChannel(
            ChannelIdMediaUpload,
            "Media upload",
            NotificationImportance.Low)
        {
            Description = "Progress updates for automatic photo and video uploads."
        });

        // Music playback — low importance, no sound.
        nm.CreateNotificationChannel(new NotificationChannel(
            MusicPlaybackService.ChannelId,
            "Music playback",
            NotificationImportance.Low)
        {
            Description = "Shows current track and playback controls."
        });

        // Calendar event reminders — high importance (heads-up, makes sound).
        // Uses the system alarm sound for a distinct tone vs regular notifications.
        var calendarChannel = new NotificationChannel(
            ChannelIdCalendarReminders,
            "Calendar reminders",
            NotificationImportance.High)
        {
            Description = "Reminders for upcoming calendar events.",
            LockscreenVisibility = NotificationVisibility.Public
        };
        var alarmUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);
        if (alarmUri is not null)
        {
            // The Android AudioAttributes.Builder chain is annotated nullable in the .NET
            // binding but always returns non-null at runtime; apply null-forgiving.
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Alarm)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!;
            calendarChannel.SetSound(alarmUri, audioAttributes);
        }
        nm.CreateNotificationChannel(calendarChannel);

        // DM channel creation notifications — high importance (sound + vibration).
        var dmChannel = new NotificationChannel(
            ChannelIdDmNotifications,
            "Direct Messages",
            NotificationImportance.High)
        {
            Description = "Notifications when someone starts a direct message with you.",
            LockscreenVisibility = NotificationVisibility.Public
        };
        nm.CreateNotificationChannel(dmChannel);
    }
}

