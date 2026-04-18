namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// EXIF and GPS metadata extracted from a photo.
/// </summary>
public sealed class PhotoMetadata
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The photo this metadata belongs to.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>Camera manufacturer (e.g. "Canon").</summary>
    public string? CameraMake { get; set; }

    /// <summary>Camera model (e.g. "EOS R5").</summary>
    public string? CameraModel { get; set; }

    /// <summary>Lens description.</summary>
    public string? LensModel { get; set; }

    /// <summary>Focal length in millimetres.</summary>
    public double? FocalLengthMm { get; set; }

    /// <summary>Aperture as an f-number.</summary>
    public double? Aperture { get; set; }

    /// <summary>Shutter speed as a string (e.g. "1/250").</summary>
    public string? ShutterSpeed { get; set; }

    /// <summary>ISO sensitivity.</summary>
    public int? Iso { get; set; }

    /// <summary>Whether the flash fired.</summary>
    public bool? FlashFired { get; set; }

    /// <summary>EXIF orientation value (1–8).</summary>
    public int? Orientation { get; set; }

    /// <summary>GPS latitude (decimal degrees, WGS 84).</summary>
    public double? Latitude { get; set; }

    /// <summary>GPS longitude (decimal degrees, WGS 84).</summary>
    public double? Longitude { get; set; }

    /// <summary>GPS altitude in metres.</summary>
    public double? AltitudeMetres { get; set; }

    /// <summary>Date the photo was originally taken (UTC).</summary>
    public DateTime? TakenAtUtc { get; set; }

    /// <summary>Navigation property to the parent photo.</summary>
    public Photo? Photo { get; set; }
}
