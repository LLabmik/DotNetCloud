using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.DTOs;
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
        return await _dbContext.Set<DotNetCloud.Core.Data.Entities.Organizations.OrganizationMember>()
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationMemberInfo?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.Set<DotNetCloud.Core.Data.Entities.Organizations.OrganizationMember>()
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
}
