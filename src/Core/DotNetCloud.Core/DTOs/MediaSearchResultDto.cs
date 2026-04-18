namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Aggregated search results across Photos, Music, and Video modules.
/// </summary>
public sealed record MediaSearchResultDto
{
    /// <summary>Photos matching the search query.</summary>
    public required IReadOnlyList<PhotoDto> Photos { get; init; }

    /// <summary>Tracks matching the search query.</summary>
    public required IReadOnlyList<TrackDto> Tracks { get; init; }

    /// <summary>Music albums matching the search query.</summary>
    public required IReadOnlyList<MusicAlbumDto> Albums { get; init; }

    /// <summary>Artists matching the search query.</summary>
    public required IReadOnlyList<ArtistDto> Artists { get; init; }

    /// <summary>Videos matching the search query.</summary>
    public required IReadOnlyList<VideoDto> Videos { get; init; }

    /// <summary>Total number of results across all types.</summary>
    public int TotalCount => Photos.Count + Tracks.Count + Albums.Count + Artists.Count + Videos.Count;
}
