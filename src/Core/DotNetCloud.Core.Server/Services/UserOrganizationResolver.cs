using DotNetCloud.Core.Data.Context;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Resolves a user's primary organization using the core database.
/// </summary>
internal sealed class UserOrganizationResolver : IUserOrganizationResolver
{
    private readonly CoreDbContext _db;

    public UserOrganizationResolver(CoreDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetOrganizationIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Guid>> GetOrganizationIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var userIdList = userIds.Distinct().ToList();
        if (userIdList.Count == 0)
            return new Dictionary<Guid, Guid>();

        // Take the first org membership per user
        var memberships = await _db.OrganizationMembers
            .AsNoTracking()
            .Where(m => userIdList.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, OrgId = g.First().OrganizationId })
            .ToDictionaryAsync(x => x.UserId, x => x.OrgId, cancellationToken);

        return memberships;
    }
}
