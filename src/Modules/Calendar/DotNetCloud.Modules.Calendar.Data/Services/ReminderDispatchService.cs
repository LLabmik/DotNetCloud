using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Background service that periodically scans for due calendar reminders
/// and dispatches them via the event bus. Handles both single-occurrence
/// and recurring events, using <see cref="ReminderLog"/> to prevent duplicates.
/// </summary>
public sealed class ReminderDispatchService : BackgroundService
{
    /// <summary>How often the service checks for due reminders.</summary>
    internal static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How far ahead of the current time to look for upcoming reminders.
    /// This should cover the largest typical reminder window (24 hours).
    /// </summary>
    internal static readonly TimeSpan LookAheadWindow = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderDispatchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderDispatchService"/> class.
    /// </summary>
    public ReminderDispatchService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderDispatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
                await ScanAndDispatchAsync(stoppingToken);
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
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var recurrenceEngine = scope.ServiceProvider.GetRequiredService<IRecurrenceEngine>();

        var now = DateTime.UtcNow;
        var lookAhead = now.Add(LookAheadWindow);

        // 1. Process non-recurring events with upcoming reminders.
        await DispatchSingleEventRemindersAsync(db, eventBus, now, lookAhead, cancellationToken);

        // 2. Process recurring events with upcoming reminders.
        await DispatchRecurringEventRemindersAsync(db, eventBus, recurrenceEngine, now, lookAhead, cancellationToken);
    }

    private async Task DispatchSingleEventRemindersAsync(
        CalendarDbContext db,
        IEventBus eventBus,
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

                await DispatchReminderAsync(db, eventBus, evt, reminder, evt.StartUtc, cancellationToken);
            }
        }
    }

    private async Task DispatchRecurringEventRemindersAsync(
        CalendarDbContext db,
        IEventBus eventBus,
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

                        await DispatchReminderAsync(db, eventBus, master, reminder, occ.StartUtc, cancellationToken);
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
        IEventBus eventBus,
        CalendarEvent evt,
        EventReminder reminder,
        DateTime occurrenceStart,
        CancellationToken cancellationToken)
    {
        var ownerId = evt.Calendar?.OwnerId ?? evt.CreatedByUserId;
        var systemCaller = new CallerContext(Guid.Empty, [], CallerType.System);

        try
        {
            // Publish calendar-specific reminder event
            await eventBus.PublishAsync(new CalendarReminderTriggeredEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CalendarEventId = evt.Id,
                UserId = ownerId,
                EventTitle = evt.Title,
                EventStartUtc = occurrenceStart
            }, systemCaller, cancellationToken);

            // Publish cross-module reminder event (core handler sends push notification)
            await eventBus.PublishAsync(new ReminderTriggeredEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UserId = ownerId,
                SourceModuleId = "dotnetcloud.calendar",
                EntityType = "CalendarEvent",
                EntityId = evt.Id,
                Title = evt.Title,
                DueAtUtc = occurrenceStart
            }, systemCaller, cancellationToken);

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
