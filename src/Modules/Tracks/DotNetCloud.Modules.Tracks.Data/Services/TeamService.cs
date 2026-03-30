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
/// Service for managing teams in the Tracks module (Option C: Core teams + Tracks role overlay).
/// Team identity and membership are owned by Core via <see cref="ITeamDirectory"/> and <see cref="ITeamManager"/>.
/// Tracks stores module-specific role assignments in <see cref="TeamRole"/>.
/// </summary>
public sealed class TeamService
{
    private readonly TracksDbContext _db;
    private readonly ITeamDirectory? _teamDirectory;
    private readonly ITeamManager? _teamManager;
    private readonly IUserDirectory? _userDirectory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TeamService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamService"/> class.
    /// </summary>
    public TeamService(
        TracksDbContext db,
        IEventBus eventBus,
        ILogger<TeamService> logger,
        ITeamDirectory? teamDirectory = null,
        ITeamManager? teamManager = null,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _teamDirectory = teamDirectory;
        _teamManager = teamManager;
        _userDirectory = userDirectory;
    }

    /// <summary>
    /// Creates a new team in Core and adds the caller as Owner in the Tracks role table.
    /// </summary>
    public async Task<TracksTeamDto> CreateTeamAsync(CreateTracksTeamDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (_teamManager is null)
            throw new System.InvalidOperationException("Team management capability is not available.");

        var organizationId = dto.OrganizationId ?? Guid.Empty;

        var teamInfo = await _teamManager.CreateTeamAsync(
            organizationId, dto.Name, dto.Description, caller.UserId, cancellationToken);

        // Create the Tracks-level Owner role for the creator
        _db.TeamRoles.Add(new TeamRole
        {
            CoreTeamId = teamInfo.Id,
            UserId = caller.UserId,
            Role = TracksTeamMemberRole.Owner,
            AssignedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team {TeamId} '{Name}' created by user {UserId}",
            teamInfo.Id, teamInfo.Name, caller.UserId);

        await _eventBus.PublishAsync(new TeamCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            TeamId = teamInfo.Id,
            Name = teamInfo.Name,
            CreatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return await BuildTeamDtoAsync(teamInfo.Id, cancellationToken)
            ?? throw new System.InvalidOperationException("Team was created but could not be retrieved.");
    }

    /// <summary>
    /// Gets a team by its Core team ID. Returns null if the team doesn't exist or caller is not a member.
    /// </summary>
    public async Task<TracksTeamDto?> GetTeamAsync(Guid coreTeamId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (_teamDirectory is null)
            return null;

        var isMember = await _teamDirectory.IsTeamMemberAsync(coreTeamId, caller.UserId, cancellationToken);
        if (!isMember)
            return null;

        return await BuildTeamDtoAsync(coreTeamId, cancellationToken);
    }

    /// <summary>
    /// Lists all teams the caller is a member of (via Core), enriched with Tracks role data.
    /// </summary>
    public async Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (_teamDirectory is null)
            return [];

        var coreTeams = await _teamDirectory.GetTeamsForUserAsync(caller.UserId, cancellationToken);

        if (coreTeams.Count == 0)
            return [];

        var coreTeamIds = coreTeams.Select(t => t.Id).ToList();

        // Load Tracks roles for these teams
        var tracksRoles = await _db.TeamRoles
            .AsNoTracking()
            .Where(r => coreTeamIds.Contains(r.CoreTeamId))
            .ToListAsync(cancellationToken);

        // Count boards per team
        var boardCounts = await _db.Boards
            .AsNoTracking()
            .Where(b => b.TeamId != null && coreTeamIds.Contains(b.TeamId.Value) && !b.IsDeleted)
            .GroupBy(b => b.TeamId!.Value)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count, cancellationToken);

        // Resolve display names for all members across all teams
        var allUserIds = tracksRoles.Select(r => r.UserId).Distinct();
        var displayNames = _userDirectory is not null
            ? await _userDirectory.GetDisplayNamesAsync(allUserIds, cancellationToken)
            : (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>();

        return coreTeams.Select(t =>
        {
            var teamRoles = tracksRoles.Where(r => r.CoreTeamId == t.Id).ToList();
            boardCounts.TryGetValue(t.Id, out var boardCount);

            return new TracksTeamDto
            {
                Id = t.Id,
                OrganizationId = t.OrganizationId,
                Name = t.Name,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                BoardCount = boardCount,
                Members = teamRoles.Select(r => new TracksTeamMemberDto
                {
                    UserId = r.UserId,
                    DisplayName = displayNames.GetValueOrDefault(r.UserId),
                    Role = r.Role,
                    JoinedAt = r.AssignedAt
                }).ToList()
            };
        }).ToList();
    }

    /// <summary>
    /// Updates a team's name/description via Core.
    /// Requires Tracks Manager or Owner role.
    /// </summary>
    public async Task<TracksTeamDto> UpdateTeamAsync(Guid coreTeamId, UpdateTracksTeamDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (_teamManager is null)
            throw new System.InvalidOperationException("Team management capability is not available.");

        await EnsureTracksTeamRoleAsync(coreTeamId, caller.UserId, TracksTeamMemberRole.Manager, cancellationToken);

        var updated = await _teamManager.UpdateTeamAsync(coreTeamId, dto.Name, dto.Description, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.TracksTeamNotFound, "Team not found.");

        _logger.LogInformation("Team {TeamId} updated by user {UserId}", coreTeamId, caller.UserId);

        return await BuildTeamDtoAsync(coreTeamId, cancellationToken)
            ?? throw new System.InvalidOperationException("Team was updated but could not be retrieved.");
    }

    /// <summary>
    /// Deletes a team. Requires Owner role.
    /// Fails if the team has boards, unless cascade is true.
    /// </summary>
    public async Task DeleteTeamAsync(Guid coreTeamId, bool cascade, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (_teamManager is null)
            throw new System.InvalidOperationException("Team management capability is not available.");

        await EnsureTracksTeamRoleAsync(coreTeamId, caller.UserId, TracksTeamMemberRole.Owner, cancellationToken);

        var activeBoards = await _db.Boards
            .Where(b => b.TeamId == coreTeamId && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        if (activeBoards.Count > 0 && !cascade)
        {
            throw new ValidationException(ErrorCodes.TracksTeamHasBoards,
                $"Team has {activeBoards.Count} board(s). Transfer or delete them first, or use cascade=true.");
        }

        if (cascade)
        {
            foreach (var board in activeBoards)
            {
                board.IsDeleted = true;
                board.DeletedAt = DateTime.UtcNow;
                board.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Remove all Tracks role assignments for this team
        var roles = await _db.TeamRoles
            .Where(r => r.CoreTeamId == coreTeamId)
            .ToListAsync(cancellationToken);

        _db.TeamRoles.RemoveRange(roles);
        await _db.SaveChangesAsync(cancellationToken);

        // Soft-delete the Core team
        await _teamManager.DeleteTeamAsync(coreTeamId, cancellationToken);

        _logger.LogInformation("Team {TeamId} deleted by user {UserId} (cascade={Cascade})",
            coreTeamId, caller.UserId, cascade);

        await _eventBus.PublishAsync(new TeamDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            TeamId = coreTeamId,
            DeletedByUserId = caller.UserId,
            CascadeBoards = cascade
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Adds a member to the team (Core + Tracks role).
    /// Requires Manager or Owner role.
    /// </summary>
    public async Task<TracksTeamMemberDto> AddMemberAsync(Guid coreTeamId, Guid userId, TracksTeamMemberRole role, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (_teamManager is null)
            throw new System.InvalidOperationException("Team management capability is not available.");

        await EnsureTracksTeamRoleAsync(coreTeamId, caller.UserId, TracksTeamMemberRole.Manager, cancellationToken);

        if (role == TracksTeamMemberRole.Owner)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                "Cannot add another owner directly. Promote an existing member instead.");

        // Check if already has a Tracks role
        var existingRole = await _db.TeamRoles
            .AnyAsync(r => r.CoreTeamId == coreTeamId && r.UserId == userId, cancellationToken);

        if (existingRole)
            throw new ValidationException(ErrorCodes.TracksAlreadyTeamMember, "User is already a team member.");

        // Add to Core team membership
        await _teamManager.AddMemberAsync(coreTeamId, userId, cancellationToken);

        // Create Tracks role assignment
        var teamRole = new TeamRole
        {
            CoreTeamId = coreTeamId,
            UserId = userId,
            Role = role,
            AssignedAt = DateTime.UtcNow
        };

        _db.TeamRoles.Add(teamRole);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} added to team {TeamId} as {Role} by {CallerId}",
            userId, coreTeamId, role, caller.UserId);

        // Resolve display name for the added member
        string? displayName = null;
        if (_userDirectory is not null)
        {
            var names = await _userDirectory.GetDisplayNamesAsync([userId], cancellationToken);
            names.TryGetValue(userId, out displayName);
        }

        return new TracksTeamMemberDto
        {
            UserId = userId,
            DisplayName = displayName,
            Role = role,
            JoinedAt = teamRole.AssignedAt
        };
    }

    /// <summary>
    /// Removes a member from the team. Requires Manager or Owner role.
    /// </summary>
    public async Task RemoveMemberAsync(Guid coreTeamId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (_teamManager is null)
            throw new System.InvalidOperationException("Team management capability is not available.");

        await EnsureTracksTeamRoleAsync(coreTeamId, caller.UserId, TracksTeamMemberRole.Manager, cancellationToken);

        var tracksRole = await _db.TeamRoles
            .FirstOrDefaultAsync(r => r.CoreTeamId == coreTeamId && r.UserId == userId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.TracksNotTeamMember, "User is not a team member.");

        if (tracksRole.Role == TracksTeamMemberRole.Owner)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole, "Cannot remove the team owner.");

        _db.TeamRoles.Remove(tracksRole);
        await _db.SaveChangesAsync(cancellationToken);

        // Remove from Core team membership
        await _teamManager.RemoveMemberAsync(coreTeamId, userId, cancellationToken);

        _logger.LogInformation("User {UserId} removed from team {TeamId} by {CallerId}",
            userId, coreTeamId, caller.UserId);
    }

    /// <summary>
    /// Updates a member's Tracks role. Requires at least Manager role.
    /// Managers can set Member/Manager roles. Only Owners can promote to Owner.
    /// </summary>
    public async Task UpdateMemberRoleAsync(Guid coreTeamId, Guid userId, TracksTeamMemberRole newRole, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Caller must be at least Manager
        var callerRole = await _db.TeamRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CoreTeamId == coreTeamId && r.UserId == caller.UserId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.TracksNotTeamMember, "You are not a team member.");

        if (callerRole.Role < TracksTeamMemberRole.Manager)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                "You must be at least a Manager to update member roles.");

        // Only Owners can promote to Owner
        if (newRole == TracksTeamMemberRole.Owner && callerRole.Role != TracksTeamMemberRole.Owner)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                "Only team Owners can promote members to Owner.");

