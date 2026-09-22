using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="ITeamDirectory"/> providing read-only access to team and membership data.
/// </summary>
/// <remarks>
/// Each operation runs on its own short-lived <see cref="CoreDbContext"/> taken from
/// <see cref="IDbContextFactory"/>. The service is scoped while the Blazor circuit holding it is
/// long-lived, so a context captured in the constructor would receive overlapping queries from
/// components whose initializers interleave (which throws "a second operation was started on this
/// context instance"). See <c>UserSettingsService</c> for the same pattern.
/// </remarks>
public sealed class TeamDirectoryService : ITeamDirectory
{
    private readonly IDbContextFactory _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="TeamDirectoryService"/>.
    /// </summary>
    /// <param name="dbContextFactory">Factory used to create a short-lived context per operation.</param>
    public TeamDirectoryService(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<TeamInfo?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var team = await dbContext.Teams
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
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var teamIds = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.TeamId)
            .ToListAsync(cancellationToken);

        if (teamIds.Count == 0)
            return [];

        var teams = await dbContext.Teams
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
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.TeamMembers
            .AsNoTracking()
            .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TeamMemberInfo?> GetTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var member = await dbContext.TeamMembers
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
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var members = await dbContext.TeamMembers
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
