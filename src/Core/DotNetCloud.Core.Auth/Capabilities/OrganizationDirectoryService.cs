using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IOrganizationDirectory"/> providing read-only access to
/// organization membership data for module authorization checks.
/// </summary>
public sealed class OrganizationDirectoryService : IOrganizationDirectory
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrganizationDirectoryService"/>.
    /// </summary>
    public OrganizationDirectoryService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> IsOrganizationMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationMemberInfo?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive, cancellationToken);

        if (member is null)
            return null;

        return new OrganizationMemberInfo
        {
            OrganizationId = member.OrganizationId,
            UserId = member.UserId,
            RoleIds = member.RoleIds.ToList(),
            IsActive = member.IsActive
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Join(_dbContext.Organizations,
                m => m.OrganizationId,
                o => o.Id,
                (m, o) => new OrganizationDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    Description = o.Description,
                    CreatedAt = o.CreatedAt,
                    IsDeleted = o.IsDeleted,
                    DeletedAt = o.DeletedAt,
                    MemberCount = o.Members.Count,
                    TeamCount = o.Teams.Count
                })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasOrgRoleAsync(Guid organizationId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId
                           && m.UserId == userId
                           && m.IsActive
                           && m.RoleIds.Contains(roleId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasManagerOrAboveRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(organizationId, userId, cancellationToken);
        return member is not null && OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(organizationId, userId, cancellationToken);
        return member?.RoleIds ?? Array.Empty<Guid>();
    }
}
