using DotNetCloud.Modules.Music.Services;

namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a starred (favorited) item. Polymorphic: can reference an artist, album, or track.
/// </summary>
public sealed class StarredItem
{
    /// <summary>Unique identifier for this star.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who starred the item.</summary>
    public Guid UserId { get; set; }

    /// <summary>The type of item that was starred.</summary>
    public StarredItemType ItemType { get; set; }

    /// <summary>The ID of the starred item (artist, album, or track).</summary>
    public Guid ItemId { get; set; }

    /// <summary>When the item was starred (UTC).</summary>
    public DateTime StarredAt { get; set; } = DateTime.UtcNow;
}
