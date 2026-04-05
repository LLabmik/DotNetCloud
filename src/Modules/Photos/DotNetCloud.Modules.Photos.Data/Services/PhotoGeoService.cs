using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Modules.Photos.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for geographic clustering of photos.
/// Groups nearby photos into clusters for map display.
/// </summary>
public sealed class PhotoGeoService
{
    private readonly PhotosDbContext _db;
    private readonly ILogger<PhotoGeoService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoGeoService"/> class.
    /// </summary>
    public PhotoGeoService(PhotosDbContext db, ILogger<PhotoGeoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets geo-tagged photos for a user.
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> GetGeoTaggedPhotosAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Include(p => p.Metadata)
            .Include(p => p.Tags)
            .Where(p => p.OwnerId == userId && p.Metadata != null && p.Metadata.Latitude != null && p.Metadata.Longitude != null)
            .OrderByDescending(p => p.TakenAt)
            .ToListAsync(cancellationToken);

        return photos.Select(PhotoService.MapToDto).ToList();
    }

    /// <summary>
    /// Gets photo clusters grouped by geographic proximity.
    /// Uses a simple grid-based clustering algorithm.
    /// </summary>
    /// <param name="userId">The user whose photos to cluster.</param>
    /// <param name="gridSizeDegrees">Grid cell size in degrees (smaller = more clusters). Default 0.5°.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<GeoClusterDto>> GetGeoClustersAsync(Guid userId, double gridSizeDegrees = 0.5, CancellationToken cancellationToken = default)
    {
        var geoPhotos = await _db.Photos
            .Include(p => p.Metadata)
            .Where(p => p.OwnerId == userId && p.Metadata != null && p.Metadata.Latitude != null && p.Metadata.Longitude != null)
            .Select(p => new
            {
                p.Id,
                Lat = p.Metadata!.Latitude!.Value,
                Lng = p.Metadata!.Longitude!.Value
            })
            .ToListAsync(cancellationToken);

        if (geoPhotos.Count == 0)
            return [];

        // Grid-based clustering
        var clusters = geoPhotos
            .GroupBy(p => new
            {
                LatBucket = Math.Floor(p.Lat / gridSizeDegrees),
                LngBucket = Math.Floor(p.Lng / gridSizeDegrees)
            })
            .Select(g => new GeoClusterDto
            {
                Latitude = g.Average(p => p.Lat),
                Longitude = g.Average(p => p.Lng),
                PhotoCount = g.Count(),
                RepresentativePhotoId = g.First().Id,
                RadiusMetres = gridSizeDegrees * 111_000 / 2 // approximate metres per degree
            })
            .ToList();

        return clusters;
    }
}
