using Android.App;
using Android.Runtime;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android application entry point. Creates the MAUI app via <see cref="MauiProgram.CreateMauiApp"/>
/// and creates the Android notification channels required by the app on API 26+.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
    // ── Notification channel IDs ─────────────────────────────────────────────
    internal const string ChannelIdConnection    = "chat_connection";
    internal const string ChannelIdMessages      = "chat_messages";
    internal const string ChannelIdMentions      = "chat_mentions";
    internal const string ChannelIdAnnouncements = "chat_announcements";
    internal const string ChannelIdUpload        = "photo_upload";

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
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // ── Notification channel setup ───────────────────────────────────────────

    private void CreateNotificationChannels()
    {
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm is null) return;

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
    }
}

