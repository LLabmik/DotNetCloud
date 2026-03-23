using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Expands recurring events using <see cref="IRecurrenceEngine"/> and merges
/// them with concrete events, respecting exception overrides.
/// </summary>
public sealed class OccurrenceExpansionService : IOccurrenceExpansionService
{
    private readonly CalendarDbContext _db;
    private readonly IRecurrenceEngine _recurrenceEngine;
    private readonly ILogger<OccurrenceExpansionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OccurrenceExpansionService"/> class.
    /// </summary>
    public OccurrenceExpansionService(
        CalendarDbContext db,
        IRecurrenceEngine recurrenceEngine,
        ILogger<OccurrenceExpansionService> logger)
    {
        _db = db;
        _recurrenceEngine = recurrenceEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> ListExpandedEventsAsync(
        Guid calendarId,
        CallerContext caller,
        DateTime from,
        DateTime to,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch non-recurring events in the window.
        var concreteEvents = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .AsNoTracking()
            .Where(e => e.CalendarId == calendarId
                && e.RecurrenceRule == null
                && e.RecurringEventId == null
                && e.EndUtc >= from && e.StartUtc <= to
                && (e.Calendar!.OwnerId == caller.UserId
                    || e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId)))
            .ToListAsync(cancellationToken);

        // 2. Fetch recurring master events in this calendar.
        var recurringMasters = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .Include(e => e.Exceptions)
            .AsNoTracking()
            .Where(e => e.CalendarId == calendarId
                && e.RecurrenceRule != null
                && e.RecurringEventId == null
                && (e.Calendar!.OwnerId == caller.UserId
                    || e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId)))
            .ToListAsync(cancellationToken);

        // 3. Fetch exception instances for these masters (already stored in the DB).
        var exceptionEvents = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .AsNoTracking()
            .Where(e => e.RecurringEventId != null
                && e.CalendarId == calendarId
                && e.EndUtc >= from && e.StartUtc <= to)
            .ToListAsync(cancellationToken);

        // 4. Expand recurrence and merge.
        var results = new List<CalendarEventDto>();

        // Add concrete events
        results.AddRange(concreteEvents.Select(MapToDto));

        // Add exception instances that fall in the window
        results.AddRange(exceptionEvents.Select(MapToDto));

        // Expand recurring masters
        foreach (var master in recurringMasters)
        {
            var duration = master.EndUtc - master.StartUtc;
            var excludedDates = new HashSet<DateTime>(
                master.Exceptions
                    .Where(ex => ex.OriginalStartUtc.HasValue)
                    .Select(ex => ex.OriginalStartUtc!.Value));

            try
            {
                var occurrences = _recurrenceEngine.Expand(
                    master.RecurrenceRule!,
                    master.StartUtc,
                    duration,
                    from,
                    to,
                    excludedDates);

                foreach (var occ in occurrences)
                {
                    // Generate a virtual event DTO for each occurrence.
                    // The ID remains the master's ID; the start/end are the occurrence times.
                    results.Add(new CalendarEventDto
                    {
                        Id = master.Id,
                        CalendarId = master.CalendarId,
                        CreatedByUserId = master.CreatedByUserId,
                        Title = master.Title,
                        Description = master.Description,
                        Location = master.Location,
                        StartUtc = occ.StartUtc,
                        EndUtc = occ.EndUtc,
                        IsAllDay = master.IsAllDay,
                        Status = master.Status,
                        RecurrenceRule = master.RecurrenceRule,
                        RecurringEventId = null,
                        OriginalStartUtc = occ.StartUtc,
                        Color = master.Color,
                        Url = master.Url,
                        IsDeleted = false,
                        CreatedAt = master.CreatedAt,
                        UpdatedAt = master.UpdatedAt,
                        Attendees = master.Attendees.Select(a => new EventAttendeeDto
                        {
                            UserId = a.UserId,
                            Email = a.Email,
                            DisplayName = a.DisplayName,
                            Role = a.Role,
                            Status = a.Status
                        }).ToList(),
                        Reminders = master.Reminders.Select(r => new EventReminderDto
                        {
                            Method = r.Method,
                            MinutesBefore = r.MinutesBefore
                        }).ToList(),
                        ETag = master.ETag
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to expand recurrence for event {EventId} with rule '{RRule}'",
                    master.Id, master.RecurrenceRule);
            }
        }

        // Sort by start time and apply pagination.
        return results
            .OrderBy(e => e.StartUtc)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> SearchExpandedEventsAsync(
        CallerContext caller,
        string? query = null,
        DateTime? from = null,
        DateTime? to = null,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        // Default window: 1 year back / 1 year forward if not specified
        var windowStart = from ?? DateTime.UtcNow.AddYears(-1);
        var windowEnd = to ?? DateTime.UtcNow.AddYears(1);

        // Get the user's calendar IDs
        var calendarIds = await _db.Calendars
            .AsNoTracking()
            .Where(c => c.OwnerId == caller.UserId
                || c.Shares.Any(s => s.SharedWithUserId == caller.UserId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var allResults = new List<CalendarEventDto>();

        foreach (var calId in calendarIds)
        {
            var expanded = await ListExpandedEventsAsync(
                calId, caller, windowStart, windowEnd, 0, 10000, cancellationToken);
            allResults.AddRange(expanded);
        }

        // Apply text search filter if provided.
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            allResults = allResults
                .Where(e =>
                    e.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.Description is not null && e.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Location is not null && e.Location.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return allResults
            .OrderBy(e => e.StartUtc)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    private static CalendarEventDto MapToDto(CalendarEvent e)
    {
        return new CalendarEventDto
        {
            Id = e.Id,
            CalendarId = e.CalendarId,
            CreatedByUserId = e.CreatedByUserId,
            Title = e.Title,
            Description = e.Description,
            Location = e.Location,
            StartUtc = e.StartUtc,
            EndUtc = e.EndUtc,
            IsAllDay = e.IsAllDay,
            Status = e.Status,
            RecurrenceRule = e.RecurrenceRule,
            RecurringEventId = e.RecurringEventId,
            OriginalStartUtc = e.OriginalStartUtc,
            Color = e.Color,
            Url = e.Url,
            IsDeleted = e.IsDeleted,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            Attendees = e.Attendees.Select(a => new EventAttendeeDto
            {
                UserId = a.UserId,
                Email = a.Email,
                DisplayName = a.DisplayName,
                Role = a.Role,
                Status = a.Status
            }).ToList(),
            Reminders = e.Reminders.Select(r => new EventReminderDto
            {
                Method = r.Method,
                MinutesBefore = r.MinutesBefore
            }).ToList(),
            ETag = e.ETag
        };
    }
}
