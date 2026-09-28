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

    /// <summary>Raised by the notification's snooze actions, to deliver the reminder again shortly.</summary>
    internal const string ActionSnooze = "net.dotnetcloud.client.action.CALENDAR_REMINDER_SNOOZE";

    /// <summary>
    /// Raised by the notification's dismiss action and by its delete intent — i.e. also when the
    /// notification is swiped away. Both mean the same thing: this reminder is done.
    /// </summary>
    internal const string ActionDismiss = "net.dotnetcloud.client.action.CALENDAR_REMINDER_DISMISS";

    internal const string ExtraEventId = "eventId";
    internal const string ExtraTitle = "title";
    internal const string ExtraCalendarId = "calendarId";
    internal const string ExtraReminderMinutesBefore = "reminderMinutesBefore";
    internal const string ExtraEventStartUtc = "eventStartUtc";
    internal const string ExtraSnoozeMinutes = "snoozeMinutes";
    internal const string ExtraIsSnooze = "isSnooze";

    /// <summary>Notification title used when an alarm carries none (an intent armed before the extras existed).</summary>
    internal const string FallbackTitle = "Calendar reminder";

    /// <summary>Delay used by the snooze action.</summary>
    internal const int SnoozeMinutes = 15;

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
        var occurrence = CreateRecord(eventId, eventStartUtc, minutesBefore);

        // ── Notification actions ──
        // A swipe-away raises the delete intent and the "Dismiss" button raises the dismiss action; both
        // mean "this reminder is done". They are answered here because the system would otherwise only
        // drop the notification while anything still armed for the occurrence raised it again.
        if (intent.Action == ActionDismiss)
        {
            DismissReminder(context, logger, occurrence, eventId);
            return;
        }

        if (intent.Action == ActionSnooze)
        {
            SnoozeReminder(
                context, logger, occurrence,
                intent.GetIntExtra(ExtraSnoozeMinutes, SnoozeMinutes), title, calendarId);
            return;
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            logger?.LogWarning("CalendarAlarmReceiver: missing event id extra.");
            return;
        }

        // An alarm that carries no title is still worth raising: it is one armed before the title extra
        // existed, or one whose action intent did not copy it. Dropping the delivery would be silent.
        var displayTitle = string.IsNullOrWhiteSpace(title) ? FallbackTitle : title;

        var isSnooze = intent.GetBooleanExtra(ExtraIsSnooze, false);

        logger?.LogInformation(
            "Calendar reminder firing: event={EventId}, title={Title}, minutesBefore={Minutes}, startUtc={StartUtc}, snooze={Snooze}.",
            eventId, displayTitle, minutesBefore, eventStartUtc, isSnooze);

        // ── At most one notification per occurrence ──
        // Delivery is the point that must never duplicate. An alarm armed by an older build (whose
        // request codes were random per process, so it could neither be replaced nor cancelled) can
        // still be pending, and re-arming a due occurrence races with this broadcast. The scheduler
        // already skips delivered occurrences; this is the other half of the same guarantee. A snooze
        // is exempt: the user asked for that delivery.
        if (!isSnooze && occurrence is { } firing &&
            !CalendarReminderPlanner.ShouldDeliver(CalendarReminderAlarmStore.GetDeliveredKeys(), firing))
        {
            logger?.LogInformation(
                "Calendar reminder for event {EventId} (T-{Minutes}min) was already delivered; not posting again.",
                eventId, minutesBefore);
            Log.Info("DotNetCloud", $"  Skipping already-delivered reminder for {eventId} (T-{minutesBefore}min).");
            return;
        }

        // ── Build the notification ──
        ShowReminderNotification(
            context, occurrence, eventId, calendarId, displayTitle, minutesBefore, eventStartUtc);

        // ── Record the occurrence as delivered ──
        // The scheduler skips occurrences that are already delivered, so a reminder that became due
        // while the app was not running is delivered exactly once instead of on every resync.
        MarkDelivered(occurrence);

        // ── Auto-reschedule next occurrence for recurring events ──
        // The CalendarReminderScheduler.RescheduleAllAsync will pick up expanded
        // recurring event occurrences from the server on next sync.
        // For immediate rescheduling, the scheduler is called from the CalendarViewModel
        // after events are loaded.
    }

    // ── Notification action handlers ────────────────────────────────────

    /// <summary>
    /// Answers a dismiss — the notification action or a swipe-away. Any snooze armed for the occurrence
    /// is cancelled, so the reminder cannot reappear once the user is done with it; the occurrence stays
    /// recorded as delivered, which is what keeps the scheduler from arming it again.
    /// </summary>
    private static void DismissReminder(
        Context context, ILogger? logger, CalendarReminderRecord? occurrence, string? eventId)
    {
        if (occurrence is not { } record)
        {
            logger?.LogDebug("Calendar reminder dismissed (no occurrence identity to cancel).");
            return;
        }

        var notificationId = CalendarAlarmIdentity.NotificationId(
            record.EventId, record.OccurrenceStartUtc, record.MinutesBefore);

        // The snoozed alarm reuses the alarm's request code and action, so this one cancellation covers
        // both the reminder alarm and a pending snooze.
        var alarmManager = CalendarAlarmScheduling.GetAlarmManager(context);
        if (alarmManager is not null)
        {
            var pendingIntent = PendingIntent.GetBroadcast(
                context,
                CalendarAlarmIdentity.RequestCode(record.EventId, record.OccurrenceStartUtc, record.MinutesBefore),
                CreateAlarmIntent(context, record, title: null, calendarId: null),
                PendingIntentFlags.Immutable | PendingIntentFlags.NoCreate);

            if (pendingIntent is not null)
            {
                alarmManager.Cancel(pendingIntent);
                pendingIntent.Cancel();
            }
        }

        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        nm?.Cancel(notificationId);

        logger?.LogInformation("Calendar reminder for event {EventId} dismissed by the user.", eventId);
        Log.Info("DotNetCloud", $"  Dismissed calendar reminder for {eventId}.");
    }

    /// <summary>
    /// Re-arms the same reminder a short while later and takes the current notification down, so the
    /// snooze is a single, deliberate re-delivery rather than an alarm that keeps firing.
    /// </summary>
    private static void SnoozeReminder(
        Context context, ILogger? logger, CalendarReminderRecord? occurrence, int snoozeMinutes,
        string? title, string? calendarId)
    {
        if (occurrence is not { } record)
        {
            logger?.LogWarning("CalendarAlarmReceiver: snooze requested without an occurrence identity.");
            return;
        }

        if (snoozeMinutes <= 0)
            snoozeMinutes = SnoozeMinutes;

        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        nm?.Cancel(CalendarAlarmIdentity.NotificationId(
            record.EventId, record.OccurrenceStartUtc, record.MinutesBefore));

        var alarmManager = CalendarAlarmScheduling.GetAlarmManager(context);
        if (alarmManager is null)
        {
            logger?.LogWarning("CalendarAlarmReceiver: AlarmManager unavailable, snooze not armed.");
            return;
        }

        // Deliberate re-delivery: the reminder stays recorded as delivered, so the scheduler never arms
        // it again, and this alarm is the only thing that can raise it.
        var intent = CreateAlarmIntent(context, record, title, calendarId, isSnooze: true);
        var requestCode = CalendarAlarmIdentity.RequestCode(
            record.EventId, record.OccurrenceStartUtc, record.MinutesBefore);

        var pendingIntent = PendingIntent.GetBroadcast(
            context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

        var triggerMillis = DateTimeOffset.UtcNow.AddMinutes(snoozeMinutes).ToUnixTimeMilliseconds();
        CalendarAlarmScheduling.Arm(context, alarmManager, triggerMillis, pendingIntent);

        // Take the occurrence out of the armed-alarm bookkeeping: it is recorded as delivered, so no pass
        // will plan it again — and without this, a pass arriving before the snooze fires would cancel it.
        try
        {
            CalendarReminderAlarmStore.ForgetScheduled(record);
        }
        catch (Exception ex)
        {
            // Best effort: a failed write can only cost the snooze, never correctness of the delivery.
            Log.Warn("DotNetCloud", $"  Failed to forget the snoozed occurrence: {ex.Message}");
        }

        logger?.LogInformation(
            "Calendar reminder for event {EventId} snoozed for {Minutes} minute(s).", record.EventId, snoozeMinutes);
        Log.Info("DotNetCloud", $"  Snoozed calendar reminder for {record.EventId} by {snoozeMinutes} minute(s).");
    }

    // ── Notification building ────────────────────────────────────────────────

    private static void ShowReminderNotification(
        Context context,
        CalendarReminderRecord? occurrence,
        string eventId,
        string? calendarId,
        string title,
        int minutesBefore,
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
        var builder = new Notification.Builder(context, MainApplication.ChannelIdCalendarReminders)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryAlarm);

        // The user must be able to shut a reminder up from the shade. Without actions a swipe only drops
        // the notification, and anything still armed for that occurrence brings it straight back.
        if (occurrence is { } record)
        {
            var dismissIntent = CreateActionPendingIntent(
                context, record, ActionDismiss, snoozeMinutes: 0, title, calendarId);

            builder.SetDeleteIntent(dismissIntent);
            builder.AddAction(new Notification.Action.Builder(
                null,
                $"Snooze {SnoozeMinutes} min",
                CreateActionPendingIntent(context, record, ActionSnooze, SnoozeMinutes, title, calendarId)).Build());
            builder.AddAction(new Notification.Action.Builder(null, "Dismiss", dismissIntent).Build());
        }

        var notification = builder.Build();

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

    /// <summary>Builds the pending intent a notification action or the delete intent raises.</summary>
    /// <remarks>
    /// These intents carry the alarm action, so "Dismiss" can cancel the very pending intent the alarm
    /// was armed with (<c>Intent.filterEquals</c> ignores the extras). Their own identity therefore has
    /// to come from the request code, and that code is keyed on the action <em>and</em> its argument,
    /// because extras take no part in <c>PendingIntent</c> identity: any argument that lives in an extra
    /// would otherwise have to be baked into the action string to stay distinct.
    /// <para>
    /// The title and calendar are copied onto every action: a snooze re-arms the reminder from this
    /// intent, so without them the re-delivered alarm would have no title to show.
    /// </para>
    /// </remarks>
    private static PendingIntent CreateActionPendingIntent(
        Context context, CalendarReminderRecord record, string action, int snoozeMinutes,
        string? title, string? calendarId)
    {
        var intent = new Intent(context, typeof(CalendarAlarmReceiver));
        intent.SetAction(action);
        intent.PutExtra(ExtraEventId, record.EventId.ToString());
        intent.PutExtra(ExtraReminderMinutesBefore, record.MinutesBefore);
        intent.PutExtra(
            ExtraEventStartUtc, record.OccurrenceStartUtc.ToString("O", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(title))
            intent.PutExtra(ExtraTitle, title);

        if (!string.IsNullOrWhiteSpace(calendarId))
            intent.PutExtra(ExtraCalendarId, calendarId);

        if (snoozeMinutes > 0)
            intent.PutExtra(ExtraSnoozeMinutes, snoozeMinutes);

        return PendingIntent.GetBroadcast(
            context,
            CalendarAlarmIdentity.ActionRequestCode(
                record.EventId, record.OccurrenceStartUtc, record.MinutesBefore, $"{action}|{snoozeMinutes}"),
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
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

    /// <summary>
    /// Builds the reminder occurrence an alarm belongs to, or <c>null</c> when the intent carries
    /// neither the event id nor the occurrence start — i.e. an alarm armed by a build that predates
    /// those extras, which cannot take part in the delivered bookkeeping.
    /// </summary>
    private static CalendarReminderRecord? CreateRecord(string? eventId, DateTime? eventStartUtc, int minutesBefore)
    {
        if (eventStartUtc is not { } startUtc || !Guid.TryParse(eventId, out var parsedEventId))
            return null;

        return new CalendarReminderRecord(parsedEventId, startUtc, minutesBefore);
    }

    /// <summary>
    /// Builds the intent an alarm for a reminder occurrence is armed with. Shared with
    /// <see cref="CalendarReminderScheduler"/> and with the snooze action, so a snooze replaces the very
    /// pending intent the scheduler armed (the identity is the request code plus the intent filter).
    /// </summary>
    /// <param name="context">Context used to resolve the receiver.</param>
    /// <param name="record">The reminder occurrence.</param>
    /// <param name="title">Event title, when known.</param>
    /// <param name="calendarId">Parent calendar, when known.</param>
    /// <param name="isSnooze">Whether this delivery was asked for by the snooze action.</param>
    /// <returns>The intent to arm.</returns>
    internal static Intent CreateAlarmIntent(
        Context context, CalendarReminderRecord record, string? title, string? calendarId, bool isSnooze = false)
    {
        var intent = new Intent(context, typeof(CalendarAlarmReceiver));
        intent.SetAction(ActionCalendarReminder);
        intent.PutExtra(ExtraEventId, record.EventId.ToString());
        intent.PutExtra(ExtraReminderMinutesBefore, record.MinutesBefore);
        intent.PutExtra(
            ExtraEventStartUtc, record.OccurrenceStartUtc.ToString("O", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(title))
            intent.PutExtra(ExtraTitle, title);

        if (!string.IsNullOrWhiteSpace(calendarId))
            intent.PutExtra(ExtraCalendarId, calendarId);

        if (isSnooze)
            intent.PutExtra(ExtraIsSnooze, true);

        return intent;
    }

    /// <summary>Records the occurrence as delivered so it is never delivered again.</summary>
    private static void MarkDelivered(CalendarReminderRecord? occurrence)
    {
        if (occurrence is not { } record)
            return;

        try
        {
            CalendarReminderAlarmStore.MarkDelivered(record);
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
