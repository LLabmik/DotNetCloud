using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="ITeamManager"/> providing write operations for Core team management.
/// </summary>
public sealed class TeamManagerService : ITeamManager
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="TeamManagerService"/>.
    /// </summary>
    public TeamManagerService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<TeamInfo> CreateTeamAsync(Guid organizationId, string name, string? description, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        // When no organization is specified, use the first available organization.
        if (organizationId == Guid.Empty)
        {
            var defaultOrg = await _dbContext.Organizations
                .Where(o => !o.IsDeleted)
                .OrderBy(o => o.CreatedAt)
                .Select(o => o.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultOrg == Guid.Empty)
                throw new InvalidOperationException("No organization exists. Create an organization first.");

            organizationId = defaultOrg;
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Teams.Add(team);

        // Add the creator as the first member
        var member = new TeamMember
        {
            TeamId = team.Id,
            UserId = createdByUserId,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.TeamMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TeamInfo
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            Name = team.Name,
            Description = team.Description,
            MemberCount = 1,
            CreatedAt = team.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<TeamInfo?> UpdateTeamAsync(Guid teamId, string? name, string? description, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);

        if (team is null)
            return null;

        if (name is not null) team.Name = name;
        if (description is not null) team.Description = description;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
    public async Task<bool> DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);

        if (team is null)
            return false;

        team.IsDeleted = true;
        team.DeletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.TeamMembers
            .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);

        if (exists)
            return false;

        _dbContext.TeamMembers.Add(new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);

        if (member is null)
            return false;

        _dbContext.TeamMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
