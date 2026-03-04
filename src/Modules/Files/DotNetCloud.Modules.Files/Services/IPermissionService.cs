using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Evaluates effective permissions for a caller on a file node.
/// Considers direct ownership, active user-level shares, and cascading
/// parent-folder shares.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Returns the most permissive share-level the caller holds on the node,
    /// or <see langword="null"/> if the caller has no access.
    /// Owners always receive <see cref="SharePermission.Full"/>.
    /// System callers always receive <see cref="SharePermission.Full"/>.
    /// </summary>
    Task<SharePermission?> GetEffectivePermissionAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the caller holds at least <paramref name="required"/>
    /// permission on the node.
    /// </summary>
    Task<bool> HasPermissionAsync(Guid nodeId, CallerContext caller, SharePermission required, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="DotNetCloud.Core.Errors.ForbiddenException"/> if the
    /// caller does not hold at least <paramref name="required"/> permission on
    /// the node.
    /// </summary>
    Task RequirePermissionAsync(Guid nodeId, CallerContext caller, SharePermission required, CancellationToken cancellationToken = default);
}
