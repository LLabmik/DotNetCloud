using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace DotNetCloud.Core.ServiceDefaults.Media;

/// <summary>
/// Extracts EXIF, GPS, camera, and orientation metadata from raster image files
/// using ImageSharp (which is already referenced by the Files module for thumbnails).
/// </summary>
public sealed class ExifMetadataExtractor : IMediaMetadataExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff"
    };

    private readonly ILogger<ExifMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifMetadataExtractor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ExifMetadataExtractor(ILogger<ExifMetadataExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public MediaType SupportedMediaType => MediaType.Photo;

    /// <inheritdoc />
    public bool CanExtract(string mimeType) => SupportedMimeTypes.Contains(mimeType);

    /// <inheritdoc />
    public async Task<MediaMetadataDto?> ExtractAsync(
        string filePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Image file not found for metadata extraction: {FilePath}", filePath);
            return null;
        }

        try
        {
            using var image = await Image.LoadAsync(filePath, cancellationToken);
            var exif = image.Metadata.ExifProfile;

            return new MediaMetadataDto
            {
                MediaType = MediaType.Photo,
                Width = image.Width,
                Height = image.Height,
                CameraMake = GetExifString(exif, ExifTag.Make),
                CameraModel = GetExifString(exif, ExifTag.Model),
                LensModel = GetExifString(exif, ExifTag.LensModel),
                FocalLengthMm = GetExifRational(exif, ExifTag.FocalLength),
                Aperture = GetExifRational(exif, ExifTag.FNumber),
                ShutterSpeed = GetShutterSpeedString(exif),
                Iso = GetExifUShortArrayFirst(exif, ExifTag.ISOSpeedRatings),
                FlashFired = GetFlashFired(exif),
                Orientation = GetExifUShort(exif, ExifTag.Orientation),
                TakenAtUtc = GetDateTaken(exif),
                Location = GetGpsCoordinates(exif)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract EXIF metadata from {FilePath}.", filePath);
            return null;
        }
    }

    private static string? GetExifString(ExifProfile? profile, ExifTag<string> tag)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(tag, out var value)) return null;
        return string.IsNullOrWhiteSpace(value?.Value) ? null : value.Value.Trim();
    }

    private static double? GetExifRational(ExifProfile? profile, ExifTag<Rational> tag)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(tag, out var value)) return null;
        if (value.Value.Denominator == 0) return null;
        return (double)value.Value.Numerator / value.Value.Denominator;
    }

    private static int? GetExifUShort(ExifProfile? profile, ExifTag<ushort> tag)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(tag, out var value)) return null;
        return (int)value.Value;
    }

    private static int? GetExifUShortArrayFirst(ExifProfile? profile, ExifTag<ushort[]> tag)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(tag, out var value)) return null;
        if (value?.Value is null || value.Value.Length == 0) return null;
        return value.Value[0];
    }

    private static string? GetShutterSpeedString(ExifProfile? profile)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(ExifTag.ExposureTime, out var value)) return null;
        if (value.Value.Denominator == 0) return null;

        var numerator = value.Value.Numerator;
        var denominator = value.Value.Denominator;

        if (numerator >= denominator)
        {
            return $"{(double)numerator / denominator:F1}s";
        }

        return $"{numerator}/{denominator}";
    }

    private static bool? GetFlashFired(ExifProfile? profile)
    {
        if (profile is null) return null;
        if (!profile.TryGetValue(ExifTag.Flash, out var value)) return null;
        // Bit 0 of the flash value indicates whether flash fired
        return (value.Value & 1) == 1;
    }

    private static DateTime? GetDateTaken(ExifProfile? profile)
    {
        if (profile is null) return null;

        string? dateString = null;
        if (profile.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal))
        {
            dateString = dateOriginal?.Value;
        }

        if (string.IsNullOrWhiteSpace(dateString) && profile.TryGetValue(ExifTag.DateTime, out var dateTime))
        {
            dateString = dateTime?.Value;
        }

        if (string.IsNullOrWhiteSpace(dateString)) return null;

        // EXIF date format: "yyyy:MM:dd HH:mm:ss"
        if (DateTime.TryParseExact(dateString, "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var result))
        {
            return result;
        }

        return null;
    }

    private static GeoCoordinate? GetGpsCoordinates(ExifProfile? profile)
    {
        if (profile is null) return null;

        if (!profile.TryGetValue(ExifTag.GPSLatitude, out var latValue) ||
            !profile.TryGetValue(ExifTag.GPSLongitude, out var lonValue) ||
            latValue?.Value is null || lonValue?.Value is null ||
            latValue.Value.Length < 3 || lonValue.Value.Length < 3)
        {
            return null;
        }

        profile.TryGetValue(ExifTag.GPSLatitudeRef, out var latRef);
        profile.TryGetValue(ExifTag.GPSLongitudeRef, out var lonRef);

        var latitude = ConvertDmsToDecimal(latValue.Value);
        var longitude = ConvertDmsToDecimal(lonValue.Value);

        if (latRef?.Value?.Equals("S", StringComparison.OrdinalIgnoreCase) == true)
            latitude = -latitude;
        if (lonRef?.Value?.Equals("W", StringComparison.OrdinalIgnoreCase) == true)
            longitude = -longitude;

        // Validate ranges
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            return null;

        double? altitude = null;
        if (profile.TryGetValue(ExifTag.GPSAltitude, out var altValue) &&
            altValue.Value.Denominator != 0)
        {
            altitude = (double)altValue.Value.Numerator / altValue.Value.Denominator;
            if (profile.TryGetValue(ExifTag.GPSAltitudeRef, out var altRef) && altRef.Value == 1)
                altitude = -altitude;
        }

        return new GeoCoordinate
        {
            Latitude = latitude,
            Longitude = longitude,
            AltitudeMetres = altitude
        };
    }

    private static double ConvertDmsToDecimal(Rational[] dms)
    {
        var degrees = dms[0].Denominator == 0 ? 0 : (double)dms[0].Numerator / dms[0].Denominator;
        var minutes = dms[1].Denominator == 0 ? 0 : (double)dms[1].Numerator / dms[1].Denominator;
        var seconds = dms[2].Denominator == 0 ? 0 : (double)dms[2].Numerator / dms[2].Denominator;

        return degrees + (minutes / 60.0) + (seconds / 3600.0);
    }
}
