using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Schedules and manages Android <see cref="global::Android.App.AlarmManager"/> alarms for
/// calendar event reminders. Supports both one-time and recurring events,
/// boot-time rescheduling, and cancellation.
/// </summary>
public interface ICalendarReminderScheduler
{
    /// <summary>
    /// Schedules <see cref="global::Android.App.AlarmManager"/> alarms for all future reminders on the
    /// given events. Existing alarms for the same event IDs are replaced.
    /// </summary>
    /// <param name="events">The events whose reminders should be scheduled.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ScheduleRemindersAsync(
        IReadOnlyList<CalendarEventDto> events,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels all pending alarms for a specific event.
    /// </summary>
    /// <param name="eventId">The event whose alarms to cancel.</param>
    void CancelReminders(Guid eventId);

    /// <summary>
    /// Cancels all pending calendar reminder alarms.
    /// </summary>
    void CancelAllReminders();

    /// <summary>
    /// Reschedules all calendar reminders from scratch after device reboot.
    /// Re-syncs events from the server and schedules alarms for all future reminders.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RescheduleAllAsync(CancellationToken ct = default);
}
