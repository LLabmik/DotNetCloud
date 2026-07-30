namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// High-level service for video transcoding.
/// Orchestrates cache lookup, ffmpeg argument building, process execution, and job tracking.
/// </summary>
public interface IVideoTranscodingService
{
    /// <summary>
    /// Determines the optimal streaming strategy for a video by probing its codecs
    /// and checking against the browser compatibility matrix.
    /// </summary>
    /// <param name="videoFilePath">Absolute path to the source video file on disk.</param>
    /// <param name="mimeType">MIME type of the video (e.g., "video/mp4").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recommended streaming strategy and parsed codec info.</returns>
    Task<(StreamingStrategy Strategy, string? VideoCodec, string? AudioCodec, string? Container, TimeSpan Duration)> DecideStreamingStrategyAsync(
        string videoFilePath,
        string mimeType,
        CancellationToken ct = default);

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
    /// Runs ffmpeg stream copy (remux) to change the container without re-encoding.
    /// Output goes to stdout via Process.StandardOutput.BaseStream for direct HTTP response streaming.
    /// Returns the ffmpeg process so the caller can pipe stdout to the HTTP response.
    /// </summary>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="videoCodec">Source video codec name (for bitstream filter selection).</param>
    /// <param name="audioCodec">Source audio codec name (for audio copy decision).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="startTime">Optional seek position in the source file (applied via -ss before -i).</param>
    /// <returns>A tuple of (ffmpeg Process, arguments string used). Caller owns the Process lifetime.</returns>
    Task<(System.Diagnostics.Process Process, string Args)> StreamCopyAsync(
        string sourceFilePath,
        string? videoCodec,
        string? audioCodec,
        CancellationToken ct = default,
        TimeSpan? startTime = null);

    /// <summary>
    /// Runs ffmpeg stream copy writing to a temp file (for subsequent PhysicalFile serving).
    /// Returns the path to the remuxed MP4 file.
    /// </summary>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="videoCodec">Source video codec name.</param>
    /// <param name="audioCodec">Source audio codec name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the remuxed output file.</returns>
    Task<string> StreamCopyToFileAsync(
        string sourceFilePath,
        string? videoCodec,
        string? audioCodec,
        CancellationToken ct = default);

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
    /// Cancels the active transcode job for the given video and user, if any.
    /// </summary>
    void CancelTranscode(Guid videoId, Guid userId);

    /// <summary>
    /// Starts an HLS (HTTP Live Streaming) transcode for a video file.
    /// Transcodes to H.264/AAC segmented into .ts files with an .m3u8 playlist.
    /// ffmpeg writes self-contained 6-second segments immediately, so the caller
    /// can start serving the playlist as soon as the first segment is written.
    /// When source audio codec is browser-compatible, only the video is re-encoded.
    /// </summary>
    /// <param name="videoId">The Video entity ID.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="mimeType">MIME type of the source video.</param>
    /// <param name="sourceVideoCodec">Source video codec from ffprobe (for bitstream filter optimization).</param>
    /// <param name="sourceAudioCodec">Source audio codec from ffprobe (for audio copy optimization).</param>
    /// <param name="seekStart">Optional seek position to start transcoding from (e.g., when user seeks beyond available HLS segments).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (jobId, outputDirectory, playlistPath).</returns>
    Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        string? sourceVideoCodec = null,
        string? sourceAudioCodec = null,
        TimeSpan? seekStart = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the active HLS transcode job for a given video ID, if any.
    /// </summary>
    TranscodingJob? GetActiveHlsJob(Guid videoId);

    /// <summary>
    /// Atomically cancels the current HLS transcode for a video+user and starts a
    /// new one from <paramref name="seekStart"/>, holding the per-video HLS lock for
    /// the entire cancel → cleanup → create sequence. This is required because a
    /// concurrent, ordinary playlist-refresh request (hls.js periodically re-fetches
    /// the growing HLS manifest via <see cref="TranscodeHlsAsync"/> with no seek) can
    /// otherwise race in between an unlocked cancel+delete and the new job's creation,
    /// spin up its own unseeked job, and have the seek wrongly reuse it — silently
    /// discarding the seek and leaving playback at position 0.
    /// </summary>
    /// <param name="videoId">The Video entity ID.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="sourceFilePath">Absolute path to the source video file.</param>
    /// <param name="seekStart">Position to start the new transcode from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (jobId, outputDirectory, playlistPath) for the new job.</returns>
    Task<(string JobId, string OutputDir, string PlaylistPath)> SeekHlsTranscodeAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        TimeSpan seekStart,
        CancellationToken ct = default);
}
