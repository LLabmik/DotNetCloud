using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrgDirectory = DotNetCloud.Core.Capabilities.IOrganizationDirectory;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="ICalendarService"/>.
/// </summary>
public sealed class CalendarService : ICalendarService
{
    private readonly CalendarDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly OrgDirectory _orgDirectory;
    private readonly ILogger<CalendarService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarService"/> class.
    /// </summary>
    public CalendarService(
        CalendarDbContext db,
        IEventBus eventBus,
        DotNetCloud.Core.Capabilities.IOrganizationDirectory orgDirectory,
        ILogger<CalendarService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _orgDirectory = orgDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CalendarDto> CreateCalendarAsync(CreateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // If org-owned, validate caller is org member with write access
        if (dto.OrganizationId.HasValue)
        {
            await ValidateOrgWriteAccessAsync(dto.OrganizationId.Value, caller, cancellationToken);
        }

        var calendar = new Models.Calendar
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            Timezone = dto.Timezone,
            OrganizationId = dto.OrganizationId
        };

        _db.Calendars.Add(calendar);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar {CalendarId} '{Name}' created by user {UserId} (org={OrgId})",
            calendar.Id, calendar.Name, caller.UserId, calendar.OrganizationId);

        return MapToDto(calendar);
    }

    /// <inheritdoc />
    public async Task<CalendarDto?> GetCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendar = await _db.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken);

        if (calendar is null)
            return null;

        if (!await CanAccessCalendarAsync(calendar, caller, requireWrite: false, cancellationToken))
            return null;

        return MapToDto(calendar);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Load all user-owned + shared calendars
        var calendars = await _db.Calendars
            .AsNoTracking()
            .Where(c => c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Also load org calendars where the user is a member
        var orgCalendars = await _db.Calendars
            .AsNoTracking()
            .Where(c => c.OrganizationId != null)
            .ToListAsync(cancellationToken);

        foreach (var orgCal in orgCalendars)
        {
            if (orgCal.OrganizationId.HasValue &&
                await _orgDirectory.IsOrganizationMemberAsync(orgCal.OrganizationId.Value, caller.UserId, cancellationToken))
            {
                if (!calendars.Any(c => c.Id == orgCal.Id))
                {
                    calendars.Add(orgCal);
                }
            }
        }

        return calendars.Select(MapToDto).OrderBy(c => c.Name).ToList();
    }

    /// <inheritdoc />
    public async Task<CalendarDto> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        if (!await CanAccessCalendarAsync(calendar, caller, requireWrite: true, cancellationToken))
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "You do not have permission to modify this calendar.");

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
            .FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        if (!await CanAccessCalendarAsync(calendar, caller, requireWrite: true, cancellationToken))
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "You do not have permission to delete this calendar.");

        // Hard-delete the calendar; cascade will remove all events, attendees, and reminders
        _db.Calendars.Remove(calendar);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar {CalendarId} hard-deleted by user {UserId}", calendarId, caller.UserId);
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
            OrganizationId = c.OrganizationId,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            SyncToken = c.SyncToken
        };
    }

    /// <summary>
    /// Checks whether the given caller can access a calendar.
    /// System admins bypass all access checks.
    /// </summary>
    private async Task<bool> CanAccessCalendarAsync(Models.Calendar calendar, CallerContext caller, bool requireWrite, CancellationToken cancellationToken)
    {
        // System admins bypass all org and ownership checks
        if (caller.HasRole(SystemRoleNames.Administrator))
            return true;

        if (calendar.OrganizationId is null)
        {
            // User-owned: owner has full access; shares checked separately
            return calendar.OwnerId == caller.UserId || calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId);
        }

        // Org-owned: user must be an active member
        var isMember = await _orgDirectory.IsOrganizationMemberAsync(calendar.OrganizationId.Value, caller.UserId, cancellationToken);
        if (!isMember) return false;
        if (!requireWrite) return true;

        // Write access: check for Manager+ role
        var member = await _orgDirectory.GetMemberAsync(calendar.OrganizationId.Value, caller.UserId, cancellationToken);
        return member is not null && OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds);
    }

    /// <summary>
    /// Validates that the caller has write access to the organization (Manager+ role or system admin).
    /// Throws <see cref="Core.Errors.ValidationException"/> if not.
    /// </summary>
    private async Task ValidateOrgWriteAccessAsync(Guid organizationId, CallerContext caller, CancellationToken cancellationToken)
    {
        // System admins bypass org role checks
        if (caller.HasRole(SystemRoleNames.Administrator))
            return;

        var member = await _orgDirectory.GetMemberAsync(organizationId, caller.UserId, cancellationToken);
        if (member is null)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "You are not a member of this organization.");

        if (!OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds))
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "You need a Manager or Admin role in this organization to create calendars.");
    }
}
