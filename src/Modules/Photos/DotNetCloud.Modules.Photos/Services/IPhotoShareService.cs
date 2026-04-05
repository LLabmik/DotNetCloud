using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Manages photo and album sharing.
/// </summary>
public interface IPhotoShareService
{
    /// <summary>Shares a photo with another user.</summary>
    Task<PhotoShareDto> SharePhotoAsync(Guid photoId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Shares an album with another user.</summary>
    Task<PhotoShareDto> ShareAlbumAsync(Guid albumId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a share.</summary>
    Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all shares for a photo.</summary>
    Task<IReadOnlyList<PhotoShareDto>> GetPhotoSharesAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all shares for an album.</summary>
    Task<IReadOnlyList<PhotoShareDto>> GetAlbumSharesAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all items shared with the caller.</summary>
    Task<IReadOnlyList<PhotoShareDto>> GetSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
