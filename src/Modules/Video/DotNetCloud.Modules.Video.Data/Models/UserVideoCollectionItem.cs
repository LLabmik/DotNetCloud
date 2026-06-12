namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Junction linking a user video collection to a canonical video via content hash.
/// </summary>
public sealed class UserVideoCollectionItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The collection this item belongs to.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>Content hash referencing the canonical video.</summary>
    public required string CanonicalContentHash { get; set; }

    /// <summary>Sort order within the collection.</summary>
    public int SortOrder { get; set; }

    /// <summary>When added to the collection (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the collection.</summary>
    public UserVideoCollection? Collection { get; set; }
}
