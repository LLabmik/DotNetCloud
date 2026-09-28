using System.Globalization;
using Android.App;
using Android.Content;
using Android.Util;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Schedules Android <see cref="global::Android.App.AlarmManager"/> alarms for calendar event reminders.
/// Uses <c>SetExactAndAllowWhileIdle()</c> for precise timing that wakes from Doze mode.
/// Supports cancellation, boot-time reschedule, and permission-aware fallback.
/// </summary>
/// <remarks>
/// <para>
/// Alarm identity is derived from <see cref="CalendarAlarmIdentity"/> and is therefore stable across
/// app processes, so re-scheduling a reminder <em>replaces</em> its pending alarm instead of adding a
/// second one. The set of armed alarms is persisted, which makes cancellation exact (any offset, not
/// just a fixed list) and lets a resync cancel alarms that are no longer wanted.
/// </para>
/// <para>
/// Occurrences that have already been delivered are remembered, so a reminder that became due while
/// the app was not running is delivered exactly once instead of re-firing on every calendar load,
/// boot and SignalR reconnect.
/// </para>
/// </remarks>
internal sealed class CalendarReminderScheduler : ICalendarReminderScheduler
{
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

        Log.Info("DotNetCloud", $"ScheduleRemindersAsync: processing {events.Count} events at {now:O}");

        // Collect every reminder occurrence we know about, keyed by its stable identity so an alarm
        // can be mapped back to the event that produced it.
        var desired = new List<CalendarReminderRecord>();
        var sources = new Dictionary<string, CalendarEventDto>(StringComparer.Ordinal);

        foreach (var evt in events)
        {
            // Guard against JSON deserialization losing DateTimeKind
            var startUtc = DateFormatHelper.EnsureUtc(evt.StartUtc);

            // Allow events that started up to 1 hour ago — they may still have
            // pending reminders (e.g. a "5 min before" reminder for an event
            // that started 3 min ago should still fire immediately).
            if (startUtc <= now.AddHours(-1))
            {
                Log.Info("DotNetCloud", $"  Skip event {evt.Id}: start {startUtc:O} >1hr ago");
                continue;
            }

            foreach (var reminder in evt.Reminders)
            {
                // Skip email reminders — those are handled server-side.
                if (reminder.Method != ReminderMethod.Notification || reminder.MinutesBefore < 0)
                    continue;

                var record = new CalendarReminderRecord(evt.Id, startUtc, reminder.MinutesBefore);
                if (!sources.TryAdd(record.Key, evt))
                    continue;

                desired.Add(record);
            }
        }

        var planned = CalendarReminderPlanner.Plan(
            desired, CalendarReminderAlarmStore.GetDeliveredKeys(), now);

        // Cancel anything armed by an earlier pass that is no longer wanted — the event was deleted or
        // edited, the reminder was removed, or the occurrence has already been delivered. Without this
        // the stale alarm stays armed and fires as a duplicate.
        var stale = CalendarReminderPlanner.ToCancel(CalendarReminderAlarmStore.GetScheduled(), planned);
        foreach (var record in stale)
        {
            CancelAlarm(context, alarmManager, record);
            _logger.LogDebug("Cancelled stale calendar reminder alarm for event {EventId}.", record.EventId);
        }

        foreach (var alarm in planned)
        {
            ScheduleSingleAlarm(context, alarmManager, sources[alarm.Reminder.Key], alarm, canScheduleExact);
        }

        CalendarReminderAlarmStore.SetScheduled(planned.Select(alarm => alarm.Reminder));

        Log.Info("DotNetCloud", $"Scheduled {planned.Count} calendar reminder alarm(s), cancelled {stale.Count} stale.");
        _logger.LogInformation(
            "Scheduled {Count} calendar reminder alarms ({CatchUp} catch-up).",
            planned.Count, planned.Count(alarm => alarm.IsCatchUp));
    }

    /// <inheritdoc />
    public void CancelReminders(Guid eventId)
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = GetAlarmManager(context);
        if (alarmManager is null)
            return;

        // Cancel every alarm armed for this event, whatever its offset. The previous implementation
        // could only cancel a fixed list of offsets, so a reminder configured outside that list (for
        // example one week before the event) stayed armed for its whole lifetime.
        var remaining = new List<CalendarReminderRecord>();
        var cancelled = 0;

        foreach (var record in CalendarReminderAlarmStore.GetScheduled())
        {
            if (record.EventId == eventId)
            {
                CancelAlarm(context, alarmManager, record);
                cancelled++;
            }
            else
            {
                remaining.Add(record);
            }
        }

        CalendarReminderAlarmStore.SetScheduled(remaining);

        _logger.LogDebug("Cancelled {Count} alarm(s) for event {EventId}.", cancelled, eventId);
    }

    /// <inheritdoc />
    public void CancelAllReminders()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = GetAlarmManager(context);
        if (alarmManager is null)
            return;

        // Actually cancel every alarm we armed. This used to clear only a preferences key while
        // leaving the alarms armed, so each "resync" added a second alarm for every reminder it
        // re-scheduled. Delivered-occurrence records are deliberately kept, so a resync does not
        // re-alert an occurrence that was already delivered.
        foreach (var record in CalendarReminderAlarmStore.GetScheduled())
        {
            CancelAlarm(context, alarmManager, record);
        }

        CalendarReminderAlarmStore.SetScheduled([]);

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
#pragma warning disable CA1416 // guarded by the SDK check above (API < S returns true early)
        return alarmManager?.CanScheduleExactAlarms() == true;
