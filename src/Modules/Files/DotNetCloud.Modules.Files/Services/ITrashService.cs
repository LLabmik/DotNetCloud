using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages trash operations: listing, restoring, and permanently deleting soft-deleted files.
/// </summary>
public interface ITrashService
{
    /// <summary>Lists items in the caller's trash.</summary>
    Task<IReadOnlyList<TrashItemDto>> ListTrashAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores a single item from the trash.</summary>
    Task<FileNodeDto> RestoreAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores all items in the caller's trash.</summary>
    Task RestoreAllAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a single item (cascades to versions, chunks, shares, tags, comments).</summary>
    Task PermanentDeleteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Empties the entire trash for the caller.</summary>
    Task EmptyTrashAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
