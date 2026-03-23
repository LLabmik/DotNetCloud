using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="ICalendarService"/>.
/// </summary>
public sealed class CalendarService : ICalendarService
{
    private readonly CalendarDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CalendarService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarService"/> class.
    /// </summary>
    public CalendarService(CalendarDbContext db, IEventBus eventBus, ILogger<CalendarService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CalendarDto> CreateCalendarAsync(CreateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendar = new Models.Calendar
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            Timezone = dto.Timezone
        };

        _db.Calendars.Add(calendar);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar {CalendarId} '{Name}' created by user {UserId}",
            calendar.Id, calendar.Name, caller.UserId);

        return MapToDto(calendar);
    }

    /// <inheritdoc />
    public async Task<CalendarDto?> GetCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendar = await _db.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calendarId &&
                (c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        return calendar is null ? null : MapToDto(calendar);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendars = await _db.Calendars
            .AsNoTracking()
            .Where(c => c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return calendars.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<CalendarDto> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == calendarId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        if (dto.Name is not null) calendar.Name = dto.Name;
        if (dto.Description is not null) calendar.Description = dto.Description;
        if (dto.Color is not null) calendar.Color = dto.Color;
        if (dto.Timezone is not null) calendar.Timezone = dto.Timezone;
        if (dto.IsVisible is not null) calendar.IsVisible = dto.IsVisible.Value;

        calendar.SyncToken = Guid.NewGuid().ToString("N");
        calendar.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar {CalendarId} updated by user {UserId}", calendarId, caller.UserId);

        return MapToDto(calendar);
    }

    /// <inheritdoc />
    public async Task DeleteCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == calendarId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        calendar.IsDeleted = true;
        calendar.DeletedAt = DateTime.UtcNow;
        calendar.UpdatedAt = DateTime.UtcNow;
        calendar.SyncToken = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar {CalendarId} soft-deleted by user {UserId}", calendarId, caller.UserId);
    }

    private static CalendarDto MapToDto(Models.Calendar c)
    {
        return new CalendarDto
        {
            Id = c.Id,
            OwnerId = c.OwnerId,
            Name = c.Name,
            Description = c.Description,
            Color = c.Color,
            Timezone = c.Timezone,
            IsDefault = c.IsDefault,
            IsVisible = c.IsVisible,
            IsDeleted = c.IsDeleted,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            SyncToken = c.SyncToken
        };
    }
}
