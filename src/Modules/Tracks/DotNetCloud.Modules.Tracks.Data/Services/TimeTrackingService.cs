using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for time tracking on cards (timer-based and manual entry).
/// </summary>
public sealed class TimeTrackingService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<TimeTrackingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeTrackingService"/> class.
    /// </summary>
    public TimeTrackingService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<TimeTrackingService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a manual time entry on a card. Requires Member role or higher.
    /// </summary>
    public async Task<TimeEntryDto> CreateTimeEntryAsync(Guid cardId, CreateTimeEntryDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Calculate duration
        int durationMinutes;
        if (dto.DurationMinutes.HasValue)
        {
            durationMinutes = dto.DurationMinutes.Value;
        }
        else if (dto.EndTime.HasValue)
        {
            if (dto.EndTime.Value <= dto.StartTime)
                throw new ValidationException(ErrorCodes.InvalidTimeEntry, "End time must be after start time.");

            durationMinutes = (int)(dto.EndTime.Value - dto.StartTime).TotalMinutes;
        }
        else
        {
            throw new ValidationException(ErrorCodes.InvalidTimeEntry, "Either DurationMinutes or EndTime must be provided.");
        }

        if (durationMinutes <= 0)
            throw new ValidationException(ErrorCodes.InvalidTimeEntry, "Duration must be positive.");

        var entry = new TimeEntry
        {
            CardId = cardId,
            UserId = caller.UserId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DurationMinutes = durationMinutes,
            Description = dto.Description
        };

        _db.TimeEntries.Add(entry);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Time entry {EntryId} ({Duration}m) created on card {CardId} by user {UserId}",
            entry.Id, durationMinutes, cardId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "time.logged", "TimeEntry", entry.Id,
            $"{{\"durationMinutes\":{durationMinutes},\"cardId\":\"{cardId}\"}}", cancellationToken);

        return MapToDto(entry);
    }

    /// <summary>
    /// Starts a timer on a card. Creates a time entry with no end time.
    /// </summary>
    public async Task<TimeEntryDto> StartTimerAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Check if user already has a running timer on this card
        var runningTimer = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.CardId == cardId && t.UserId == caller.UserId && t.EndTime == null, cancellationToken);

        if (runningTimer is not null)
            throw new ValidationException(ErrorCodes.InvalidTimeEntry, "Timer is already running on this card.");

        var entry = new TimeEntry
        {
            CardId = cardId,
            UserId = caller.UserId,
            StartTime = DateTime.UtcNow,
            DurationMinutes = 0
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Timer started on card {CardId} by user {UserId}", cardId, caller.UserId);

        return MapToDto(entry);
    }

    /// <summary>
    /// Stops a running timer on a card. Calculates duration from start to now.
    /// </summary>
    public async Task<TimeEntryDto> StopTimerAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.CardId == cardId && t.UserId == caller.UserId && t.EndTime == null, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.InvalidTimeEntry, "No running timer found on this card.");

        entry.EndTime = DateTime.UtcNow;
        entry.DurationMinutes = (int)(entry.EndTime.Value - entry.StartTime!.Value).TotalMinutes;
        if (entry.DurationMinutes < 1) entry.DurationMinutes = 1; // Minimum 1 minute
        entry.UpdatedAt = DateTime.UtcNow;

        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Timer stopped on card {CardId} by user {UserId}. Duration: {Duration}m",
            cardId, caller.UserId, entry.DurationMinutes);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "time.logged", "TimeEntry", entry.Id,
            $"{{\"durationMinutes\":{entry.DurationMinutes},\"cardId\":\"{cardId}\"}}", cancellationToken);

        return MapToDto(entry);
    }

    /// <summary>
    /// Gets all time entries for a card.
    /// </summary>
    public async Task<IReadOnlyList<TimeEntryDto>> GetTimeEntriesAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(t => t.CardId == cardId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return entries.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets the total tracked minutes for a card.
    /// </summary>
    public async Task<int> GetTotalMinutesAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return await _db.TimeEntries
            .Where(t => t.CardId == cardId)
            .SumAsync(t => t.DurationMinutes, cancellationToken);
    }

    /// <summary>
    /// Deletes a time entry. Only the entry's owner can delete.
    /// </summary>
    public async Task DeleteTimeEntryAsync(Guid entryId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.Id == entryId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.TimeEntryNotFound, "Time entry not found.");

        if (entry.UserId != caller.UserId)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Only the owner of the time entry can delete it.");

        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Time entry {EntryId} deleted by user {UserId}", entryId, caller.UserId);
    }

    private static TimeEntryDto MapToDto(TimeEntry t) => new()
    {
        Id = t.Id,
        CardId = t.CardId,
        UserId = t.UserId,
        StartTime = t.StartTime ?? t.CreatedAt,
        EndTime = t.EndTime,
        DurationMinutes = t.DurationMinutes,
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };
}
