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
    /// <summary>
    /// How far ahead a resync looks for events. Generous on purpose: the Android reminder picker tops
    /// out at one day, but a reminder created on another client (web, desktop) can be a week or more
    /// ahead of its event, and such a reminder must be armed even though the user never opened the
    /// month the event lives in.
    /// </summary>
    private static readonly TimeSpan ResyncHorizon = TimeSpan.FromDays(90);

    /// <summary>
    /// How far back a resync looks, so a reminder for an event that has just started can still catch up.
    /// </summary>
    private static readonly TimeSpan ResyncLookBack = TimeSpan.FromDays(1);

    /// <summary>
    /// Serializes the passes that touch the persisted alarm bookkeeping. Without it a boot reschedule,
    /// a SignalR reconnect and a calendar load can interleave and re-arm an occurrence that was
    /// recorded as delivered in between.
    /// </summary>
    private readonly SemaphoreSlim _passLock = new(1, 1);

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
        CalendarReminderCoverage coverage,
        CancellationToken ct = default)
    {
        // Passes are serialized: a boot reschedule, a SignalR connect/reconnect and a calendar load all
        // arrive within seconds of each other, and an interleaved pass could re-arm an occurrence that
        // the delivery broadcast had just recorded as delivered — a duplicate notification.
        await _passLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ScheduleCore(events, coverage, ct);
        }
        finally
        {
            _passLock.Release();
        }
    }

    /// <summary>Applies a single scheduling pass. The caller must hold <see cref="_passLock"/>.</summary>
    private void ScheduleCore(
        IReadOnlyList<CalendarEventDto> events,
        CalendarReminderCoverage coverage,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var context = global::Android.App.Application.Context;
        var alarmManager = CalendarAlarmScheduling.GetAlarmManager(context);
        if (alarmManager is null)
        {
            _logger.LogWarning("AlarmManager not available, cannot schedule reminders.");
            return;
        }

        if (!CalendarAlarmScheduling.CanScheduleExactAlarms(context))
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

            Log.Info("DotNetCloud", $"  Event {evt.Id}: '{evt.Title}' at {startUtc:O}, " +
                $"{evt.Reminders.Count} reminder(s)");

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

        var deliveredKeys = CalendarReminderAlarmStore.GetDeliveredKeys();
        var planned = CalendarReminderPlanner.Plan(desired, deliveredKeys, now);

        Log.Info("DotNetCloud", $"  Reminders: desired={desired.Count}, alreadyDelivered={deliveredKeys.Count}, " +
            $"planned={planned.Count}, coverage={coverage.FromUtc:O}..{coverage.ToUtc:O}");

        // Cancel anything armed by an earlier pass that is no longer wanted — the event was deleted or
        // edited, the reminder was removed, or the occurrence has already been delivered. Without this
        // the stale alarm stays armed and fires as a duplicate. Only occurrences this pass fetched are
        // governed, so it can never disarm a reminder that belongs to another slice of the calendar.
        var stale = CalendarReminderPlanner.ToCancel(
            CalendarReminderAlarmStore.GetScheduled(), planned, coverage);
        foreach (var record in stale)
        {
            CancelAlarm(context, alarmManager, record);
            _logger.LogDebug("Cancelled stale calendar reminder alarm for event {EventId}.", record.EventId);
        }

        // Re-read the delivered set: an alarm that fired while this pass was planning has recorded its
        // delivery in between, and arming that occurrence again would post a second notification.
        var deliveredSincePlanning = CalendarReminderAlarmStore.GetDeliveredKeys();
        var armed = new List<CalendarReminderRecord>(planned.Count);
        var skipped = 0;

        foreach (var alarm in planned)
        {
            if (!CalendarReminderPlanner.ShouldDeliver(deliveredSincePlanning, alarm.Reminder))
            {
                skipped++;
                _logger.LogDebug(
                    "Skipped reminder while it was being armed: event {EventId} was delivered by a running alarm.",
                    alarm.Reminder.EventId);
                continue;
            }

            ScheduleSingleAlarm(context, alarmManager, sources[alarm.Reminder.Key], alarm);
            armed.Add(alarm.Reminder);
        }

        CalendarReminderAlarmStore.SetScheduled(armed);

        Log.Info("DotNetCloud", $"Scheduled {armed.Count} calendar reminder alarm(s), cancelled {stale.Count} stale, " +
            $"skipped {skipped} already delivered.");
        _logger.LogInformation(
            "Scheduled {Count} calendar reminder alarms ({CatchUp} catch-up).",
            armed.Count, armed.Count(record => record.TriggerUtc <= now));
    }

    /// <inheritdoc />
    public void CancelReminders(Guid eventId)
    {
        _passLock.Wait();

        try
        {
            CancelRemindersCore(eventId);
        }
        finally
        {
            _passLock.Release();
        }
    }

    /// <summary>Cancels every alarm armed for one event. The caller must hold <see cref="_passLock"/>.</summary>
    private void CancelRemindersCore(Guid eventId)
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = CalendarAlarmScheduling.GetAlarmManager(context);
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
        _passLock.Wait();

        try
        {
            CancelAllRemindersCore();
        }
        finally
        {
            _passLock.Release();
        }
    }

    /// <summary>Cancels every armed alarm. The caller must hold <see cref="_passLock"/>.</summary>
    private void CancelAllRemindersCore()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = CalendarAlarmScheduling.GetAlarmManager(context);
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

            // Fetch from all calendars. The window is deliberately wider than the visible month: a
            // reminder with a long lead (created on another client) belongs to an event that can be
            // further out than any month the user has opened.
            var from = DateTime.UtcNow - ResyncLookBack;
            var to = DateTime.UtcNow + ResyncHorizon;

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
                        from: from,
                        to: to,
                        ct: ct)
                    .ConfigureAwait(false);
                allEvents.AddRange(events);
            }

            _logger.LogInformation(
                "RescheduleAllAsync: fetched {EventCount} upcoming events across {CalendarCount} calendars.",
                allEvents.Count, calendars.Count);

            // Cancelling and re-arming are one step under the pass lock, so a concurrent pass can never
            // interleave with them. The fetch stays outside the lock — it is the slow part.
            await _passLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                CancelAllRemindersCore();
                ScheduleCore(allEvents, CalendarReminderCoverage.Around(from, to), ct);
            }
            finally
            {
                _passLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RescheduleAllAsync: failed to reschedule alarms.");
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ScheduleSingleAlarm(
        Context context,
        AlarmManager alarmManager,
        CalendarEventDto evt,
        PlannedCalendarAlarm alarm)
    {
        var triggerTimeUtc = alarm.FireAtUtc;
        var minutesBefore = alarm.Reminder.MinutesBefore;
        var pendingIntent = CreateAlarmPendingIntent(context, evt, alarm.Reminder);

        var triggerMillis = new DateTimeOffset(
            DateTime.SpecifyKind(triggerTimeUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        Log.Info("DotNetCloud", $"    triggerMillis={triggerMillis} for local-time {triggerTimeUtc:O} " +
            $"(device local={DateTime.Now:O}, catchUp={alarm.IsCatchUp})");

        var exact = CalendarAlarmScheduling.Arm(context, alarmManager, triggerMillis, pendingIntent);

        _logger.LogDebug(
            "Scheduled {Precision} alarm for event {EventId} at {TriggerTime} (T-{MinutesBefore}min, catchUp={CatchUp}).",
            exact ? "exact" : "inexact",
            evt.Id, triggerTimeUtc.ToString("O"), minutesBefore, alarm.IsCatchUp);
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
        // The receiver owns the intent contract, so a snooze armed from the notification produces the
        // same pending intent this alarm used and can replace or cancel it.
        var intent = CalendarAlarmReceiver.CreateAlarmIntent(
            context, reminder, evt.Title, evt.CalendarId.ToString());

        // The request code is derived from the reminder's stable identity, so re-scheduling the same
        // reminder in a later process updates the existing alarm rather than adding another one.
        var requestCode = CalendarAlarmIdentity.RequestCode(
            reminder.EventId, reminder.OccurrenceStartUtc, reminder.MinutesBefore);

        var flags = PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent;

        // GetBroadcast is annotated nullable, but Android returns a valid PendingIntent
        // for a well-formed request; it never yields null here.
        return PendingIntent.GetBroadcast(context, requestCode, intent, flags)!;
    }
}
