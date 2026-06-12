namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Per-user video collection — user-defined groupings of videos (e.g., "Favorites", "Watch Later").
/// </summary>
public sealed class UserVideoCollection
{
    /// <summary>Unique identifier for this collection.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this collection.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Collection name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>When the collection was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the collection was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Items in this collection.</summary>
    public ICollection<UserVideoCollectionItem> Items { get; set; } = new List<UserVideoCollectionItem>();
}
