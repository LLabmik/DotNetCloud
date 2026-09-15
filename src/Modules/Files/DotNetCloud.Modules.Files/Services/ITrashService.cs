using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages trash operations: listing, restoring, and permanently deleting soft-deleted files.
/// </summary>
public interface ITrashService
{
    /// <summary>Lists items in the caller's trash.</summary>
    /// <remarks>
    /// <see cref="TrashItemDto.OriginalPath"/> is the name-based path of the folder the item will be
    /// restored into (<c>"/"</c> when it restores to the root level).
    /// </remarks>
    Task<IReadOnlyList<TrashItemDto>> ListTrashAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores a single item from the trash.</summary>
    /// <remarks>
    /// If the item's original parent folder is itself still in the trash, the whole deleted ancestor
    /// chain is restored with it (siblings included) so the subtree returns to its original path.
    /// Only when no ancestor can be restored — it was permanently purged — does the item fall back to
    /// the root level, and that fallback is logged.
    /// </remarks>
    Task<FileNodeDto> RestoreAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores all items in the caller's trash.</summary>
    /// <remarks>
    /// Only top-level items (nodes that were deleted from the root level) are restored individually;
    /// their descendants follow their restored parent, which preserves the original directory structure.
    /// </remarks>
    Task RestoreAllAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a single item (cascades to versions, chunks, shares, tags, comments).</summary>
    Task PermanentDeleteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Empties the entire trash for the caller.</summary>
    Task EmptyTrashAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the total size of all items in the caller's trash.</summary>
    Task<long> GetTrashSizeAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
