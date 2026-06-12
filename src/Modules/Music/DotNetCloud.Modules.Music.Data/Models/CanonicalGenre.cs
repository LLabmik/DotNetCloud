namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical (shared) genre — stored once by name.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalGenre
{
    /// <summary>Unique identifier for this genre.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Genre name (e.g. "Rock", "Jazz", "Classical").</summary>
    public required string Name { get; set; }

    /// <summary>Track associations for this genre.</summary>
    public ICollection<CanonicalTrackGenre> TrackGenres { get; set; } = new List<CanonicalTrackGenre>();
}
