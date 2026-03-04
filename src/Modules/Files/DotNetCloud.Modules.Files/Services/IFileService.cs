using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Core file and folder operations: CRUD, tree navigation, move/copy, favorites.
/// </summary>
public interface IFileService
{
    /// <summary>Creates a new folder.</summary>
    Task<FileNodeDto> CreateFolderAsync(CreateFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a file or folder by ID.</summary>
    Task<FileNodeDto?> GetNodeAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists children of a folder.</summary>
    Task<IReadOnlyList<FileNodeDto>> ListChildrenAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists root-level nodes for the caller.</summary>
    Task<IReadOnlyList<FileNodeDto>> ListRootAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Renames a file or folder.</summary>
    Task<FileNodeDto> RenameAsync(Guid nodeId, RenameNodeDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Moves a file or folder to a new parent.</summary>
    Task<FileNodeDto> MoveAsync(Guid nodeId, MoveNodeDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Copies a file or folder to a new parent.</summary>
    Task<FileNodeDto> CopyAsync(Guid nodeId, Guid targetParentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a file or folder (moves to trash).</summary>
    Task DeleteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Toggles the favorite status of a node.</summary>
    Task<FileNodeDto> ToggleFavoriteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists the caller's favorite nodes.</summary>
    Task<IReadOnlyList<FileNodeDto>> ListFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches for files and folders by name.</summary>
    Task<PagedResult<FileNodeDto>> SearchAsync(string query, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists the most recently updated files for the caller.</summary>
    Task<IReadOnlyList<FileNodeDto>> ListRecentAsync(int count, CallerContext caller, CancellationToken cancellationToken = default);
}
