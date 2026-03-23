using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="ICalendarEventService"/>.
/// </summary>
public sealed class CalendarEventService : ICalendarEventService
{
    private readonly CalendarDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CalendarEventService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventService"/> class.
    /// </summary>
    public CalendarEventService(CalendarDbContext db, IEventBus eventBus, ILogger<CalendarEventService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Verify the calendar exists and user has access
        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == dto.CalendarId &&
                (c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId && s.Permission == CalendarSharePermission.ReadWrite)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found or access denied.");

        if (dto.StartUtc >= dto.EndUtc && !dto.IsAllDay)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.InvalidEventTimeRange, "Event end time must be after start time.");
        }

        var calendarEvent = new CalendarEvent
        {
            CalendarId = dto.CalendarId,
            CreatedByUserId = caller.UserId,
            Title = dto.Title,
            Description = dto.Description,
            Location = dto.Location,
            StartUtc = dto.StartUtc,
            EndUtc = dto.EndUtc,
            IsAllDay = dto.IsAllDay,
            RecurrenceRule = dto.RecurrenceRule,
            Color = dto.Color,
            Url = dto.Url
        };

        foreach (var attendee in dto.Attendees)
        {
            calendarEvent.Attendees.Add(new EventAttendee
            {
                UserId = attendee.UserId,
                Email = attendee.Email,
                DisplayName = attendee.DisplayName,
                Role = attendee.Role,
                Status = attendee.Status
            });
        }

        foreach (var reminder in dto.Reminders)
        {
            calendarEvent.Reminders.Add(new EventReminder
            {
                Method = reminder.Method,
                MinutesBefore = reminder.MinutesBefore
            });
        }

        _db.CalendarEvents.Add(calendarEvent);

        // Update calendar sync token
        calendar.SyncToken = Guid.NewGuid().ToString("N");
        calendar.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event {EventId} '{Title}' created by user {UserId} in calendar {CalendarId}",
            calendarEvent.Id, calendarEvent.Title, caller.UserId, dto.CalendarId);

        await _eventBus.PublishAsync(new CalendarEventCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = calendarEvent.Id,
            CalendarId = dto.CalendarId,
            Title = calendarEvent.Title,
            CreatedByUserId = caller.UserId,
            StartUtc = calendarEvent.StartUtc,
            EndUtc = calendarEvent.EndUtc,
            IsRecurring = calendarEvent.RecurrenceRule is not null
        }, caller, cancellationToken);

        return MapToDto(calendarEvent);
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto?> GetEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await QueryEvents()
            .FirstOrDefaultAsync(e => e.Id == eventId &&
                (e.Calendar!.OwnerId == caller.UserId ||
                 e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        return calendarEvent is null ? null : MapToDto(calendarEvent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid calendarId, CallerContext caller, DateTime? from = null, DateTime? to = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = QueryEvents()
            .Where(e => e.CalendarId == calendarId &&
                (e.Calendar!.OwnerId == caller.UserId ||
                 e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId)));

        if (from.HasValue)
            query = query.Where(e => e.EndUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.StartUtc <= to.Value);

        var events = await query
            .OrderBy(e => e.StartUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return events.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> UpdateEventAsync(Guid eventId, UpdateCalendarEventDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendarEvent = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.Calendar!.OwnerId == caller.UserId ||
                 e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId && s.Permission == CalendarSharePermission.ReadWrite)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarEventNotFound, "Calendar event not found or access denied.");

        if (dto.Title is not null) calendarEvent.Title = dto.Title;
        if (dto.Description is not null) calendarEvent.Description = dto.Description;
        if (dto.Location is not null) calendarEvent.Location = dto.Location;
        if (dto.StartUtc.HasValue) calendarEvent.StartUtc = dto.StartUtc.Value;
        if (dto.EndUtc.HasValue) calendarEvent.EndUtc = dto.EndUtc.Value;
        if (dto.IsAllDay.HasValue) calendarEvent.IsAllDay = dto.IsAllDay.Value;
        if (dto.Status.HasValue) calendarEvent.Status = dto.Status.Value;
        if (dto.RecurrenceRule is not null) calendarEvent.RecurrenceRule = dto.RecurrenceRule;
        if (dto.Color is not null) calendarEvent.Color = dto.Color;
        if (dto.Url is not null) calendarEvent.Url = dto.Url;

        if (dto.Attendees is not null)
        {
            _db.EventAttendees.RemoveRange(calendarEvent.Attendees);
            calendarEvent.Attendees.Clear();
            foreach (var attendee in dto.Attendees)
            {
                calendarEvent.Attendees.Add(new EventAttendee
                {
                    UserId = attendee.UserId,
                    Email = attendee.Email,
                    DisplayName = attendee.DisplayName,
                    Role = attendee.Role,
                    Status = attendee.Status
                });
            }
        }

        if (dto.Reminders is not null)
        {
            _db.EventReminders.RemoveRange(calendarEvent.Reminders);
            calendarEvent.Reminders.Clear();
            foreach (var reminder in dto.Reminders)
            {
                calendarEvent.Reminders.Add(new EventReminder
                {
                    Method = reminder.Method,
                    MinutesBefore = reminder.MinutesBefore
                });
            }
        }

        calendarEvent.ETag = Guid.NewGuid().ToString("N");
        calendarEvent.UpdatedAt = DateTime.UtcNow;
        calendarEvent.UpdatedByUserId = caller.UserId;
        if (calendarEvent.Calendar is not null)
        {
            calendarEvent.Calendar.SyncToken = Guid.NewGuid().ToString("N");
            calendarEvent.Calendar.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event {EventId} updated by user {UserId}", eventId, caller.UserId);

        await _eventBus.PublishAsync(new CalendarEventUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = eventId,
            CalendarId = calendarEvent.CalendarId,
            UpdatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(calendarEvent);
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.Calendar!.OwnerId == caller.UserId ||
                 e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId && s.Permission == CalendarSharePermission.ReadWrite)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarEventNotFound, "Calendar event not found or access denied.");

        calendarEvent.IsDeleted = true;
        calendarEvent.DeletedAt = DateTime.UtcNow;
        calendarEvent.UpdatedAt = DateTime.UtcNow;
        calendarEvent.ETag = Guid.NewGuid().ToString("N");

        // Update calendar sync token
        if (calendarEvent.Calendar is not null)
        {
            calendarEvent.Calendar.SyncToken = Guid.NewGuid().ToString("N");
            calendarEvent.Calendar.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event {EventId} soft-deleted by user {UserId}", eventId, caller.UserId);

        await _eventBus.PublishAsync(new CalendarEventDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = eventId,
            CalendarId = calendarEvent.CalendarId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> RsvpAsync(Guid eventId, EventRsvpDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendarEvent = await _db.CalendarEvents
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .Include(e => e.Calendar)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarEventNotFound, "Calendar event not found.");

        var attendee = calendarEvent.Attendees.FirstOrDefault(a => a.UserId == caller.UserId)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.AttendeeNotFound, "You are not an attendee of this event.");

        attendee.Status = dto.Status;
        attendee.Comment = dto.Comment;
        attendee.RespondedAt = DateTime.UtcNow;

        calendarEvent.ETag = Guid.NewGuid().ToString("N");
        calendarEvent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} RSVP'd {Status} to event {EventId}", caller.UserId, dto.Status, eventId);

        await _eventBus.PublishAsync(new CalendarEventRsvpEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = eventId,
            AttendeeUserId = caller.UserId,
            Status = dto.Status.ToString()
        }, caller, cancellationToken);

        return MapToDto(calendarEvent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(CallerContext caller, string? query = null, DateTime? from = null, DateTime? to = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var q = QueryEvents()
            .Where(e => e.Calendar!.OwnerId == caller.UserId ||
                        e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(e =>
                e.Title.Contains(term) ||
                (e.Description != null && e.Description.Contains(term)) ||
                (e.Location != null && e.Location.Contains(term)));
        }

        if (from.HasValue)
            q = q.Where(e => e.EndUtc >= from.Value);

        if (to.HasValue)
            q = q.Where(e => e.StartUtc <= to.Value);

        var events = await q
            .OrderBy(e => e.StartUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return events.Select(MapToDto).ToList();
    }

    private IQueryable<CalendarEvent> QueryEvents()
    {
        return _db.CalendarEvents
            .Include(e => e.Calendar)
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .AsNoTracking();
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
