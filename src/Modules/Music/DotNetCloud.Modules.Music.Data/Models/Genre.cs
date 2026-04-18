namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a music genre.
/// </summary>
public sealed class Genre
{
    /// <summary>Unique identifier for this genre.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Genre name (e.g. "Rock", "Jazz", "Classical").</summary>
    public required string Name { get; set; }

    /// <summary>Track associations for this genre.</summary>
    public ICollection<TrackGenre> TrackGenres { get; set; } = new List<TrackGenre>();
}
