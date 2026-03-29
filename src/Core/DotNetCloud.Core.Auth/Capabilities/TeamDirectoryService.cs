using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="ITeamDirectory"/> providing read-only access to team and membership data.
/// </summary>
public sealed class TeamDirectoryService : ITeamDirectory
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="TeamDirectoryService"/>.
    /// </summary>
    public TeamDirectoryService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<TeamInfo?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.Teams
            .AsNoTracking()
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null)
            return null;

        return new TeamInfo
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            Name = team.Name,
            Description = team.Description,
            MemberCount = team.Members.Count,
            CreatedAt = team.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamInfo>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var teamIds = await _dbContext.TeamMembers
            .AsNoTracking()
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.TeamId)
            .ToListAsync(cancellationToken);

        if (teamIds.Count == 0)
            return [];

        var teams = await _dbContext.Teams
            .AsNoTracking()
            .Include(t => t.Members)
            .Where(t => teamIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        return teams.Select(t => new TeamInfo
        {
            Id = t.Id,
            OrganizationId = t.OrganizationId,
            Name = t.Name,
            Description = t.Description,
            MemberCount = t.Members.Count,
            CreatedAt = t.CreatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TeamMembers
            .AsNoTracking()
            .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TeamMemberInfo?> GetTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.TeamMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);

        if (member is null)
            return null;

        return new TeamMemberInfo
        {
            TeamId = member.TeamId,
            UserId = member.UserId,
            RoleIds = member.RoleIds.ToList(),
            JoinedAt = member.JoinedAt
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamMemberInfo>> GetTeamMembersAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.TeamMembers
            .AsNoTracking()
            .Where(tm => tm.TeamId == teamId)
            .ToListAsync(cancellationToken);

        return members.Select(m => new TeamMemberInfo
        {
            TeamId = m.TeamId,
            UserId = m.UserId,
            RoleIds = m.RoleIds.ToList(),
            JoinedAt = m.JoinedAt
        }).ToList();
    }
}
