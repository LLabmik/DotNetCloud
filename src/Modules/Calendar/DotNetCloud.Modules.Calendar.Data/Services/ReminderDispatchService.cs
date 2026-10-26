using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Background service that periodically scans for due calendar reminders and raises a platform
/// reminder event for each one. Handles both single-occurrence and recurring events, using
/// <see cref="ReminderLog"/> to prevent duplicates.
/// </summary>
/// <remarks>
/// The service decides <i>when</i> a reminder is due; it does not deliver anything. The core's
/// notification pipeline turns the raised event into the in-app notification plus the real-time and
/// push/email deliveries.
/// </remarks>
public sealed class ReminderDispatchService : BackgroundService
{
    /// <summary>The id this module reports on the platform event it raises.</summary>
    internal const string ModuleId = "dotnetcloud.calendar";

    /// <summary>How often the service checks for due reminders.</summary>
    internal static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Lower bound of the look-ahead window, covering the common short-lead reminders.
    /// </summary>
    internal static readonly TimeSpan MinimumLookAheadWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Upper bound of the look-ahead window. A single extreme offset must not make every scan load
    /// the whole calendar.
    /// </summary>
    internal static readonly TimeSpan MaximumLookAheadWindow = TimeSpan.FromDays(366);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker? _tracker;
    private readonly ILogger<ReminderDispatchService> _logger;

    private const string ServiceName = "Calendar Reminder Dispatch";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderDispatchService"/> class.
    /// </summary>
    public ReminderDispatchService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderDispatchService> logger,
        IBackgroundServiceTracker? tracker = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderDispatchService started. Scan interval: {Interval}s", ScanInterval.TotalSeconds);

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(ScanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _tracker.RunTrackedAsync(
                    ServiceName,
                    () => ScanAndDispatchAsync(stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reminder scan cycle");
            }
        }

        _logger.LogInformation("ReminderDispatchService stopped");
    }

    /// <summary>
    /// Single scan cycle: finds due reminders and dispatches them.
    /// Visible for testing.
    /// </summary>
    internal async Task ScanAndDispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPlatformEventPublisher>();
        var recurrenceEngine = scope.ServiceProvider.GetRequiredService<IRecurrenceEngine>();

        var now = DateTime.UtcNow;
        var lookAhead = now.Add(await ResolveLookAheadWindowAsync(db, cancellationToken));

        // 1. Process non-recurring events with upcoming reminders.
        await DispatchSingleEventRemindersAsync(db, publisher, now, lookAhead, cancellationToken);

