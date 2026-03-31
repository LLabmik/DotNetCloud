using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing project boards.
/// </summary>
public sealed class BoardService
{
    private readonly TracksDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ActivityService _activityService;
    private readonly TeamService _teamService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<BoardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardService"/> class.
    /// </summary>
    public BoardService(TracksDbContext db, IEventBus eventBus, ActivityService activityService, TeamService teamService, ILogger<BoardService> logger, IUserDirectory? userDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _activityService = activityService;
        _teamService = teamService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new board and adds the caller as owner.
    /// </summary>
    public async Task<BoardDto> CreateBoardAsync(CreateBoardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // If a team is specified, verify the caller is at least a Manager on that team
        if (dto.TeamId.HasValue)
        {
            await _teamService.EnsureTracksTeamRoleAsync(dto.TeamId.Value, caller.UserId, TracksTeamMemberRole.Manager, cancellationToken);
        }

        var board = new Board
        {
            Title = dto.Title,
            Description = dto.Description,
            Color = dto.Color,
            TeamId = dto.TeamId,
            OwnerId = caller.UserId
        };

        board.Members.Add(new BoardMember
        {
            BoardId = board.Id,
            UserId = caller.UserId,
            Role = BoardMemberRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        _db.Boards.Add(board);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Board {BoardId} '{Title}' created by user {UserId}",
            board.Id, board.Title, caller.UserId);

        await _activityService.LogAsync(board.Id, caller.UserId, "board.created", "Board", board.Id,
            $"{{\"title\":\"{board.Title}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new BoardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = board.Id,
            Title = board.Title,
            OwnerId = caller.UserId
        }, caller, cancellationToken);

        return await GetBoardDtoAsync(board.Id, cancellationToken)
            ?? throw new System.InvalidOperationException("Board was created but could not be retrieved.");
    }

    /// <summary>
    /// Gets a board by ID. Returns null if the caller is not a member.
    /// </summary>
    public async Task<BoardDto?> GetBoardAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Check effective role (direct membership or team-derived)
        var effectiveRole = await _teamService.GetEffectiveBoardRoleAsync(boardId, caller.UserId, cancellationToken);

        if (effectiveRole is null)
            return null;

        return await GetBoardDtoAsync(boardId, cancellationToken);
    }

    /// <summary>
    /// Lists all boards the caller is a member of.
    /// </summary>
    public async Task<IReadOnlyList<BoardDto>> ListBoardsAsync(CallerContext caller, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        // Direct board memberships
        var directBoardIds = await _db.BoardMembers
            .Where(m => m.UserId == caller.UserId)
            .Select(m => m.BoardId)
            .ToListAsync(cancellationToken);

        // Team-derived board access: find all teams the user has a Tracks role in, then their boards
        var teamIds = await _db.TeamRoles
            .Where(r => r.UserId == caller.UserId)
            .Select(r => r.CoreTeamId)
            .ToListAsync(cancellationToken);

        var teamBoardIds = teamIds.Count > 0
            ? await _db.Boards
                .Where(b => b.TeamId != null && teamIds.Contains(b.TeamId.Value) && !b.IsDeleted)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken)
            : [];

        var allBoardIds = directBoardIds.Union(teamBoardIds).Distinct().ToList();

