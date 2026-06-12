using System.Collections.Concurrent;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// In-memory progress tracking for video stream preparation.
/// Tracks the pipeline stages: chunk reconstruction → ffprobe → remux/transcode → streaming.
/// Thread-safe (ConcurrentDictionary).
/// </summary>
public sealed class StreamProgressState
{
    private readonly ConcurrentDictionary<Guid, StreamProgressEntry> _entries = new();

    /// <summary>
    /// Gets or creates a progress entry for a video.
    /// </summary>
    public StreamProgressEntry GetOrCreate(Guid videoId)
    {
        return _entries.GetOrAdd(videoId, _ => new StreamProgressEntry());
    }

    /// <summary>
    /// Gets the current progress for a video, or null if not tracked.
    /// </summary>
    public StreamProgressEntry? Get(Guid videoId)
    {
        return _entries.TryGetValue(videoId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Removes the progress entry for a video (called when streaming begins or fails).
    /// </summary>
    public void Remove(Guid videoId)
    {
        _entries.TryRemove(videoId, out _);
    }
}

/// <summary>
/// Represents the current progress of a stream preparation pipeline.
/// </summary>
public sealed class StreamProgressEntry
{
    /// <summary>
    /// Current pipeline stage.
    /// </summary>
    public StreamProgressStage Stage { get; set; } = StreamProgressStage.Reconstructing;

    /// <summary>
    /// Progress percentage (0-100) for the current stage.
    /// </summary>
    public double Percent { get; set; }

    /// <summary>
    /// Human-readable status message (e.g. "Assembling video file…").
    /// </summary>
    public string Message { get; set; } = "Assembling video file…";

    /// <summary>
    /// When the entry was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The streaming strategy that will be used (set after probing).
    /// </summary>
    public string? Strategy { get; set; }
}

/// <summary>
/// Stages of the stream preparation pipeline.
/// </summary>
public enum StreamProgressStage
{
    /// <summary>Chunks are being reassembled into a temp file.</summary>
    Reconstructing,

    /// <summary>ffprobe is analyzing codecs.</summary>
    Probing,

    /// <summary>ffmpeg stream copy (remux) is in progress.</summary>
    Remuxing,

    /// <summary>ffmpeg HLS transcode is starting (waiting for first segment).</summary>
    Transcoding,

    /// <summary>Stream is ready, playback starting.</summary>
    Streaming,

    /// <summary>An error occurred during preparation.</summary>
    Failed
}
