using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages file and folder sharing: user, team, and public link shares.
/// </summary>
public interface IShareService
{
    /// <summary>Creates a new share on a file or folder.</summary>
    Task<FileShareDto> CreateShareAsync(Guid fileNodeId, CreateShareDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing share.</summary>
    Task<FileShareDto> UpdateShareAsync(Guid shareId, UpdateShareDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a share.</summary>
    Task DeleteShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all shares on a file or folder.</summary>
    Task<IReadOnlyList<FileShareDto>> GetSharesAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all files shared with the caller.</summary>
    Task<IReadOnlyList<FileShareDto>> GetSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all files shared by the caller.</summary>
    Task<IReadOnlyList<FileShareDto>> GetSharedByMeAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Resolves a public link token to a share. Returns null if invalid/expired.</summary>
    Task<FileShareDto?> ResolvePublicLinkAsync(string linkToken, string? password, CancellationToken cancellationToken = default);

    /// <summary>Increments the download counter on a public link share.</summary>
    Task IncrementDownloadCountAsync(Guid shareId, CancellationToken cancellationToken = default);
}
