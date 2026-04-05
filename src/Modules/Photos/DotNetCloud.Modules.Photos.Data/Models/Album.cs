namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// Represents a photo album.
/// </summary>
public sealed class Album
{
    /// <summary>Unique identifier for this album.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this album.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Album title.</summary>
    public required string Title { get; set; }

    /// <summary>Optional album description.</summary>
    public string? Description { get; set; }

    /// <summary>Cover photo ID (displayed as album thumbnail).</summary>
    public Guid? CoverPhotoId { get; set; }

    /// <summary>Whether the album has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the album was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the album was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Photos in this album.</summary>
    public ICollection<AlbumPhoto> AlbumPhotos { get; set; } = new List<AlbumPhoto>();

    /// <summary>Shares for this album.</summary>
    public ICollection<PhotoShare> Shares { get; set; } = new List<PhotoShare>();
}
