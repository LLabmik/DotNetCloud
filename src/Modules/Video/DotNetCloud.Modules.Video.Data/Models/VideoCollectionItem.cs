namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Junction entity between VideoCollection and Video. Supports ordering.
/// </summary>
public sealed class VideoCollectionItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The collection this item belongs to.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>The video in this collection slot.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Sort order within the collection.</summary>
    public int SortOrder { get; set; }

    /// <summary>When added to the collection (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the collection.</summary>
    public VideoCollection? Collection { get; set; }

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
