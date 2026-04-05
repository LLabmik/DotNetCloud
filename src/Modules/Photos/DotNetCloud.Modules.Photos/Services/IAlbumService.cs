using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Manages photo albums.
/// </summary>
public interface IAlbumService
{
    /// <summary>Creates a new album.</summary>
    Task<AlbumDto> CreateAlbumAsync(CreateAlbumDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets an album by ID.</summary>
    Task<AlbumDto?> GetAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all albums for the caller.</summary>
    Task<IReadOnlyList<AlbumDto>> ListAlbumsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an album.</summary>
    Task<AlbumDto> UpdateAlbumAsync(Guid albumId, UpdateAlbumDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes an album.</summary>
    Task DeleteAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a photo to an album.</summary>
    Task AddPhotoToAlbumAsync(Guid albumId, Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a photo from an album.</summary>
    Task RemovePhotoFromAlbumAsync(Guid albumId, Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all photos in an album.</summary>
    Task<IReadOnlyList<PhotoDto>> GetAlbumPhotosAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);
}
