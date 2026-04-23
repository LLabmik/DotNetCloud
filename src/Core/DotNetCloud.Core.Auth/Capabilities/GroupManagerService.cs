using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IGroupManager"/> providing write operations for Core group management.
/// </summary>
public sealed class GroupManagerService : IGroupManager
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GroupManagerService"/>.
    /// </summary>
    public GroupManagerService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<GroupInfo> CreateGroupAsync(Guid organizationId, string name, string? description, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        EnsureNameAllowed(normalizedName);

        if (organizationId == Guid.Empty)
        {
            organizationId = await ResolveDefaultOrganizationIdAsync(cancellationToken);

            if (organizationId == Guid.Empty)
                throw new InvalidOperationException("No organization exists. Create an organization first.");
        }

        var exists = await _dbContext.Groups
            .AnyAsync(g => g.OrganizationId == organizationId && g.Name == normalizedName, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"A group named '{normalizedName}' already exists in the organization.");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = normalizedName,
            Description = NormalizeDescription(description),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroup(group, memberCount: 0);
    }

    /// <inheritdoc />
    public async Task<GroupInfo?> UpdateGroupAsync(Guid groupId, string? name, string? description, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
            return null;

        EnsureMutable(group);

        if (name is not null)
        {
            var normalizedName = NormalizeName(name);
            EnsureNameAllowed(normalizedName);
            var duplicate = await _dbContext.Groups
                .AnyAsync(g => g.OrganizationId == group.OrganizationId && g.Id != groupId && g.Name == normalizedName, cancellationToken);

            if (duplicate)
                throw new InvalidOperationException($"A group named '{normalizedName}' already exists in the organization.");

            group.Name = normalizedName;
        }

        if (description is not null)
            group.Description = NormalizeDescription(description);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroup(group, group.Members.Count);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
            return false;

        EnsureMutable(group);

        group.IsDeleted = true;
        group.DeletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddMemberAsync(Guid groupId, Guid userId, Guid? addedByUserId = null, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
            throw new InvalidOperationException("Group does not exist.");

        EnsureMutable(group);

        var exists = await _dbContext.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);

        if (exists)
            return false;

        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
            throw new InvalidOperationException("User does not exist.");

        _dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = addedByUserId
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
            return false;

        EnsureMutable(group);

        var member = await _dbContext.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);

        if (member is null)
            return false;

        _dbContext.GroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Guid> ResolveDefaultOrganizationIdAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Organizations
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name is required.", nameof(name));

        return name.Trim();
    }

    private static void EnsureNameAllowed(string name)
    {
        if (string.Equals(name, Group.AllUsersGroupName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The name '{Group.AllUsersGroupName}' is reserved for the built-in organization group.");
    }

    private static void EnsureMutable(Group group)
    {
        if (group.IsAllUsersGroup)
            throw new InvalidOperationException("The built-in All Users group is system-managed and cannot be modified directly.");
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static GroupInfo MapGroup(Group group, int memberCount)
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
}