namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// A tag applied to a photo.
/// </summary>
public sealed class PhotoTag
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The photo this tag belongs to.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>The tag value (e.g. "vacation", "landscape").</summary>
    public required string Tag { get; set; }

    /// <summary>When the tag was applied (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the parent photo.</summary>
    public Photo? Photo { get; set; }
}
