using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing board lists (columns).
/// </summary>
public sealed class ListService
{
    private const double PositionGap = 1000.0;

    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<ListService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListService"/> class.
    /// </summary>
    public ListService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<ListService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new list on a board. Requires Member role or higher.
    /// </summary>
    public async Task<BoardListDto> CreateListAsync(Guid boardId, CreateBoardListDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Board must exist
        var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);
        if (!boardExists)
            throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        // Position: append after last list
        var maxPos = await _db.BoardLists
            .Where(l => l.BoardId == boardId)
            .MaxAsync(l => (double?)l.Position, cancellationToken) ?? 0;

        var list = new BoardList
        {
            BoardId = boardId,
            Title = dto.Title,
            Color = dto.Color,
            CardLimit = dto.CardLimit,
            Position = maxPos + PositionGap
        };

        _db.BoardLists.Add(list);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("List {ListId} '{Title}' created on board {BoardId} by user {UserId}",
            list.Id, list.Title, boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "list.created", "BoardList", list.Id,
            $"{{\"title\":\"{list.Title}\"}}", cancellationToken);

        return MapToDto(list);
    }

    /// <summary>
    /// Gets all lists for a board, ordered by position.
    /// </summary>
    public async Task<IReadOnlyList<BoardListDto>> GetListsAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var lists = await _db.BoardLists
            .AsNoTracking()
            .Include(l => l.Cards.Where(c => !c.IsDeleted))
            .Where(l => l.BoardId == boardId && !l.IsArchived)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        return lists.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a list. Requires Member role or higher.
    /// </summary>
    public async Task<BoardListDto> UpdateListAsync(Guid listId, UpdateBoardListDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var list = await _db.BoardLists
            .FirstOrDefaultAsync(l => l.Id == listId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "List not found.");

        await _boardService.EnsureBoardRoleAsync(list.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        if (dto.Title is not null) list.Title = dto.Title;
        if (dto.Color is not null) list.Color = dto.Color;
        if (dto.CardLimit.HasValue) list.CardLimit = dto.CardLimit.Value;

        list.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("List {ListId} updated by user {UserId}", listId, caller.UserId);

        return MapToDto(list);
    }

    /// <summary>
    /// Archives a list. Requires Admin role or higher.
    /// </summary>
    public async Task DeleteListAsync(Guid listId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var list = await _db.BoardLists
            .FirstOrDefaultAsync(l => l.Id == listId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "List not found.");

        await _boardService.EnsureBoardRoleAsync(list.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        list.IsArchived = true;
        list.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("List {ListId} archived by user {UserId}", listId, caller.UserId);

        await _activityService.LogAsync(list.BoardId, caller.UserId, "list.archived", "BoardList", listId,
            $"{{\"title\":\"{list.Title}\"}}", cancellationToken);
    }

    /// <summary>
    /// Reorders lists in a board using gap-based positioning.
    /// </summary>
    public async Task ReorderListsAsync(Guid boardId, IReadOnlyList<Guid> listIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var lists = await _db.BoardLists
            .Where(l => l.BoardId == boardId && listIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        for (var i = 0; i < listIds.Count; i++)
        {
            var list = lists.FirstOrDefault(l => l.Id == listIds[i]);
            if (list is not null)
            {
                list.Position = (i + 1) * PositionGap;
                list.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total non-deleted, non-archived cards in a list (for WIP limit checks).
    /// </summary>
    internal async Task<int> GetActiveCardCountAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        return await _db.Cards
            .CountAsync(c => c.ListId == listId && !c.IsDeleted && !c.IsArchived, cancellationToken);
    }

    /// <summary>
    /// Gets the WIP limit for a list, or null if unlimited.
    /// </summary>
    internal async Task<int?> GetCardLimitAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        return await _db.BoardLists
            .Where(l => l.Id == listId)
            .Select(l => l.CardLimit)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static BoardListDto MapToDto(BoardList l) => new()
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
