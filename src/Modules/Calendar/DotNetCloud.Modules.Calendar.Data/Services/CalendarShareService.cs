using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="ICalendarShareService"/>.
/// </summary>
public sealed class CalendarShareService : ICalendarShareService
{
    private readonly CalendarDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CalendarShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarShareService"/> class.
    /// </summary>
    public CalendarShareService(
        CalendarDbContext db,
        IEventBus eventBus,
        ILogger<CalendarShareService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CalendarShare> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, CalendarSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        // Org calendars do not use shares — membership IS the share
        if (calendar.OrganizationId.HasValue)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "Organization calendars cannot be shared. Organization membership controls access.");

        if (calendar.OwnerId != caller.UserId)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found or you are not the owner.");

        var share = new CalendarShare
        {
            CalendarId = calendarId,
            SharedWithUserId = userId,
            SharedWithTeamId = teamId,
            Permission = permission,
            CreatedByUserId = caller.UserId,
            UpdatedByUserId = caller.UserId
        };

        _db.CalendarShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        if (userId.HasValue)
        {
            await _eventBus.PublishAsync(new ResourceSharedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                SharedByUserId = caller.UserId,
                SharedWithUserId = userId.Value,
                SourceModuleId = "dotnetcloud.calendar",
                EntityType = "Calendar",
                EntityId = calendarId,
                EntityDisplayName = calendar.Name,
                Permission = permission.ToString()
            }, caller, cancellationToken);
        }

        _logger.LogInformation("Calendar {CalendarId} shared by user {UserId} with user={SharedUserId} team={SharedTeamId}",
            calendarId, caller.UserId, userId, teamId);

        return share;
    }

    /// <inheritdoc />
    public async Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var share = await _db.CalendarShares
            .Include(s => s.Calendar)
            .FirstOrDefaultAsync(s => s.Id == shareId && s.Calendar!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Share not found or you are not the calendar owner.");

        _db.CalendarShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar share {ShareId} removed by user {UserId}", shareId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarShare>> ListSharesAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        return await _db.CalendarShares
            .AsNoTracking()
            .Where(s => s.CalendarId == calendarId && s.Calendar!.OwnerId == caller.UserId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
