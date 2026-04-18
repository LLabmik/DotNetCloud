namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// Represents a photo or album share with another user.
/// </summary>
public sealed class PhotoShare
{
    /// <summary>Unique share identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Photo being shared (null if album share).</summary>
    public Guid? PhotoId { get; set; }

    /// <summary>Album being shared (null if photo share).</summary>
    public Guid? AlbumId { get; set; }

    /// <summary>User who created the share.</summary>
    public Guid SharedByUserId { get; set; }

    /// <summary>User this is shared with (null for public link).</summary>
    public Guid? SharedWithUserId { get; set; }

    /// <summary>Permission level.</summary>
    public PhotoSharePermissionLevel Permission { get; set; } = PhotoSharePermissionLevel.ReadOnly;

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the share expires (null for permanent).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Navigation property to the photo (if photo share).</summary>
    public Photo? Photo { get; set; }

    /// <summary>Navigation property to the album (if album share).</summary>
    public Album? Album { get; set; }
}

/// <summary>
/// Permission levels for photo/album shares.
/// </summary>
public enum PhotoSharePermissionLevel
{
    /// <summary>View only.</summary>
    ReadOnly = 0,

    /// <summary>View and download.</summary>
    Download = 1,

    /// <summary>View, download, and add photos (album shares only).</summary>
    Contribute = 2
}
