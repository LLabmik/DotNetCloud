namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Per-user artist junction — lightweight record linking a user to a canonical artist.
/// All intrinsic artist properties live on <see cref="CanonicalArtist"/>.
/// </summary>
public sealed class UserArtist
{
    /// <summary>Unique identifier for this user artist record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this artist record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The canonical artist ID.</summary>
    public Guid CanonicalArtistId { get; set; }

    /// <summary>Whether the artist has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the artist record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the artist record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical artist.</summary>
    public CanonicalArtist? CanonicalArtist { get; set; }
}
