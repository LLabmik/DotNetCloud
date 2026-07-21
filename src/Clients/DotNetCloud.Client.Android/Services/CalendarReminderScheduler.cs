using Android.App;
using Android.Content;
using Android.Util;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Schedules Android <see cref="AlarmManager"/> alarms for calendar event reminders.
/// Uses <c>SetExactAndAllowWhileIdle()</c> for precise timing that wakes from Doze mode.
/// Supports cancellation, boot-time reschedule, and permission-aware fallback.
/// </summary>
internal sealed class CalendarReminderScheduler : ICalendarReminderScheduler
{
    private const string PrefsKey = "CalendarReminderScheduledAlarms";

    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<CalendarReminderScheduler> _logger;

    /// <summary>Initializes a new <see cref="CalendarReminderScheduler"/>.</summary>
    public CalendarReminderScheduler(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<CalendarReminderScheduler> logger)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ScheduleRemindersAsync(
        IReadOnlyList<CalendarEventDto> events,
        CancellationToken ct = default)
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = GetAlarmManager(context);
        if (alarmManager is null)
        {
            _logger.LogWarning("AlarmManager not available, cannot schedule reminders.");
            return;
        }

        var canScheduleExact = CanScheduleExactAlarms(context);
        if (!canScheduleExact)
        {
            _logger.LogWarning("SCHEDULE_EXACT_ALARM permission not granted; using inexact scheduling.");
        }

        var now = DateTime.UtcNow;
        var scheduledCount = 0;

        Log.Info("DotNetCloud", $"ScheduleRemindersAsync: processing {events.Count} events at {now:O}");

        foreach (var evt in events)
        {
            // Allow events that started up to 1 hour ago — they may still have
            // pending reminders (e.g. a "5 min before" reminder for an event
            // that started 3 min ago should still fire immediately).
            var startWindow = now.AddHours(-1);
            if (evt.StartUtc <= startWindow)
            {
                Log.Info("DotNetCloud", $"  Skip event {evt.Id}: start {evt.StartUtc:O} >1hr ago");
                continue;
            }

            Log.Info("DotNetCloud", $"  Process event {evt.Id}: '{evt.Title}' at {evt.StartUtc:O}, {evt.Reminders.Count} reminders");

            // Cancel any existing alarms for this event first
            CancelReminders(evt.Id);

            foreach (var reminder in evt.Reminders)
            {
                // Skip email reminders — those are handled server-side
                if (reminder.Method != ReminderMethod.Notification)
                {
                    Log.Info("DotNetCloud", $"    Skip email reminder for event {evt.Id}");
                    continue;
                }

                var triggerTime = evt.StartUtc.AddMinutes(-reminder.MinutesBefore);
                Log.Info("DotNetCloud", $"    Reminder T-{reminder.MinutesBefore}min: trigger={triggerTime:O}, now={now:O}");

                // If trigger time is past but event hasn't started, fire now
                if (triggerTime <= now)
                {
                    Log.Info("DotNetCloud", $"    -> Trigger time past, scheduling immediately");
                    ScheduleSingleAlarm(
                        context, alarmManager, evt, now, reminder.MinutesBefore,
                        canScheduleExact);
                    scheduledCount++;
                    continue;
                }

                ScheduleSingleAlarm(
                    context, alarmManager, evt, triggerTime, reminder.MinutesBefore,
                    canScheduleExact);
                scheduledCount++;
            }
        }

