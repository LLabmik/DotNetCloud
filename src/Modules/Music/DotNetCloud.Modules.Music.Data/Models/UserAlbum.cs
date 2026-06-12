namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Per-user album junction — lightweight record linking a user to a canonical album.
/// All intrinsic album properties live on <see cref="CanonicalAlbum"/>.
/// </summary>
public sealed class UserAlbum
{
    /// <summary>Unique identifier for this user album record.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this album record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The canonical album ID.</summary>
    public Guid CanonicalAlbumId { get; set; }

    /// <summary>Whether the album has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the album record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the album record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical album.</summary>
    public CanonicalAlbum? CanonicalAlbum { get; set; }
}
