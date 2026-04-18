using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for extracting and storing EXIF metadata from photos.
/// </summary>
public sealed class PhotoMetadataService
{
    private readonly PhotosDbContext _db;
    private readonly IMediaMetadataExtractor? _exifExtractor;
    private readonly ILogger<PhotoMetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoMetadataService"/> class.
    /// </summary>
    public PhotoMetadataService(PhotosDbContext db, ILogger<PhotoMetadataService> logger, IMediaMetadataExtractor? exifExtractor = null)
    {
        _db = db;
        _logger = logger;
        _exifExtractor = exifExtractor;
    }

    /// <summary>
    /// Extracts metadata from a photo file and stores it.
    /// </summary>
    public async Task<PhotoMetadata?> ExtractAndStoreAsync(Guid photoId, string filePath, string mimeType, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FindAsync([photoId], cancellationToken);
        if (photo is null) return null;

        MediaMetadataDto? extracted = null;
        if (_exifExtractor is not null && _exifExtractor.CanExtract(mimeType))
        {
            extracted = await _exifExtractor.ExtractAsync(filePath, mimeType, cancellationToken);
        }

        var metadata = new PhotoMetadata
        {
            PhotoId = photoId,
            CameraMake = extracted?.CameraMake,
            CameraModel = extracted?.CameraModel,
            LensModel = extracted?.LensModel,
            FocalLengthMm = extracted?.FocalLengthMm,
            Aperture = extracted?.Aperture,
            ShutterSpeed = extracted?.ShutterSpeed,
            Iso = extracted?.Iso,
            FlashFired = extracted?.FlashFired,
            Orientation = extracted?.Orientation,
            Latitude = extracted?.Location?.Latitude,
            Longitude = extracted?.Location?.Longitude,
            AltitudeMetres = extracted?.Location?.AltitudeMetres,
            TakenAtUtc = extracted?.TakenAtUtc
        };

        // Update photo dimensions if extracted
        if (extracted?.Width.HasValue == true)
        {
            photo.Width = extracted.Width;
            photo.Height = extracted.Height;
        }

        if (extracted?.TakenAtUtc.HasValue == true)
        {
            photo.TakenAt = extracted.TakenAtUtc.Value;
        }

        _db.PhotoMetadata.Add(metadata);
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Metadata extracted for photo {PhotoId}: {CameraMake} {CameraModel}", photoId, metadata.CameraMake, metadata.CameraModel);

        return metadata;
    }

    /// <summary>
    /// Gets metadata for a photo.
    /// </summary>
    public async Task<PhotoMetadata?> GetMetadataAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        return await _db.PhotoMetadata.FirstOrDefaultAsync(m => m.PhotoId == photoId, cancellationToken);
    }
}
