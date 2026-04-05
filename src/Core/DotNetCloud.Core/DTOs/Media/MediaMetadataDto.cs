namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Extracted metadata for a media item, regardless of type (photo, audio, video).
/// Individual fields are populated based on the media type; unused fields remain <c>null</c>.
/// </summary>
public sealed record MediaMetadataDto
{
    /// <summary>
    /// The type of media this metadata describes.
    /// </summary>
    public required MediaType MediaType { get; init; }

    // ── Dimensions (Photo / Video) ──────────────────────────────────────

    /// <summary>Image or video width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Image or video height in pixels.</summary>
    public int? Height { get; init; }

    // ── Duration (Audio / Video) ────────────────────────────────────────

    /// <summary>Duration of the audio or video track.</summary>
    public TimeSpan? Duration { get; init; }

    // ── Codec / Format ──────────────────────────────────────────────────

    /// <summary>Primary codec name (e.g. "h264", "aac", "flac").</summary>
    public string? Codec { get; init; }

    /// <summary>Bitrate in bits per second, or <c>null</c> if unknown.</summary>
    public long? Bitrate { get; init; }

    /// <summary>Sample rate in Hz (audio only).</summary>
    public int? SampleRate { get; init; }

    /// <summary>Number of audio channels (audio / video with audio track).</summary>
    public int? Channels { get; init; }

    // ── Photo / EXIF ────────────────────────────────────────────────────

    /// <summary>Camera manufacturer (e.g. "Canon").</summary>
    public string? CameraMake { get; init; }

    /// <summary>Camera model (e.g. "EOS R5").</summary>
    public string? CameraModel { get; init; }

    /// <summary>Lens description.</summary>
    public string? LensModel { get; init; }

    /// <summary>EXIF focal length in millimetres.</summary>
    public double? FocalLengthMm { get; init; }

    /// <summary>Aperture as an f-number (e.g. 2.8).</summary>
    public double? Aperture { get; init; }

    /// <summary>Shutter speed as a human-readable string (e.g. "1/250").</summary>
    public string? ShutterSpeed { get; init; }

    /// <summary>ISO sensitivity value.</summary>
    public int? Iso { get; init; }

    /// <summary>Flash fired status.</summary>
    public bool? FlashFired { get; init; }

    /// <summary>EXIF orientation value (1–8).</summary>
    public int? Orientation { get; init; }

    /// <summary>Date the photo/video was originally taken (UTC), from EXIF or media metadata.</summary>
    public DateTime? TakenAtUtc { get; init; }

    /// <summary>GPS coordinates extracted from EXIF or video metadata.</summary>
    public GeoCoordinate? Location { get; init; }

    // ── Audio Tags ──────────────────────────────────────────────────────

    /// <summary>Track title from embedded tags.</summary>
    public string? Title { get; init; }

    /// <summary>Artist name(s) from embedded tags.</summary>
    public string? Artist { get; init; }

    /// <summary>Album name from embedded tags.</summary>
    public string? Album { get; init; }

    /// <summary>Album artist from embedded tags.</summary>
    public string? AlbumArtist { get; init; }

    /// <summary>Genre from embedded tags.</summary>
    public string? Genre { get; init; }

    /// <summary>Track number within the album.</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Total number of tracks on the album.</summary>
    public int? TrackCount { get; init; }

    /// <summary>Disc number.</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Total number of discs.</summary>
    public int? DiscCount { get; init; }

    /// <summary>Release year.</summary>
    public int? Year { get; init; }

    /// <summary>Whether the audio file has embedded album artwork.</summary>
    public bool? HasEmbeddedArt { get; init; }

    // ── Video-Specific ──────────────────────────────────────────────────

    /// <summary>Video frame rate (frames per second).</summary>
    public double? FrameRate { get; init; }

    /// <summary>Number of audio tracks in the video container.</summary>
    public int? AudioTrackCount { get; init; }

    /// <summary>Number of subtitle tracks in the video container.</summary>
    public int? SubtitleTrackCount { get; init; }
}
