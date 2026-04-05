namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Represents a geographic coordinate pair (latitude/longitude) with optional altitude.
/// Used for photo geo-tagging and map-based clustering.
/// </summary>
public sealed record GeoCoordinate
{
    /// <summary>
    /// Latitude in decimal degrees (WGS 84). Range: −90 to +90.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude in decimal degrees (WGS 84). Range: −180 to +180.
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Altitude in metres above sea level, or <c>null</c> if unavailable.
    /// </summary>
    public double? AltitudeMetres { get; init; }
}