        var query = _db.Boards
            .AsNoTracking()
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Swimlanes)
            .Where(b => allBoardIds.Contains(b.Id) && !b.IsDeleted);

        if (!includeArchived)
            query = query.Where(b => !b.IsArchived);

        var boards = await query
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync(cancellationToken);

        // Compute card counts for all active lists across all boards in a single query
        var allSwimlaneIds = boards.SelectMany(b => b.Swimlanes.Where(l => !l.IsArchived).Select(l => l.Id)).ToList();
        var cardCounts = await GetCardCountsByListAsync(allSwimlaneIds, cancellationToken);

        var memberIds = boards.SelectMany(b => b.Members.Select(m => m.UserId)).Distinct().ToList();
        var displayNames = await ResolveDisplayNamesAsync(memberIds, cancellationToken);

        return boards.Select(b => MapToDto(b, cardCounts, displayNames)).ToList();
    }

    /// <summary>
    /// Updates an existing board. Requires Admin or Owner role.
    /// </summary>
    public async Task<BoardDto> UpdateBoardAsync(Guid boardId, UpdateBoardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        var board = await _db.Boards
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        if (dto.Title is not null) board.Title = dto.Title;
        if (dto.Description is not null) board.Description = dto.Description;
        if (dto.Color is not null) board.Color = dto.Color;
        if (dto.IsArchived.HasValue) board.IsArchived = dto.IsArchived.Value;
        if (dto.LockSwimlanes.HasValue) board.LockSwimlanes = dto.LockSwimlanes.Value;

        board.UpdatedAt = DateTime.UtcNow;
        board.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Board {BoardId} updated by user {UserId}", boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "board.updated", "Board", boardId, null, cancellationToken);

        return await GetBoardDtoAsync(boardId, cancellationToken)
            ?? throw new System.InvalidOperationException("Board was updated but could not be retrieved.");
    }

    /// <summary>
    /// Soft-deletes a board. Requires Owner role.
    /// </summary>
    public async Task DeleteBoardAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Owner, cancellationToken);

        var board = await _db.Boards
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        board.IsDeleted = true;
        board.DeletedAt = DateTime.UtcNow;
        board.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Board {BoardId} deleted by user {UserId}", boardId, caller.UserId);

        await _eventBus.PublishAsync(new BoardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = boardId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Adds a member to the board. Requires Admin or Owner role.
    /// </summary>
    public async Task<BoardMemberDto> AddMemberAsync(Guid boardId, Guid userId, BoardMemberRole role, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        var exists = await _db.BoardMembers
            .AnyAsync(m => m.BoardId == boardId && m.UserId == userId, cancellationToken);

        if (exists)
            throw new ValidationException(ErrorCodes.NotBoardMember, "User is already a board member.");

        // Cannot add another Owner
        if (role == BoardMemberRole.Owner)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Cannot add another owner. Transfer ownership instead.");

        var member = new BoardMember
        {
            BoardId = boardId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        _db.BoardMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} added to board {BoardId} as {Role} by {CallerId}",
            userId, boardId, role, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "member.added", "BoardMember", member.Id,
            $"{{\"userId\":\"{userId}\",\"role\":\"{role}\"}}", cancellationToken);

        var names = await ResolveDisplayNamesAsync([userId], cancellationToken);

        return new BoardMemberDto
        {
            UserId = userId,
            DisplayName = names.TryGetValue(userId, out var name) ? name : null,
            Role = role,
            JoinedAt = member.JoinedAt
        };
    }

    /// <summary>
    /// Removes a member from the board. Requires Admin or Owner role.
    /// </summary>
    public async Task RemoveMemberAsync(Guid boardId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        var member = await _db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.NotBoardMember, "User is not a board member.");

        if (member.Role == BoardMemberRole.Owner)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Cannot remove the board owner.");

        _db.BoardMembers.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} removed from board {BoardId} by {CallerId}",
            userId, boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "member.removed", "BoardMember", member.Id,
            $"{{\"userId\":\"{userId}\"}}", cancellationToken);
    }

    /// <summary>
    /// Updates a member's role. Requires Owner role.
    /// </summary>
    public async Task UpdateMemberRoleAsync(Guid boardId, Guid userId, BoardMemberRole newRole, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Owner, cancellationToken);

        var member = await _db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.NotBoardMember, "User is not a board member.");

        if (member.Role == BoardMemberRole.Owner && newRole != BoardMemberRole.Owner)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Cannot demote the board owner.");

        member.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} role changed to {Role} on board {BoardId} by {CallerId}",
            userId, newRole, boardId, caller.UserId);
    }

    /// <summary>
    /// Gets the caller's role on a board, or null if not a member.
    /// Considers both direct membership and team-derived access.
    /// </summary>
    public async Task<BoardMemberRole?> GetMemberRoleAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _teamService.GetEffectiveBoardRoleAsync(boardId, userId, cancellationToken);
    }

    /// <summary>
    /// Ensures the user has at least the specified role on the board.
    /// Considers both direct membership and team-derived access.
    /// </summary>
    internal async Task EnsureBoardRoleAsync(Guid boardId, Guid userId, BoardMemberRole minimumRole, CancellationToken cancellationToken = default)
    {
        var effectiveRole = await _teamService.GetEffectiveBoardRoleAsync(boardId, userId, cancellationToken);

        if (effectiveRole is null)
            throw new ValidationException(ErrorCodes.NotBoardMember, "You are not a member of this board.");

        if (effectiveRole.Value < minimumRole)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole,
                $"Requires at least {minimumRole} role. You have {effectiveRole.Value}.");
    }

    /// <summary>
    /// Ensures the user is at least a Viewer on the board.
    /// </summary>
    internal async Task EnsureBoardMemberAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureBoardRoleAsync(boardId, userId, BoardMemberRole.Viewer, cancellationToken);
    }

    private async Task<BoardDto?> GetBoardDtoAsync(Guid boardId, CancellationToken cancellationToken)
    {
        var board = await _db.Boards
            .AsNoTracking()
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Swimlanes)
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);

        if (board is null) return null;

        var listIds = board.Swimlanes.Where(l => !l.IsArchived).Select(l => l.Id).ToList();
        var cardCounts = await GetCardCountsByListAsync(listIds, cancellationToken);

        var directMemberIds = board.Members.Select(m => m.UserId).Distinct().ToHashSet();
        var allMemberIds = new List<Guid>(directMemberIds);

        // For team-owned boards, include team members who don't have direct board membership
        List<BoardMemberDto>? teamDerivedMembers = null;
        if (board.TeamId.HasValue)
        {
            var teamRoles = await _db.TeamRoles
                .AsNoTracking()
                .Where(r => r.CoreTeamId == board.TeamId.Value)
                .ToListAsync(cancellationToken);

            teamDerivedMembers = teamRoles
                .Where(r => !directMemberIds.Contains(r.UserId))
                .Select(r => new BoardMemberDto
                {
                    UserId = r.UserId,
                    Role = r.Role switch
                    {
                        TracksTeamMemberRole.Owner => BoardMemberRole.Owner,
                        TracksTeamMemberRole.Manager => BoardMemberRole.Admin,
                        TracksTeamMemberRole.Member => BoardMemberRole.Member,
                        _ => BoardMemberRole.Viewer
                    },
                    JoinedAt = r.AssignedAt
                })
                .ToList();

            allMemberIds.AddRange(teamDerivedMembers.Select(m => m.UserId));
        }

        var displayNames = await ResolveDisplayNamesAsync(allMemberIds.Distinct().ToList(), cancellationToken);

        var dto = MapToDto(board, cardCounts, displayNames);

        // Append team-derived members with resolved display names
        if (teamDerivedMembers is { Count: > 0 })
        {
            var resolved = teamDerivedMembers.Select(m => m with
            {
                DisplayName = displayNames.TryGetValue(m.UserId, out var name) ? name : null
            }).ToList();
            dto = dto with { Members = [.. dto.Members, .. resolved] };
        }

        return dto;
    }

    private async Task<Dictionary<Guid, int>> GetCardCountsByListAsync(IReadOnlyList<Guid> listIds, CancellationToken cancellationToken)
    {
        if (listIds.Count == 0) return new Dictionary<Guid, int>();

        return await _db.Cards
            .Where(c => listIds.Contains(c.SwimlaneId) && !c.IsDeleted && !c.IsArchived)
            .GroupBy(c => c.SwimlaneId)
            .Select(g => new { SwimlaneId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SwimlaneId, x => x.Count, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveDisplayNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (_userDirectory is null || userIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _userDirectory.GetDisplayNamesAsync(userIds, cancellationToken);
    }

    private static BoardDto MapToDto(Board b, Dictionary<Guid, int>? cardCounts = null, IReadOnlyDictionary<Guid, string>? displayNames = null) => new()
    {
        Id = b.Id,
        OwnerId = b.OwnerId,
        TeamId = b.TeamId,
        Title = b.Title,
        Description = b.Description,
        Color = b.Color,
        LockSwimlanes = b.LockSwimlanes,
        IsArchived = b.IsArchived,
        IsDeleted = b.IsDeleted,
        DeletedAt = b.DeletedAt,
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt,
        ETag = b.ETag,
        Members = b.Members.Select(m => new BoardMemberDto
        {
            UserId = m.UserId,
            DisplayName = displayNames is not null && displayNames.TryGetValue(m.UserId, out var name) ? name : null,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        }).ToList(),
        Swimlanes = b.Swimlanes.Where(l => !l.IsArchived).OrderBy(l => l.Position).Select(l => new BoardSwimlaneDto
        {
            Id = l.Id,
            BoardId = l.BoardId,
            Title = l.Title,
            Color = l.Color,
            Position = (int)l.Position,
            CardLimit = l.CardLimit,
            IsDone = l.IsDone,
            CardCount = cardCounts is not null && cardCounts.TryGetValue(l.Id, out var count) ? count : 0,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        }).ToList(),
        Labels = b.Labels.Select(l => new LabelDto
        {
            Id = l.Id,
            BoardId = l.BoardId,
            Title = l.Title,
            Color = l.Color
        }).ToList()
    };
}