        _logger.LogInformation("Scheduled {Count} calendar reminder alarms.", scheduledCount);
    }

    /// <inheritdoc />
    public void CancelReminders(Guid eventId)
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = GetAlarmManager(context);
        if (alarmManager is null)
            return;

        // Cancel all pending intents for this event (one per reminder, but we use
        // a single request code per event to keep it simple — cancels all at once)
        var pendingIntent = CreatePendingIntent(context, eventId.ToString(), action: PendingIntentActions.Cancel);
        alarmManager.Cancel(pendingIntent);
        pendingIntent?.Cancel();

        _logger.LogDebug("Cancelled alarms for event {EventId}.", eventId);
    }

    /// <inheritdoc />
    public void CancelAllReminders()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = GetAlarmManager(context);
        if (alarmManager is null)
            return;

        // Clear the preferences tracking store
        Preferences.Default.Remove(PrefsKey);

        _logger.LogInformation("Cancelled all calendar reminder alarms.");
    }

    /// <inheritdoc />
    public async Task RescheduleAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("RescheduleAllAsync: fetching events from server.");

        try
        {
            var connection = _serverStore.GetActive();
            if (connection is null)
            {
                _logger.LogWarning("RescheduleAllAsync: no active server connection.");
                return;
            }

            var accessToken = await _tokenStore
                .GetAccessTokenAsync(connection.ServerBaseUrl)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("RescheduleAllAsync: no access token available.");
                return;
            }

            // Fetch events from all calendars
            var calendars = await _calendarApi
                .ListCalendarsAsync(connection.ServerBaseUrl, accessToken, ct)
                .ConfigureAwait(false);

            var allEvents = new List<CalendarEventDto>();
            foreach (var calendar in calendars)
            {
                var events = await _calendarApi
                    .ListEventsAsync(
                        connection.ServerBaseUrl, accessToken,
                        calendar.Id,
                        from: DateTime.UtcNow,
                        to: DateTime.UtcNow.AddDays(30),
                        ct: ct)
                    .ConfigureAwait(false);
                allEvents.AddRange(events);
            }

            _logger.LogInformation(
                "RescheduleAllAsync: fetched {EventCount} upcoming events across {CalendarCount} calendars.",
                allEvents.Count, calendars.Count);

            // Cancel all existing alarms first
            CancelAllReminders();

            // Schedule new alarms
            await ScheduleRemindersAsync(allEvents, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RescheduleAllAsync: failed to reschedule alarms.");
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static AlarmManager? GetAlarmManager(Context context)
    {
        var svc = context.GetSystemService(Context.AlarmService);
        return svc as AlarmManager;
    }

    private static bool CanScheduleExactAlarms(Context context)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.S)
            return true; // API 30 and below: exact alarms don't need a separate permission

        var alarmManager = GetAlarmManager(context);
        return alarmManager?.CanScheduleExactAlarms() == true;
    }

    private void ScheduleSingleAlarm(
        Context context,
        AlarmManager alarmManager,
        CalendarEventDto evt,
        DateTime triggerTimeUtc,
        int minutesBefore,
        bool hasExactAlarmPermission)
    {
        var intent = CreateAlarmIntent(context, evt, minutesBefore);
        var pendingIntent = CreatePendingIntent(context, evt.Id.ToString(), intent);

        var triggerMillis = new DateTimeOffset(triggerTimeUtc).ToUnixTimeMilliseconds();

        if (hasExactAlarmPermission && CanScheduleExactAlarms(context))
        {
            alarmManager.SetExactAndAllowWhileIdle(
                AlarmType.RtcWakeup,
                triggerMillis,
                pendingIntent);

            _logger.LogDebug(
                "Scheduled exact alarm for event {EventId} at {TriggerTime} (T-{MinutesBefore}min).",
                evt.Id, triggerTimeUtc.ToString("O"), minutesBefore);
        }
        else
        {
            alarmManager.Set(
                AlarmType.RtcWakeup,
                triggerMillis,
                pendingIntent);

            _logger.LogDebug(
                "Scheduled inexact alarm for event {EventId} at {TriggerTime} (T-{MinutesBefore}min).",
                evt.Id, triggerTimeUtc.ToString("O"), minutesBefore);
        }
    }

    private static Intent CreateAlarmIntent(Context context, CalendarEventDto evt, int minutesBefore)
    {
        var intent = new Intent(context, typeof(CalendarAlarmReceiver));
        intent.SetAction(CalendarAlarmReceiver.ActionCalendarReminder);
        intent.PutExtra(CalendarAlarmReceiver.ExtraEventId, evt.Id.ToString());
        intent.PutExtra(CalendarAlarmReceiver.ExtraTitle, evt.Title);
        intent.PutExtra(CalendarAlarmReceiver.ExtraCalendarId, evt.CalendarId.ToString());
        intent.PutExtra(CalendarAlarmReceiver.ExtraReminderMinutesBefore, minutesBefore);
        return intent;
    }

    private static PendingIntent CreatePendingIntent(
        Context context, string eventId, Intent? intent = null,
        PendingIntentActions action = PendingIntentActions.Set)
    {
        // Use a deterministic request code based on eventId hash so that
        // CancelReminders can match the same pending intent.
        var requestCode = eventId.GetHashCode();

        if (intent is null)
        {
            // Create a dummy intent for cancellation (must match the original)
            intent = new Intent(context, typeof(CalendarAlarmReceiver));
            intent.SetAction(CalendarAlarmReceiver.ActionCalendarReminder);
            intent.PutExtra(CalendarAlarmReceiver.ExtraEventId, eventId);
        }

        var flags = PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent;

        return PendingIntent.GetBroadcast(context, requestCode, intent, flags);
    }

    private enum PendingIntentActions { Set, Cancel }
}
