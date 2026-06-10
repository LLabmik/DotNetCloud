namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// High-level service for video transcoding.
/// Orchestrates cache lookup, ffmpeg argument building, process execution, and job tracking.
/// </summary>
public interface IVideoTranscodingService
{
    /// <summary>
    /// Checks whether the video can be served directly to HTML5 browsers
    /// without transcoding. Uses ffprobe to inspect codecs.
    /// </summary>
    /// <param name="videoFilePath">Absolute path to the source video file on disk.</param>
    /// <param name="mimeType">MIME type of the video (e.g., "video/mp4").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the video can be direct-played.</returns>
    Task<bool> CanDirectPlayAsync(string videoFilePath, string mimeType, CancellationToken ct = default);

    /// <summary>
    /// Transcodes a video file and returns the job ID and output path.
    /// Uses cache when available. Launches ffmpeg in the background.
    /// </summary>
    /// <param name="videoId">The Video entity ID.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="mimeType">MIME type of the source video.</param>
    /// <param name="seekStart">Optional seek position for partial transcode.</param>
    /// <param name="seekDuration">Optional duration for partial transcode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (jobId, outputFilePath).</returns>
    Task<(string JobId, string OutputPath)> TranscodeAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current progress of a transcode job.
    /// Returns null if the job does not exist.
    /// </summary>
    TranscodingJob? GetProgress(string jobId);

    /// <summary>
    /// Cancels a running transcode job.
    /// </summary>
    void CancelTranscode(string jobId);

    /// <summary>
    /// Starts an HLS (HTTP Live Streaming) transcode for a video file.
    /// Transcodes to H.264/AAC segmented into .ts files with an .m3u8 playlist.
    /// ffmpeg writes self-contained 6-second segments immediately, so the caller
    /// can start serving the playlist as soon as the first segment is written.
    /// </summary>
    /// <param name="videoId">The Video entity ID.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="mimeType">MIME type of the source video.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (jobId, outputDirectory, playlistPath).</returns>
    Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the active HLS transcode job for a given video ID, if any.
    /// </summary>
    TranscodingJob? GetActiveHlsJob(Guid videoId);
}
