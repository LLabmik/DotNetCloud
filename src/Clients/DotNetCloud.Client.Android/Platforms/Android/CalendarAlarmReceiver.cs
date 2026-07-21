using Android.App;
using Android.Content;
using Android.Media;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// BroadcastReceiver that fires when a calendar reminder alarm reaches its trigger time.
/// Displays a high-priority notification with the system alarm sound and deep-links to
/// the event detail page.
/// </summary>
/// <remarks>
/// Declared in AndroidManifest.xml as an exported-false receiver.
/// Intents are created by <see cref="CalendarReminderScheduler"/> with the following extras:
/// <list type="bullet">
///   <item><c>eventId</c> (string) — GUID of the calendar event.</item>
///   <item><c>title</c> (string) — Event title for the notification.</item>
///   <item><c>calendarId</c> (string) — GUID of the parent calendar.</item>
///   <item><c>reminderMinutesBefore</c> (int) — How many minutes before the event this reminder fires.</item>
/// </list>
/// </remarks>
[BroadcastReceiver(Exported = false)]
public sealed class CalendarAlarmReceiver : BroadcastReceiver
{
    internal const string ActionCalendarReminder = "net.dotnetcloud.client.action.CALENDAR_REMINDER";
    internal const string ExtraEventId = "eventId";
    internal const string ExtraTitle = "title";
    internal const string ExtraCalendarId = "calendarId";
    internal const string ExtraReminderMinutesBefore = "reminderMinutesBefore";

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;

        var logger = SafeResolveLogger();

        var eventId = intent.GetStringExtra(ExtraEventId);
        var title = intent.GetStringExtra(ExtraTitle);
        var calendarId = intent.GetStringExtra(ExtraCalendarId);
        var minutesBefore = intent.GetIntExtra(ExtraReminderMinutesBefore, 0);

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(title))
        {
            logger?.LogWarning("CalendarAlarmReceiver: missing required extras.");
            return;
        }

        logger?.LogInformation(
            "Calendar reminder firing: event={EventId}, title={Title}, minutesBefore={Minutes}.",
            eventId, title, minutesBefore);

        // ── Build the notification ──
        ShowReminderNotification(context, eventId, calendarId, title, minutesBefore);

        // ── Auto-reschedule next occurrence for recurring events ──
        // The CalendarReminderScheduler.RescheduleAllAsync will pick up expanded
        // recurring event occurrences from the server on next sync.
        // For immediate rescheduling, the scheduler is called from the CalendarViewModel
        // after events are loaded.
    }

    // ── Notification building ────────────────────────────────────────────────

    private static void ShowReminderNotification(
        Context context, string eventId, string? calendarId, string title, int minutesBefore)
    {
        Log.Info("DotNetCloud", $"ShowReminderNotification ENTERED: event={eventId}, title={title}, minutesBefore={minutesBefore}");

        var body = minutesBefore > 0
            ? $"Starts in {FormatMinutes(minutesBefore)}"
            : "Event is starting now";

        // ── Check POST_NOTIFICATIONS permission (Android 13+) ──
        var hasNotificationPermission = CheckNotificationPermission(context);
        if (!hasNotificationPermission)
        {
            Log.Warn("DotNetCloud", "  POST_NOTIFICATIONS not granted — notification will be suppressed by OS. " +
                "Go to Settings > Notifications to enable.");
        }

        // Deep-link intent: open MainActivity with extras to route to EventDetailPage
        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionMain);
        openIntent.AddCategory(Intent.CategoryLauncher);
        openIntent.PutExtra("eventId", eventId);
        if (!string.IsNullOrWhiteSpace(calendarId))
            openIntent.PutExtra("calendarId", calendarId);

        var pendingIntent = PendingIntent.GetActivity(
            context,
            eventId.GetHashCode(), // deterministic — replaces existing pending intent for same event
            openIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var iconRes = context.Resources!.GetIdentifier(
            "ic_notification", "drawable", context.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;
        Log.Info("DotNetCloud", $"  iconRes={iconRes}");

        Log.Info("DotNetCloud", $"  Building notification with channelId={MainApplication.ChannelIdCalendarReminders}");
        var notification = new Notification.Builder(context, MainApplication.ChannelIdCalendarReminders)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryAlarm)
            .Build();

        Log.Info("DotNetCloud", $"  Notification built, getting NotificationManager...");
        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (nm is null)
        {
            Log.Warn("DotNetCloud", "  NotificationManager is NULL — cannot show notification");
            return;
        }

        // Use a deterministic notification ID so duplicate alarms for the same event replace each other
        var notificationId = 3000 + (eventId.GetHashCode() & 0x0FFF);
        Log.Info("DotNetCloud", $"  Calling nm.Notify(id={notificationId})...");
        nm.Notify(notificationId, notification);
        Log.Info("DotNetCloud", $"  nm.Notify completed successfully");

        // ── Also check notification channel importance ──
        var channel = nm.GetNotificationChannel(MainApplication.ChannelIdCalendarReminders);
        if (channel is not null)
        {
            Log.Info("DotNetCloud", $"  Notification channel '{channel.Id}': importance={channel.Importance}, " +
                $"name='{channel.Name}', sound={channel.Sound}, canShowBadge={channel.CanShowBadge}");
            if (channel.Importance == NotificationImportance.None)
            {
                Log.Warn("DotNetCloud", "  !! Channel importance is NONE — notifications will not show. " +
                    "User must enable in Settings > Apps > DotNetCloud > Notifications > Calendar reminders.");
            }
            else if (channel.Importance < NotificationImportance.Default)
            {
                Log.Warn("DotNetCloud", $"  !! Channel importance is {channel.Importance} (LOW) — no sound/channel on lock screen");
            }
        }
        else
        {
            Log.Warn("DotNetCloud", "  Notification channel 'calendar_reminders' NOT FOUND! Did OnCreate run?");
        }
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes < 60)
            return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
        var hours = minutes / 60;
        var mins = minutes % 60;
        return mins > 0
            ? $"{hours}h {mins}m"
            : $"{hours} hour{(hours == 1 ? "" : "s")}";
    }

    private static bool CheckNotificationPermission(Context context)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.Tiramisu)
            return true; // Pre-Android 13: no runtime permission needed

        try
        {
            var permissionResult = context.CheckCallingOrSelfPermission(
                global::Android.Manifest.Permission.PostNotifications);
            var granted = permissionResult == global::Android.Content.PM.Permission.Granted;
            Log.Info("DotNetCloud", $"  POST_NOTIFICATIONS: {(granted ? "GRANTED" : "DENIED")}");
            return granted;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"  Error checking POST_NOTIFICATIONS: {ex.Message}");
            return false;
        }
    }

    private static ILogger<CalendarAlarmReceiver>? SafeResolveLogger()
    {
        try
        { return Ioc.Default.GetService<ILogger<CalendarAlarmReceiver>>(); }
        catch { return null; }
    }
}
