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
/// <remarks>
/// Each operation runs on its own short-lived <see cref="CoreDbContext"/> taken from
/// <see cref="IDbContextFactory"/>. The service is scoped while the Blazor circuit holding it is
/// long-lived, so a context captured in the constructor would receive overlapping queries from
/// components whose initializers interleave (which throws "a second operation was started on this
/// context instance"). See <c>UserSettingsService</c> for the same pattern.
/// </remarks>
public sealed class OrganizationDirectoryService : IOrganizationDirectory
{
    private readonly IDbContextFactory _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="OrganizationDirectoryService"/>.
    /// </summary>
    /// <param name="dbContextFactory">Factory used to create a short-lived context per operation.</param>
    public OrganizationDirectoryService(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<bool> IsOrganizationMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationMemberInfo?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var member = await FindMemberAsync(dbContext, organizationId, userId, cancellationToken);

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
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Join(dbContext.Organizations,
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
        await using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId
                           && m.UserId == userId
                           && m.IsActive
                           && m.RoleIds.Contains(roleId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasManagerOrAboveRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var member = await FindMemberAsync(dbContext, organizationId, userId, cancellationToken);
        return member is not null && OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();

        var member = await FindMemberAsync(dbContext, organizationId, userId, cancellationToken);
        return member is null ? [] : member.RoleIds.ToList();
    }

    /// <summary>
    /// Reads an active membership for the given organization and user on the supplied context.
    /// </summary>
    /// <param name="dbContext">The context owned by the calling operation.</param>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The membership entity, or <see langword="null"/>.</returns>
    private static Task<OrganizationMember?> FindMemberAsync(
        CoreDbContext dbContext,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive, cancellationToken);
    }
}
