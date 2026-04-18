namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a collection of videos (series, playlist, etc.).
/// </summary>
public sealed class VideoCollection
{
    /// <summary>Unique identifier for this collection.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this collection.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Collection name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the collection has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the collection was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the collection was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Videos in this collection.</summary>
    public ICollection<VideoCollectionItem> Items { get; set; } = new List<VideoCollectionItem>();
}