        var tracksRole = await _db.TeamRoles
            .FirstOrDefaultAsync(r => r.CoreTeamId == coreTeamId && r.UserId == userId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.TracksNotTeamMember, "User is not a team member.");

        // Managers cannot demote Owners
        if (tracksRole.Role == TracksTeamMemberRole.Owner && callerRole.Role != TracksTeamMemberRole.Owner)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                "Only Owners can change another Owner's role.");

        // Prevent demoting the last owner
        if (tracksRole.Role == TracksTeamMemberRole.Owner && newRole != TracksTeamMemberRole.Owner)
        {
            var ownerCount = await _db.TeamRoles
                .CountAsync(r => r.CoreTeamId == coreTeamId && r.Role == TracksTeamMemberRole.Owner, cancellationToken);

            if (ownerCount <= 1)
                throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                    "Cannot demote the last team owner. Promote another member to Owner first.");
        }

        tracksRole.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} role changed to {Role} on team {TeamId} by {CallerId}",
            userId, newRole, coreTeamId, caller.UserId);
    }

    /// <summary>
    /// Transfers a board to or from a team. Requires board Owner role
    /// and (if transferring to a team) at least Manager role on the target team.
    /// </summary>
    public async Task TransferBoardAsync(Guid boardId, Guid? targetCoreTeamId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Verify the caller owns the board
        var boardMember = await _db.BoardMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == caller.UserId, cancellationToken);

        if (boardMember is null)
            throw new ValidationException(ErrorCodes.NotBoardMember, "You are not a member of this board.");

        if (boardMember.Role < BoardMemberRole.Owner)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Only board owners can transfer boards.");

        var board = await _db.Boards
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        // If transferring to a team, verify caller is a team member.
        // Board owners can transfer to any team they belong to (no Manager requirement).
        if (targetCoreTeamId.HasValue)
        {
            if (_teamDirectory is not null)
            {
                var teamInfo = await _teamDirectory.GetTeamAsync(targetCoreTeamId.Value, cancellationToken);
                if (teamInfo is null)
                    throw new ValidationException(ErrorCodes.TracksTeamNotFound, "Target team not found.");
            }

            var tracksRole = await _db.TeamRoles
                .FirstOrDefaultAsync(r => r.CoreTeamId == targetCoreTeamId.Value && r.UserId == caller.UserId, cancellationToken);

            if (tracksRole is null)
                throw new ValidationException(ErrorCodes.TracksNotTeamMember, "You are not a member of this team.");

            // Auto-promote board owner to Manager if they're only a Member
            if (tracksRole.Role < TracksTeamMemberRole.Manager)
            {
                tracksRole.Role = TracksTeamMemberRole.Manager;
                _logger.LogInformation("Auto-promoted user {UserId} to Manager on team {TeamId} after board transfer",
                    caller.UserId, targetCoreTeamId.Value);
            }
        }

        board.TeamId = targetCoreTeamId;
        board.UpdatedAt = DateTime.UtcNow;
        board.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Board {BoardId} transferred to team {TeamId} by user {UserId}",
            boardId, targetCoreTeamId?.ToString() ?? "(personal)", caller.UserId);
    }

    /// <summary>
    /// Lists boards belonging to a team. Caller must be a core team member.
    /// </summary>
    public async Task<IReadOnlyList<BoardDto>> ListTeamBoardsAsync(Guid coreTeamId, CallerContext caller, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        // Verify caller is a core team member
        if (_teamDirectory is not null)
        {
            var isMember = await _teamDirectory.IsTeamMemberAsync(coreTeamId, caller.UserId, cancellationToken);
            if (!isMember)
                throw new ValidationException(ErrorCodes.TracksNotTeamMember, "You are not a member of this team.");
        }

        var query = _db.Boards
            .AsNoTracking()
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Lists)
            .Where(b => b.TeamId == coreTeamId && !b.IsDeleted);

        if (!includeArchived)
            query = query.Where(b => !b.IsArchived);

        var boards = await query
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync(cancellationToken);

        return boards.Select(b => new BoardDto
        {
            Id = b.Id,
            OwnerId = b.OwnerId,
            TeamId = b.TeamId,
            Title = b.Title,
            Description = b.Description,
            Color = b.Color,
            IsArchived = b.IsArchived,
            IsDeleted = b.IsDeleted,
            DeletedAt = b.DeletedAt,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            ETag = b.ETag,
            Members = b.Members.Select(m => new BoardMemberDto
            {
                UserId = m.UserId,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList(),
            Lists = b.Lists.Where(l => !l.IsArchived).OrderBy(l => l.Position).Select(l => new BoardListDto
            {
                Id = l.Id,
                BoardId = l.BoardId,
                Title = l.Title,
                Color = l.Color,
                Position = (int)l.Position,
                CardLimit = l.CardLimit,
                CardCount = 0,
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
        }).ToList();
    }

    /// <summary>
    /// Gets the effective board role for a user considering both direct board membership and team-derived access.
    /// Team role mapping: Owner → Board Owner, Manager → Board Admin, Member → Board Member.
    /// The higher of the two roles (direct vs team-derived) wins.
    /// </summary>
    public async Task<BoardMemberRole?> GetEffectiveBoardRoleAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        // Direct board membership
        var directMember = await _db.BoardMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId, cancellationToken);

        var directRole = directMember?.Role;

        // Get the board's team
        var board = await _db.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);

        if (board?.TeamId is null)
            return directRole;

        // Check Tracks team role
        var tracksRole = await _db.TeamRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CoreTeamId == board.TeamId && r.UserId == userId, cancellationToken);

        if (tracksRole is null)
        {
            // If user is a core team member without a Tracks role, grant default Member access
            if (_teamDirectory is not null)
            {
                var isMember = await _teamDirectory.IsTeamMemberAsync(board.TeamId.Value, userId, cancellationToken);
                if (isMember)
                {
                    var defaultRole = BoardMemberRole.Member;
                    return directRole is null ? defaultRole :
                        directRole.Value >= defaultRole ? directRole.Value : defaultRole;
                }
            }

            return directRole;
        }

        var teamDerivedRole = tracksRole.Role switch
        {
            TracksTeamMemberRole.Owner => BoardMemberRole.Owner,
            TracksTeamMemberRole.Manager => BoardMemberRole.Admin,
            TracksTeamMemberRole.Member => BoardMemberRole.Member,
            _ => BoardMemberRole.Viewer
        };

        // Return the higher of the two roles
        if (directRole is null)
            return teamDerivedRole;

        return directRole.Value >= teamDerivedRole ? directRole.Value : teamDerivedRole;
    }

    /// <summary>
    /// Ensures the user has at least the specified Tracks role on the team.
    /// </summary>
    internal async Task EnsureTracksTeamRoleAsync(Guid coreTeamId, Guid userId, TracksTeamMemberRole minimumRole, CancellationToken cancellationToken = default)
    {
        var tracksRole = await _db.TeamRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CoreTeamId == coreTeamId && r.UserId == userId, cancellationToken);

        if (tracksRole is null)
            throw new ValidationException(ErrorCodes.TracksNotTeamMember, "You are not a member of this team.");

        if (tracksRole.Role < minimumRole)
            throw new ValidationException(ErrorCodes.TracksInsufficientTeamRole,
                $"Requires at least {minimumRole} role. You have {tracksRole.Role}.");
    }

    private async Task<TracksTeamDto?> BuildTeamDtoAsync(Guid coreTeamId, CancellationToken cancellationToken)
    {
        if (_teamDirectory is null)
            return null;

        var teamInfo = await _teamDirectory.GetTeamAsync(coreTeamId, cancellationToken);
        if (teamInfo is null)
            return null;

        var tracksRoles = await _db.TeamRoles
            .AsNoTracking()
            .Where(r => r.CoreTeamId == coreTeamId)
            .ToListAsync(cancellationToken);

        var boardCount = await _db.Boards
            .CountAsync(b => b.TeamId == coreTeamId && !b.IsDeleted, cancellationToken);

        // Resolve display names
        var userIds = tracksRoles.Select(r => r.UserId).Distinct();
        var displayNames = _userDirectory is not null
            ? await _userDirectory.GetDisplayNamesAsync(userIds, cancellationToken)
            : (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>();

        return new TracksTeamDto
        {
            Id = teamInfo.Id,
            OrganizationId = teamInfo.OrganizationId,
            Name = teamInfo.Name,
            Description = teamInfo.Description,
            CreatedAt = teamInfo.CreatedAt,
            BoardCount = boardCount,
            Members = tracksRoles.Select(r => new TracksTeamMemberDto
            {
                UserId = r.UserId,
                DisplayName = displayNames.GetValueOrDefault(r.UserId),
                Role = r.Role,
                JoinedAt = r.AssignedAt
            }).ToList()
        };
    }
}