        // 2. Process recurring events with upcoming reminders.
        await DispatchRecurringEventRemindersAsync(db, publisher, recurrenceEngine, now, lookAhead, cancellationToken);
    }

    /// <summary>
    /// Resolves how far ahead to look for events with reminders that may now be due: the largest
    /// configured minutes-before offset, floored at <see cref="MinimumLookAheadWindow"/> and capped at
    /// <see cref="MaximumLookAheadWindow"/>.
    /// </summary>
    /// <remarks>
    /// A reminder is due when <c>StartUtc - MinutesBefore &lt;= now</c>, so the candidate events must be
    /// loaded from at least that far ahead. With a fixed 24-hour window an event further away than the
    /// window was never even considered, so a long-lead reminder could only fire once the event came
    /// within 24 hours — a one-week reminder therefore fired roughly six days late. Deriving the window
    /// from the data keeps every offset correct without hard-coding a maximum lead time. Widening the
    /// window is safe: <see cref="ReminderLog"/> still guarantees each reminder fires only once.
    /// </remarks>
    internal static async Task<TimeSpan> ResolveLookAheadWindowAsync(
        CalendarDbContext db,
        CancellationToken cancellationToken)
    {
        var maxMinutesBefore = await db.EventReminders
            .MaxAsync(r => (int?)r.MinutesBefore, cancellationToken)
            .ConfigureAwait(false);

        var window = TimeSpan.FromMinutes(
            Math.Max(maxMinutesBefore ?? 0, MinimumLookAheadWindow.TotalMinutes));
        return window > MaximumLookAheadWindow ? MaximumLookAheadWindow : window;
    }

    private async Task DispatchSingleEventRemindersAsync(
        CalendarDbContext db,
        IPlatformEventPublisher publisher,
        DateTime now,
        DateTime lookAhead,
        CancellationToken cancellationToken)
    {
        // Find non-recurring events that start within the look-ahead window
        // and have at least one reminder configured.
        var events = await db.CalendarEvents
            .Include(e => e.Reminders)
            .Include(e => e.Calendar)
            .Where(e => !e.IsDeleted
                && e.RecurrenceRule == null
                && e.RecurringEventId == null
                && e.Reminders.Count > 0
                && e.StartUtc <= lookAhead
                && e.StartUtc >= now.AddDays(-1)) // Don't process very old events
            .ToListAsync(cancellationToken);

        foreach (var evt in events)
        {
            foreach (var reminder in evt.Reminders)
            {
                // Dispatch every due reminder. Delivery routing (push vs email)
                // is decided downstream by the event consumer based on Method.
                var triggerTime = evt.StartUtc.AddMinutes(-reminder.MinutesBefore);

                // Is it due now?
                if (triggerTime > now)
                {
                    continue;
                }

                // Has it already been dispatched?
                var alreadyFired = await db.ReminderLogs
                    .AnyAsync(l => l.ReminderId == reminder.Id
                        && l.OccurrenceStartUtc == evt.StartUtc
                        && l.Success,
                        cancellationToken);

                if (alreadyFired)
                {
                    continue;
                }

                await DispatchReminderAsync(db, publisher, evt, reminder, evt.StartUtc, cancellationToken);
            }
        }
    }

    private async Task DispatchRecurringEventRemindersAsync(
        CalendarDbContext db,
        IPlatformEventPublisher publisher,
        IRecurrenceEngine recurrenceEngine,
        DateTime now,
        DateTime lookAhead,
        CancellationToken cancellationToken)
    {
        // Find recurring master events that have reminders.
        var masters = await db.CalendarEvents
            .Include(e => e.Reminders)
            .Include(e => e.Exceptions)
            .Include(e => e.Calendar)
            .Where(e => !e.IsDeleted
                && e.RecurrenceRule != null
                && e.RecurringEventId == null
                && e.Reminders.Count > 0)
            .ToListAsync(cancellationToken);

        foreach (var master in masters)
        {
            var maxReminderMinutes = master.Reminders.Max(r => r.MinutesBefore);
            var duration = master.EndUtc - master.StartUtc;
            var excludedDates = new HashSet<DateTime>(
                master.Exceptions
                    .Where(ex => ex.OriginalStartUtc.HasValue)
                    .Select(ex => ex.OriginalStartUtc!.Value));

            // Expand occurrences from "now minus largest reminder window" to lookAhead
            var expandFrom = now.AddMinutes(-maxReminderMinutes);

            try
            {
                var occurrences = recurrenceEngine.Expand(
                    master.RecurrenceRule!,
                    master.StartUtc,
                    duration,
                    expandFrom,
                    lookAhead,
                    excludedDates,
                    maxOccurrences: 100);

                foreach (var occ in occurrences)
                {
                    foreach (var reminder in master.Reminders)
                    {
                        // Dispatch every due reminder. Delivery routing (push vs email)
                        // is decided downstream by the event consumer based on Method.
                        var triggerTime = occ.StartUtc.AddMinutes(-reminder.MinutesBefore);

                        if (triggerTime > now)
                        {
                            continue;
                        }

                        var alreadyFired = await db.ReminderLogs
                            .AnyAsync(l => l.ReminderId == reminder.Id
                                && l.OccurrenceStartUtc == occ.StartUtc
                                && l.Success,
                                cancellationToken);

                        if (alreadyFired)
                        {
                            continue;
                        }

                        await DispatchReminderAsync(db, publisher, master, reminder, occ.StartUtc, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to expand recurrence for reminder scan: event {EventId} rule '{RRule}'",
                    master.Id, master.RecurrenceRule);
            }
        }
    }

    private async Task DispatchReminderAsync(
        CalendarDbContext db,
        IPlatformEventPublisher publisher,
        CalendarEvent evt,
        EventReminder reminder,
        DateTime occurrenceStart,
        CancellationToken cancellationToken)
    {
        var ownerId = evt.Calendar?.OwnerId ?? evt.CreatedByUserId;

        try
        {
            // Raise the platform reminder event and stop there. Delivery — the in-app notification,
            // the real-time bell update and the push/email channels — belongs to the core's
            // notification pipeline. This module used to deliver it itself, which meant inventing a
            // System caller inside a module process (and broadcasting a SignalR event nothing
            // consumed). The module now only reports that the reminder came due.
            await publisher.PublishAsync(new ReminderTriggeredEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                UserId = ownerId,
                SourceModuleId = ModuleId,
                EntityType = "CalendarEvent",
                EntityId = evt.Id,
                Title = evt.Title,
                DueAtUtc = occurrenceStart
            }, cancellationToken);

            // Log successful dispatch
            db.ReminderLogs.Add(new ReminderLog
            {
                ReminderId = reminder.Id,
                EventId = evt.Id,
                OccurrenceStartUtc = occurrenceStart,
                TriggeredAtUtc = DateTime.UtcNow,
                Success = true
            });

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Reminder dispatched: event {EventId} '{Title}' occurrence {Occurrence} to user {UserId} ({Method}, {Minutes}min before)",
                evt.Id, evt.Title, occurrenceStart, ownerId, reminder.Method, reminder.MinutesBefore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch reminder for event {EventId} occurrence {Occurrence}",
                evt.Id, occurrenceStart);

            // Log the failure
            db.ReminderLogs.Add(new ReminderLog
            {
                ReminderId = reminder.Id,
                EventId = evt.Id,
                OccurrenceStartUtc = occurrenceStart,
                TriggeredAtUtc = DateTime.UtcNow,
                Success = false,
                ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to persist reminder failure log");
            }
        }
    }
}
