using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing board swimlanes (columns).
/// </summary>
public sealed class SwimlaneService
{
    private const double PositionGap = 1000.0;

    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<SwimlaneService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwimlaneService"/> class.
    /// </summary>
    public SwimlaneService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<SwimlaneService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new swimlane on a board. Requires Member role or higher.
    /// </summary>
    public async Task<BoardSwimlaneDto> CreateSwimlaneAsync(Guid boardId, CreateBoardSwimlaneDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Board must exist
        var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);
        if (!boardExists)
            throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        // Position: append after last swimlane
        var maxPos = await _db.BoardSwimlanes
            .Where(l => l.BoardId == boardId)
            .MaxAsync(l => (double?)l.Position, cancellationToken) ?? 0;

        var swimlane = new BoardSwimlane
        {
            BoardId = boardId,
            Title = dto.Title,
            Color = dto.Color,
            CardLimit = dto.CardLimit,
            Position = maxPos + PositionGap
        };

        _db.BoardSwimlanes.Add(swimlane);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Swimlane {SwimlaneId} '{Title}' created on board {BoardId} by user {UserId}",
            swimlane.Id, swimlane.Title, boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "swimlane.created", "BoardSwimlane", swimlane.Id,
            $"{{\"title\":\"{swimlane.Title}\"}}", cancellationToken);

        return MapToDto(swimlane);
    }

    /// <summary>
    /// Gets all swimlanes for a board, ordered by position.
    /// </summary>
    public async Task<IReadOnlyList<BoardSwimlaneDto>> GetSwimlanesAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var swimlanes = await _db.BoardSwimlanes
            .AsNoTracking()
            .Include(l => l.Cards.Where(c => !c.IsDeleted))
            .Where(l => l.BoardId == boardId && !l.IsArchived)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        return swimlanes.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a swimlane. Requires Member role or higher.
    /// </summary>
    public async Task<BoardSwimlaneDto> UpdateSwimlaneAsync(Guid swimlaneId, UpdateBoardSwimlaneDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var swimlane = await _db.BoardSwimlanes
            .FirstOrDefaultAsync(l => l.Id == swimlaneId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found.");

        await _boardService.EnsureBoardRoleAsync(swimlane.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        if (dto.Title is not null) swimlane.Title = dto.Title;
        if (dto.Color is not null) swimlane.Color = dto.Color;
        if (dto.CardLimit.HasValue) swimlane.CardLimit = dto.CardLimit.Value;

        swimlane.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Swimlane {SwimlaneId} updated by user {UserId}", swimlaneId, caller.UserId);

        return MapToDto(swimlane);
    }

    /// <summary>
    /// Archives a swimlane. Requires Admin role or higher.
    /// </summary>
    public async Task DeleteSwimlaneAsync(Guid swimlaneId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var swimlane = await _db.BoardSwimlanes
            .FirstOrDefaultAsync(l => l.Id == swimlaneId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found.");

        await _boardService.EnsureBoardRoleAsync(swimlane.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        swimlane.IsArchived = true;
        swimlane.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Swimlane {SwimlaneId} archived by user {UserId}", swimlaneId, caller.UserId);

        await _activityService.LogAsync(swimlane.BoardId, caller.UserId, "swimlane.archived", "BoardSwimlane", swimlaneId,
            $"{{\"title\":\"{swimlane.Title}\"}}", cancellationToken);
    }

    /// <summary>
    /// Reorders swimlanes in a board using gap-based positioning.
    /// </summary>
    public async Task ReorderSwimlanesAsync(Guid boardId, IReadOnlyList<Guid> swimlaneIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var swimlanes = await _db.BoardSwimlanes
            .Where(l => l.BoardId == boardId && swimlaneIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        for (var i = 0; i < swimlaneIds.Count; i++)
        {
            var swimlane = swimlanes.FirstOrDefault(l => l.Id == swimlaneIds[i]);
            if (swimlane is not null)
            {
                swimlane.Position = (i + 1) * PositionGap;
                swimlane.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total non-deleted, non-archived cards in a swimlane (for WIP limit checks).
    /// </summary>
    internal async Task<int> GetActiveCardCountAsync(Guid swimlaneId, CancellationToken cancellationToken = default)
    {
        return await _db.Cards
            .CountAsync(c => c.SwimlaneId == swimlaneId && !c.IsDeleted && !c.IsArchived, cancellationToken);
    }

    /// <summary>
    /// Gets the WIP limit for a swimlane, or null if unlimited.
    /// </summary>
    internal async Task<int?> GetCardLimitAsync(Guid swimlaneId, CancellationToken cancellationToken = default)
    {
        return await _db.BoardSwimlanes
            .Where(l => l.Id == swimlaneId)
            .Select(l => l.CardLimit)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static BoardSwimlaneDto MapToDto(BoardSwimlane l) => new()
    {
        Id = l.Id,
        BoardId = l.BoardId,
        Title = l.Title,
        Color = l.Color,
        Position = (int)l.Position,
        CardLimit = l.CardLimit,
        CardCount = l.Cards.Count(c => !c.IsDeleted && !c.IsArchived),
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt
    };
}
