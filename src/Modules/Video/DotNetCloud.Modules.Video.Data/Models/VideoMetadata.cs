namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Metadata extracted from a video file (resolution, codec, bitrate, etc.).
/// </summary>
public sealed class VideoMetadata
{
    /// <summary>Unique identifier for this metadata record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The video this metadata belongs to.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Video width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Video height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Frame rate (frames per second).</summary>
    public double FrameRate { get; set; }

    /// <summary>Video codec name (e.g. "h264", "hevc", "vp9").</summary>
    public string? VideoCodec { get; set; }

    /// <summary>Audio codec name (e.g. "aac", "opus", "ac3").</summary>
    public string? AudioCodec { get; set; }

    /// <summary>Video bitrate in bps.</summary>
    public long Bitrate { get; set; }

    /// <summary>Number of audio tracks.</summary>
    public int AudioTrackCount { get; set; }

    /// <summary>Number of subtitle tracks embedded in the file.</summary>
    public int SubtitleTrackCount { get; set; }

    /// <summary>Container format (e.g. "mp4", "mkv", "webm").</summary>
    public string? ContainerFormat { get; set; }

    /// <summary>When metadata was extracted (UTC).</summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
