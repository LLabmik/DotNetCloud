using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Manages photo CRUD and query operations.
/// </summary>
public interface IPhotoService
{
    /// <summary>Creates a photo record from an uploaded file.</summary>
    Task<PhotoDto> CreatePhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single photo by ID.</summary>
    Task<PhotoDto?> GetPhotoAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists photos with paging.</summary>
    Task<IReadOnlyList<PhotoDto>> ListPhotosAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Returns photos within a date range for timeline view.</summary>
    Task<IReadOnlyList<PhotoDto>> GetTimelineAsync(CallerContext caller, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Toggles the favorite flag on a photo.</summary>
    Task<PhotoDto> ToggleFavoriteAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Returns all favorited photos.</summary>
    Task<IReadOnlyList<PhotoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a photo.</summary>
    Task DeletePhotoAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches photos by query string.</summary>
    Task<IReadOnlyList<PhotoDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default);
}
