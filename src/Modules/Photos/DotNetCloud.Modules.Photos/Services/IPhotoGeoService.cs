using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Provides geographic clustering and querying for photos.
/// </summary>
public interface IPhotoGeoService
{
    /// <summary>Gets all geo-tagged photos for a user.</summary>
    Task<IReadOnlyList<PhotoDto>> GetGeoTaggedPhotosAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets geographic clusters for map display.</summary>
    Task<IReadOnlyList<GeoClusterDto>> GetGeoClustersAsync(Guid userId, double gridSizeDegrees = 0.5, CancellationToken cancellationToken = default);
}
