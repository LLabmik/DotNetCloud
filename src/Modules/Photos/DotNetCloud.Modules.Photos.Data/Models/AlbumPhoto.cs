namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// Junction entity for the many-to-many relationship between albums and photos.
/// </summary>
public sealed class AlbumPhoto
{
    /// <summary>The album.</summary>
    public Guid AlbumId { get; set; }

    /// <summary>The photo.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>Order/position of the photo within the album.</summary>
    public int SortOrder { get; set; }

    /// <summary>When the photo was added to the album (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the album.</summary>
    public Album? Album { get; set; }

    /// <summary>Navigation property to the photo.</summary>
    public Photo? Photo { get; set; }
}
