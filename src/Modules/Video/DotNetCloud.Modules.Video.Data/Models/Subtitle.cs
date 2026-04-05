namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a subtitle track associated with a video (SRT or VTT format).
/// </summary>
public sealed class Subtitle
{
    /// <summary>Unique identifier for this subtitle.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The video this subtitle belongs to.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Language code (e.g. "en", "fr", "es").</summary>
    public required string Language { get; set; }

    /// <summary>Optional label (e.g. "English (SDH)", "Forced").</summary>
    public string? Label { get; set; }

    /// <summary>Format: "srt" or "vtt".</summary>
    public required string Format { get; set; }

    /// <summary>Subtitle file content.</summary>
    public required string Content { get; set; }

    /// <summary>Whether this is the default subtitle track.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When the subtitle was uploaded (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
