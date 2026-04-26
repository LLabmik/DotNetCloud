namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Video thumbnail sizes for poster display.
/// </summary>
public enum VideoThumbnailSize
{
    /// <summary>Poster thumbnail for library grid view (300px wide).</summary>
    Poster = 300
}

/// <summary>
/// Generates, stores, and retrieves video poster thumbnails extracted via FFmpeg.
/// </summary>
public interface IVideoThumbnailService
{
    /// <summary>
    /// Returns a JPEG thumbnail stream for the given video,
    /// or <c>(null, null)</c> when no thumbnail is available.
    /// </summary>
    /// <param name="videoId">The video record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid videoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a poster thumbnail from the video file and stores it in the database.
    /// Extracts a frame at ~2 seconds (or first frame for very short videos) using FFmpeg,
    /// then resizes to 300px wide JPEG.
    /// </summary>
    /// <param name="videoId">The video record ID.</param>
    /// <param name="fileNodeId">The Files-module node ID for the video file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateThumbnailAsync(
        Guid videoId,
        Guid fileNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cached thumbnail for the specified video.
    /// </summary>
    /// <param name="videoId">The video whose thumbnail should be purged.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteThumbnailAsync(Guid videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates multiple screenshots from the video at different timestamps
    /// and stores them on disk as a fallback when no external poster is available.
    /// </summary>
    Task GenerateScreenshotsAsync(Guid videoId, Guid fileNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paths to all generated screenshots for a video,
    /// or null if none exist.
    /// </summary>
    Task<IReadOnlyList<string>?> GetScreenshotPathsAsync(Guid videoId, CancellationToken cancellationToken = default);
}