#pragma warning restore CA1416
    }

    private void ScheduleSingleAlarm(
        Context context,
        AlarmManager alarmManager,
        CalendarEventDto evt,
        PlannedCalendarAlarm alarm,
        bool hasExactAlarmPermission)
    {
        var triggerTimeUtc = alarm.FireAtUtc;
        var minutesBefore = alarm.Reminder.MinutesBefore;
        var pendingIntent = CreateAlarmPendingIntent(context, evt, alarm.Reminder);

        var triggerMillis = new DateTimeOffset(
            DateTime.SpecifyKind(triggerTimeUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        Log.Info("DotNetCloud", $"    triggerMillis={triggerMillis} for local-time {triggerTimeUtc:O} " +
            $"(device local={DateTime.Now:O}, catchUp={alarm.IsCatchUp})");

        if (hasExactAlarmPermission && CanScheduleExactAlarms(context))
        {
            alarmManager.SetExactAndAllowWhileIdle(
                AlarmType.RtcWakeup,
                triggerMillis,
                pendingIntent);

            _logger.LogDebug(
                "Scheduled exact alarm for event {EventId} at {TriggerTime} (T-{MinutesBefore}min, catchUp={CatchUp}).",
                evt.Id, triggerTimeUtc.ToString("O"), minutesBefore, alarm.IsCatchUp);
        }
        else
        {
            alarmManager.Set(
                AlarmType.RtcWakeup,
                triggerMillis,
                pendingIntent);

            _logger.LogDebug(
                "Scheduled inexact alarm for event {EventId} at {TriggerTime} (T-{MinutesBefore}min, catchUp={CatchUp}).",
                evt.Id, triggerTimeUtc.ToString("O"), minutesBefore, alarm.IsCatchUp);
        }
    }

    /// <summary>
    /// Cancels a previously armed reminder alarm.
    /// </summary>
    /// <remarks>
    /// Only the request code and the intent filter (action + component) have to match the original —
    /// <c>Intent.filterEquals</c> ignores extras — so a bare intent is enough to find the alarm.
    /// <see cref="PendingIntentFlags.NoCreate"/> avoids creating a pending intent just to cancel it.
    /// </remarks>
    private static void CancelAlarm(Context context, AlarmManager alarmManager, CalendarReminderRecord record)
    {
        var intent = new Intent(context, typeof(CalendarAlarmReceiver));
        intent.SetAction(CalendarAlarmReceiver.ActionCalendarReminder);

        var requestCode = CalendarAlarmIdentity.RequestCode(
            record.EventId, record.OccurrenceStartUtc, record.MinutesBefore);

        var pendingIntent = PendingIntent.GetBroadcast(
            context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.NoCreate);

        if (pendingIntent is null)
            return;

        alarmManager.Cancel(pendingIntent);
        pendingIntent.Cancel();
    }

    private static PendingIntent CreateAlarmPendingIntent(
        Context context, CalendarEventDto evt, CalendarReminderRecord reminder)
    {
        var intent = CreateAlarmIntent(context, evt, reminder);

        // The request code is derived from the reminder's stable identity, so re-scheduling the same
        // reminder in a later process updates the existing alarm rather than adding another one.
        var requestCode = CalendarAlarmIdentity.RequestCode(
            reminder.EventId, reminder.OccurrenceStartUtc, reminder.MinutesBefore);

        var flags = PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent;

        // GetBroadcast is annotated nullable, but Android returns a valid PendingIntent
        // for a well-formed request; it never yields null here.
        return PendingIntent.GetBroadcast(context, requestCode, intent, flags)!;
    }

    private static Intent CreateAlarmIntent(Context context, CalendarEventDto evt, CalendarReminderRecord reminder)
    {
        var intent = new Intent(context, typeof(CalendarAlarmReceiver));
        intent.SetAction(CalendarAlarmReceiver.ActionCalendarReminder);
        intent.PutExtra(CalendarAlarmReceiver.ExtraEventId, evt.Id.ToString());
        intent.PutExtra(CalendarAlarmReceiver.ExtraTitle, evt.Title);
        intent.PutExtra(CalendarAlarmReceiver.ExtraCalendarId, evt.CalendarId.ToString());
        intent.PutExtra(CalendarAlarmReceiver.ExtraReminderMinutesBefore, reminder.MinutesBefore);

        // Carried so the notification can report the real time remaining at the moment it fires.
        intent.PutExtra(
            CalendarAlarmReceiver.ExtraEventStartUtc,
            reminder.OccurrenceStartUtc.ToString("O", CultureInfo.InvariantCulture));

        return intent;
    }
}
