using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITeamDirectory = DotNetCloud.Core.Capabilities.ITeamDirectory;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="ICalendarShareService"/>.
/// </summary>
public sealed class CalendarShareService : ICalendarShareService
{
    private readonly CalendarDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ITeamDirectory? _teamDirectory;
    private readonly ILogger<CalendarShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarShareService"/> class.
    /// </summary>
    public CalendarShareService(
        CalendarDbContext db,
        IEventBus eventBus,
        ILogger<CalendarShareService> logger,
        ITeamDirectory? teamDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _teamDirectory = teamDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the caller's team IDs for team-share queries (empty when the team directory
    /// capability is unavailable).
    /// </summary>
    private async Task<Guid[]> GetCallerTeamIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        if (_teamDirectory is null)
        {
            return [];
        }

        try
        {
            var teams = await _teamDirectory.GetTeamsForUserAsync(caller.UserId, cancellationToken);
            return teams.Select(t => t.Id).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve team memberships for {UserId}", caller.UserId);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<CalendarShare> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, CalendarSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // User XOR team target.
        if (userId is null && teamId is null)
        {
            throw new ArgumentException("A calendar share requires a user or a team target.", nameof(teamId));
        }

        if (userId is not null && teamId is not null)
        {
            throw new ArgumentException("A calendar share cannot target both a user and a team.", nameof(teamId));
        }

        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        // Org calendars do not use shares — membership IS the share
        if (calendar.OrganizationId.HasValue)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.Forbidden, "Organization calendars cannot be shared. Organization membership controls access.");

        if (calendar.OwnerId != caller.UserId)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found or you are not the owner.");

        // Check if already shared with this target (dedupe per target).
        CalendarShare? existingShare;
        if (userId is { } targetUserId)
        {
            existingShare = await _db.CalendarShares
                .FirstOrDefaultAsync(s => s.CalendarId == calendarId && s.SharedWithUserId == targetUserId, cancellationToken);
        }
        else
        {
            existingShare = await _db.CalendarShares
                .FirstOrDefaultAsync(s => s.CalendarId == calendarId && s.SharedWithTeamId == teamId, cancellationToken);
        }

        if (existingShare is not null)
        {
            existingShare.Permission = permission;
            existingShare.UpdatedByUserId = caller.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            return existingShare;
        }

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

        await _eventBus.PublishAsync(new ResourceSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = caller.UserId,
            SharedWithUserId = userId,
            SharedWithTeamId = teamId,
            SourceModuleId = "dotnetcloud.calendar",
            EntityType = "Calendar",
            EntityId = calendarId,
            EntityDisplayName = calendar.Name,
            Permission = permission.ToString()
        }, caller, cancellationToken);

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarSharedItem>> ListSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var teamIds = await GetCallerTeamIdsAsync(caller, cancellationToken);

        var shares = await _db.CalendarShares
            .AsNoTracking()
            .Include(s => s.Calendar)
            .Where(s =>
                s.SharedWithUserId == caller.UserId ||
                (s.SharedWithTeamId != null && teamIds.Contains(s.SharedWithTeamId.Value)))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return shares
            .Where(s => s.Calendar is not null && !s.Calendar!.IsDeleted)
            .Select(s => new CalendarSharedItem
            {
                CalendarId = s.CalendarId,
                Name = s.Calendar!.Name,
                OwnerId = s.Calendar.OwnerId,
                CreatedByUserId = s.CreatedByUserId,
                SharedWithUserId = s.SharedWithUserId,
                SharedWithTeamId = s.SharedWithTeamId,
                Permission = s.Permission,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.Calendar.UpdatedAt
            })
            .ToList();
    }
}
