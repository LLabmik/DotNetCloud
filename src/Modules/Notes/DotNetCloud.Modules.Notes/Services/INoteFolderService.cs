using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Notes.Services;

/// <summary>
/// Note folder CRUD operations.
/// </summary>
public interface INoteFolderService
{
    /// <summary>Creates a new folder.</summary>
    Task<NoteFolderDto> CreateFolderAsync(CreateNoteFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a folder by ID.</summary>
    Task<NoteFolderDto?> GetFolderAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists folders for the calling user.</summary>
    Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(CallerContext caller, Guid? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing folder.</summary>
    Task<NoteFolderDto> UpdateFolderAsync(Guid folderId, UpdateNoteFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a folder.</summary>
    Task DeleteFolderAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default);
}
