using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Evaluates effective permissions by checking ownership, direct user shares,
/// and cascading parent-folder shares.
/// </summary>
internal sealed class PermissionService : IPermissionService
{
    private readonly FilesDbContext _db;
    private readonly IShareAccessMembershipResolver? _membershipResolver;

    /// <summary>
    /// Initializes a new instance of <see cref="PermissionService"/>.
    /// </summary>
    public PermissionService(
        FilesDbContext db,
        IShareAccessMembershipResolver? membershipResolver = null)
    {
        _db = db;
        _membershipResolver = membershipResolver;
    }

    /// <inheritdoc />
    public async Task<SharePermission?> GetEffectivePermissionAsync(
        Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        // System callers bypass all restrictions.
        if (caller.Type == CallerType.System)
            return SharePermission.Full;

        // Project only the fields needed to avoid loading entire entity.
        var node = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => new { n.OwnerId, n.MaterializedPath })
            .FirstOrDefaultAsync(cancellationToken);

        if (node is null)
            return null;

        // Owners always have Full permission.
        if (node.OwnerId == caller.UserId)
            return SharePermission.Full;

        // Collect the node itself plus every ancestor from the materialized path.
        var idsToCheck = ParseAncestorIds(node.MaterializedPath);
        idsToCheck.Add(nodeId);

        var now = DateTime.UtcNow;
        var memberships = _membershipResolver is null
            ? ShareAccessMembership.Empty
            : await _membershipResolver.ResolveAsync(caller.UserId, cancellationToken);
        var teamIds = memberships.TeamIds.ToArray();
        var groupIds = memberships.GroupIds.ToArray();

        // Find the most permissive active, non-public-link share for this user,
        // their teams, or their groups.
        // Permission is stored as a string in the database, so we fetch matching
        // share permission strings and resolve the max in memory.
        var permissions = await _db.FileShares
            .AsNoTracking()
            .Where(s =>
                idsToCheck.Contains(s.FileNodeId)
                && s.ShareType != ShareType.PublicLink
                && (
                    (s.ShareType == ShareType.User && s.SharedWithUserId == caller.UserId)
                    || (teamIds.Length > 0 && s.ShareType == ShareType.Team && s.SharedWithTeamId.HasValue && teamIds.Contains(s.SharedWithTeamId.Value))
                    || (groupIds.Length > 0 && s.ShareType == ShareType.Group && s.SharedWithGroupId.HasValue && groupIds.Contains(s.SharedWithGroupId.Value))
                )
                && (s.ExpiresAt == null || s.ExpiresAt > now))
            .Select(s => s.Permission)
            .ToListAsync(cancellationToken);

        if (permissions.Count == 0)
            return null;

        return permissions.Max();
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(
        Guid nodeId, CallerContext caller, SharePermission required, CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectivePermissionAsync(nodeId, caller, cancellationToken);
        return effective.HasValue && effective.Value >= required;
    }

    /// <inheritdoc />
    public async Task RequirePermissionAsync(
        Guid nodeId, CallerContext caller, SharePermission required, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(nodeId, caller, required, cancellationToken))
            throw new ForbiddenException("You do not have permission to perform this operation.");
    }

    /// <summary>
    /// Extracts ancestor node IDs from a materialized path string such as
    /// <c>/guid1/guid2/guid3</c>.
    /// </summary>
    private static List<Guid> ParseAncestorIds(string materializedPath)
    {
        return materializedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.TryParse(segment, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
