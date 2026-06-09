namespace DotNetCloud.Core.Services;

/// <summary>
/// Media type filter used by the media library scanner.
/// Maps to user-facing scan options (Photos, Music, Video) and includes
/// an "All" option for unfiltered scans.
/// </summary>
/// <remarks>
/// This is distinct from <c>DotNetCloud.Core.DTOs.Media.MediaType</c>
/// which has the internal representation (Photo/Audio/Video without "All").
/// </remarks>
public enum MediaScanType
{
    /// <summary>All media types.</summary>
    All,
    /// <summary>Photo/image files only.</summary>
    Photos,
    /// <summary>Audio/music files only.</summary>
    Music,
    /// <summary>Video files only.</summary>
    Video
}
