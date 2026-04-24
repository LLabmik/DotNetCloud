using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages admin-defined shared folders exposed through the Files module.
/// </summary>
public interface IAdminSharedFolderService
{
    /// <summary>Lists all registered admin shared folders.</summary>
    Task<IReadOnlyList<AdminSharedFolderDto>> GetSharedFoldersAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single admin shared folder by ID.</summary>
    Task<AdminSharedFolderDto> GetSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Browses directories beneath the local filesystem root.</summary>
    Task<AdminSharedFolderDirectoryBrowseDto> BrowseDirectoriesAsync(string? sourcePath, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Creates a new admin shared folder definition.</summary>
    Task<AdminSharedFolderDto> CreateSharedFolderAsync(CreateAdminSharedFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing admin shared folder definition and its grants.</summary>
    Task<AdminSharedFolderDto> UpdateSharedFolderAsync(Guid sharedFolderId, UpdateAdminSharedFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes an existing admin shared folder definition.</summary>
    Task DeleteSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Requests a full reindex for a shared folder.</summary>
    Task<AdminSharedFolderDto> RequestReindexAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Schedules the next rescan for a shared folder.</summary>
    Task<AdminSharedFolderDto> ScheduleRescanAsync(Guid sharedFolderId, DateTime? nextScheduledScanAt, CallerContext caller, CancellationToken cancellationToken = default);
}