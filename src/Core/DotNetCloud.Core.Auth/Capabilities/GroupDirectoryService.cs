using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IGroupDirectory"/> providing read-only access to group and membership data.
/// </summary>
public sealed class GroupDirectoryService : IGroupDirectory
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GroupDirectoryService"/>.
    /// </summary>
    public GroupDirectoryService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<GroupInfo?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group is null)
            return null;

        var memberCount = group.IsAllUsersGroup
            ? await GetActiveOrganizationMemberCountAsync(group.OrganizationId, cancellationToken)
            : group.Members.Count;

        return MapGroup(group, memberCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupInfo>> GetGroupsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            organizationId = await ResolveDefaultOrganizationIdAsync(cancellationToken);
            if (organizationId == Guid.Empty)
                return [];
        }

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => g.OrganizationId == organizationId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var allUsersMemberCount = groups.Any(g => g.IsAllUsersGroup)
            ? await GetActiveOrganizationMemberCountAsync(organizationId, cancellationToken)
            : 0;

        return groups.Select(group => MapGroup(group, group.IsAllUsersGroup ? allUsersMemberCount : group.Members.Count)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupInfo>> GetGroupsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync(cancellationToken);

        var organizationIds = await _dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(om => om.UserId == userId && om.IsActive)
            .Select(om => om.OrganizationId)
            .ToListAsync(cancellationToken);

        if (groupIds.Count == 0 && organizationIds.Count == 0)
            return [];

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => groupIds.Contains(g.Id) || (g.IsAllUsersGroup && organizationIds.Contains(g.OrganizationId)))
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var organizationMemberCounts = await GetActiveOrganizationMemberCountsAsync(
            groups.Where(g => g.IsAllUsersGroup).Select(g => g.OrganizationId),
            cancellationToken);

        return groups.Select(group => MapGroup(
            group,
            group.IsAllUsersGroup
                ? organizationMemberCounts.GetValueOrDefault(group.OrganizationId)
                : group.Members.Count)).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.OrganizationId, g.IsAllUsersGroup })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
            return false;

        if (group.IsAllUsersGroup)
        {
            return await _dbContext.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(om => om.OrganizationId == group.OrganizationId && om.UserId == userId && om.IsActive, cancellationToken);
        }

        return await _dbContext.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GroupMemberInfo?> GetGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.OrganizationId, g.IsAllUsersGroup })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
            return null;

        if (group.IsAllUsersGroup)
        {
            var organizationMember = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(om => om.OrganizationId == group.OrganizationId && om.UserId == userId && om.IsActive, cancellationToken);

            if (organizationMember is null)
                return null;

            return new GroupMemberInfo
            {
                GroupId = groupId,
                UserId = organizationMember.UserId,
                AddedAt = organizationMember.JoinedAt,
                AddedByUserId = organizationMember.InvitedByUserId
            };
        }

        var member = await _dbContext.GroupMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);

        return member is null ? null : MapMember(member);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupMemberInfo>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.OrganizationId, g.IsAllUsersGroup })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
            return [];

        if (group.IsAllUsersGroup)
        {
            var organizationMembers = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .Where(om => om.OrganizationId == group.OrganizationId && om.IsActive)
                .OrderBy(om => om.JoinedAt)
                .ToListAsync(cancellationToken);

            return organizationMembers.Select(member => new GroupMemberInfo
            {
                GroupId = groupId,
                UserId = member.UserId,
                AddedAt = member.JoinedAt,
                AddedByUserId = member.InvitedByUserId
            }).ToList();
        }

        var members = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.GroupId == groupId)
            .OrderBy(gm => gm.AddedAt)
            .ToListAsync(cancellationToken);

        return members.Select(MapMember).ToList();
    }

    private async Task<Guid> ResolveDefaultOrganizationIdAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Organizations
            .AsNoTracking()
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<int> GetActiveOrganizationMemberCountAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return _dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(om => om.OrganizationId == organizationId && om.IsActive)
            .CountAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, int>> GetActiveOrganizationMemberCountsAsync(
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        var organizationIdList = organizationIds.Distinct().ToList();
        if (organizationIdList.Count == 0)
            return [];

        return await _dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(om => organizationIdList.Contains(om.OrganizationId) && om.IsActive)
            .GroupBy(om => om.OrganizationId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
    }

    private static GroupInfo MapGroup(DotNetCloud.Core.Data.Entities.Organizations.Group group, int memberCount)
    {
        return new GroupInfo
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Description = group.Description,
            IsAllUsersGroup = group.IsAllUsersGroup,
            MemberCount = memberCount,
            CreatedAt = group.CreatedAt
        };
    }

    private static GroupMemberInfo MapMember(DotNetCloud.Core.Data.Entities.Organizations.GroupMember member)
    {
        return new GroupMemberInfo
        {
            GroupId = member.GroupId,
            UserId = member.UserId,
            AddedAt = member.AddedAt,
            AddedByUserId = member.AddedByUserId
        };
    }
}