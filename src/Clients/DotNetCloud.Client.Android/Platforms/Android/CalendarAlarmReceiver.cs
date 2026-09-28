using System.Globalization;
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
///   <item><c>eventStartUtc</c> (string) — ISO-8601 UTC start of the occurrence, used to report the real time remaining.</item>
/// </list>
/// </remarks>
[BroadcastReceiver(Name = "net.dotnetcloud.client.CalendarAlarmReceiver", Exported = false)]
public sealed class CalendarAlarmReceiver : BroadcastReceiver
{
    internal const string ActionCalendarReminder = "net.dotnetcloud.client.action.CALENDAR_REMINDER";
    internal const string ExtraEventId = "eventId";
    internal const string ExtraTitle = "title";
    internal const string ExtraCalendarId = "calendarId";
    internal const string ExtraReminderMinutesBefore = "reminderMinutesBefore";
    internal const string ExtraEventStartUtc = "eventStartUtc";

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
        var eventStartUtc = ParseEventStartUtc(intent.GetStringExtra(ExtraEventStartUtc));

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(title))
        {
            logger?.LogWarning("CalendarAlarmReceiver: missing required extras.");
            return;
        }

        logger?.LogInformation(
            "Calendar reminder firing: event={EventId}, title={Title}, minutesBefore={Minutes}, startUtc={StartUtc}.",
            eventId, title, minutesBefore, eventStartUtc);

        // ── Build the notification ──
        ShowReminderNotification(context, eventId, calendarId, title, minutesBefore, eventStartUtc);

        // ── Record the occurrence as delivered ──
        // The scheduler skips occurrences that are already delivered, so a reminder that became due
        // while the app was not running is delivered exactly once instead of on every resync.
        MarkDelivered(eventId, eventStartUtc, minutesBefore);

        // ── Auto-reschedule next occurrence for recurring events ──
        // The CalendarReminderScheduler.RescheduleAllAsync will pick up expanded
        // recurring event occurrences from the server on next sync.
        // For immediate rescheduling, the scheduler is called from the CalendarViewModel
        // after events are loaded.
    }

    // ── Notification building ────────────────────────────────────────────────

    private static void ShowReminderNotification(
        Context context, string eventId, string? calendarId, string title, int minutesBefore,
        DateTime? eventStartUtc)
    {
        Log.Info("DotNetCloud", $"ShowReminderNotification ENTERED: event={eventId}, title={title}, minutesBefore={minutesBefore}");

        // Report the time actually remaining, not the configured offset: a catch-up delivery would
        // otherwise announce the full offset again (a "one week before" reminder said
        // "Starts in 168 hours" even when it fired late).
        var body = eventStartUtc is { } startUtc
            ? CalendarReminderText.FormatBody(startUtc, DateTime.UtcNow)
            : minutesBefore > 0
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

        int requestCode;
        int notificationId;

        if (Guid.TryParse(eventId, out var parsedEventId) && eventStartUtc is { } occurrenceStart)
        {
            requestCode = CalendarAlarmIdentity.RequestCode(parsedEventId, occurrenceStart, minutesBefore);
            notificationId = CalendarAlarmIdentity.NotificationId(parsedEventId, occurrenceStart, minutesBefore);
        }
        else
        {
            // Fired by an alarm armed before the occurrence-start extra existed.
            var fallbackHash = CalendarAlarmIdentity.StableHash(eventId);
            requestCode = fallbackHash;
            notificationId = CalendarAlarmIdentity.NotificationIdBase + (fallbackHash & 0x0FFF);
        }

        var pendingIntent = PendingIntent.GetActivity(
            context,
            requestCode, // process-stable — updates the existing pending intent instead of adding one
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

    /// <summary>
    /// Parses the occurrence-start extra, normalizing the kind to <see cref="DateTimeKind.Utc"/>
    /// (JSON/round-trip parsing of a value without a suffix would otherwise yield a local or
    /// unspecified kind and shift the computed time remaining).
    /// </summary>
    private static DateTime? ParseEventStartUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return DateTime.TryParse(
            raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }

    /// <summary>Records the occurrence as delivered so it is never delivered again.</summary>
    private static void MarkDelivered(string eventId, DateTime? eventStartUtc, int minutesBefore)
    {
        if (eventStartUtc is not { } startUtc || !Guid.TryParse(eventId, out var parsedEventId))
            return;

        try
        {
            CalendarReminderAlarmStore.MarkDelivered(
                new CalendarReminderRecord(parsedEventId, startUtc, minutesBefore));
        }
        catch (Exception ex)
        {
            // Best effort — a failed write may allow a catch-up re-delivery, which beats crashing
            // the broadcast.
            Log.Warn("DotNetCloud", $"  Failed to record the delivered reminder: {ex.Message}");
        }
    }

    private static bool CheckNotificationPermission(Context context)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.Tiramisu)
            return true; // Pre-Android 13: no runtime permission needed

        try
        {
#pragma warning disable CA1416 // guarded by the Tiramisu (API 33) SDK check above
            var permissionResult = context.CheckCallingOrSelfPermission(
                global::Android.Manifest.Permission.PostNotifications);
#pragma warning restore CA1416
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
